using Godot;
using static Godot.GD;

using System;
using System.Collections.Generic;
using System.Linq;

public partial class ThingEditor : Control {
    private ItemList things;
    private Node3D worldRoot;
    private Button showAnimations;
    private HBoxContainer animationList;

    private bool animListActive;
    private Theme theme;

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {
        Thing thing = Load<PackedScene>("src/Things/" + things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        this.worldRoot.AddChild(thing);
        //this.exportButton.AddParts([.. thing.parts]);
        foreach (Part part in thing.parts) {
            part.thing = thing;
            if (part.parentPart == null) {
                part.Reparent(this.worldRoot);
                // Move Animator to top part
                if (part is DeformingPart defPart) {
                    AnimationPlayer animPlayer = thing.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
                    animPlayer.Reparent(defPart);
                    defPart.animationPlayer = animPlayer;

                    foreach (string animLibStr in animPlayer.GetAnimationLibraryList()) {
                        AnimationLibrary animLib = animPlayer.GetAnimationLibrary(animLibStr);
                        if (animLib.GetAnimationListSize() > 0 && 
                            this.animationList.GetChildCount() == 1 && 
                            this.animationList.GetChild(0).Name == "Default") {
                            this.animationList.GetChild<Label>(0).Hide();
                        }
                        foreach (StringName animStr in animLib.GetAnimationList()) {

                            // Adjust track paths for relocation of animation player to main part for editing
                            Animation anim = animLib.GetAnimation(animStr);
                            for (int i = 0; i < anim.GetTrackCount(); i++) {
                                // use NodePath.slice wherever the hell that becomes a thing
                                NodePath originalPath = anim.TrackGetPath(i);
                                string newPath = "";
                                for (int j = 1; j < originalPath.GetNameCount(); j++) {
                                    if (j != 1) { newPath += "/"; }
                                    newPath += originalPath.GetName(j);
                                }
                                for (int j = 0; j < originalPath.GetSubNameCount(); j++) {
                                    newPath += ":" + originalPath.GetSubName(j);
                                }
                                anim.TrackSetPath(i, newPath);
                            }

                            Button animLabel = new() {
                                Name = animStr + "Button",
                                Text = animStr,
                                GrowVertical = GrowDirection.Both,
                                SizeFlagsHorizontal = SizeFlags.Fill,
                                SizeFlagsVertical = SizeFlags.Fill,
                                CustomMinimumSize = new Vector2(this.animationList.Size.X * 0.2f, 0f),
                                Theme = this.theme
                            };
                            this.animationList.AddChild(animLabel);
                            animLabel.Pressed += () => defPart.DoAnimation(animLibStr, animStr);
                        }
                    }
                }
            }
            part.ToggleEditorMode();
        }
        thing.Hide();
    }

    private static void KillTween(Tween t) {
        t.Kill();
    }

    private void ToggleAnimationView() {
        Tween t = CreateTween().SetParallel(true);
        t.Pause();
        t.Finished += () => KillTween(t);
        if (!animListActive) {
            t.TweenProperty(this.animationList, "offset_left", 0f, 0.2f);
            t.TweenProperty(this.animationList, "offset_right", 0f, 0.2f);
            this.animListActive = true;
            this.animationList.MouseFilter = MouseFilterEnum.Pass;
        }
        else {
            float offset = this.animationList.Size.X;
            t.TweenProperty(this.animationList, "offset_left", -offset, 0.2f);
            t.TweenProperty(this.animationList, "offset_right", -offset, 0.2f);
            this.animListActive = false;
            this.animationList.MouseFilter = MouseFilterEnum.Ignore;
        }
        t.Play();
    }

    public override void _Ready() {
        CanvasLayer uiLayer = GetChild<CanvasLayer>(0);
        this.things = uiLayer.GetChild<ItemList>(0);
        this.things.MultiSelected += ThingSelected;

        this.showAnimations = uiLayer.GetChild<Button>(1);
        this.showAnimations.Pressed += ToggleAnimationView;

        this.animationList = uiLayer.GetChild<HBoxContainer>(2);
        float offset = this.animationList.Size.X;
        this.animationList.OffsetLeft = -offset;
        this.animationList.OffsetRight = -offset;

        this.worldRoot = GetChild<SubViewportContainer>(1).GetChild<SubViewport>(0).GetChild<Node3D>(0);
        this.animListActive = false;
        this.theme = Load<Theme>("src/UI/Themes/ThingEditor.tres");
        //highestSelectionIndex = 0;
        //selectedPart = null;
        //exportButton = GetChild<ExportModelTransformData>(1);
    }

    public override void _Process(double delta) {
        //foreach (Thing thing in selectedThings) {
        //    thing.Position
    }
}