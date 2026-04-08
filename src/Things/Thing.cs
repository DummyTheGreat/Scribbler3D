using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.Control;
using static Godot.GD;

public partial class Thing : Node3D
{
    //[Signal]
    //public delegate void PartPreparedEventHandler();

    [Signal]
    public delegate void PartAnimationStatusEventHandler(bool status);

    public Thing[] ancestors;
    public StringName UID;
    public AnimationPlayer animationPlayer;

    public Thing parentThing;
    public Godot.Collections.Array<Thing> connectedThings = [];

    private Material selectionGlowMaterial;
    private Material secondarySelectionGlowMaterial;
    private MeshInstance3D skinMesh;

    public bool joining;
    public bool receiving;
    public List<AlignmentPlane> bindingQuads;
    [Export] public PartCollider activeCollider;

    private PackedScene colliderScene;
    private float t;
    private Transform3D destTransform;
    private Transform3D startTransform;

    public enum State {
        Editor,
        Active
    }

    public State state;

    private Dictionary<StringName, List<Thing>> thingTracker;

    public void InitTracker() { thingTracker = []; }

    public void AddToTracker(Thing thing) {
        if (!this.thingTracker.TryAdd(thing.UID, [thing])) {
            this.thingTracker[thing.UID].Add(thing);
        }
    }

    public int GetTrackedCount(Thing thing) {
        this.thingTracker.TryGetValue(thing.UID, out List<Thing> list);
        if (list == null) { return 0; }
        return list.Count;
    }

    public bool RemoveFromTracker(Thing thing) {
        this.thingTracker.TryGetValue(thing.UID, out List<Thing> list);
        return list.Remove(thing);
    }

    public partial class ImportData : Resource {

        public string complexName;
        public string singletonName;
        public NodePath receiverPath;
        public PackedScene scene;
        public Godot.Collections.Array<Resource> boneData;
        public Godot.Collections.Array<Resource> connectedThingData;

        public ImportData(Resource thingData) {
            this.receiverPath = thingData.Get("pathToReceiver").AsNodePath();
            this.boneData = thingData.Get("boneData").AsGodotArray<Resource>();
            this.singletonName = thingData.Get("singletonName").AsString();
            this.complexName = thingData.Get("complexName").AsString();
            this.scene = thingData.Get("scene").As<PackedScene>();
            this.connectedThingData = thingData.Get("connectedThings").AsGodotArray<Resource>();
        }
    }

    public ImportData importData;

    public static Thing Create(Resource thingData) {
        Thing newThing = thingData.Get("scene").As<PackedScene>().Instantiate<Thing>();
        newThing.importData = new(thingData);
        newThing.InitTracker();
        newThing.ProcessImportData();
        return newThing;
    }

    public static Thing Create(ImportData importData) {
        Thing newThing = importData.scene.Instantiate<Thing>();
        newThing.importData = importData;
        newThing.InitTracker();
        newThing.ProcessImportData();
        return newThing;
    }

    public virtual void ProcessImportData() {}

    public virtual MeshInstance3D GetSkinMesh() {
        return null;
    }

    public enum TraversalType {
        Collider,
        Parent
    }

    // Get Thing at the top of the hierarchy
    public Thing GetHierarch(TraversalType t) {
        Thing topPart = this;
        if (t == TraversalType.Collider) {
            while (topPart.activeCollider != null) {
                topPart = topPart.activeCollider.GetBoundCollider().associatedPart;
            }
            return topPart;
        }
        else {
            while (topPart.parentThing != null) {
                topPart = topPart.parentThing;
            }
            return topPart;
        }
    }

    public void PrepareThing(AlignmentPlane connector, Node parent) {
        string partName = this.GetMeta("PartName").AsString();
        string thingName = this.GetMeta("ThingName").AsString();
        int variantNum = this.GetMeta("PartVariant").AsInt32();
        bool isMirror = this.GetMeta("IsMirror").AsBool();
        this.UID = thingName + partName + (isMirror ? "Mirror" : "");

        int trackedCount = 0;
        if (parent is Thing tParent) {
            trackedCount = tParent.GetTrackedCount(this);
            tParent.AddToTracker(this);
        }

        this.Name = "Part_" + thingName + "_" + partName + (isMirror ? "_Mirror" : "") + "_V" + variantNum.ToString() + "_I" + (trackedCount + 1).ToString();
        this.animationPlayer = this.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
        AlignmentPlane receiver = null;
        if (parent is Thing thingParent) {
            receiver = thingParent.GetNode<AlignmentPlane>(this.importData.receiverPath);
            receiver.AddSibling(this);
            this.parentThing = thingParent;
            thingParent.connectedThings.Add(this);

            if (this.animationPlayer == null) {
                this.animationPlayer = new();
                this.AddChild(this.animationPlayer);
            }
            ThingTools.MergeAnimations(GetHierarch(TraversalType.Parent), this);
        }
        else {
            parent.AddChild(this);
        }
        EstablishColliders();
        if (receiver != null) {
            this.GlobalTransform = ThingTools.CalculateJoinTransform(connector, receiver, this.GlobalTransform);
        }
    }

