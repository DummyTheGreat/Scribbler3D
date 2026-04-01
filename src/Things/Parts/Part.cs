using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Godot.GD;
using static System.Formats.Asn1.AsnWriter;

public partial class Part : Node3D {

    [Signal]
    public delegate void PartPreparedEventHandler();

    public Part parentPart;
    public Godot.Collections.Array<Part> connectedParts = [];

    public StringName UID;
    public AnimationPlayer animationPlayer;
    public PackedScene duplicateScene;

    private Material selectionGlowMaterial;
    private Material secondarySelectionGlowMaterial;

    // This doesn't change until the part a new Thing is CREATED, not just when the part connects to another
    public Thing thing;

    public bool joining;
    public bool receiving;
    public List<AlignmentPlane> bindingQuads;
    public PartCollider activeCollider;
    public bool editorMode;
    public MeshInstance3D skinMesh;

    private bool dragging;
    private PackedScene colliderScene;
    private float t;
    private Transform3D destTransform;
    private Transform3D startTransform;

    public ThingEditor editor;

    public partial class ImportData : Resource {

        public NodePath receiverPath;
        public Godot.Collections.Array<Resource> boneData;

        public ImportData(Resource pData) {
            this.receiverPath = pData.Get("receiver").AsNodePath();
            this.boneData = pData.Get("boneData").AsGodotArray<Resource>();
        }
    }

    public ImportData importData;

    public virtual ImportData CreateImportData(Resource partImportData) {
        this.importData = new(partImportData);
        return this.importData;
    }


    // This is going to be called a lot so it should be optimized wherever possible
    public static Transform3D CalculateJoinTransform(AlignmentPlane connectingPlane, AlignmentPlane receivingPlane, Transform3D connectorGlobalPos) {

        Vector3 frontBWorld = receivingPlane.GlobalTransform * receivingPlane.GetFront();

        Basis recLocalFrame = receivingPlane.GetLocalFrame();
        Basis aLocalFrame = connectingPlane.GetLocalFrame();

        // Pure local dimension ratio — receiver world scale already lives in GlobalTransform.Basis
        Vector2 conDims = connectingPlane.GetDimensions();
        Vector2 recDims = receivingPlane.GetDimensions();
        float sx = (conDims.X > 1e-8f) ? (recDims.X / conDims.X) : 1f;
        float sz = (conDims.Y > 1e-8f) ? (recDims.Y / conDims.Y) : 1f;
        Basis S = new(
            new Vector3(sx, 0, 0), 
            new Vector3(0, 1, 0), 
            new Vector3(0, 0, sz)
            );

        // receiver world basis * receiver local frame * flip * scale * inverse connector local frame
        // Reading right to left: rotate out of connector frame, scale, flip normal,
        // rotate into receiver local frame, apply receiver full world basis (includes bone scale)
        Basis desiredQuadABasisW = receivingPlane.GlobalTransform.Basis * recLocalFrame * S * aLocalFrame.Inverse();

        // Pin connector's front vertex to receiver's front vertex
        Vector3 desiredQuadAOriginW = frontBWorld - (desiredQuadABasisW * connectingPlane.GetFront());
        Transform3D desiredQuadAGlobal = new(desiredQuadABasisW, desiredQuadAOriginW);

        // Convert to Part-relative transform
        Transform3D quadInPart = connectorGlobalPos.AffineInverse() * connectingPlane.GlobalTransform;
        return desiredQuadAGlobal * quadInPart.AffineInverse();
    }

    public void ToggleEditorMode() {
        this.editorMode = !this.editorMode;
        EstablishColliders();
    }

    public virtual MeshInstance3D GetSkinMesh() {
        return null;
    }

    // Receiver
    public virtual void AttachPart(Part connector) {
        connector.parentPart = this;
        if (!this.connectedParts.Contains(connector)) {
            this.connectedParts.Add(connector);
        }
        Part topPart = this;
        while (topPart.activeCollider != null) {
            topPart = topPart.activeCollider.GetBoundCollider().associatedPart;
        }
        topPart.MergeAnimations(connector);
    }

