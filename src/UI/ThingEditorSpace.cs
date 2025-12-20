using Godot;
using static Godot.GD;
using System;

public partial class ThingEditorSpace : Node3D
{
    private float collisionRayLength = 1000f;
    private Camera3D camera;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Part collidingPart;
    public override void _Input(InputEvent @event) {
        //if (!this.editorMode || this.joining || this.recieving) return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) {
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

            this.collidingPart = (collisions["collider"].AsGodotObject() as Node).GetParentOrNull<Part>();
            if (this.collidingPart == null) return;

            Vector3 clickPosition = (Vector3)collisions["position"];
            this.dragPlane = new Plane(this.camera.GlobalTransform.Basis.Z, clickPosition);
            this.dragOffset = this.collidingPart.GlobalPosition - clickPosition;

            this.collidingPart.Selected();
        }
        else if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }) {
            this.collidingPart.Unselected();
            this.collidingPart = null;
        }

        if (@event is InputEventMouseMotion motion && this.collidingPart != null) {
            Vector3 origin = this.camera.ProjectRayOrigin(motion.GlobalPosition);
            Vector3 direction = this.camera.ProjectRayNormal(motion.GlobalPosition);

            var point = dragPlane.IntersectsRay(origin, direction);
            if (point == null) return;

            collidingPart.GlobalPosition = point.Value + dragOffset;
        }
    }

    public override void _Ready() {
        this.camera = this.GetChild<Node3D>(0).GetChild<Node3D>(0).GetChild<Camera3D>(0);
    }
}
