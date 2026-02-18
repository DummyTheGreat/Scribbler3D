using Godot;
using static Godot.GD;
using System;

public partial class ThingEditorSpace : Node3D
{
    //[Signal]
    //public delegate void SelectedPartUpdatedEventHandler(Part part);
    public Part selectedPart;

    private float collisionRayLength = 1000f;
    private Camera3D camera;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Transform3D startTransform;

    private bool moving;
    private MouseButton heldButton;
    private EditorController controller;
    private Vector2 mouseStart;
    private float yaw;
    private float pitch;
    private Vector3 pivot;

    public override void _Input(InputEvent @event) {
        if (@event is InputEventMouseButton { Pressed: true } mouse && (mouse.ButtonIndex == MouseButton.Left || mouse.ButtonIndex == MouseButton.Right)) {
            this.camera = GetViewport().GetCamera3D();
            if (this.camera == null) return;

            Vector3 origin = this.camera.ProjectRayOrigin(mouse.GlobalPosition);
            Vector3 direction = this.camera.ProjectRayNormal(mouse.GlobalPosition);
            Vector3 end = origin + direction * this.collisionRayLength;

            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            Godot.Collections.Dictionary collisions = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (collisions.Count == 0) return;

            MeshInstance3D hit = (collisions["collider"].AsGodotObject() as Node).GetParentOrNull<MeshInstance3D>();
            Part clickedPart = hit?.GetParentOrNull<Part>();
            if (clickedPart == null) return;

            if (clickedPart != this.selectedPart) {
                Print("hee");
                this.selectedPart?.Unselected();
                clickedPart.Selected();
                this.selectedPart = clickedPart;
            }
            else {
                // Position
                Vector3 clickPosition = (Vector3)collisions["position"];
                this.dragPlane = new Plane(this.camera.GlobalTransform.Basis.Z, clickPosition);
                this.dragOffset = this.selectedPart.GlobalPosition - clickPosition;

                this.selectedPart.MoveSelected();
                this.controller.ToggleInput(false);
                this.heldButton = mouse.ButtonIndex;
                Input.MouseMode = mouse.ButtonIndex == MouseButton.Right ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.ConfinedHidden;
                this.moving = true;
            }
        }
        else if (@event is InputEventMouseButton { Pressed: false } endmouse && (endmouse.ButtonIndex == MouseButton.Left || endmouse.ButtonIndex == MouseButton.Right)) {
            if (this.moving) {
                this.selectedPart?.StopSelected();
                this.controller.ToggleInput(true);
                Input.MouseMode = Input.MouseModeEnum.Visible;
                this.moving = false;
            }
        }

        if (@event is InputEventMouseMotion motion && this.moving) {
            if (this.heldButton == MouseButton.None) return;

            if (this.heldButton == MouseButton.Left) {
                Vector3 origin = this.camera.ProjectRayOrigin(motion.GlobalPosition);
                Vector3 direction = this.camera.ProjectRayNormal(motion.GlobalPosition);

                Vector3? point = dragPlane.IntersectsRay(origin, direction);
                if (point == null) return;

                this.selectedPart.GlobalPosition = point.Value + dragOffset;
            }
            else {
                float speed = 0.02f;

                this.yaw += -motion.Relative.X * speed;
                this.pitch -= motion.Relative.Y * speed;

                this.selectedPart.GlobalRotation = new Vector3(selectedPart.GlobalRotation.X, yaw, pitch);
            }
        }
    }

    public override void _Ready() {
        this.controller = this.GetChild<EditorController>(0);
        this.camera = this.controller.GetChild<Node3D>(0).GetChild<Camera3D>(0);

    }
}
