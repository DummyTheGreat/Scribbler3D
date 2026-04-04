using Godot;
using System;
using System.Linq;
using static Godot.GD;
using static System.Formats.Asn1.AsnWriter;

public partial class WorldRoot : Node3D
{
    public Camera3D camera;
    private Node3D selected;

    public enum EditorState {
        Editor,
        Testing
    }

    public EditorState state;

    public T GetSelected<T>() where T : class {
        if (this.selected is T res) {
            return res;
        }
        return default;
    }

    public WorldSpace GetWorldSpace<T>() where T : WorldSpace {
        if (this.selected is T res) {
            return res;
        }
        return this.GetChild<WorldSpace>(0);
    }

    public void ClearSelected() {
        this.selected = null;
    }

    public void SwitchState(PackedScene newScene) {
        Node3D testGrounds = newScene.Instantiate<Node3D>();
        Node3D currentChild = this.GetChild<Node3D>(0);
        this.RemoveChild(currentChild);
        currentChild.QueueFree();

        this.AddChild(testGrounds);
        this.MoveChild(testGrounds, 0);
    }

    private void SetSelected(Node3D selected) {
        this.selected = selected;
    }

    public override void _Ready() {
        FreeCam con = Load<PackedScene>("res://src/World/FreeCam.tscn").Instantiate<FreeCam>();
        this.AddChild(con);
        this.camera = con.GetChild<Node3D>(0).GetChild<Camera3D>(0);
        this.camera.Position = new Vector3(0f, 0f, 10f);

        ThingEditorSpace space = this.GetChild<ThingEditorSpace>(0);
        space.PartIsMoving += con.ToggleInput;
        space.PartSelected += SetSelected;

        this.state = EditorState.Editor;
    }
}
