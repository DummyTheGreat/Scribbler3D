using Godot;
using Godot.Collections;
using System;
using System.IO;
using System.Linq;
using static Godot.GD;
using static System.Runtime.InteropServices.JavaScript.JSType;

[Tool]
public partial class PostImportPart : EditorScenePostImport
{
    Script staticPartScript;
    Script deformingPartScript;
    Script alignmentPlaneScript;

    private static void NameToMeta(Node node) {
        string[] nameItems = node.Name.ToString().Split('_');

        node.SetMeta("MeshType", nameItems[0]);
        node.SetMeta("ThingName", nameItems[1]);
        node.SetMeta("PartName", nameItems[2]);

        string[] orientations = ["Left", "Right", "Front", "Rear", "Top", "Bottom"];

        // Minimum should always be three. Type_Thing_Part
        if (nameItems.Length >= 4 && nameItems[3].All(char.IsAsciiDigit)) {
            node.SetMeta("VariantNumber", nameItems[3].ToInt());
            if (nameItems.Length == 5) {
                node.SetMeta("Orientation", nameItems[4]);
            }
        }
        else if (nameItems.Length == 4 && orientations.Contains(nameItems[3])) {
            node.SetMeta("Orientation", nameItems[3]);
        }
    }

    private void ValidateAndInitAlignmentPlane(MeshInstance3D mesh) {
        string meshName = mesh.Name.ToString();
        if ((Script)mesh.GetScript() == null && (meshName.Contains("Connector") || meshName.Contains("Receiver"))) {
            mesh.SetScript(this.alignmentPlaneScript);
            NameToMeta(mesh);
            mesh.Hide();
        }
    }

    private void ModifyNodeTree(Node3D part) {

        MeshInstance3D partBounds = null;
        MeshInstance3D skin = null;
        Skeleton3D partSkeleton = null;

        foreach (Node child in part.GetChildren()) {

            if (child is MeshInstance3D mesh) {
                if (mesh.Name.ToString().StartsWith("Bounds") && mesh.GetChildCount() == 0) {
                    partBounds = mesh;
                    Aabb local = mesh.Mesh.GetAabb();

                    StaticBody3D a = new() { Name = "CollisionBody" };
                    BoxShape3D sh = new() { Size = local.Size };
                    CollisionShape3D col = new() { Name = "CollisionShape", Shape = sh };

                    a.AddChild(col);
                    mesh.AddChild(a);

                    a.Owner = part;
                    col.Owner = part;

                    mesh.Hide();
                }
                else if (mesh.Name.ToString().StartsWith("Skin")) {
                    skin = mesh;
                }
                ValidateAndInitAlignmentPlane(mesh);
            }

            if (child is Skeleton3D skeleton) {
                partSkeleton = skeleton;

                foreach (BoneAttachment3D bone in skeleton.GetChildren().Where(x => x is BoneAttachment3D).Cast<BoneAttachment3D>()) {

                    MeshInstance3D alignmentPlane = bone.GetChild<MeshInstance3D>(0);
                    ValidateAndInitAlignmentPlane(alignmentPlane);
                }

                skin = (MeshInstance3D)skeleton.GetChildren().First(x => x is MeshInstance3D mesh);
            }
        }

        if (partSkeleton != null && (Script)part.GetScript() == null) {
            part.SetScript(this.deformingPartScript);
        }
        else if ((Script)part.GetScript() == null) {
            part.SetScript(this.staticPartScript);
        }

        if (partBounds != null) {
            if (partSkeleton != null) { 
                partSkeleton.Position -= partBounds.Position;
            }
            else {
                foreach (Node3D c in part.GetChildren().OfType<Node3D>().Where(x => x != partBounds)) {
                    c.Position -= partBounds.Position;
                }
            }
            partBounds.Position = Vector3.Zero;

        }
    }

