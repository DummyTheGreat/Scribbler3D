using Godot;
using static Godot.GD;
using System;

public partial class ThingEditorSpace : Node3D
{
    private float collisionRayLength = 1000f;
    private Camera3D camera;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Transform3D startTransform;
    private Part collidingPart;
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
            this.collidingPart = hit?.GetParentOrNull<Part>();

            if (this.collidingPart == null) return;

            // Rotation
            this.pivot = hit.GlobalTransform.Origin;
            //this.startRotation = this.collidingPart.GlobalTransform;

            // Position
            Vector3 clickPosition = (Vector3)collisions["position"];
            this.dragPlane = new Plane(this.camera.GlobalTransform.Basis.Z, clickPosition);
            this.dragOffset = this.collidingPart.GlobalPosition - clickPosition;

            this.collidingPart.Selected();
            this.controller.ToggleInput(false);
            this.heldButton = mouse.ButtonIndex;
            Input.MouseMode = mouse.ButtonIndex == MouseButton.Right ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.ConfinedHidden;
        }
        else if (@event is InputEventMouseButton { Pressed: false } endmouse && (endmouse.ButtonIndex == MouseButton.Left || endmouse.ButtonIndex == MouseButton.Right)) {
            this.collidingPart?.Unselected();
            this.collidingPart = null;
            this.controller.ToggleInput(true);
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && this.collidingPart != null) {
            if (this.heldButton == MouseButton.None) return;

            if (this.heldButton == MouseButton.Left) {
                Vector3 origin = this.camera.ProjectRayOrigin(motion.GlobalPosition);
                Vector3 direction = this.camera.ProjectRayNormal(motion.GlobalPosition);

                Vector3? point = dragPlane.IntersectsRay(origin, direction);
                if (point == null) return;

                collidingPart.GlobalPosition = point.Value + dragOffset;
            }
            else {
                float speed = 0.02f;

                this.yaw += -motion.Relative.X * speed;
                this.pitch -= motion.Relative.Y * speed;

                this.collidingPart.GlobalRotation = new Vector3(collidingPart.GlobalRotation.X, yaw, pitch);
            }
        }
    }

    public override void _Ready() {
        this.controller = this.GetChild<EditorController>(0);
        this.camera = this.controller.GetChild<Node3D>(0).GetChild<Camera3D>(0);

    }
}
