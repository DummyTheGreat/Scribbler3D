using Godot;
using static Godot.GD;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class BoneScaleSlider : HBoxContainer
{
    private Label label;
    private HSlider slider;

    private Skeleton3D skeleton;
    private string boneName;
    private Dictionary<int, Vector3> childRestScales = [];

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

    private void OnValueChange(double value) {
        int boneIndex = this.skeleton.FindBone(this.boneName);
        Vector3 scale = this.skeleton.GetBonePoseScale(boneIndex);
        scale.Y = (float)value;
        this.skeleton.SetBonePoseScale(boneIndex, scale);
    }

    private void CorrectPartUnifications(bool valueChanged) {
        if (valueChanged) {
            foreach (BoneAttachment3D attachment in this.skeleton.GetChildren().OfType<BoneAttachment3D>()) {
                foreach (Part part in attachment.GetChildren().OfType<Part>()) {
                    AlignmentPlane connector = part.activeCollider.plane;
                    AlignmentPlane receiver = part.activeCollider.GetBoundCollider().plane;
                    Transform3D t = Part.CalculateJoinTransform(connector, receiver, part.GlobalTransform);
                    part.GlobalTransform = t;
                }
            }
        }
    }

    public override void _Ready() {
        this.label = this.GetChild<Label>(0);
        this.slider = this.GetChild<HSlider>(1);
        this.label.Text = this.boneName;
        int boneIndex = this.skeleton.FindBone(this.boneName);
        this.slider.Value = this.skeleton.GetBonePoseScale(boneIndex).Y;
        this.slider.ValueChanged += OnValueChange;
        this.slider.DragEnded += CorrectPartUnifications;

        foreach (int childIndex in this.skeleton.GetBoneChildren(boneIndex)) {
            // Capture the original scale once at startup
            this.childRestScales[childIndex] = this.skeleton.GetBonePoseScale(childIndex);
        }
    }
}
