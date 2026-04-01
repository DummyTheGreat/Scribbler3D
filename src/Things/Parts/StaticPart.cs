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
        return (MeshInstance3D)this.GetChildren().First(x => x.Name.ToString().StartsWith("Skin") && x is MeshInstance3D);
    }

    // Receiver
    public override void AttachPart(Part connector) {
        Node receiverGroup = connector.activeCollider.GetBoundCollider().GetParentOrNull<Node>();
        connector.Reparent(receiverGroup);
        base.AttachPart(connector);
    }

    // Receiver
    public override void DetachPart(Part connector) {
        base.DetachPart(connector);
    }

    public override void StopSelected() {
        foreach (Node node in this.GetChildren().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.StopSelected();
    }

    public override void MoveSelected() {
        base.MoveSelected();
        foreach (Node node in this.GetChildren().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        Node group = this.GetChildren().Where(x => x.GetChildren().Contains(quad)).FirstOrDefault();
        group.AddChild(collider);
    }

    public override void _Ready() {
        this.bindingQuads = [.. this.GetChildren().SelectMany(x => x.GetChildren()).OfType<AlignmentPlane>()];
        base._Ready();
    }
}
