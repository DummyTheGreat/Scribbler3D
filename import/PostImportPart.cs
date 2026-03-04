using Godot;
using static Godot.GD;
using System;
using System.Linq;
using Godot.Collections;

[Tool]
public partial class PostImportPart : EditorScenePostImport
{
    Script staticPartScript;
    Script deformingPartScript;
    Script alignmentPlaneScript;

    private void ValidateAndInitAlignmentPlane(MeshInstance3D mesh) {
        string meshName = mesh.Name.ToString();
        if ((Script)mesh.GetScript() == null && (meshName.Contains("Connector") || meshName.Contains("Receiver"))) {
            mesh.SetScript(this.alignmentPlaneScript);
            string[] nameItems = meshName.Split('_');
            Dictionary<string, string> data = new() {
                { "MeshType", nameItems[0]},
                { "ThingName", nameItems[1]},
                { "PartName", nameItems[2]},
            };

            string[] orientations = ["Left", "Right", "Front", "Rear", "Top", "Bottom"];

            // Minimum should always be three. Type_Thing_Part
            if (nameItems.Length >= 4 && nameItems[3].All(char.IsAsciiDigit)) {
                data.Add("VariantNumber", nameItems[3]);
                if (nameItems.Length == 5) {
                    data.Add("Orientation", nameItems[4]);
                }
            }
            else if (nameItems.Length == 4 && orientations.Contains(nameItems[3])) {
                data.Add("Orientation", nameItems[3]);
            }

            mesh.SetMeta("AlignmentData", data);
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
            //part.Position += partBounds.Position;
            //skin.Position -= partBounds.Position;
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

    private static void AnimationSetup(Node scene, AnimationPlayer animationPlayer, string thingName, string partName) {

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
                string newAnimName = thingName + partName + aStr;
                a.SetMeta("AnimationGroup", aStr);
                //string[] origins = [thingName];
                //a.SetMeta("AnimtationOrigin", origins);
                Print(a.GetMeta("AnimationGroup"));
                thingLib.AddAnimation(newAnimName, a);

                a.LoopMode = Animation.LoopModeEnum.Linear;
            }
            animationPlayer.AddAnimationLibrary(thingName + partName + "Lib", thingLib);
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

        Dictionary<string, string> partData = new() {
                { "ThingName", thingName},
                { "PartName", partName},
            };
        scene.SetMeta("PartData", partData);

        ModifyNodeTree((Node3D)scene);

        AnimationPlayer animationPlayer = scene.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();

        AnimationSetup(scene, animationPlayer, thingName, partName);
        string mirrorName = thingName + "_" + partName + "_Mirror_Model";
        if (scene.Name != mirrorName) {
            string path = "assets/Models/" + thingName + "/Mirrors/" + mirrorName + ".glb";
            if (FileAccess.FileExists(path)) { 
                PackedScene mirror = Load<PackedScene>(path);
                Print("Mirror Found");
                scene.SetMeta("Mirror", mirror);
            }
        }

        scene.Name = importedPartName;

        return scene;
    }
}
