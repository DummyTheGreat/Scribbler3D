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

        if ((Script)part.GetScript() == null && part.Name.ToString().EndsWith("Armature")) {
            part.SetScript(this.deformingPartScript);
        }
        else if ((Script)part.GetScript() == null && part.Name.ToString().EndsWith("Group")) {
            part.SetScript(this.staticPartScript);
        }

        MeshInstance3D partBounds = null;
        Skeleton3D partSkeleton = null;

        foreach (Node child in part.GetChildren()) {

            if (child is MeshInstance3D mesh) {
                if (mesh.Name.ToString().EndsWith("Bounds") && mesh.GetChildCount() == 0) {
                    partBounds = mesh;
                    Aabb local = mesh.Mesh.GetAabb();

                    //Vector3 meshSize = local * mesh.GlobalTransform;

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

        if (parent is Node3D partParent) {
            // Do part/bone connections
            string partName = part.Name.ToString().Replace("Armature", string.Empty);
            Skeleton3D parentSkeleton = partParent.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
            if (parentSkeleton != null) {
                // Parent part to bone attachment which contains matching receiver
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
                if (partParent.Name.ToString().EndsWith("Armature") || partParent.Name.ToString().EndsWith("Group")) {
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

        // Add imported animations to model animation player and remove useless imported animations
        if (animationPlayer == null) {

            string sceneName = scene.Name.ToString();
            string thingName = scene.Name.ToString()[..sceneName.FindN("_")];
            string libraryName = thingName + "_Animation";

            // If there is no associated library then there are no animations and thus there is no need for an animation player
            if (ResourceLoader.Exists("res://assets/Models/" + libraryName + ".glb")) {
                AnimationLibrary library = GD.Load<AnimationLibrary>("assets/Models/" + libraryName + ".glb");
                animationPlayer = new() { Name = "AnimationPlayer" };
                animationPlayer.AddAnimationLibrary(thingName + "Library", library);

                Godot.Collections.Array<StringName> animationNames = library.GetAnimationList();
                foreach (StringName animationName in animationNames) {
                    Animation a = animationPlayer.GetAnimation(thingName + "Library/" + animationName);

                    int count = 0;
                    a.Optimize();
                    while (count < a.GetTrackCount()) {
                        if (a.TrackGetKeyCount(count) <= 1) {
                            a.RemoveTrack(count);
                        }
                        else {
                            count++;
                        }
                    }

                    a.LoopMode = Animation.LoopModeEnum.Linear;
                }

                scene.AddChild(animationPlayer);
                animationPlayer.Owner = scene;

                // Animation tree
                AnimationTree tree = new();
                scene.AddChild(tree);
                tree.Owner = scene;
            }
            else {
                GD.Print("No Animation Library Found for model: ", scene.Name);
            }
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
