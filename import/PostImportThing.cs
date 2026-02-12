using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;

[Tool]
public partial class PostImportThing : EditorScenePostImport
{
    Script thingScript;
    Script staticPartScript;
    Script deformingPartScript;
    Script alignmentPlaneScript;

    private void ValidateAndInitAlignmentPlane(MeshInstance3D mesh) {
        if ((Script)mesh.GetScript() == null &&
            (mesh.Name.ToString().EndsWith("Connector") || mesh.Name.ToString().EndsWith("Receiver"))) {
            mesh.SetScript(this.alignmentPlaneScript);
            mesh.Hide();
        }
    }

    private void ModifyNodeTree(Node3D part, Node parent, Node scene) {

        part.Owner = scene;

        MeshInstance3D partBounds = null;
        Skeleton3D partSkeleton = null;

        foreach (Node child in part.GetChildren()) {

            if (child is MeshInstance3D mesh) {
                if (mesh.Name.ToString().EndsWith("Bounds") && mesh.GetChildCount() == 0) {
                    partBounds = mesh;
                    Aabb local = mesh.Mesh.GetAabb();

                    StaticBody3D a = new() { Name = "CollisionBody" };
                    BoxShape3D sh = new() { Size = local.Size };
                    CollisionShape3D col = new() { Name = "CollisionShape", Shape = sh };

                    a.AddChild(col);
                    mesh.AddChild(a);

                    a.Owner = scene;
                    col.Owner = scene;

                    mesh.Hide();
                }
                ValidateAndInitAlignmentPlane(mesh);
            }

            if (child is Skeleton3D skeleton) {
                partSkeleton = skeleton;

                foreach (BoneAttachment3D bone in skeleton.GetChildren().Where(x => x is BoneAttachment3D).Cast<BoneAttachment3D>()) {

                    MeshInstance3D alignmentPlane = bone.GetChild<MeshInstance3D>(0);
                    ValidateAndInitAlignmentPlane(alignmentPlane);
                }
            }
        }

        if (partSkeleton != null && (Script)part.GetScript() == null) {
            part.SetScript(this.deformingPartScript);
        }
        else if ((Script)part.GetScript() == null) {
            part.SetScript(this.staticPartScript);
        }

        if (parent is Node3D partParent) {
            // Do part/bone connections
            Skeleton3D parentSkeleton = partParent.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
            if (parentSkeleton != null) {
                // Parent part to bone attachment which contains matching receiver
                string partName = part.GetMeta("extras").AsGodotDictionary<string, string>()["PartName"];
                foreach (Node skelChild in parentSkeleton.GetChildren()) {

                    if (skelChild is BoneAttachment3D attach && (attach.FindChild(partName + "Receiver") != null)) {
                        part.Owner = null;
                        part.Reparent(attach, false);
                        part.Owner = scene;
                        break;
                    }
                }
            }

            if (partBounds != null) {

                part.Position += partBounds.Position;
                partBounds.Position = Vector3.Zero;
                if (partSkeleton != null) { partSkeleton.Position -= part.Position; }
                else {
                    foreach (Node3D c in part.GetChildren().OfType<Node3D>().Where(x => x != partBounds)) {
                        c.Position -= part.Position;
                    }
                }

                //Get parent position and apply it as inverse position to part
                if (partParent.Name.ToString().EndsWith("Part")) {
                    part.Position -= partParent.Position;
                }
            }
        }



        // Recursion to child parts
        foreach (Node3D child in part.GetChildren().Where(x => x.GetType() == typeof(Node3D)).Cast<Node3D>().ToList()) {
            ModifyNodeTree(child, part, scene);
        }
    }

    private static void AnimationSetup(Node scene) {
        AnimationPlayer animationPlayer = scene.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
        string sceneName = scene.Name.ToString();
        string thingName = sceneName[..sceneName.RFind("_")];
        scene.Name = thingName;

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
                        Node exists = scene.GetNodeOrNull(path);
                        if (exists == null && depth > 2) {
                            Node armature = scene.FindChild(path.GetName(depth - 2));
                            a.TrackSetPath(count, scene.GetPathTo(armature) + "/Skeleton3D:" + path.GetSubName(0));
                        }
                        // Next
                        count++;
                    }
                }

                string aStr = animationName.ToString();
                string newAnimName = thingName + "" + aStr;
                a.SetMeta("AnimationGroup", aStr);
                //string[] origins = [thingName];
                //a.SetMeta("AnimtationOrigin", origins);
                GD.Print(a.GetMeta("AnimationGroup"));
                thingLib.AddAnimation(newAnimName, a);

                a.LoopMode = Animation.LoopModeEnum.Linear;
            }
            animationPlayer.AddAnimationLibrary(thingName + "Lib", thingLib);
            animationPlayer.RemoveAnimationLibrary("");

            // Animation tree
            //AnimationTree tree = new() { Name = "AnimationTree" };
            //scene.AddChild(tree);
            //tree.Owner = scene;
            //tree.AnimPlayer = tree.GetPathTo(animationPlayer);
            //AnimationNodeBlendTree blendTree = new();
            //tree.TreeRoot = blendTree;

        }
        else {
            GD.Print("No Animation Library Found for model: ", scene.Name);
        }
    }

    public override GodotObject _PostImport(Node scene) {

        this.thingScript = GD.Load<Script>("src/Things/Thing.cs");
        this.staticPartScript = GD.Load<Script>("src/Things/Parts/StaticPart.cs");
        this.deformingPartScript = GD.Load<Script>("src/Things/Parts/DeformingPart.cs");
        this.alignmentPlaneScript = GD.Load<Script>("src/Things/Parts/AlignmentPlane.cs");

        if ((Script)scene.GetScript() == null) { scene.SetScript(this.thingScript); }

        foreach (Node3D part in scene.GetChildren().Where(x => x.GetType() == typeof(Node3D)).Cast<Node3D>().ToList()) {
            ModifyNodeTree(part, scene, scene);
        }

        AnimationSetup(scene);

        return scene;
    }
}
