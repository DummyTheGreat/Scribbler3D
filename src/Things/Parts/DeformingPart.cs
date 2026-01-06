using Godot;
using static Godot.GD;

using System;
using System.Linq;

/*
 * Deforming Parts are parts that will have changes to their mesh's form through bone manipulation.
 * 
 * They possess a node hierarchy like such:
 * 
 * Root (DeformingPart)
 * ├── Skeleton (Skeleton3D)
 * ├───── Mesh (MeshInstance3D)
 * ├───── Connector/Receiver Bone Socket (BoneAttachment3D)
 * ├──────── Connector/Receiver (MeshInstance3D)
 * ├───── ...
 * ├── Child Part (Part)
 * ├── ...
 */
public partial class DeformingPart : Part {

    private Skeleton3D skeleton;
    public override void _Ready() {
        base._Ready();
        this.skeleton = this.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
        this.bindingQuads = [.. 
            this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren()).Cast<AlignmentPlane>()
            ];
    }

    public override void Unselected() {
        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.Unselected();
    }

    public override void Selected() {
        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        BoneAttachment3D b = this.skeleton.GetChildren().OfType<BoneAttachment3D>().Where(x => x.GetChildren().Contains(quad)).FirstOrDefault();
        b.AddChild(collider);
    }
}
