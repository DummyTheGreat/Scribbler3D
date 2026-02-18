using Godot;
using static Godot.GD;

using System;
using System.Collections.Generic;
using System.Linq;

public partial class ThingEditor : Control {
    private ItemList things;
    private ThingEditorSpace worldRoot;
    private Button showAnimations;
    private HBoxContainer animationList;
    private VBoxContainer toolList;
    private Button duplicateSelected;
    private Button deleteSelected;
    private Button createNewThing;

    private bool animListActive;
    private Theme theme;
    private Dictionary<string, int> dupeTracker;


    private Part ThingSceneDivision(Part part, Thing thing) {
        List<(Part, string)> partToParent = [];

        foreach (Part child in part.connectedParts) {
            Part newChild = ThingSceneDivision(child.Duplicate() as Part, thing);
            Node parent = child.GetParent();
            partToParent.Add((newChild, parent.Name));
            parent.RemoveChild(child);
            child.QueueFree();
        }
        Part newPart = part.PackPart();
        foreach ((Part, string) pair in partToParent) {
            newPart.FindChild(pair.Item2).AddChild(pair.Item1);
            newPart.connectedParts.Add(pair.Item1);
            pair.Item1.parentPart = pair.Item1.GetPathTo(newPart);
        }
        newPart.thing = thing;
        newPart.space = this.worldRoot;
        part.QueueFree();
        return newPart;
    }

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {
        Thing thing = Load<PackedScene>("src/Things/" + things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        foreach (Part thingPart in thing.parts.Where(x => x.parentPart == null)) {
            Part part = ThingSceneDivision(thingPart.Duplicate() as Part, thing);
            this.worldRoot.AddChild(part);

            if (part is DeformingPart defPart) {
                Print(thing);
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
        //this.exportButton.AddParts([.. thing.parts]);
        foreach (Part part in thing.parts) {


            if (part.parentPart == null) {
                part.Reparent(this.worldRoot);
                // Move Animator to top part
                
            }

            part.ToggleEditorMode();
        }
        thing.Hide();
    }

    private static void KillTween(Tween t) {
        t.Kill();
    }

    // Signal when "Show Animations" is pressed
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

    // Signal when "Duplicate Selected" is pressed
    private void DuplicateSelectedPart() {

        static void RemovePartColliders(Node node) {
            foreach (Node child in node.GetChildren()) {
                if (child is PartCollider pc) {
                    node.RemoveChild(pc);
                    pc.QueueFree();
                }
                RemovePartColliders(child);
            }
        }

        Part p = this.worldRoot.selectedPart;
        // For now only allow duplication with singleton parts
        if (p != null && p.parentPart == null && p.connectedParts.Count == 0) {

            Part dupe = p.Duplicate() as Part;

            string name = p.Name.ToString();
            string originalName = name.Contains('_') ? name[..name.RFind("_")] : name;
            if (this.dupeTracker.ContainsKey(originalName)) {
                this.dupeTracker[originalName] += 1;
            }
            else {
                this.dupeTracker.Add(originalName, 1);
            }
            dupe.Name = originalName + "_" + this.dupeTracker[originalName];

            RemovePartColliders(dupe);
            Print(dupe.Name);
            dupe.parentPart = null;
            this.worldRoot.AddChild(dupe);
            dupe.ToggleEditorMode();
            dupe.Unselected();

        }
    }

    // Signal when "Delete Selected" is pressed
    private void DeleteSelectedPart() {

    }

    // Signal when "Create New Thing" is pressed
    private void CreateNewThing() {

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

        this.toolList = uiLayer.GetChild<VBoxContainer>(3);
        this.duplicateSelected = this.toolList.GetChild<Button>(0);
        this.duplicateSelected.Pressed += DuplicateSelectedPart;
        this.deleteSelected = this.toolList.GetChild<Button>(1);
        this.deleteSelected.Pressed += DeleteSelectedPart;
        this.createNewThing = this.toolList.GetChild<Button>(2);
        this.createNewThing.Pressed += CreateNewThing;

        this.worldRoot = GetChild<SubViewportContainer>(1).GetChild<SubViewport>(0).GetChild<ThingEditorSpace>(0);
        this.animListActive = false;
        this.theme = Load<Theme>("src/UI/Themes/ThingEditor.tres");

        this.dupeTracker = [];
    }

    public override void _Process(double delta) {
        //foreach (Thing thing in selectedThings) {
        //    thing.Position
    }
}