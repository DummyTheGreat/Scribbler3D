using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class ThingEditorSpace : WorldSpace
{
    [Signal]
    public delegate void PartIsMovingEventHandler(Part part, bool state);

    [Signal]
    public delegate void PartSelectedEventHandler(Node3D selected);

    private Part selectedPart;
    private float collisionRayLength = 1000f;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private bool moving;
    private MouseButton heldButton;
    private float yaw;
    private float pitch;
    private Vector3 pivot;

    public override void CreateThingChild(Resource thingData) {
        base.CreateThingChild(thingData);
        Thing thing = Thing.Create(thingData);
        thing.Assemble(this);
        // Animation stuff
        Part[] newChildren = [.. this.GetChildren().OfType<Part>().Where(x => x.thing == thing)];
        foreach (Part part in newChildren) {
            if (part.animationPlayer == null) {
                AnimationPlayer placeholder = new();
                part.AddChild(placeholder);
                part.animationPlayer = placeholder;
            }
        }
    }

    public override void _Input(InputEvent @event) {
        Camera3D camera = GetViewport().GetCamera3D();
        if (@event is InputEventMouseButton { Pressed: true } mouse && (mouse.ButtonIndex == MouseButton.Left || mouse.ButtonIndex == MouseButton.Right)) {
            if (camera == null) return;

            Vector3 origin = camera.ProjectRayOrigin(mouse.GlobalPosition);
            Vector3 direction = camera.ProjectRayNormal(mouse.GlobalPosition);
            Vector3 end = origin + direction * this.collisionRayLength;

            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            Godot.Collections.Dictionary collisions = this.GetParent<WorldRoot>().GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (collisions.Count == 0) {
                if (mouse.ButtonIndex == MouseButton.Left) {
                    this.selectedPart?.Unselected();
                    this.selectedPart = null;
                    EmitSignalPartSelected(null);
                }
                return;
            }

            MeshInstance3D hit = (collisions["collider"].AsGodotObject() as Node).GetParentOrNull<MeshInstance3D>();
            Part clickedPart = hit?.GetParentOrNull<Part>();
            if (clickedPart == null) return;
            
            if (clickedPart != this.selectedPart) { // Selected
                this.selectedPart?.Unselected();
                clickedPart.Selected();
                this.selectedPart = clickedPart;
                EmitSignalPartSelected(clickedPart);
            }
            else { // Moving selected
                Vector3 clickPosition = (Vector3)collisions["position"];
                this.dragPlane = new Plane(camera.GlobalTransform.Basis.Z, clickPosition);
                this.dragOffset = this.selectedPart.GlobalPosition - clickPosition;

                this.selectedPart.MoveSelected();
                EmitSignalPartIsMoving(this.selectedPart, true);
                this.heldButton = mouse.ButtonIndex;
                Input.MouseMode = mouse.ButtonIndex == MouseButton.Right ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.ConfinedHidden;
                this.moving = true;
            }
        }
        else if (@event is InputEventMouseButton { Pressed: false } endmouse && (endmouse.ButtonIndex == MouseButton.Left || endmouse.ButtonIndex == MouseButton.Right)) {
            if (this.moving) {
                this.selectedPart?.StopSelected();
                EmitSignalPartIsMoving(this.selectedPart, false);
                Input.MouseMode = Input.MouseModeEnum.Visible;
                this.moving = false;
            }
        }

        if (@event is InputEventMouseMotion motion && this.moving) {
            if (this.heldButton == MouseButton.None) return;

            if (this.heldButton == MouseButton.Left) {
                Vector3 origin = camera.ProjectRayOrigin(motion.GlobalPosition);
                Vector3 direction = camera.ProjectRayNormal(motion.GlobalPosition);

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
    }
}
