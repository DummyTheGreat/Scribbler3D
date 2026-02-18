using Godot;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class Part : Node3D {

    [Export]
    public NodePath parentPart;

    // Only includes connectors of different thing type from the part's thing
    [Export]
    public Godot.Collections.Array<Part> connectedParts;

    [Export]
    public int depth;

    private Material selectionGlowMaterial;

    // This doesn't change until the part a new Thing is CREATED, not just when the part connects to another
    public Thing thing;
    public string partName;

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

    public ThingEditorSpace space;
    public PackedScene scene;


    private static Basis MakeRightHanded(Basis b) {
        //b = b.Orthonormalized();
        if (b.Determinant() < 0f) {
            // Flip one axis to remove the reflection (choose X by convention)
            b.X = -b.X;
        }
        return b;
    }

    public static Transform3D CalculateJoinTransform(AlignmentPlane connectingPlane, AlignmentPlane receivingPlane, Transform3D connectorGlobalPos, Part part) {

        static Vector3 ProjectOntoPlane(Vector3 v, Vector3 n) => v - n * n.Dot(v);

        // Get the receiver's points of interest in global format
        Vector3 centerBWorld = receivingPlane.GlobalTransform * receivingPlane.GetCentroid();
        Vector3 frontBWorld = receivingPlane.GlobalTransform * receivingPlane.GetFront();

        // receiver plane B's global normal
        Basis nmB = receivingPlane.GlobalTransform.Basis.Inverse().Transposed();
        Vector3 normalBWorld = (nmB * receivingPlane.GetLocalNormal()).Normalized();

        // The objective is for plane A to lay facing plane B so its normal (up basis) should face plane B's normal
        Vector3 upBWorld = (-normalBWorld).Normalized();
        // Target forward basis of receiving plane
        Vector3 forwardBWorld = ProjectOntoPlane(frontBWorld - centerBWorld, normalBWorld).Normalized();
        Vector3 rightBWorld = upBWorld.Cross(forwardBWorld).Normalized();

        Basis targetBasisW = new(rightBWorld, upBWorld, forwardBWorld);

        Basis aLocalFrame = connectingPlane.GetLocalFrame();

        float sx = (connectingPlane.GetDimensions().X > 1e-8f) ? (receivingPlane.GetDimensions().X / connectingPlane.GetDimensions().X) : 1f;
        float sz = (connectingPlane.GetDimensions().Y > 1e-8f) ? (receivingPlane.GetDimensions().Y / connectingPlane.GetDimensions().Y) : 1f;

        // scale only in-plane axes of the *target frame*
        Basis S = new(
            new Vector3(sx, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, sz)
        );

        // Desired global basis for plane A
        Basis desiredQuadABasisW = targetBasisW * S * aLocalFrame.Inverse();
        //desiredQuadABasisW = MakeRightHanded(desiredQuadABasisW);

        // Matches front vertices and origins together for positional correctness
        Vector3 desiredQuadAOriginW = frontBWorld - (desiredQuadABasisW * connectingPlane.GetFront());
        Transform3D desiredQuadAGlobal = new(desiredQuadABasisW, desiredQuadAOriginW);

        // Transform the Transform3D to be relative to this Part and not it's child quad so that this Part
        // will transform properly

        // REMOVE THIS OR SOMETHING FOR STATIC
        //if (part is DeformingPart) {
        Transform3D quadInPart = connectorGlobalPos.AffineInverse() * connectingPlane.GlobalTransform; // <-- Remove for static part?
        return desiredQuadAGlobal * quadInPart.AffineInverse();
        //}
        //else {
        //    return desiredQuadAGlobal;
        //}

    }

    private Part GetParentPart() {
        return GetNodeOrNull<Part>(this.parentPart);
    }

    public void ToggleEditorMode() {
        this.editorMode = !this.editorMode;
        EstablishColliders();
    }

    public void SetDrag(bool d) {
        this.dragging = d;
    }

    public virtual MeshInstance3D GetSkinMesh() {
        return null;
    }

    // Receiver
    public virtual void AttachPart(Part connector) {
        if (!this.connectedParts.Contains(connector)) {
            this.connectedParts.Add(connector);
            this.connectedParts.Sort();
        }
    }

    // Receiver
    public virtual void DetachPart(Part connector) {
        connector.Reparent(this.space);
        connector.parentPart = null;
        this.connectedParts.Remove(connector);
    }

    public virtual void Unselected() {
        int surfaceCount = this.skinMesh.GetSurfaceOverrideMaterialCount();
        for (int i = 0; i < surfaceCount; i++) {
            this.skinMesh.SetSurfaceOverrideMaterial(i, null);
        }
    }

    public virtual void StopSelected() {
        if (this.activeCollider != null) {
            JoiningInitialization();
            this.joining = true;
            this.activeCollider.GetBoundCollider().associatedPart.receiving = true;
        }
    }

    public virtual void Selected() {
        int surfaceCount = this.skinMesh.GetSurfaceOverrideMaterialCount();
        for (int i = 0; i < surfaceCount; i++) {
            this.skinMesh.SetSurfaceOverrideMaterial(i, this.selectionGlowMaterial);
        }
    }

    public virtual void MoveSelected() {
        if (this.activeCollider != null && this.GetParent() is not Thing) {
            Part receiverPart = this.activeCollider.GetBoundCollider().associatedPart;
            receiverPart.CallDeferred(nameof(DetachPart), this);
        }
    }

    public Part PackPart() {
        this.Owner = null;
        this.parentPart = null;
        this.connectedParts = [];
        static void TransferTreeOwnership(Node node, Node newOwner) {
            foreach (Node child in node.GetChildren()) {
                child.Owner = newOwner;
                TransferTreeOwnership(child, newOwner);
            }
        }
        TransferTreeOwnership(this, this);
        PackedScene partScene = new();
        partScene.Pack(this);
        Part newPart = partScene.Instantiate<Part>();
        newPart.scene = partScene;
        return newPart;
    }


    public virtual void AddPartCollider(PartCollider collider, MeshInstance3D quad) { }

    public virtual void JoiningInitialization() {


        PartCollider connector = activeCollider; // Plane A collider
        PartCollider receiver = activeCollider.GetBoundCollider(); // Plane B collider

        this.destTransform = CalculateJoinTransform(connector.plane, receiver.plane, this.GlobalTransform, this);
        this.startTransform = this.GlobalTransform;
        this.t = 0f;
    }

    public void ConnectColliderSignals(PartCollider collider) {
        collider.PartConnect += PartConnect;
        collider.PartDisconnect += PartDisconnect;
    }

    // Signal function recieved from PartCollider
    private void PartConnect(PartCollider newCollider) {
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
            if (planeNormal.Dot(partOutWorld) < 0f) {
                quad.FlipNormal();
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
        this.partName = this.GetMeta("extras").AsGodotDictionary<string, string>()["PartName"];
        this.colliderScene = Load<PackedScene>("src/Things/Parts/PartCollider.tscn");

        this.skinMesh = GetSkinMesh();
        this.selectionGlowMaterial = Load<Material>("src/Materials/SelectionGlowMaterial.tres");

        //int surfaces = this.skinMesh.GetSurfaceOverrideMaterialCount();
        //for (int i = 0; i < surfaces; i++)
        //    _original[i] = Target.GetSurfaceOverrideMaterial(i);

        //int count = 0; Part root = this;
        //while (root.GetParentPart() is not null) { count++; root = root.GetParentPart(); }
        //this.depth = count;
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
