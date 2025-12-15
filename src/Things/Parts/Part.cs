using Godot;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class Part : MeshInstance3D {

    public int siblingIndex;

    private List<MeshInstance3D> bindingPoints;
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

    [Signal]
    public delegate void PartSelectedEventHandler(Part part, int index);

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

    public void CancelClick() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        EmitSignal(SignalName.PartSelected, this, -1);
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
        // Set part to recive this part as recieving a join
        //this.activeCollider.GetBoundCollider().GetAssociatedPart().recieving = true;

        // Rotational destination calc
        //destRotation = (this.activeCollider.GetBoundCollider().GlobalRotation - this.activeCollider.Rotation) % (2 * Mathf.Pi);
        //destRotation = destRotation < 0 ? destRotation + (2 * Mathf.Pi) : destRotation;

        // Scale destination calc
        //float scaleRatio = this.activeCollider.GetBoundCollider().GetDiameter() / this.activeCollider.GetDiameter();
        //destScale = this.Scale * scaleRatio;
        //this.activeCollider.SetDiameter(this.activeCollider.GetDiameter() * scaleRatio);

        // Simulate the rotation and scaling on the part beforehand in order to accurately determine its positional destination
        //Transform2D transformedPart = new(destRotation, destScale, this.Skew, this.GlobalTransform.Origin);
        //Transform2D simulatedColliderTransform = transformedPart * this.activeCollider.Transform;
        //Vector2 colliderOffset = this.GlobalPosition - simulatedColliderTransform.Origin;
        //destPosition = this.activeCollider.GetBoundCollider().GlobalPosition + colliderOffset;
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
        for (int i = 0; i < this.bindingPoints.Count - 1; i++) {

            Print(this.bindingPoints[i].Mesh.SurfaceGetArrays(0));

            //PartCollider partCollider = colliderScene.Instantiate<PartCollider>();
            //this.AddChild(partCollider);

            //Vector2 vec = line.Item2 - line.Item1;
            //Vector2 perpNormal = new Vector2(-vec.Y, vec.X).Normalized();
            //Vector2 midpoint = (line.Item2 + line.Item1) * 0.5f;

            //// Set collider up based on the boundary line's endpoint coordinates
            //partCollider.Position = midpoint;
            //partCollider.Rotate(vec.Angle());
            //partCollider.SetDiameter(vec.Length());
            ////vec.Rotated()
            //RectangleShape2D box = new() { Size = new Vector2(vec.Length(), 30f) };
            //CircleShape2D circle = new() { Radius = vec.Length() * 0.5f };
            //partCollider.GetChild<CollisionShape2D>(0).Shape = circle;

            //partCollider.PartConnect += PartConnect;
            //partCollider.PartDisconnect += PartDisconnect;

            //// Basically iterate forwards only once if the current line is continuing otherwise iterate forward twice
            //if (i + 1 < this.bindingPoints.Count - 1 &&
            //    (this.bindingPoints[i + 1].Name.ToString()[0] == this.bindingPoints[i + 2].Name.ToString()[0])) {
            //    i--;
            //}
            //i++;
        }
    }

    public override void _Ready() {

        this.bindingPoints = [.. this.GetChildren().Where(x => x.GetType() == typeof(MeshInstance3D)).ToList().Cast<MeshInstance3D>()];
        this.editorMode = false;
        this.dragging = false;
        this.joining = false;
        this.recieving = false;
        this.dragOffset = Vector3.Zero;
        this.colliderScene = Load<PackedScene>("src/Things/Parts/PartCollider.tscn");
        this.destScale = Vector3.One;
        this.siblingIndex = this.GetIndex();
    }

    //public override void _Input(InputEvent @event) {

    //    if (@event is InputEventMouseMotion motion) {
    //        this.GlobalPosition += (-camera.GlobalTransform.Basis.X * motion.Relative.X +
    //            camera.GlobalTransform.Basis.Y * motion.Relative.Y) * PanSensitivity;
    //    }

    //    if (this.dragging && @event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }) {
    //        CancelClick();
    //    }
    //    else if (this.dragging && @event is InputEventMouseMotion motion) {
    //            this.GlobalPosition = motion.Position - this.dragOffset;
    //    }
    //}

    public override void _Process(double delta) {
        //if (this.editorMode) {

        //    if (this.activeCollider != null && this.joining) {
        //        t += (float)delta * 0.5f;
        //        //t = -(Math.Cos(Math.PI * t) - 1) / 2.0;

        //        this.GlobalPosition = this.GlobalPosition.Lerp(destPosition, t);
        //        this.GlobalRotation = Mathf.LerpAngle(this.GlobalRotation, destRotation, t);
        //        this.Scale = this.Scale.Lerp(this.destScale, t);

        //        float partRotationPosCompare = this.GlobalRotation < 0 ? this.GlobalRotation + (2 * Mathf.Pi) : this.GlobalRotation;
        //        if (this.GlobalPosition.IsEqualApprox(destPosition) && Mathf.IsEqualApprox(partRotationPosCompare, destRotation)) {
        //            Print("Sealed");
        //            this.t = 0f;
        //            this.joining = false;
        //            this.Reparent(activeCollider.GetBoundCollider().GetAssociatedPart());
        //            this.activeCollider.ToggleLinkVisibility(false);
        //            this.activeCollider.GetBoundCollider().GetAssociatedPart().recieving = false;

        //        }
        //    }
        //}
    }
}
