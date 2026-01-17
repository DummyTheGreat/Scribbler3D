using Godot;
using System;
using System.Collections.Generic;
using System.Text;

// AI Generated
public partial class SkeletonBoneTools : Node
{
    public static string DumpSkeletonTree(Skeleton3D skel, bool includeRestPose = false) {
        int count = skel.GetBoneCount();
        if (count == 0)
            return $"{skel.Name}: (no bones)";

        // Build children list per bone
        var children = new List<int>[count];
        for (int i = 0; i < count; i++)
            children[i] = new List<int>();

        var roots = new List<int>();
        for (int i = 0; i < count; i++) {
            int parent = skel.GetBoneParent(i);
            if (parent >= 0 && parent < count) children[parent].Add(i);
            else roots.Add(i);
        }

        // Deterministic order: sort children by name
        for (int i = 0; i < count; i++)
            children[i].Sort((a, b) => string.CompareOrdinal(skel.GetBoneName(a), skel.GetBoneName(b)));
        roots.Sort((a, b) => string.CompareOrdinal(skel.GetBoneName(a), skel.GetBoneName(b)));

        var sb = new StringBuilder();
        sb.AppendLine($"Skeleton: {skel.Name}  (bones={count})");

        for (int r = 0; r < roots.Count; r++) {
            bool isLastRoot = (r == roots.Count - 1);
            DumpBoneRecursive(skel, roots[r], children, sb, prefix: "", isLast: isLastRoot, includeRestPose: includeRestPose);
        }

        return sb.ToString();
    }

    private static void DumpBoneRecursive(
        Skeleton3D skel,
        int bone,
        List<int>[] children,
        StringBuilder sb,
        string prefix,
        bool isLast,
        bool includeRestPose) {
        string branch = isLast ? "└─ " : "├─ ";
        string name = skel.GetBoneName(bone);
        int parent = skel.GetBoneParent(bone);

        sb.Append(prefix).Append(branch)
          .Append(name)
          .Append($"  [#{bone}, parent={parent}]");

        if (includeRestPose) {
            // Rest transform is in skeleton-local space
            Transform3D rest = skel.GetBoneGlobalRest(bone);
            Transform3D pose = skel.GetBoneGlobalPose(bone);
            Vector3 o = rest.Origin;
            Vector3 oo = pose.Origin;
            // Basis columns are X,Y,Z (Godot forward is -Z, but showing basis raw is still useful)
            sb.Append($"  rest.origin=({o.X:0.###},{o.Y:0.###},{o.Z:0.###})");
            sb.Append($"  pose.origin=({oo.X:0.###},{oo.Y:0.###},{oo.Z:0.###})");

        }

        sb.AppendLine();

        string childPrefix = prefix + (isLast ? "   " : "│  ");
        var kids = children[bone];
        for (int i = 0; i < kids.Count; i++) {
            bool lastChild = (i == kids.Count - 1);
            DumpBoneRecursive(skel, kids[i], children, sb, childPrefix, lastChild, includeRestPose);
        }
    }

    public static MeshInstance3D DrawSkeleton(Skeleton3D skeleton) {

        static StandardMaterial3D MakeOverlayLineMaterial() {
            var mat = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true, // optional if you set per-vertex colors
                Transparency = BaseMaterial3D.TransparencyEnum.Disabled
            };

            mat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled; // <- key
            mat.NoDepthTest = true; // <- key (ensures it renders regardless of depth)

            // Optional: avoid culling
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

            return mat;
        }

        var mesh = new ImmediateMesh();

        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        mesh.SurfaceSetNormal(Vector3.Up); // normals don't matter for lines
        mesh.SurfaceSetUV(Vector2.Zero);

        int boneCount = skeleton.GetBoneCount();
        var skelGlobal = skeleton.GlobalTransform;

        for (int child = 0; child < boneCount; child++) {
            int parent = skeleton.GetBoneParent(child);
            if (parent < 0) continue; // root bone has no parent

            // Bone poses are in skeleton-local space
            Transform3D parentPoseL = skeleton.GetBoneGlobalPose(parent);
            Transform3D childPoseL = skeleton.GetBoneGlobalPose(child);

            // Convert to world space
            Vector3 parentW = (skelGlobal * parentPoseL).Origin;
            Vector3 childW = (skelGlobal * childPoseL).Origin;

            mesh.SurfaceAddVertex(parentW);
            mesh.SurfaceAddVertex(childW);
        }

        mesh.SurfaceEnd();

        var mi = new MeshInstance3D {
            Name = $"{skeleton.Name}_BoneLines",
            Mesh = mesh,
            // This is world-space line geometry already; keep identity transform.
            GlobalTransform = Transform3D.Identity
        };

        mi.MaterialOverride = MakeOverlayLineMaterial();

        return mi;
    }
}
