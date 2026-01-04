using Godot;
using static Godot.GD;

using System;
using System.Linq;

/*
 * Static Parts are parts that will not have any changes to their mesh's form.
 * 
 * They possess a node hierarchy like such:
 * 
 * Root (StaticPart)
 * ├── Mesh (MeshInstance3D)
 * ├── Connector/Receiver (MeshInstance3D)
 * ├── ...
 * ├── Child Part (Part)
 * ├── ...
 */
public partial class StaticPart : Part
{
    public override void _Ready() {
        base._Ready();
        this.bindingQuads = [.. this.GetChildren().Where(x => x is MeshInstance3D mesh && mesh.HasMeta("IsReceiver")).Cast<MeshInstance3D>().ToList()];
    }

    public override void Unselected() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.Unselected();
    }

    public override void Selected() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        this.AddChild(collider);
    }
}
