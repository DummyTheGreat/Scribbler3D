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
    private ThingEditorSpace space;

    private void AttachPart(Part connector, BoneAttachment3D receiverBone) {
        connector.Reparent(receiverBone);

    }

    private void DetachPart(Part connector) {
        connector.Reparent(this.space);
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
        if (this.activeCollider != null) {
            Part receivingPart = this.activeCollider.GetBoundCollider().associatedPart;
            if (receivingPart is DeformingPart deformingReceiver) {
                Print("SDJKLFSD");
                CallDeferred(nameof(DetachPart), this);

            }
        }

        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        BoneAttachment3D b = this.skeleton.GetChildren().OfType<BoneAttachment3D>().Where(x => x.GetChildren().Contains(quad)).FirstOrDefault();
        //Print(b);
        b.AddChild(collider);
    }

    // Connector
    public override void SealJoin() {
        base.SealJoin();
        Part receivingPart = this.activeCollider.GetBoundCollider().associatedPart;
        BoneAttachment3D receiverSocket = this.activeCollider.GetBoundCollider().GetParentOrNull<BoneAttachment3D>();
        if (receiverSocket == null) {
            PushWarning("No Receiver? What the hell!!!");
        }
        CallDeferred(nameof(AttachPart), this, receiverSocket);
    }
    public override void _Ready() {
        base._Ready();
        this.space = this.Owner.GetParentOrNull<ThingEditorSpace>(); 
        if (this.space == null) {
            PushWarning("Thing Space not found");
        }
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
