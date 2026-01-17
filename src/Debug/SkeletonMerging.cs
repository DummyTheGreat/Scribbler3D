using Godot;
using static Godot.GD;
using System;
using System.Linq;

public partial class SkeletonMerging : Node
{
    Skeleton3D skeleton;

    private static Skin RebuildSkinForSkeleton(MeshInstance3D mesh, Skeleton3D receiver, Skeleton3D connector) {
        var oldSkin = mesh.Skin;
        if (oldSkin == null) return null;

        var newSkin = new Skin();
        int binds = oldSkin.GetBindCount();

        Transform3D connectorToReceiver = receiver.GlobalTransform.AffineInverse() * connector.GlobalTransform;
        Transform3D receiverToConnector = connectorToReceiver.AffineInverse();

        for (int i = 0; i < binds; i++) {
            StringName bindName = oldSkin.GetBindName(i);
            int receiverBone = receiver.FindBone(bindName);

            if (receiverBone < 0) {
                Print("SDFIOJSDFIOLJSD");
            }


            Transform3D oldBindPose = oldSkin.GetBindPose(i);

            // Key step: convert bind pose to receiver skeleton space
            Transform3D newBindPose = oldBindPose * receiverToConnector;

            newSkin.AddBind(receiverBone, newBindPose);
            newSkin.SetBindName(newSkin.GetBindCount() - 1, bindName);
        }

        return newSkin;
    }

    private static void ReplaceSkeleton(Skeleton3D oldSkel, Skeleton3D newSkel) {

        // Fix attachment indices

        foreach (BoneAttachment3D attachment in oldSkel.GetChildren().OfType<BoneAttachment3D>()) {
            BoneAttachment3D newAttachment = new() {
                Name = attachment.Name,
                BoneIdx = newSkel.FindBone(attachment.BoneName),
                BoneName = attachment.BoneName,
                UseExternalSkeleton = true,
                ExternalSkeleton = attachment.ExternalSkeleton,
            };
            newSkel.AddChild(newAttachment);
            foreach (Node child in attachment.GetChildren()) {
                child.Reparent(newAttachment);
            }
            //attachment.BoneIdx = newSkel.FindBone(attachment.BoneName);

        }
        string oldSkelName = oldSkel.Name;
        oldSkel.GetChildren().OfType<MeshInstance3D>().First().Reparent(newSkel);
        oldSkel.Free();
        newSkel.Name = oldSkelName;
    }

    private static void SetConnectorChildrenOnConnect(Skeleton3D receiver, Skeleton3D connector, int socket) {
        MeshInstance3D skinMesh = connector.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();

        if (skinMesh != null) {
            skinMesh.Skeleton = skinMesh.GetPathTo(receiver);

            skinMesh.Skin = RebuildSkinForSkeleton(skinMesh, receiver, connector);
        }
        foreach (BoneAttachment3D attachment in connector.GetChildren().OfType<BoneAttachment3D>()) {
            BoneAttachment3D newAttachment = new() {
                Name = attachment.Name,
                BoneIdx = receiver.FindBone(attachment.BoneName),
                BoneName = attachment.BoneName,
                UseExternalSkeleton = true,
                ExternalSkeleton = attachment.GetPathTo(receiver),
            };
            attachment.ReplaceBy(newAttachment);
        }
    }
    private static void SetConnectorChildrenOnDisconnect(Skeleton3D receiver, Skeleton3D disconnector) {
        MeshInstance3D skinMesh = disconnector.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
        if (skinMesh != null) {
            skinMesh.Skin = RebuildSkinForSkeleton(skinMesh, disconnector, receiver);
            //Transform3D meshInOldSkel = receiver.GlobalTransform.AffineInverse() * skinMesh.GlobalTransform;
            skinMesh.Skeleton = skinMesh.GetPathTo(disconnector);
            //skinMesh.GlobalTransform = disconnector.GlobalTransform * meshInOldSkel;
        }

        foreach (BoneAttachment3D attachment in disconnector.GetChildren().OfType<BoneAttachment3D>()) {
            BoneAttachment3D newAttachment = new() {
                Name = attachment.Name,
                BoneIdx = disconnector.FindBone(attachment.BoneName),
                BoneName = attachment.BoneName,
                UseExternalSkeleton = true,
                ExternalSkeleton = attachment.GetPathTo(disconnector),
            };
            attachment.ReplaceBy(newAttachment);
        }
    }