    private static void AnimationSetup(Node scene, AnimationPlayer animationPlayer) {

        // Add imported animations to model animation player and remove useless imported animations
        if (animationPlayer != null) {

            string[] animationNames = animationPlayer.GetAnimationList();
            //Dictionary<string, Animation> combinedAnimations = [];
            AnimationLibrary thingLib = new();

            foreach (StringName animationName in animationNames) {
                Animation a = animationPlayer.GetAnimation(animationName);
                int count = 0;
                a.Optimize();
                while (count < a.GetTrackCount()) {
                    NodePath path = a.TrackGetPath(count);
                    int depth = path.GetNameCount();

                    if (a.TrackGetKeyCount(count) <= 1 || path.GetName(depth - 1) != "Skeleton3D") {
                        // Filter out useless tracks
                        a.RemoveTrack(count);
                    }
                    else {
                        // Adjust nodepaths for tracks to work with new node tree setup
                        a.TrackSetPath(count, "Skeleton3D:" + path.GetSubName(0));
                        // Next
                        count++;
                    }
                }

                string aStr = animationName.ToString();
                bool isMA = scene.GetMeta("IsMirror").AsBool();
                string newAnimName = scene.GetMeta("ThingName").AsString() + scene.GetMeta("PartName").AsString() + (isMA ? "Mirror" : "") + aStr;
                a.SetMeta("AnimationGroup", aStr);

                Print(a.GetMeta("AnimationGroup"));
                thingLib.AddAnimation(newAnimName, a);

                a.LoopMode = Animation.LoopModeEnum.Linear;
            }
            bool isMirror = scene.GetMeta("IsMirror").AsBool();
            string newLibName = scene.GetMeta("ThingName").AsString() + scene.GetMeta("PartName").AsString() + (isMirror ? "Mirror" : "") + "Lib";
            animationPlayer.AddAnimationLibrary(newLibName, thingLib);
            animationPlayer.RemoveAnimationLibrary("");
        }
        else {
            GD.Print("No Animation Library Found for model: ", scene.Name);
        }
    }

    public override GodotObject _PostImport(Node scene) {

        static void SetChildOwners(Node node, Node partScene) {
            foreach (Node child in node.GetChildren()) {
                child.Owner = partScene;
                SetChildOwners(child, partScene);
            }
        }

        this.staticPartScript = Load<Script>("src/Things/Parts/StaticPart.cs");
        this.deformingPartScript = Load<Script>("src/Things/Parts/DeformingPart.cs");
        this.alignmentPlaneScript = Load<Script>("src/Things/Parts/AlignmentPlane.cs");

        Node3D part = (Node3D)scene.GetChild(0);

        Node[] children = [.. part.GetChildren()];

        foreach (Node child in children) {
            child.Owner = null;
            child.Reparent(scene, false);
            child.Owner = scene;
        }

        scene.RemoveChild(part);
        string importedPartName = part.Name;
        part.QueueFree();
        string sceneName = scene.Name.ToString();
        int firstUnderscore = sceneName.Find("_");
        string thingName = sceneName[..firstUnderscore];
        string remainingName = sceneName[(firstUnderscore + 1)..];
        int secondUnderscore = remainingName.Find("_");
        string partName = remainingName[..secondUnderscore];

        ModifyNodeTree((Node3D)scene);

        AnimationPlayer animationPlayer = scene.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();

        string oldName = scene.Name.ToString();
        scene.Name = importedPartName;
        NameToMeta(scene);

        string mirrorName = thingName + "_" + partName + "_Mirror_Model";
        string path;

        if (oldName != mirrorName) {
            scene.SetMeta("IsMirror", false);
            path = "assets/Models/" + thingName + "/Mirrors/" + mirrorName + ".glb";
        }
        else {
            scene.SetMeta("IsMirror", true);
            Print("Is Mirror");
            path = "assets/Models/" + oldName + ".glb";
        }

        if (ResourceLoader.Exists(path)) {
            PackedScene mirror = Load<PackedScene>(path);
            Print("Mirror Found");
            scene.SetMeta("Mirror", mirror);
        }

        AnimationSetup(scene, animationPlayer);

        Script dataGeneration = Load<Script>("res://import/GenerateThingData.gd");
        Print(dataGeneration.Call("generate", scene));
        return scene;
    }
}
