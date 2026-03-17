using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static Godot.GD;

public partial class ThingEditor : Control {

    private ItemList things;
    private ThingEditorSpace worldRoot;
    private Button showAnimations;
    private HBoxContainer animationList;
    private VBoxContainer toolList;
    private Button duplicateSelected;
    private Button deleteSelected;
    private Button createNewThing;
    private Button rotateClockwise;
    private Button rotateCounterClockwise;
    private Button toggleQuadList;

    private bool animListActive;
    private Theme theme;
    private Dictionary<StringName, List<Part>> dupeTracker;

    public ThingEditorSpace GetEditorSpace() { return this.worldRoot; }

    public Dictionary<StringName, List<Part>> GetDupeTracker() { return this.dupeTracker; }

    public void AddPartSlidersToToolList(Skeleton3D skeleton) {
        static void TraverseBoneTree(int boneIndex, Skeleton3D skeleton, VBoxContainer toolList) {
            string boneName = skeleton.GetBoneName(boneIndex);
            if (boneName == "neutral_bone") return;

            if (!(boneName.Contains(".Rec") || boneName.Contains(".Con"))) {
                BoneScaleSlider slider = BoneScaleSlider.Create(boneName, skeleton);
                toolList.AddChild(slider);
            }

            foreach (int childBone in skeleton.GetBoneChildren(boneIndex)) {
                TraverseBoneTree(childBone, skeleton, toolList);
            }
        }

        foreach (int rootBone in skeleton.GetParentlessBones()) {
            TraverseBoneTree(rootBone, skeleton, this.toolList);
        }
    }

    public void RemovePartSlidersFromToolList(Skeleton3D skeleton) {
        foreach (BoneScaleSlider slider in this.toolList.GetChildren().OfType<BoneScaleSlider>()) {
            if (skeleton == slider.GetReferencedSkeleton()) {
                this.toolList.RemoveChild(slider);
            }
        }
    }