    // Receiver handles this
    private void MergeSkeletons(Skeleton3D connectingSkeleton, string partName, int selectedSocketIndex) {

        static void ConvertBoneToReceiver(Skeleton3D receiver, Skeleton3D connector, int receiverParentIndex, int connectorIndex, string partName) {
            string connectingBoneName = connector.GetBoneName(connectorIndex);
            int boneFromConnector = receiver.AddBone(connectingBoneName);

            receiver.SetBoneParent(boneFromConnector, receiverParentIndex);

            Transform3D connectingBoneGlobalRest = connector.GlobalTransform * connector.GetBoneGlobalRest(connectorIndex);
            Transform3D convertedConnectingBoneRest = receiver.GlobalTransform.AffineInverse() * connectingBoneGlobalRest;
            Transform3D parentRest = receiver.GetBoneGlobalRest(receiverParentIndex);
            Transform3D childLocalRest = parentRest.AffineInverse() * convertedConnectingBoneRest;
            receiver.SetBoneRest(boneFromConnector, childLocalRest);


            Transform3D connectingBoneGlobalPose = connector.GlobalTransform * connector.GetBoneGlobalPose(connectorIndex);
            Transform3D convertedConnectingBonePose = receiver.GlobalTransform.AffineInverse() * connectingBoneGlobalPose;
            Transform3D parentPose = receiver.GetBoneGlobalPose(receiverParentIndex);
            Transform3D childLocalPose = parentPose.AffineInverse() * convertedConnectingBonePose;
            receiver.SetBonePose(boneFromConnector, childLocalPose);

            // Create a new bone for the receiver that matches the connector in its own local space
            receiver.SetBoneMeta(boneFromConnector, "From" + partName, true);

            foreach (int childIndex in connector.GetBoneChildren(connectorIndex)) {
                ConvertBoneToReceiver(receiver, connector, boneFromConnector, childIndex, partName);
            }

        }

        // Move bones to receiver
        int[] boneIndices = connectingSkeleton.GetParentlessBones();
        Transform3D connectorToReceiver = this.skeleton.GlobalTransform.AffineInverse() * connectingSkeleton.GlobalTransform;

        // For now, should only execute once
        foreach (int rootBone in boneIndices) {
            ConvertBoneToReceiver(this.skeleton, connectingSkeleton, selectedSocketIndex, rootBone, partName);
        }
        //Print(SkeletonBoneTools.DumpSkeletonTree(this.skeleton));
        CallDeferred(nameof(SetConnectorChildrenOnConnect), this.skeleton, connectingSkeleton, selectedSocketIndex);
    }

    // Receiver handles this
    private void SplitSkeletons(Skeleton3D disconnectingSkeleton, string partName) {

        static void CopyBoneToNewSkeleton(Skeleton3D newSkeleton, Skeleton3D oldSkeleton, int parentIndex, int boneIndex, string partName) {
            // Skip bones marked from the part associated with the disconnecting skeleton
            if (oldSkeleton.HasBoneMeta(boneIndex, "From" + partName)) {
                return;
            }

            string childBoneName = oldSkeleton.GetBoneName(boneIndex);
            int newBone = newSkeleton.AddBone(childBoneName);

            if (parentIndex != -1) {
                string parentName = oldSkeleton.GetBoneName(parentIndex);
                int newParentIndex = newSkeleton.FindBone(parentName);
                newSkeleton.SetBoneParent(newBone, newParentIndex);
            }

            Transform3D childRest = oldSkeleton.GetBoneRest(boneIndex);
            newSkeleton.SetBoneRest(newBone, childRest);

            Transform3D childPose = oldSkeleton.GetBonePose(boneIndex);
            newSkeleton.SetBonePose(newBone, childPose);

            StringName[] metas = [.. oldSkeleton.GetBoneMetaList(boneIndex)];
            if (metas.Length > 0) {
                newSkeleton.SetBoneMeta(newBone, metas[0], oldSkeleton.GetBoneMeta(boneIndex, metas[0]));
            }

            foreach (int childIndex in oldSkeleton.GetBoneChildren(boneIndex)) {
                CopyBoneToNewSkeleton(newSkeleton, oldSkeleton, boneIndex, childIndex, partName);
            }

        }

        // Copy the receiver skeleton except for bones matching the disconnecting skeleton
        Skeleton3D newSkeleton = new();
        int[] boneIndices = this.skeleton.GetParentlessBones();
        foreach (int rootBone in boneIndices) {
            CopyBoneToNewSkeleton(newSkeleton, this.skeleton, -1, rootBone, partName);
        }

        Skeleton3D oldSkeleton = this.skeleton;
        newSkeleton.Transform = oldSkeleton.Transform;

        CallDeferred(nameof(SetConnectorChildrenOnDisconnect), this.skeleton, disconnectingSkeleton);

        this.AddChild(newSkeleton);
        this.skeleton = newSkeleton;
        CallDeferred(nameof(ReplaceSkeleton), oldSkeleton, newSkeleton);
    }
}
