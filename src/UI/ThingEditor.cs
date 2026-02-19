using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static Godot.GD;

public partial class ThingEditor : Control {

    [Signal]
    public delegate void DuplicateAddedEventHandler();

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
    private Dictionary<StringName, List<Part>> dupeTracker;

    public AnimationLibrary GetPartAnimationLibrary(StringName UID, string libName, Part part) {
        // First check if dupes exists, if none exist then the library should not exist either when this is called
        if (this.dupeTracker.TryGetValue(UID, out List<Part> value)) {
            foreach (Part matchingPart in value) {
                // Part is NOT connected to a receiver and thus has its own AnimationPlayer
                if (matchingPart is DeformingPart pasdf && pasdf.animationPlayer != null) {
                    Print("Has Duplicate library: ", pasdf.animationPlayer.GetAnimationLibraryList());
                }
                if (matchingPart != part && 
                    matchingPart.activeCollider == null && 
                    matchingPart is DeformingPart defPart && 
                    defPart.animationPlayer.HasAnimationLibrary(libName)) {
                    return defPart.animationPlayer.GetAnimationLibrary(libName);
                }
            }
        }
        return null;
    }


    /**
     * For the sake of editing, dissolve the thing scene into individual part scenes then reform original part structure without thing overhead
     **/
    private Part ThingSceneDivision(Part part, Thing thing) {
        List<(Part, string)> partToParent = [];

        foreach (NodePath childPath in part.connectedParts) {
            Part originalChild = part.GetNode<Part>(childPath);
            // Duplicated child might create a memory leak, check back later
            Part newChild = ThingSceneDivision(originalChild.Duplicate() as Part, thing);
            Node parent = originalChild.GetParent();
            partToParent.Add((newChild, parent.Name));
            parent.RemoveChild(originalChild);
            originalChild.QueueFree();
        }
        Part newPart = part.PackPart();
        foreach ((Part, string) pair in partToParent) {
            if (newPart.Name != pair.Item2) {
                newPart.FindChild(pair.Item2).AddChild(pair.Item1);
            }
            else {
                newPart.AddChild(pair.Item1);
            }
            newPart.connectedParts.Add(newPart.GetPathTo(pair.Item1));
            pair.Item1.parentPart = pair.Item1.GetPathTo(newPart);
        }
        // change to an init
        newPart.thing = thing;
        newPart.space = this.worldRoot;
        newPart.editor = this;
        newPart.partName = newPart.GetMeta("extras").AsGodotDictionary<string, string>()["PartName"];
        newPart.UID = newPart.thing.Name + newPart.partName;
        part.QueueFree();
        return newPart;
    }

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {

        static void PrepareParts(Part part, Dictionary<StringName, List<Part>> tracker) {
            part.ToggleEditorMode();
            if (!tracker.TryAdd(part.UID, [part])) {
                tracker[part.UID].Add(part);
            }
            foreach (NodePath childPath in part.connectedParts) {
                Part child = part.GetNode<Part>(childPath);
                PrepareParts(child, tracker);
            }
        }

        Thing thing = Load<PackedScene>("src/Things/" + things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        foreach (Part thingPart in thing.parts.Where(x => x.parentPart == null)) {
            Part part = ThingSceneDivision(thingPart.Duplicate() as Part, thing);
            this.worldRoot.AddChild(part);
            if (part is DeformingPart defPart) {

                AnimationPlayer animPlayer = thing.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
                animPlayer.Owner = null;
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
            PrepareParts(part, dupeTracker);
        }
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

        Part p = this.worldRoot.selectedPart;
        // For now only allow duplication with singleton parts
        if (p != null && p.parentPart == null && p.connectedParts.Count == 0) {

            Part dupe = p.scene.Instantiate<Part>();

            string name = p.Name.ToString();
            string originalName = name.Contains('_') ? name[..name.RFind("_")] : name;

            // THIS SHOULD NEVER FAIL EEEEEEEVVVVVEEEEEEEEER
            this.dupeTracker[p.UID].Add(dupe);

            dupe.Name = originalName + "_" + this.dupeTracker[p.UID].Count;
            dupe.thing = p.thing;
            dupe.space = this.worldRoot;
            dupe.editor = this;
            dupe.partName = p.partName;
            dupe.UID = p.UID;
            dupe.scene = p.scene;
            Print(dupe.Name);
            
            //dupe.parentPart = null;
            this.worldRoot.AddChild(dupe);
            dupe.GlobalTransform = p.GlobalTransform;
            dupe.ToggleEditorMode();
            if (p is DeformingPart dp) {
                AnimationPlayer da = dp.animationPlayer.Duplicate() as AnimationPlayer;
                dupe.AddChild(da);
                (dupe as DeformingPart).animationPlayer = da;
            }
            dupe.Unselected();

            Print(dupe.GlobalPosition);


        }
    }

    // Signal when "Delete Selected" is pressed
    private void DeleteSelectedPart() {
        Part p = this.worldRoot.selectedPart;
        if (p != null && p.parentPart == null && p.connectedParts.Count == 0) {

            p.Unselected();
            this.worldRoot.selectedPart = null;
            this.worldRoot.RemoveChild(p);

            // THIS SHOULD NEVER FAIL EEEEEEEVVVVVEEEEEEEEER
            this.dupeTracker[p.UID].Remove(p);
            p.QueueFree();
        }
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