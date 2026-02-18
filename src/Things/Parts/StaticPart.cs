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
    public override MeshInstance3D GetSkinMesh() {
        return (MeshInstance3D)this.GetChildren().First(x => x.Name.ToString().EndsWith("Mesh") && x is MeshInstance3D);
    }

    // Receiver
    public override void AttachPart(Part connector) { 
        base.AttachPart(connector);
        connector.Reparent(this);
        connector.parentPart = connector.GetPathTo(this);
    }

    // Receiver
    public override void DetachPart(Part connector) {
        base.DetachPart(connector);
    }

    public override void StopSelected() {
        foreach (Node node in this.GetChildren()) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.StopSelected();
    }

    public override void MoveSelected() {
        base.MoveSelected();
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

    public override void _Ready() {
        this.bindingQuads = [.. this.GetChildren().Where(x => x is AlignmentPlane mesh).Cast<AlignmentPlane>().ToList()];
        base._Ready();
    }
}
