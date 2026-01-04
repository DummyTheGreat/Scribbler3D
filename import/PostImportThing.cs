using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class PostImportThing : EditorScenePostImport
{
    public override GodotObject _PostImport(Node scene) {

        Script thingScript = GD.Load<Script>("src/Things/Thing.cs");
        Script staticPartScript = GD.Load<Script>("src/Things/Parts/StaticPart.cs");
        Script deformingPartScript = GD.Load<Script>("src/Things/Parts/DeformingPart.cs");

        if ((Script)scene.GetScript() == null) { scene.SetScript(thingScript); }

        Stack<Node3D> stack = new([.. scene.GetChildren().Where(x => x.GetType() == typeof(Node3D)).Cast<Node3D>().ToList()]);

        while (stack.Count > 0) {
            Node3D part = stack.Pop();

            part.Owner = scene;

            if ((Script)part.GetScript() == null && part.Name.ToString().EndsWith("Armature")) {
                part.SetScript(deformingPartScript);
            }
            else if ((Script)part.GetScript() == null && part.Name.ToString().EndsWith("Static")) {
                part.SetScript(staticPartScript);
            }

            MeshInstance3D partBounds = null;
            Skeleton3D partSkeleton = null;

            foreach (Node child in part.GetChildren()) {

                if (child is MeshInstance3D mesh && mesh.Name.ToString().EndsWith("Bounds") && mesh.GetChildCount() == 0) {
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

                if (child is Skeleton3D skeleton) {
                    partSkeleton = skeleton;
                }

                if (child.GetType() == typeof(Node3D)) {

                    stack.Push((Node3D)child);
                }
            }

            // Transform Armature origin to bounds origin
            if (partBounds != null && partSkeleton != null) {
                //Vector3 boundsOffset = partBounds.Transform.Origin;
                //Transform3D tOffset = new (Basis.Identity, boundsOffset);
                //Transform3D invTOffset = tOffset.AffineInverse();

                part.Position += partBounds.Position;
                partBounds.Position = Vector3.Zero;
                partSkeleton.Position -= part.Position;

                //Get parent transform
                Node3D parent = part.GetParent<Node3D>();
                if (parent.Name.ToString().EndsWith("Armature")) {
                    part.Position -= parent.Position;
                }
            }
        }

        return scene;
    }
}
