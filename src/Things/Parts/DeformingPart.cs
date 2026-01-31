using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Godot.GD;

/*
 * Deforming Parts are parts that will have changes to their mesh's form through bone manipulation.
 * 
 * They possess a node hierarchy like such:
 * 
 * Root (DeformingPart)
 * ├── Bounds (MeshInstance3D)
 * ├── Skeleton (Skeleton3D)
 * ├───── SkinMesh (MeshInstance3D)
 * ├───── Connector/Receiver Bone Socket (BoneAttachment3D)
 * ├──────── Connector/Receiver (AlignmentPlane)
 * ├───── ...
 * ├── Child Part (Part)
 * ├── ...
 */
public partial class DeformingPart : Part {

    private Skeleton3D skeleton;
    private MeshInstance3D skeletonDrawing;

    public override void Unselected() {
        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.Unselected();
    }

    public override void Selected() {
        base.Selected();

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

    public override void _Ready() {
        base._Ready();
        this.skeleton = this.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
        this.bindingQuads = [..
            this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren()).OfType<AlignmentPlane>()
            ];
        foreach (AlignmentPlane quad in bindingQuads) {
            //Print(this.Name);
            //Print(quad.Name);
        }
    }
}
