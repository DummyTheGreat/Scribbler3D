using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    public Skeleton3D skeleton;
    public Fabrik3D inverseKin;

    private void UpdateSkeletonScale() {
        // Apply skeleton changes
        this.skeleton = this.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
        foreach (Resource bData in this.importData.boneData) {
            int idx = bData.Get("boneIndex").AsInt32();
            Vector3 newScale = new(
                bData.Get("xScale").As<float>(),
                bData.Get("yScale").As<float>(),
                bData.Get("zScale").As<float>());
            this.skeleton.SetBonePoseScale(idx, newScale);
        }
    }

    public override ImportData CreateImportData(Resource partImportData) {
        ImportData d = base.CreateImportData(partImportData);
        UpdateSkeletonScale();
        return d;
    }

    public override MeshInstance3D GetSkinMesh() {
        return this.skeleton.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
    }

    public void ToggleInvKin(bool toggle, int settingIndex, NodePath target) {
        this.inverseKin.Active = toggle;
        this.inverseKin.SetTargetNode(settingIndex, target);
    }

    // Receiver
    public override void AttachPart(Part connector) {
        BoneAttachment3D receiverSocket = connector.activeCollider.GetBoundCollider().GetParentOrNull<BoneAttachment3D>();
        connector.Reparent(receiverSocket);
        base.AttachPart(connector);
        if (connector is DeformingPart defCon && defCon.inverseKin.GetSettingCount() > 0) {
            defCon.ToggleInvKin(true, 0, defCon.inverseKin.GetPathTo(receiverSocket));
        }
    }

    public override void Selected() {
        this.editor.AddPartSlidersToToolList(this.skeleton);
        base.Selected();
    }

    public override void Unselected() {
        this.editor.RemovePartSlidersFromToolList(this.skeleton);
        base.Unselected();
    }

    public override void StopSelected() {
        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.StopSelected();
    }

    public override void MoveSelected() {
        if (this.inverseKin.GetSettingCount() > 0) {
            ToggleInvKin(false, 0, null);
        }

        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
        base.MoveSelected();

    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        BoneAttachment3D b = this.skeleton.GetChildren().OfType<BoneAttachment3D>().Where(x => x.GetChildren().Contains(quad)).FirstOrDefault();
        b.AddChild(collider);
    }

    public void InitInvKin() {
        this.inverseKin = new() { MinDistance = 0.1f, AngularDeltaLimit = 4.0f, Active = false };

        int[] parentless = this.skeleton.GetParentlessBones();
        int realRoot = this.skeleton.GetBoneName(parentless[0]) == "neutral_bone" ? parentless[1] : parentless[0];
        BoneAttachment3D[] attachments = this.skeleton.GetChildren().OfType<BoneAttachment3D>().ToArray();
        foreach (BoneAttachment3D attachment in attachments) {
            AlignmentPlane p = attachment.GetChildren().OfType<AlignmentPlane>().First();
            // Leaf bone connector
            if (p.GetMeta("MeshType").AsString() == "Connector" && this.skeleton.GetBoneChildren(attachment.BoneIdx).Length == 0) {
                int count = this.inverseKin.GetSettingCount();
                this.inverseKin.SetSettingCount(count + 1);
                this.inverseKin.SetRootBone(count, realRoot);
                this.inverseKin.SetEndBone(count, attachment.BoneIdx);

            }
        }
        this.skeleton.AddChild(this.inverseKin);
    }

    public override void _Ready() {
        this.skeleton ??= this.GetChildren().OfType<Skeleton3D>().First();
        InitInvKin();
        this.bindingQuads = [..
            this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren()).OfType<AlignmentPlane>()
            ];
        base._Ready();
        BoneAttachment3D attParent = this.GetParentOrNull<BoneAttachment3D>();
        if (attParent != null && this.inverseKin.GetSettingCount() > 0) {
            ToggleInvKin(true, 0, this.inverseKin.GetPathTo(attParent));
        }
    }
}