    public void Assemble(Node parent) {

        static AlignmentPlane FindConnector(Node node) {
            if (node is AlignmentPlane plane && plane.GetMeta("MeshType").AsString() == "Connector") {
                return plane;
            }

            foreach (Node child in node.GetChildren()) {
                AlignmentPlane connectorPlane = FindConnector(child);
                if (connectorPlane != null) {
                    return connectorPlane;
                }
            }
            return null;
        }
        AlignmentPlane connector = FindConnector(this);
        PrepareThing(connector, parent);

        foreach (Resource conData in this.importData.connectedThingData) {
            Thing childThing = Thing.Create(conData);
            childThing.Assemble(this);
        }
    }

    // Receiver
    public virtual void AttachPart(Thing connector) {
        connector.parentThing = this;
        if (!this.connectedThings.Contains(connector)) {
            this.connectedThings.Add(connector);
        }
        ThingTools.MergeAnimations(GetHierarch(TraversalType.Collider), connector);
    }

    // Receiver
    public virtual void DetachPart(Thing connector) {
        ThingTools.SplitAnimations(this, connector);
        connector.Reparent(GetHierarch(TraversalType.Collider).GetParent());
        connector.parentThing = null;
        this.connectedThings.Remove(connector);
        connector.Scale = new Vector3(1, 1, 1);
    }

    public virtual void Unselected() {
        static void TraverseAll(Thing thing) {
            int surfaceCount = thing.skinMesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++) {
                thing.skinMesh.SetSurfaceOverrideMaterial(i, null);
            }

            foreach (Thing child in thing.connectedThings) {
                TraverseAll(child);
            }
        }
        Thing t = GetHierarch(TraversalType.Collider);
        TraverseAll(t);
    }

    public virtual void Selected() {
        static void TraverseAll(Thing thing, Thing primary) {
            int surfaceCount = thing.skinMesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++) {
                thing.skinMesh.SetSurfaceOverrideMaterial(i, thing == primary ? thing.selectionGlowMaterial : thing.secondarySelectionGlowMaterial);
            }

            foreach (Thing child in thing.connectedThings) {
                TraverseAll(child, primary);
            }
        }
        Thing t = GetHierarch(TraversalType.Collider);
        TraverseAll(t, this);
    }

    public virtual void MoveSelected() {
        if (this.activeCollider != null && this.GetParent() is not Thing) {
            Thing receiverPart = this.activeCollider.GetBoundCollider().associatedPart;
            receiverPart.GetHierarch(TraversalType.Parent).CallDeferred(nameof(DetachPart), this);
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
            EmitSignalPartAnimationStatus(true);
            this.animationPlayer.Stop();
        }
        else {
            EmitSignalPartAnimationStatus(false);
            this.animationPlayer.Play(name);
        }
    }

    // Connector
    public void JoiningInitialization() {
        PartCollider connector = this.activeCollider; // Plane A collider
        PartCollider receiver = this.activeCollider.GetBoundCollider(); // Plane B collider

        this.destTransform = ThingTools.CalculateJoinTransform(connector.plane, receiver.plane, this.GlobalTransform);
        this.startTransform = this.GlobalTransform;
        this.t = 0f;
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
            Basis b = new(right, planeNormal, -newForward);

            partCollider.GlobalTransform = new Transform3D(b, partCollider.GlobalTransform.Origin);

            SphereShape3D circle = new() { Radius = (quad.GetCentroid() - quad.GetFront()).Length() };
            partCollider.GetChild<CollisionShape3D>(0).Shape = circle;
        }
    }

    public virtual void AddPartCollider(PartCollider collider, MeshInstance3D quad) { }

    public virtual void SealJoin() {
        // Temporary fix for interpolation not working
        this.GlobalTransform = this.destTransform;
        Print("Sealed");
        this.t = 0f;
        this.joining = false;
        this.activeCollider.ToggleLinkVisibility(false);
        Thing receiverThing = this.activeCollider.GetBoundCollider().associatedPart;
        receiverThing.receiving = false;
        receiverThing.CallDeferred(nameof(AttachPart), this);
    }



    public override void _Ready() {
        this.bindingQuads = this.bindingQuads ?? [];
        this.joining = false;
        this.receiving = false;
        this.colliderScene = Load<PackedScene>("src/Things/Parts/PartCollider.tscn");
        this.skinMesh = GetSkinMesh();

        this.selectionGlowMaterial = Load<Material>("src/Materials/SelectionGlowMaterial.tres");
        this.secondarySelectionGlowMaterial = Load<Material>("src/Materials/SecondarySelectionGlow.tres");
    }

    public override void _Process(double delta) {
        if (this.state == State.Editor) {

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