    public AnimationLibrary GetPartAnimationLibrary(StringName UID, string libName, Part part) {
        if (!part.editorMode) { return null; }

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

    public void AddToTracker(Part part) {
        if (!this.dupeTracker.TryAdd(part.UID, [part])) {
            this.dupeTracker[part.UID].Add(part);
        }
    }

    public int GetTrackedCount(Part part) {
        this.dupeTracker.TryGetValue(part.UID, out List<Part> list);
        if (list == null) { return 0; }
        return list.Count;
    }

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {

        Thing thing = Load<PackedScene>("src/Things/" + this.things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        Print(thing.Name);
        thing.Assemble(this.worldRoot, this);
        // Animation stuff
        Part[] newChildren = [.. this.worldRoot.GetChildren().OfType<Part>().Where(x => x.thing == thing)];
        foreach (Part part in newChildren) {
            if (part.animationPlayer != null) {
                foreach (StringName libStr in part.animationPlayer.GetAnimationLibraryList()) {
                    AnimationLibrary lib = part.animationPlayer.GetAnimationLibrary(libStr);
                    foreach (StringName animStr in lib.GetAnimationList()) {
                        Print(animStr);
                        if (lib.GetAnimationListSize() > 0 &&
                            this.animationList.GetChildCount() == 1 &&
                            this.animationList.GetChild(0).Name == "Default") {
                            this.animationList.GetChild<Label>(0).Hide();
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
                        animLabel.Pressed += () => part.DoAnimation(libStr, animStr);
                    }
                }
            }
            else {
                AnimationPlayer placeholder = new();
                part.AddChild(placeholder);
                part.animationPlayer = placeholder;
            }

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
            Part dupe = p.duplicateScene.Instantiate<Part>();
            dupe.PreparePart(null, null, this.worldRoot, p.thing, this, p);
            dupe.duplicateScene = p.duplicateScene;
            // TODO: Figure out why Instantiated scene doesn't have motherfucking animations?
            // Jerry rigged trick but I don't know how tf else to fix this
            AnimationPlayer dap;
            if (dupe.animationPlayer != null) {
                dap = dupe.animationPlayer;
                dupe.RemoveChild(dap);
                dap.QueueFree();
            }
            dap = (AnimationPlayer)p.animationPlayer.Duplicate();
            dupe.AddChild(dap);
            dupe.animationPlayer = dap;

            dupe.GlobalTransform = p.GlobalTransform;
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

    private void ToggleQuadList() {
        Part p = this.worldRoot.selectedPart;
        if (p != null) {

            if (p.bindingQuads.Count == 1 && p.bindingQuads[0].GetMeta("MeshType").AsString() == "Connector") {
                Print("Cannot modify singleton connector");
                return;
            }

            QuadList list = Load<PackedScene>("res://src/UI/QuadList.tscn").Instantiate<QuadList>();
            PackedScene quadItemScene = Load<PackedScene>("res://src/UI/QuadItem.tscn");
            foreach (AlignmentPlane plane in p.bindingQuads) {
                string partName = plane.GetMeta("PartName").AsString();
                string meshType = plane.GetMeta("MeshType").AsString();

                Button quadItem = quadItemScene.Instantiate<Button>();
                quadItem.Pressed += () => FlipQuad(meshType, plane);
                Node container = quadItem.GetChild(0);
                container.GetChild<Label>(0).Text = partName;
                container.GetChild<Label>(1).Text = meshType;
                list.GetChild(0).AddChild(quadItem);
            }

            list.Visible = false;
            Camera3D camera = this.worldRoot.GetViewport().GetCamera3D();
            
            Vector2 pos = camera.UnprojectPosition(p.GlobalPosition);
            this.worldRoot.AddChild(list);
            list.Position = pos;
            list.Visible = true;
        }
    }

    private void FlipQuad(string meshType, AlignmentPlane plane) {
        //if (this.worldRoot.selectedPart is DeformingPart defPart) {
        //    MeshInstance3D skin = this.worldRoot.selectedPart.GetSkinMesh();
        //    skin.Skin = null;

        //    BoneAttachment3D socket = plane.GetParentOrNull<BoneAttachment3D>();
        //    if (socket == null) { return; }
        //    int rootIndex = defPart.skeleton.GetParentlessBones().First();
        //    int socketIndex = socket.BoneIdx;
        //    int socketParent = defPart.skeleton.GetBoneParent(socketIndex);
        //    // Make socket the new root and move the old root 
        //    defPart.skeleton.UnparentBoneAndRest(socketIndex);
        //    while (socketParent != rootIndex) {
        //        int current = socketParent;
        //        socketParent = defPart.skeleton.GetBoneParent(socketParent);
        //        defPart.skeleton.UnparentBoneAndRest(current);
        //    }

        //}
    }

    private void RotatePart(float degrees) {
        Part p = this.worldRoot.selectedPart;
        if (p.activeCollider == null) { return; }
        AlignmentPlane connectingPlane = p.activeCollider.plane;
        AlignmentPlane receivingPlane = p.activeCollider.GetBoundCollider().plane;
        connectingPlane.ShiftFrontToNext(degrees);
        Transform3D newTransform = Part.CalculateJoinTransform(connectingPlane, receivingPlane, p.GlobalTransform);
        p.GlobalTransform = newTransform;
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
        HBoxContainer rotationButtons = this.toolList.GetChild<HBoxContainer>(3);
        this.rotateClockwise = rotationButtons.GetChild<Button>(0);
        this.rotateClockwise.Pressed += () => RotatePart(90);
        this.rotateCounterClockwise = rotationButtons.GetChild<Button>(1);
        this.rotateCounterClockwise.Pressed += () => RotatePart(-90);
        this.toggleQuadList = this.toolList.GetChild<Button>(4);
        this.toggleQuadList.Pressed += ToggleQuadList;

        this.worldRoot = GetChild<SubViewportContainer>(1).GetChild<SubViewport>(0).GetChild<ThingEditorSpace>(0);
        this.animListActive = false;
        this.theme = Load<Theme>("src/UI/Themes/ThingEditor.tres");

        this.dupeTracker = [];
    }
}