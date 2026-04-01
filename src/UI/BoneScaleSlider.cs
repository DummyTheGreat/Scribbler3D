using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static Godot.GD;

public partial class BoneScaleSlider : HBoxContainer
{
    private Label label;
    private HSlider slider;

    private Skeleton3D skeleton;
    private string boneName;
    private int boneIndex;


    private enum BoneAxis { X, Y, Z }
    private BoneAxis axis;

    public void SwitchAxis(bool toggle, Button axisButton) {
        if (toggle == false) { return; }
        string currentAxis = axisButton == null ? this.axis.ToString() : axisButton.Name;
        switch (currentAxis) {
            case "X":
                this.axis = BoneAxis.X;
                this.slider.Value = this.skeleton.GetBonePoseScale(this.boneIndex).X;
                break;
            case "Y":
                this.axis = BoneAxis.Y;
                this.slider.Value = this.skeleton.GetBonePoseScale(this.boneIndex).Y;
                break;
            case "Z":
                this.axis = BoneAxis.Z;
                this.slider.Value = this.skeleton.GetBonePoseScale(this.boneIndex).Z;
                break;
            default:
                break;
        }
    }

    public static BoneScaleSlider Create(string boneName, Skeleton3D skeleton) {
        PackedScene scene = Load<PackedScene>("res://src/UI/BoneScaleSlider.tscn");
        BoneScaleSlider slider = scene.Instantiate<BoneScaleSlider>();
        slider.boneName = boneName;
        slider.skeleton = skeleton;
        return slider;
    }

    public Skeleton3D GetReferencedSkeleton() {
        return this.skeleton;
    }

    private static void CancelScaleInheritance(Skeleton3D skeleton, int boneIdx, Vector3 oldParentScale, Vector3 newParentScale) {
        if (oldParentScale.X == 0f || oldParentScale.Y == 0f || oldParentScale.Z == 0f)
            return;

        Vector3 scaleRatio = new(
            newParentScale.X / oldParentScale.X,
            newParentScale.Y / oldParentScale.Y,
            newParentScale.Z / oldParentScale.Z
        );

        int[] children = skeleton.GetBoneChildren(boneIdx);

        foreach (int childIdx in children) {

            BoneAttachment3D attachment = skeleton.GetChildren().OfType<BoneAttachment3D>().FirstOrDefault(x => x.BoneIdx == childIdx);

            if (attachment != null) {
                AlignmentPlane p = attachment.GetChildren().OfType<AlignmentPlane>().First();
                if (p.GetMeta("MeshType").AsString() == "Receiver") {
                    return;
                }
            }

            Quaternion childRot = skeleton.GetBonePoseRotation(childIdx);
            Vector3 childScale = skeleton.GetBonePoseScale(childIdx);

            float effectiveX = (scaleRatio * (childRot * Vector3.Right)).Length();
            float effectiveY = (scaleRatio * (childRot * Vector3.Up)).Length();
            float effectiveZ = (scaleRatio * (childRot * Vector3.Back)).Length();

            skeleton.SetBonePoseScale(childIdx, new(
                childScale.X / effectiveX,
                childScale.Y / effectiveY,
                childScale.Z / effectiveZ
            ));

            if (skeleton.HasBoneMeta(childIdx, "Slider")) {
                BoneScaleSlider sl = skeleton.GetBoneMeta(childIdx, "Slider").As<BoneScaleSlider>();
                sl.SwitchAxis(true, null);
            }
        }
    }

    private void InvertScale(Skeleton3D skeleton, int boneIdx, Vector3 newScale) {

        static Vector3 GetBoneTailLocal(Skeleton3D skeleton, int boneIdx) {
            int[] children = skeleton.GetBoneChildren(boneIdx);
            if (children.Length > 0) {
                // Child bone's rest position is expressed in this bone's local space
                return skeleton.GetBoneRest(children[0]).Origin;
            }
            else {
                // Leaf bone: infer length from the rest pose origin distance from parent
                Vector3 restOrigin = skeleton.GetBoneRest(boneIdx).Origin;
                float length = restOrigin.Length();
                if (length > 0.0001f) {
                    // Assume the bone points along its local +Y (standard for most rigs)
                    return new Vector3(0.0f, length, 0.0f);
                }
                return Vector3.Zero;
            }
        }


        // --- Determine the tail's local offset ---
        Vector3 tailLocal = GetBoneTailLocal(skeleton, boneIdx);
        if (tailLocal == Vector3.Zero) {
            GD.PushWarning($"Bone {boneIdx} has no detectable tail; skipping.");
            return;
        }

        // --- Current pose state ---
        Vector3 posePos = skeleton.GetBonePosePosition(boneIdx);
        Quaternion poseRot = skeleton.GetBonePoseRotation(boneIdx);
        Vector3 poseScale = skeleton.GetBonePoseScale(boneIdx);

        // --- Compute position compensation ---
        // How much the tail would shift due to the scale change (in bone-local space),
        // then rotate into parent space.
        Vector3 scaleDelta = poseScale - newScale;
        Vector3 compensation = poseRot * (scaleDelta * tailLocal);

        // --- Apply ---
        skeleton.SetBonePoseScale(boneIdx, newScale);
        skeleton.SetBonePosePosition(boneIdx, posePos + compensation);
    }

    private void OnValueChange(double value) {

        Vector3 scale = this.skeleton.GetBonePoseScale(this.boneIndex);
        Vector3 newScale = new(
            this.axis == BoneAxis.X ? (float)value : scale.X,
            this.axis == BoneAxis.Y ? (float)value : scale.Y,
            this.axis == BoneAxis.Z ? (float)value : scale.Z);
        this.skeleton.SetBonePoseScale(this.boneIndex, newScale);

        CancelScaleInheritance(this.skeleton, this.boneIndex, scale, newScale);
    }

    private void CorrectPartUnifications(bool valueChanged) {

        static void TransformPart(Part part) {
            AlignmentPlane connector = part.activeCollider.plane;
            AlignmentPlane receiver = part.activeCollider.GetBoundCollider().plane;
            Transform3D t = Part.CalculateJoinTransform(connector, receiver, part.GlobalTransform);
            part.GlobalTransform = t;
        }

        if (valueChanged) {
            BoneAttachment3D[] children = this.skeleton.GetChildren().OfType<BoneAttachment3D>().ToArray();
            foreach (BoneAttachment3D attachment in children) {
                Part[] childParts = attachment.GetChildren().OfType<Part>().ToArray();
                AlignmentPlane plane = attachment.GetChildren().OfType<AlignmentPlane>().FirstOrDefault();
                foreach (Part part in childParts) { // A Receiver
                    TransformPart(part);
                }
                if (childParts.Length == 0 && plane != null && plane.GetMeta("MeshType").AsString() == "Connector") { // A Connector
                    TransformPart(this.skeleton.GetParent<Part>());
                }
            }
        }
    }

    public override void _Ready() {
        this.label = this.GetChild<Label>(0);
        this.slider = this.GetChild<HSlider>(1);
        this.label.Text = this.boneName;
        this.boneIndex = this.skeleton.FindBone(this.boneName);
        this.slider.Value = this.skeleton.GetBonePoseScale(this.boneIndex).Y;
        this.slider.ValueChanged += OnValueChange;
        this.slider.DragEnded += CorrectPartUnifications;
        this.axis = BoneAxis.Y;
    }
}
