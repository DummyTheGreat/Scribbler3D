using Godot;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class Part : MeshInstance3D {

    public int siblingIndex;

    private List<MeshInstance3D> bindingQuads;
    private bool editorMode;
    private bool dragging;
    private Vector3 dragOffset;
    private PackedScene colliderScene;
    private PartCollider activeCollider;
    private float t;
    public bool joining;
    private bool recieving;
    private Vector3 destPosition;
    private Vector3 destRotation;
    private Vector3 destScale;
    private Transform3D destTransform;

    [Signal]
    public delegate void PartSelectedEventHandler(Part part, int index);

    static Vector3 Centroid(Vector3[] v) {
        Vector3 sum = Vector3.Zero;
        foreach (var p in v) sum += p;
        return sum / v.Length;
    }

    static Basis MakeRightHanded(Basis b) {
        b = b.Orthonormalized();
        if (b.Determinant() < 0f) {
            // Flip one axis to remove the reflection (choose X by convention)
            b.X = -b.X;
        }
        return b;
    }

    public void ToggleEditorMode() {
        this.editorMode = !this.editorMode;
        EstablishColliders();
    }

    public void SetDrag(bool d) {
        this.dragging = d;
    }

    public void DeferredReparenting(Node newParent) {
        this.Reparent(newParent);
    }

    public void Unselected() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        //EmitSignal(SignalName.PartSelected, this, -1);
        this.joining = this.activeCollider != null;

        if (this.joining) {
            JoiningInitialization();
        }
    }

    public void Selected() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }

            this.activeCollider?.ToggleLinkVisibility(true);
            //EmitSignal(SignalName.PartSelected, this, this.GetIndex());
        }
    }

    public void JoiningInitialization() {

        static Vector3 ProjectOntoPlane(Vector3 v, Vector3 n) => v - n * n.Dot(v);

        PartCollider connector = activeCollider;                 // Plane A collider
        PartCollider receiver = activeCollider.GetBoundCollider(); // Plane B collider

        MeshInstance3D quadA = connector.plane; // connector plane A
        MeshInstance3D quadB = receiver.plane;  // receiver plane B

        // To be quite honest I'm not entirely sure what's going on here yet but eventually I will
        Vector3[] bVertsL = receiver.GetVertices();
        Vector3 bCenterW = quadB.GlobalTransform * Centroid(bVertsL);
        Vector3 bFrontW = quadB.GlobalTransform * bVertsL[receiver.GetFrontIndex()];

        // receiver plane B's global normal
        Basis nmB = quadB.GlobalTransform.Basis.Inverse().Transposed();
        Vector3 nB_W = (nmB * receiver.GetLocalNormal()).Normalized();

        // The objective is for plane A to lay facing plane B so its normal (up basis) should face plane B's normal
        Vector3 upTargetW = (-nB_W).Normalized();

        // Define target twist using plane B's forward 
        Vector3 fTargetW = ProjectOntoPlane(bFrontW - bCenterW, nB_W);
        if (fTargetW.LengthSquared() < 1e-10f) fTargetW = Vector3.Right;
        fTargetW = fTargetW.Normalized();
        fTargetW = ProjectOntoPlane(fTargetW, upTargetW).Normalized();

        Vector3 rTargetW = upTargetW.Cross(fTargetW).Normalized();
        if (rTargetW.LengthSquared() < 1e-10f) {
            fTargetW = ProjectOntoPlane(Vector3.Forward, upTargetW).Normalized();
            rTargetW = upTargetW.Cross(fTargetW).Normalized();
        }
        fTargetW = rTargetW.Cross(upTargetW).Normalized();

        Basis targetBasisW = new (rTargetW, upTargetW, fTargetW);

        // Basically find the connecting part's local basis so that its inverse can be used to calculate the proper 
        // rotational destination of the part
        Vector3[] aVertsL = connector.GetVertices();
        Vector3 aCenterL = Centroid(aVertsL);
        Vector3 aFrontL = aVertsL[connector.GetFrontIndex()];

        Vector3 upA_L = connector.GetLocalNormal().Normalized();

        Vector3 fA_L = ProjectOntoPlane(aFrontL - aCenterL, upA_L);
        if (fA_L.LengthSquared() < 1e-10f) fA_L = Vector3.Right;
        fA_L = fA_L.Normalized();
        fA_L = ProjectOntoPlane(fA_L, upA_L).Normalized();

        Vector3 rA_L = upA_L.Cross(fA_L).Normalized();
        if (rA_L.LengthSquared() < 1e-10f) {
            fA_L = ProjectOntoPlane(Vector3.Forward, upA_L).Normalized();
            rA_L = upA_L.Cross(fA_L).Normalized();
        }
        fA_L = rA_L.Cross(upA_L).Normalized();

        Basis aLocalFrame = new (rA_L, upA_L, fA_L);

        // Desired global basis for plane A
        Basis desiredQuadABasisW = targetBasisW * aLocalFrame.Inverse();

        // Matches front vertices and origins together for positional correctness
        Vector3 desiredQuadAOriginW = bFrontW - (desiredQuadABasisW * aFrontL);

        Transform3D desiredQuadAGlobal = new Transform3D(desiredQuadABasisW, desiredQuadAOriginW);

        // Transform the Transform3D to be relative to this Part and not it's child quad so that this Part
        // will transform properly
        Transform3D solved = desiredQuadAGlobal * quadA.Transform.AffineInverse();
        solved.Basis = MakeRightHanded(solved.Basis);
        this.destTransform = solved;
    }

    // Signal function recieved from PartCollider
    private void PartConnect(PartCollider newCollider, bool init) {
        this.activeCollider = newCollider;
        if (init) {
            this.JoiningInitialization();
            this.joining = true;
        }
    }

    // Signal function recieved from PartCollider
    private void PartDisconnect() {
        this.activeCollider = null;
        Node partsNode = this.FindParent("Parts");
        if (partsNode != this.GetParent()) {
            CallDeferred(nameof(DeferredReparenting), partsNode);
        }
    }

    private void EstablishColliders() {
        foreach (MeshInstance3D quad in this.bindingQuads) {
            Print(this.Name, this.bindingQuads.Count);
            Vector3[] vertices = (Vector3[])quad.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
            Vector3[] globalVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                globalVertices[i] = quad.GlobalTransform * vertices[i];

            float lowestX = float.MaxValue;
            int frontIndex = 0;

            for (int i = 0; i < globalVertices.Length; i++) {
                bool isTieX = Mathf.IsEqualApprox(globalVertices[i].X, lowestX);
                if ((isTieX && globalVertices[i].Y > globalVertices[frontIndex].Y) ||
                    (!isTieX && globalVertices[i].X < lowestX)) {
                    lowestX = globalVertices[i].X;
                    frontIndex = i;
                }
            }

            PartCollider partCollider = colliderScene.Instantiate<PartCollider>();
            this.AddChild(partCollider);
            // Center of plane
            Vector3 centroidLocal = Centroid(vertices);
            Vector3 VertexToCenter = centroidLocal - vertices[0];

            partCollider.plane = quad;

            // Set collider up based on the boundary line's endpoint coordinates
            partCollider.GlobalPosition = quad.GlobalTransform * centroidLocal;
            partCollider.SetFrontIndex(frontIndex);
            partCollider.SetVertices(vertices);

            // Find the geometric normal of the plane formed by the vertices and convert to global space
            partCollider.ComputeLocalNormalFromSurface(0);

            // Find the direction from which the quad connector is located relative to the part to determine which way its normal should face
            Vector3 quadCentroidWorld = quad.GlobalTransform * centroidLocal;
            Vector3 partCenterWorld = this.GlobalTransform.Origin;
            Vector3 outwardWorld = (quadCentroidWorld - partCenterWorld).Normalized();

            // Get global normal of quad connector
            Basis normalMatrix = quad.GlobalTransform.Basis.Inverse().Transposed();
            Vector3 planeNormal = (normalMatrix * partCollider.GetLocalNormal()).Normalized();

            // If normal points inward then flip the local normal
            if (planeNormal.Dot(outwardWorld) < 0f) {
                partCollider.SetLocalNormal(-partCollider.GetLocalNormal());
                planeNormal = -planeNormal;
            }

            // Rotate to align with normal of binding quad
            Vector3 forward = -GlobalTransform.Basis.Z;
            if (Mathf.Abs(planeNormal.Dot(forward.Normalized())) > 0.99f) { forward = Vector3.Forward; }

            Vector3 right = forward.Cross(planeNormal).Normalized();
            Vector3 newForward = planeNormal.Cross(right).Normalized();
            Basis b = new Basis(right, planeNormal, newForward);
            b = MakeRightHanded(b);
            partCollider.GlobalTransform = new Transform3D(b, partCollider.GlobalTransform.Origin);

            SphereShape3D circle = new() { Radius = VertexToCenter.Length() };
            partCollider.GetChild<CollisionShape3D>(0).Shape = circle;

            partCollider.PartConnect += PartConnect;
            partCollider.PartDisconnect += PartDisconnect;
        }
    }

    public override void _Ready() {

        this.bindingQuads = [.. this.GetChildren().Where(x => x.GetType() == typeof(MeshInstance3D)).ToList().Cast<MeshInstance3D>()];
        this.editorMode = false;
        this.dragging = false;
        this.joining = false;
        this.recieving = false;
        this.dragOffset = Vector3.Zero;
        this.colliderScene = Load<PackedScene>("src/Things/Parts/PartCollider.tscn");
        this.destScale = Vector3.One;
        this.siblingIndex = this.GetIndex();
    }

    public override void _Process(double delta) {
        if (this.editorMode) {

            if (this.activeCollider != null && this.joining) {
                t += (float)delta * 0.5f;
                //t = -(Math.Cos(Math.PI * t) - 1) / 2.0;

                //this.GlobalPosition = this.GlobalPosition.Lerp(destPosition, t);
                //this.GlobalRotation = this.GlobalRotation.Lerp(destRotation, t);
                this.GlobalTransform = this.GlobalTransform.InterpolateWith(destTransform, t);
                //this.Scale = this.Scale.Lerp(this.destScale, t);

                if (this.GlobalTransform.IsEqualApprox(destTransform)) {
                    Print("Sealed");
                    this.t = 0f;
                    this.joining = false;
                    //this.Reparent(activeCollider.GetBoundCollider().GetAssociatedPart());
                    //this.activeCollider.ToggleLinkVisibility(false);
                    this.activeCollider.GetBoundCollider().GetAssociatedPart().recieving = false;

                }
            }
        }
    }
}
