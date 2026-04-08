using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class ThingEditor : Control {

    private ItemList things;
    private WorldRoot worldRoot;

    private Button goToTestGround;
    private VBoxContainer toolList;
    private Button duplicateSelected;
    private Button deleteSelected;
    private Button createNewThing;
    private Button rotateClockwise;
    private Button rotateCounterClockwise;
    private Button toggleQuadList;
    private Button toggleAnimationList;
    private Button mirrorSelected;
    private QuadList animationList;
    private SubViewportContainer worldContainer;
    private Theme theme;

    public void AddPartSlidersToToolList(Skeleton3D skeleton) {

        int[] socketIndices = skeleton.GetChildren().OfType<BoneAttachment3D>().Select(x => x.BoneIdx).ToArray();
        int[] rootBones = skeleton.GetParentlessBones();

        static void TraverseBoneTree(int boneIndex, Skeleton3D skeleton, VBoxContainer toolList, int[] excludedIdxs, HBoxContainer axisButtons) {
            string boneName = skeleton.GetBoneName(boneIndex);
            if (boneName == "neutral_bone") return;

            if (!excludedIdxs.Contains(boneIndex)) {
                BoneScaleSlider slider = BoneScaleSlider.Create(boneName, skeleton);
                foreach (Button child in axisButtons.GetChildren().OfType<Button>()) {
                    child.Toggled += (toggle) => slider.SwitchAxis(toggle, child);
                }
                toolList.AddChild(slider);
                Thing parent = skeleton.GetParentOrNull<Thing>();
                if (parent != null) {
                    parent.PartAnimationStatus += slider.ToggleEdit;
                }
                skeleton.SetBoneMeta(boneIndex, "Slider", slider);
            }

            foreach (int childBone in skeleton.GetBoneChildren(boneIndex)) {
                TraverseBoneTree(childBone, skeleton, toolList, excludedIdxs, axisButtons);
            }
        }

        HBoxContainer axisButtons = Load<PackedScene>("res://src/UI/BoneAxisSelect.tscn").Instantiate<HBoxContainer>();
        axisButtons.GetChild<Button>(1).ButtonPressed = true;

        foreach (int rootBone in rootBones) {
            TraverseBoneTree(rootBone, skeleton, this.toolList, socketIndices.Concat(rootBones).ToArray(), axisButtons);
        }
        this.toolList.AddChild(axisButtons);
    }

    public void RemovePartSlidersFromToolList(Skeleton3D skeleton) {
        // Remove xyz
        this.toolList.RemoveChild(this.toolList.GetChild(-1));
        foreach (BoneScaleSlider slider in this.toolList.GetChildren().OfType<BoneScaleSlider>()) {
            if (skeleton == slider.GetReferencedSkeleton()) {
                this.toolList.RemoveChild(slider);
            }
        }
    }

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {
        this.things.SetItemSelectable((int)index, false);
        string dataPath = "res://src/Things/Data/Resources/" + this.things.GetItemText((int)index);
        DirAccess dir = DirAccess.Open(dataPath);
        foreach (string fileName in dir.GetFiles()) {
            Resource thingData = Load<Resource>(dataPath + "/" + fileName);
            if (thingData.Get("pathToReceiver").AsNodePath().IsEmpty) {
                this.worldRoot.GetWorldSpace<ThingEditorSpace>().CreateThingChild(thingData);
                break;
            }
        }
    }

    // Signal when "Duplicate Selected" is pressed
    private void DuplicateSelectedPart(Thing paramPart = null) {

        Thing Duplication(Thing originalPart, Node parent, NodePath connectorPath, NodePath receiverPath) {
            Thing dupe = Thing.Create(originalPart.importData);
            dupe.PrepareThing(
                connectorPath == "" ? null : dupe.GetNode<AlignmentPlane>(connectorPath),
                parent);

            // Only top part will have/need animation player
            if (parent == this.worldRoot) {

                if (dupe.animationPlayer != null) {
                    dupe.RemoveChild(dupe.animationPlayer);
                    dupe.animationPlayer.QueueFree();
                    dupe.animationPlayer = null;
                }
                dupe.GlobalTransform = originalPart.GlobalTransform;
            }

            foreach (Thing childPart in originalPart.connectedThings) {
                AlignmentPlane childConnector = childPart.activeCollider.plane;
                AlignmentPlane parentReceiver = childPart.activeCollider.GetBoundCollider().plane;
                Duplication(childPart, dupe, childPart.GetPathTo(childConnector), originalPart.GetPathTo(parentReceiver));
            }

            return dupe;
        }

        static string IterateName(string libName, GodotObject lib, bool updateMeta, int iter = 1) {
            string[] libNameItems = libName.Split('_');
            if (libNameItems.Length < 3) { Print("Something is wrong with animations"); return ""; }
            libNameItems[^1] = "I" + (lib.GetMeta("Instance").AsInt32() + iter).ToString();
            if (updateMeta) {
                lib.SetMeta("Instance", lib.GetMeta("Instance").AsInt32() + iter);
            }
            return String.Join("_", libNameItems);
        }

        Thing p = paramPart ?? this.worldRoot.GetSelected<Thing>();
        // For now only allow duplication with singleton parts
        if (p != null && p.parentThing == null) {
            Thing dupe = Duplication(p, this.worldRoot.GetChild(0), "", "");

            AnimationPlayer dupePlayer = new();
            foreach (StringName libName in p.animationPlayer.GetAnimationLibraryList()) {
                AnimationLibrary lib = p.animationPlayer.GetAnimationLibrary(libName);
                // New Name
                string newLibName = IterateName(libName, lib, true);
                if (newLibName == "") { continue; }
                // New Library
                AnimationLibrary dupeLib = new();

                foreach (StringName animname in lib.GetAnimationList()) {
                    Animation anim = lib.GetAnimation(animname);
                    // New Name
                    string newAnimName = IterateName(animname, anim, true);
                    Print(newAnimName);
                    if (newAnimName == "") { continue; }
                    // new Animation
                    Animation dupeAnim = new();

                    for (int trackNum = 0; trackNum<anim.GetTrackCount(); trackNum++) {
                        anim.CopyTrack(trackNum, dupeAnim);
                        NodePath trackPath = anim.TrackGetPath(trackNum);
                        if (dupe.GetNodeOrNull(trackPath) == null) {
                            string[] pathSteps = trackPath.GetConcatenatedNames().Split('/');
                            string frontier = pathSteps[0];
                            for (int i = 0; i<pathSteps.Length; i++) {
                                Node frontierNode = p.GetNodeOrNull(frontier);
                                if (frontierNode.HasMeta("Instance")) {
                                    pathSteps[i] = IterateName(pathSteps[i], frontierNode, false);
                                }
                                if (i + 1 < pathSteps.Length) {
                                    frontier += "/" + pathSteps[i + 1];
                                }
                            }
                            string newPath = String.Join('/', pathSteps);
                            newPath += ":" + trackPath.GetConcatenatedSubNames();
                            Print(trackPath);
                            Print(newPath);
                            dupeAnim.TrackSetPath(trackNum, newPath);
                        }
                    }

                    dupeAnim.SetMeta("AnimationGroup", anim.GetMeta("AnimationGroup").AsString());
                    dupeAnim.SetMeta("Instance", anim.GetMeta("Instance").AsInt32() + 1);
                    dupeAnim.LoopMode = Animation.LoopModeEnum.Linear;

                    dupeLib.AddAnimation(newAnimName, dupeAnim);
                }

                dupeLib.SetMeta("Instance", lib.GetMeta("Instance").AsInt32() + 1);
                dupePlayer.AddAnimationLibrary(newLibName, dupeLib);
            }

            dupe.animationPlayer = dupePlayer;
            dupe.AddChild(dupePlayer);
        }
    }

    // Signal when "Delete Selected" is pressed
    private void DeleteSelectedPart() {
        
        static void Delete(Thing part, Node parent) {
            Thing[] childPartList = part.connectedThings.ToArray();
            foreach (Thing child in childPartList) {
                Delete(child, part);
            }
            part.Unselected();
            if (part.parentThing != null) {
                part.GetHierarch(Thing.TraversalType.Collider).DetachPart(part);
                part.activeCollider.ClearColliderRelation();
            }
            if (parent is Thing tParent) {
                tParent.RemoveFromTracker(part);
                tParent.connectedThings.Remove(part);
            }
            part.GetParent().RemoveChild(part);
            part.QueueFree();
        }

        Thing p = this.worldRoot.GetSelected<Thing>();
        if (p != null) {
            this.worldRoot.ClearSelected();
            Delete(p, this.worldRoot.GetChild(0));
        }
    }

    private void NameNewThing() {
        Control ui = Load<PackedScene>("res://src/UI/SingleInput.tscn").Instantiate<Control>();
        LineEdit input = ui.GetChild<LineEdit>(1);
        Button submit = ui.GetChild<Button>(2);
        submit.Pressed += () => CreateNewThing(input.Text);
        this.worldContainer.AddChild(ui);
        ui.Position = (this.worldContainer.Size * 0.5f) - (ui.Size * 0.5f);
    }

    // Signal when "Create New Thing" is pressed
    private void CreateNewThing(string name) {

        Thing p = this.worldRoot.GetSelected<Thing>();
        if (p != null) {
            Script dataGeneration = Load<Script>("res://import/GenerateThingData.gd");
            dataGeneration.Call("GenerateRuntime", p.GetHierarch(Thing.TraversalType.Parent), name);
        }
    }

    private void ToggleQuadList() {
        Thing p = this.worldRoot.GetSelected<Thing>();
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
                quadItem.Pressed += () => list.FlipQuad(meshType, plane);
                Node container = quadItem.GetChild(0);
                container.GetChild<Label>(0).Text = partName;
                container.GetChild<Label>(1).Text = meshType;
                list.GetChild(0).AddChild(quadItem);
            }

            list.Visible = false;
            Camera3D camera = this.worldRoot.GetViewport().GetCamera3D();
            
            Vector2 pos = camera.UnprojectPosition(p.GlobalPosition);
            this.worldContainer.AddChild(list);
            list.Position = pos;
            list.Visible = true;
        }
    }

    private void RotatePart(float degrees) {
        Thing p = this.worldRoot.GetSelected<Thing>();
        if (p.activeCollider == null) { return; }
        AlignmentPlane connectingPlane = p.activeCollider.plane;
        AlignmentPlane receivingPlane = p.activeCollider.GetBoundCollider().plane;
        connectingPlane.ShiftFrontToNext(degrees);
        Transform3D newTransform = ThingTools.CalculateJoinTransform(connectingPlane, receivingPlane, p.GlobalTransform);
        p.GlobalTransform = newTransform;
    }

    private void ToggleAnimationList() {
        Thing p = this.worldRoot.GetSelected<Thing>();
        if (p == null) { return; }

        this.animationList = Load<PackedScene>("res://src/UI/QuadList.tscn").Instantiate<QuadList>();
        this.animationList.Hide();
        this.worldContainer.AddChild(this.animationList);
        this.animationList.AddTopPartAnimationsToList(p);
        Camera3D camera = this.worldRoot.GetViewport().GetCamera3D();
        Vector2 pos = camera.UnprojectPosition(p.GlobalPosition);
        this.animationList.Position = pos;
        this.animationList.Visible = true;
    }

    private void MirrorPart() {
        Thing p = this.worldRoot.GetSelected<Thing>();
        if (p != null && (p.HasMeta("Mirror") || p.HasMeta("IsMirror"))) {

            PackedScene mirrorPartScene = p.GetMeta("Mirror").As<PackedScene>();
            Thing mirrorPart = mirrorPartScene.Instantiate<Thing>();
            mirrorPart.importData.scene = mirrorPartScene;

            if (mirrorPart.GetTrackedCount(mirrorPart) > 0) {
                DuplicateSelectedPart(mirrorPart);
            }
            else {
                mirrorPart.PrepareThing(null, this.worldRoot);
            }
        }
    }

    private void GoToTestGround() {
        PackedScene scene = Load<PackedScene>("res://src/World/TestingGround.tscn");
        this.worldRoot.SwitchState(scene);

        CanvasLayer uiLayer = GetChild<CanvasLayer>(0);
        uiLayer.Hide();
        this.worldContainer.SetAnchor(Side.Left, 0.0f, true);
        this.worldContainer.SetAnchor(Side.Right, 1.0f, true);
    }

    public override void _Ready() {
        CanvasLayer uiLayer = GetChild<CanvasLayer>(0);
        this.things = uiLayer.GetChild<ItemList>(0);
        this.things.MultiSelected += ThingSelected;
        string path = "res://src/Things/Data/Resources/";
        string[] dirs = DirAccess.Open(path).GetDirectories();
        foreach (string dir in dirs) {
            this.things.AddItem(dir.Split('.').First());
        }

        this.goToTestGround = uiLayer.GetChild<Button>(1);
        this.goToTestGround.Pressed += GoToTestGround;

        this.toolList = uiLayer.GetChild<VBoxContainer>(2);
        this.duplicateSelected = this.toolList.GetChild<Button>(0);
        this.duplicateSelected.Pressed += () => DuplicateSelectedPart();
        this.deleteSelected = this.toolList.GetChild<Button>(1);
        this.deleteSelected.Pressed += DeleteSelectedPart;
        this.createNewThing = this.toolList.GetChild<Button>(2);
        this.createNewThing.Pressed += NameNewThing;
        HBoxContainer rotationButtons = this.toolList.GetChild<HBoxContainer>(3);
        this.rotateClockwise = rotationButtons.GetChild<Button>(0);
        this.rotateClockwise.Pressed += () => RotatePart(90);
        this.rotateCounterClockwise = rotationButtons.GetChild<Button>(1);
        this.rotateCounterClockwise.Pressed += () => RotatePart(-90);
        this.toggleQuadList = this.toolList.GetChild<Button>(4);
        this.toggleQuadList.Pressed += ToggleQuadList;
        this.toggleAnimationList = this.toolList.GetChild<Button>(5);
        this.toggleAnimationList.Pressed += ToggleAnimationList;
        this.mirrorSelected = this.toolList.GetChild<Button>(6);
        this.mirrorSelected.Pressed += MirrorPart;

        this.worldContainer = this.GetChild<SubViewportContainer>(1);
        this.worldRoot = this.worldContainer.GetChild<SubViewport>(0).GetChild<WorldRoot>(0);
        this.theme = Load<Theme>("src/UI/Themes/ThingEditor.tres");
    }
}