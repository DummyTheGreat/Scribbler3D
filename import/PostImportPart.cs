using Godot;
using System;
using System.Linq;

[Tool]
public partial class PostImportPart : EditorScenePostImport
{
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

    private void ModifyNodeTree(Node3D part, Node scene) {

        MeshInstance3D partBounds = null;
        Skeleton3D partSkeleton = null;
        GD.Print("SDFSDFSDF");

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

        if (partBounds != null) {

            part.Position += partBounds.Position;
            partBounds.Position = Vector3.Zero;
            if (partSkeleton != null) { partSkeleton.Position -= part.Position; }
            else {
                foreach (Node3D c in scene.GetChildren().OfType<Node3D>().Where(x => x != partBounds)) {
                    c.Position -= part.Position;
                }
            }
        }
    }

    private static void AnimationSetup(Node scene) {
        AnimationPlayer animationPlayer = scene.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
        string sceneName = scene.Name.ToString();
        string thingName = sceneName[..sceneName.Find("_")];
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
        }
        else {
            GD.Print("No Animation Library Found for model: ", scene.Name);
        }
    }

    public override GodotObject _PostImport(Node scene) {

        this.staticPartScript = GD.Load<Script>("src/Things/Parts/StaticPart.cs");
        this.deformingPartScript = GD.Load<Script>("src/Things/Parts/DeformingPart.cs");
        this.alignmentPlaneScript = GD.Load<Script>("src/Things/Parts/AlignmentPlane.cs");

        ModifyNodeTree((Node3D)scene.GetChild(0), scene);
        AnimationSetup(scene);
        return scene;
    }
}
