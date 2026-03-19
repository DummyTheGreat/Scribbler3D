using Godot;
using System;

public partial class QuadList : HBoxContainer {
    private VBoxContainer list;
    private Button close;
    private VScrollBar scroll;

    private void Close() {
        this.QueueFree();
    }

    private void UpdateScroll(Node node) {

    }

    public void FlipQuad(string meshType, AlignmentPlane plane) {

        PartCollider respCollider = plane.GetCollider();
        Part thisPart = respCollider.associatedPart;

        //respCollider.CollisionLayer = meshType == "Connector" ? (uint)1 : (uint)2;
        //respCollider.CollisionMask = meshType == "Connector" ? (uint)0 : (uint)1;

        if (meshType == "Connector") { // Con to Rec
            thisPart.MoveSelected();
            respCollider.CollisionLayer = (uint)1;
            respCollider.CollisionMask = (uint)0;
            respCollider.ClearColliderRelation();
            thisPart.StopSelected();
        }
        else { // Rec to Con

            PartCollider bound = respCollider.GetBoundCollider();
            if (bound != null) {
                Part boundPart = bound.associatedPart;
                boundPart.MoveSelected();
                respCollider.CollisionLayer = (uint)2;
                respCollider.CollisionMask = (uint)1;
                bound.ClearColliderRelation();
                boundPart.StopSelected();
            }
            else {
                respCollider.CollisionLayer = (uint)2;
                respCollider.CollisionMask = (uint)1;
            }
        }


        if (thisPart is DeformingPart defPart) {

            BoneAttachment3D socket = plane.GetParentOrNull<BoneAttachment3D>();

            if (meshType == "Receiver") {
                int endIndex = socket.BoneIdx;
                int middleIndex = defPart.skeleton.GetBoneParent(endIndex);
                int rootIndex = defPart.skeleton.GetBoneParent(middleIndex);

                defPart.inverseKin.SetSettingCount(1);
                defPart.inverseKin.SetRootBone(0, rootIndex);
                defPart.inverseKin.SetMiddleBone(0, middleIndex);
                defPart.inverseKin.SetEndBone(0, endIndex);

            }
            else {
                defPart.inverseKin.ClearSettings();
            }
        }

        plane.SetMeta("MeshType", meshType == "Connector" ? "Receiver" : "Connector");
        Close();
    }

    public override void _Ready() {
        this.list = this.GetChild<VBoxContainer>(0);
        this.list.ChildEnteredTree += UpdateScroll;
        this.close = this.GetChild(1).GetChild<Button>(0);
        this.close.Pressed += Close;
        this.scroll = this.GetChild(1).GetChild<VScrollBar>(1);
        this.scroll.MaxValue = 0f;
    }
}
