using Godot;
using System;
using System.Collections.Generic;
using static Godot.GD;

public partial class FreeCam : Node3D
{
    public float LookSensitivity = 0.0025f;
    public float PanSensitivity = 0.01f;
    public float MinPitch = -80f;
    public float MaxPitch = 80f;

    public float ZoomStep = 0.2f;
    public float MinZoom = 1.0f;
    public float MaxZoom = 20.0f;

    private Camera3D camera;
    private Node3D pivot;
    private bool panning;
    private bool allowRotate;
    private float zoomAxis;

    public void ToggleInput(Thing p, bool state) {
        SetProcessInput(!state);
        this.allowRotate = false;
        this.panning = false;
    }

    public override void _Input(InputEvent @event) {

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } right) {
            allowRotate = right.Pressed;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle) {
            panning = middle.Pressed;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp }) {
            zoomAxis = Mathf.Clamp(zoomAxis - ZoomStep, MinZoom, MaxZoom);
            camera.Position = new Vector3(camera.Position.X, camera.Position.Y, zoomAxis);
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.WheelDown }) {
            zoomAxis = Mathf.Clamp(zoomAxis + ZoomStep, MinZoom, MaxZoom);
            camera.Position = new Vector3(camera.Position.X, camera.Position.Y, zoomAxis);
        }

        if (@event is InputEventMouseMotion motion) {

            if (allowRotate) {
                // Yaw
                RotateY(-motion.Relative.X * LookSensitivity);
                // Pitch
                float pitch = pivot.Rotation.X;
                pitch -= motion.Relative.Y * LookSensitivity;
                pitch = Mathf.Clamp(
                    pitch,
                    Mathf.DegToRad(MinPitch),
                    Mathf.DegToRad(MaxPitch)
                );
                pivot.Rotation = new Vector3(pitch, 0f, 0f);
            }

            if (panning) {
                this.GlobalPosition += (-camera.GlobalTransform.Basis.X * motion.Relative.X + 
                    camera.GlobalTransform.Basis.Y * motion.Relative.Y) * PanSensitivity;
            }
        }
    }

    public override void _Ready() {
        pivot = this.GetChild<Node3D>(0);
        camera = pivot.GetChild<Camera3D>(0);
        zoomAxis = camera.Position.Z;
    }

    public override void _Process(double delta) {

    }
}