    // Receiver
    public virtual void DetachPart(Part connector) {
        SplitAnimations(connector);
        connector.Reparent(this.editor.GetEditorSpace());
        connector.parentPart = null;
        this.connectedParts.Remove(connector);
        connector.Scale = new Vector3(1, 1, 1);
    }

    public virtual void Unselected() {
        static void TraverseAll(Part part) {
            int surfaceCount = part.skinMesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++) {
                part.skinMesh.SetSurfaceOverrideMaterial(i, null);
            }

            foreach (Part child in part.connectedParts) {
                TraverseAll(child);
            }
        }

        Part topPart = this;
        while (topPart.activeCollider != null) {
            topPart = topPart.activeCollider.GetBoundCollider().associatedPart;
        }
        TraverseAll(topPart);
    }

    public virtual void Selected() {
        static void TraverseAll(Part part, Part primary) {
            int surfaceCount = part.skinMesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++) {
                part.skinMesh.SetSurfaceOverrideMaterial(i, part == primary ? part.selectionGlowMaterial : part.secondarySelectionGlowMaterial);
            }

            foreach (Part child in part.connectedParts) {
                TraverseAll(child, primary);
            }
        }

        Part topPart = this;
        while (topPart.activeCollider != null) {
            topPart = topPart.activeCollider.GetBoundCollider().associatedPart;
        }
        TraverseAll(topPart, this);
    }

    public virtual void MoveSelected() {
        if (this.activeCollider != null && this.GetParent() is not Thing) {
            Part receiverPart = this.activeCollider.GetBoundCollider().associatedPart;

            // Loop to top part
            while (receiverPart.activeCollider != null) {
                receiverPart = receiverPart.activeCollider.GetBoundCollider().associatedPart;
            }
            receiverPart.CallDeferred(nameof(DetachPart), this);
        }
    }

    public virtual void StopSelected() {
        if (this.activeCollider != null) {
            JoiningInitialization();
            this.joining = true;
            this.activeCollider.GetBoundCollider().associatedPart.receiving = true;
        }
    }

    public void DoAnimation(StringName animationLibrary, StringName animation) {
        string name = animationLibrary + "/" + animation;
        if (this.animationPlayer.IsPlaying() && this.animationPlayer.CurrentAnimation.Equals(name)) {
            this.animationPlayer.Stop();
        }
        else {
            this.animationPlayer.Play(name);
        }
    }

    private static void AddNewLibrary(AnimationLibrary newLib, Part newLibOwner) {
        string newLibName =
            "Library" +
            newLibOwner.GetMeta("ThingName").AsString() +
            newLibOwner.GetMeta("PartName").AsString() +
            (newLibOwner.GetMeta("IsMirror").AsBool() ? "Mirror" : "") +
            "_V" + newLibOwner.GetMeta("PartVariant").AsString() +
            "_I" + newLibOwner.GetMeta("Instance").AsString();
        newLib.SetMeta("Instance", newLibOwner.GetMeta("Instance").AsInt32());
        newLibOwner.animationPlayer.AddAnimationLibrary(newLibName, newLib);
    }

    private static void AddNewAnimation(AnimationLibrary lib, Animation oldAnim, Animation newAnim, Part newAnimOwner) {
        newAnim.LoopMode = Animation.LoopModeEnum.Linear;
        string animationGroup = oldAnim.GetMeta("AnimationGroup").AsString();
        string newAnimName =
            "Animation" +
            newAnimOwner.GetMeta("ThingName").AsString() +
            newAnimOwner.GetMeta("PartName").AsString() +
            (newAnimOwner.GetMeta("IsMirror").AsBool() ? "Mirror" : "") +
            animationGroup +
            "_V" + newAnimOwner.GetMeta("PartVariant").AsString() +
            "_I" + newAnimOwner.GetMeta("Instance").AsString();
        newAnim.SetMeta("AnimationGroup", animationGroup);
        newAnim.SetMeta("Instance", newAnimOwner.GetMeta("Instance").AsInt32());
        lib.AddAnimation(newAnimName, newAnim);
    }

    public void MergeAnimations(Part connector) {
        while (connector.animationPlayer.GetAnimationLibraryList().Count > 0) {
            StringName conLibStr = connector.animationPlayer.GetAnimationLibraryList().First();
            AnimationLibrary conLib = connector.animationPlayer.GetAnimationLibrary(conLibStr);

            foreach (StringName conAnimStr in conLib.GetAnimationList()) {
                //StringName conAnimStr = conLib.GetAnimationList().First();
                Animation conAnim = conLib.GetAnimation(conAnimStr);
                string conAnimGroup = conAnim.GetMeta("AnimationGroup").AsString();
                bool match = false;

                // Match by animation group
                foreach (string recAnimStr in this.animationPlayer.GetAnimationList()) {
                    Animation recAnim = this.animationPlayer.GetAnimation(recAnimStr);
                    string recAnimGroup = recAnim.GetMeta("AnimationGroup").AsString();

                    if (conAnimGroup.Equals(recAnimGroup)) {
                        // Merge
                        Print("Animation Merge");
                        for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                            NodePath oldPath = conAnim.TrackGetPath(i);
                            string newPath = this.GetPathTo(connector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                            conAnim.TrackSetPath(i, newPath);
                            conAnim.CopyTrack(i, recAnim);
                            conAnim.TrackSetPath(i, oldPath);
                        }
                        match = true;
                        break;
                    }
                }

                // There is no matching animation group in the receiver's animation list so create create new receiver animation
                if (!match) {

                    Godot.Collections.Array<StringName> recLibs = this.animationPlayer.GetAnimationLibraryList();
                    AnimationLibrary recLib;

                    if (recLibs.Count > 1) {  }
                    switch (recLibs.Count) {
                        case 0:
                            recLib = new();
                            AddNewLibrary(recLib, this);
                            break;
                        case 1:
                            recLib = this.animationPlayer.GetAnimationLibrary(recLibs.First());
                            break;
                        default:
                            Print("Why in the hell is there more than one library");
                            recLib = this.animationPlayer.GetAnimationLibrary(recLibs.First());
                            break;
                    }

                    Animation newRecAnim = new();
                    Print("New Animation");
                    // Copy tracks
                    for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                        NodePath oldPath = conAnim.TrackGetPath(i);
                        string newPath = this.GetPathTo(connector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                        conAnim.TrackSetPath(i, newPath);
                        conAnim.CopyTrack(i, newRecAnim);
                        conAnim.TrackSetPath(i, oldPath);
                    }

                    AddNewAnimation(recLib, conAnim, newRecAnim, this);
                }

                // All duplicated parts share the same library. Only delete animations if this is the last remaining copy referencing the library
                conLib.RemoveAnimation(conAnimStr);
            }

            connector.animationPlayer.RemoveAnimationLibrary(conLibStr);
        }

        //foreach (defConnector.animationPlayer.GetAnimationLibraryList()
        connector.RemoveChild(connector.animationPlayer);
        connector.animationPlayer.QueueFree();
        connector.animationPlayer = null;
    }

    public void SplitAnimations(Part connector) {
        this.animationPlayer.Pause();
        AnimationPlayer connectorAnimator = new();
        connector.animationPlayer = connectorAnimator;
        foreach (string library in this.animationPlayer.GetAnimationLibraryList()) {
            AnimationLibrary receiverLib = this.animationPlayer.GetAnimationLibrary(library);
            AnimationLibrary newConLib = new();
            AddNewLibrary(newConLib, connector);

            List<string> conChildren = [connector.Name.ToString()];
            foreach (Part p in connector.connectedParts) {
                conChildren.Add(p.Name.ToString());
            }

            foreach (string anim in receiverLib.GetAnimationList()) {
                Animation receiverAnim = receiverLib.GetAnimation(anim);
                Animation newConnectorAnim = new();
                int index = 0;
                while (index < receiverAnim.GetTrackCount()) {
                    NodePath trackPath = receiverAnim.TrackGetPath(index);
                    List<string> pathNames = [.. trackPath.GetConcatenatedNames().Split("/")];
                    bool removal = false;
                    foreach (string connectorName in conChildren) {
                        if (pathNames.Contains(connectorName)) {

                            // Fix path name for detached part
                            int partNameIndex = pathNames.IndexOf(connectorName);
                            string[] p = pathNames.Select((item, index) => new { Item = item, Index = index })
                                .Where(x => x.Index > partNameIndex)
                                .Select(x => x.Item)
                                .ToArray();
                            string newPathName = String.Join("/", p);
                            newPathName += ":" + trackPath.GetConcatenatedSubNames();

                            receiverAnim.TrackSetPath(index, newPathName);
                            receiverAnim.CopyTrack(index, newConnectorAnim);
                            Print("Animation added to: ", connectorName, " ", anim, " ", newPathName);
                            removal = true;
                            receiverAnim.RemoveTrack(index);
                            break;
                        }
                    }

                    if (removal == false) { index++; }

                }
                if (newConnectorAnim.GetTrackCount() > 0) {
                    AddNewAnimation(newConLib, receiverAnim, newConnectorAnim, connector);
                }
            }
        }
        connector.AddChild(connectorAnimator);
    }

    public void PreparePart(AlignmentPlane connector, AlignmentPlane receiver, Node parent, Thing partThing, ThingEditor partEditor) {
        this.thing = partThing;
        this.editor = partEditor;
        string partName = this.GetMeta("PartName").AsString();
        string thingName = this.GetMeta("ThingName").AsString();
        int variantNum = this.GetMeta("PartVariant").AsInt32();
        bool isMirror = this.GetMeta("IsMirror").AsBool();
        this.UID = thingName + partName + (isMirror ? "Mirror" : "");

        int trackedCount = partEditor.GetTrackedCount(this);
        this.Name = "Part_" + thingName + "_" + partName + (isMirror ? "_Mirror" : "") + "_V" + variantNum.ToString() + "_I" + (trackedCount + 1).ToString();

        this.animationPlayer = this.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
        partEditor.AddToTracker(this);

        if (parent is Part partParent) {
            receiver.AddSibling(this);
            this.parentPart = partParent;
            partParent.connectedParts.Add(this);
            Part topPart = partParent;

            while (topPart.parentPart != null) {
                topPart = topPart.parentPart;
            }

            if (this.animationPlayer == null) { 
                this.animationPlayer = new();
                this.AddChild(this.animationPlayer);
            }
            topPart.MergeAnimations(this);
        }
        else {
            parent.AddChild(this);
        }
        ToggleEditorMode();
        if (receiver != null) {
            this.GlobalTransform = CalculateJoinTransform(connector, receiver, this.GlobalTransform);
        }
        EmitSignal("PartPrepared");
    }

    public virtual void AddPartCollider(PartCollider collider, MeshInstance3D quad) { }

    // Connector
    public virtual void JoiningInitialization() {

        PartCollider connector = activeCollider; // Plane A collider
        PartCollider receiver = activeCollider.GetBoundCollider(); // Plane B collider

        this.destTransform = CalculateJoinTransform(connector.plane, receiver.plane, this.GlobalTransform);
        this.startTransform = this.GlobalTransform;
        this.t = 0f;
    }

    public void ConnectColliderSignals(PartCollider collider) {
        collider.PartConnect += PartConnect;
        collider.PartDisconnect += PartDisconnect;
    }

    // Signal function recieved from PartCollider
    public void PartConnect(PartCollider newCollider) {
        Print("Part Connect");
        this.activeCollider = newCollider;
    }

    // Signal function recieved from PartCollider
    private void PartDisconnect() {
        this.activeCollider = null;
    }

    private void EstablishColliders() {
        
        foreach (AlignmentPlane quad in this.bindingQuads) {
            PartCollider partCollider = colliderScene.Instantiate<PartCollider>();
            partCollider.SetPlane(quad);
            quad.SetCollider(partCollider);
            this.AddPartCollider(partCollider, quad);

            // Set collider up based on the boundary line's endpoint coordinates
            partCollider.GlobalPosition = quad.GlobalTransform * quad.GetCentroid();

            // Find the direction from which the quad connector is located relative to the part to determine which way its normal should face
            Vector3 quadCentroidWorld = quad.GlobalTransform * quad.GetCentroid();
            Vector3 partCenterWorld = this.GlobalTransform.Origin;
            // The direction away from the part's origin (typically the center)
            Vector3 partOutWorld = (quadCentroidWorld - partCenterWorld).Normalized();

            // Get the global normal of the alignment plane
            Basis normalMatrix = quad.GlobalTransform.Basis.Inverse().Transposed();
            Vector3 planeNormal = (normalMatrix * quad.GetLocalNormal()).Normalized();

            // If normal points inward then flip the local normal
            if ((planeNormal.Dot(partOutWorld) < 0f && quad.GetMeta("MeshType").AsString() == "Connector") ||
                (planeNormal.Dot(partOutWorld) >= 0f && quad.GetMeta("MeshType").AsString() == "Receiver")) {
                quad.FlipNormal();
                Print("Flip: ", quad.Name);
                planeNormal = -planeNormal;
            }

            // Rotate to align with normal of binding quad
            Vector3 forward = (-quad.GlobalTransform.Basis.Z).Normalized();
            if (Mathf.Abs(planeNormal.Dot(forward.Normalized())) > 0.99f) { forward = Vector3.Up; }
            Vector3 right = forward.Normalized().Cross(planeNormal).Normalized();
            Vector3 newForward = planeNormal.Cross(right).Normalized();
            Basis b = new (right, planeNormal, -newForward);

            partCollider.GlobalTransform = new Transform3D(b, partCollider.GlobalTransform.Origin);

            SphereShape3D circle = new() { Radius = (quad.GetCentroid() - quad.GetFront()).Length() };
            partCollider.GetChild<CollisionShape3D>(0).Shape = circle;

            ConnectColliderSignals(partCollider);
        }
    }

    public virtual void SealJoin() {
        // Temporary fix for interpolation not working
        this.GlobalTransform = this.destTransform;
        Print("Sealed");
        this.t = 0f;
        this.joining = false;
        this.activeCollider.ToggleLinkVisibility(false);
        Part receiverPart = this.activeCollider.GetBoundCollider().associatedPart;
        receiverPart.receiving = false;
        receiverPart.CallDeferred(nameof(AttachPart), this);
    }

    public override void _Ready() {
        this.bindingQuads = this.bindingQuads ?? []; 
        this.editorMode = false;
        this.dragging = false;
        this.joining = false;
        this.receiving = false;
        this.colliderScene = Load<PackedScene>("src/Things/Parts/PartCollider.tscn");

        this.skinMesh = GetSkinMesh();
        this.selectionGlowMaterial = Load<Material>("src/Materials/SelectionGlowMaterial.tres");
        this.secondarySelectionGlowMaterial = Load<Material>("src/Materials/SecondarySelectionGlow.tres");
    }

    public override void _Process(double delta) {
        if (this.editorMode) {

            if (this.activeCollider != null && this.joining) {
                float diffy = 0.001f;
                t += (float)delta * 10f;
                t = Mathf.Clamp(t, 0f, 1f);

                this.GlobalTransform = this.startTransform.InterpolateWith(destTransform, t);

                bool positionCheck = this.GlobalTransform.Origin.DistanceTo(destTransform.Origin) <= diffy;
                Quaternion qa = this.GlobalTransform.Basis.GetRotationQuaternion();
                Quaternion qb = destTransform.Basis.GetRotationQuaternion();
                bool rotationCheck = qa.AngleTo(qb) <= diffy;
                bool scaleCheck = this.GlobalTransform.Basis.Scale.DistanceTo(destTransform.Basis.Scale) <= diffy;

                if (positionCheck && rotationCheck && scaleCheck) {
                    SealJoin();
                }
            }
        }
    }
}
