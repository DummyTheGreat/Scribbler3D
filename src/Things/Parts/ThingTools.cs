using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class ThingTools : Node {

    // This is going to be called a lot so it should be optimized wherever possible
    public static Transform3D CalculateJoinTransform(AlignmentPlane connectingPlane, AlignmentPlane receivingPlane, Transform3D connectorGlobalPos) {

        Vector3 frontBWorld = receivingPlane.GlobalTransform * receivingPlane.GetFront();

        Basis recLocalFrame = receivingPlane.GetLocalFrame();
        Basis aLocalFrame = connectingPlane.GetLocalFrame();

        // Pure local dimension ratio — receiver world scale already lives in GlobalTransform.Basis
        Vector2 conDims = connectingPlane.GetDimensions();
        Vector2 recDims = receivingPlane.GetDimensions();
        float sx = (conDims.X > 1e-8f) ? (recDims.X / conDims.X) : 1f;
        float sz = (conDims.Y > 1e-8f) ? (recDims.Y / conDims.Y) : 1f;
        Basis S = new(
            new Vector3(sx, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, sz)
            );

        // receiver world basis * receiver local frame * flip * scale * inverse connector local frame
        // Reading right to left: rotate out of connector frame, scale, flip normal,
        // rotate into receiver local frame, apply receiver full world basis (includes bone scale)
        Basis desiredQuadABasisW = receivingPlane.GlobalTransform.Basis * recLocalFrame * S * aLocalFrame.Inverse();

        // Pin connector's front vertex to receiver's front vertex
        Vector3 desiredQuadAOriginW = frontBWorld - (desiredQuadABasisW * connectingPlane.GetFront());
        Transform3D desiredQuadAGlobal = new(desiredQuadABasisW, desiredQuadAOriginW);

        // Convert to Part-relative transform
        Transform3D quadInPart = connectorGlobalPos.AffineInverse() * connectingPlane.GlobalTransform;
        return desiredQuadAGlobal * quadInPart.AffineInverse();
    }

    private static void AddNewLibrary(AnimationLibrary newLib, Thing newLibOwner) {
        string newLibName =
            "Library" +
            newLibOwner.GetMeta("ThingName").AsString() +
            newLibOwner.GetMeta("PartName").AsString() +
            (newLibOwner.GetMeta("IsMirror").AsBool() ? "Mirror" : "") +
            "_V" + newLibOwner.GetMeta("PartVariant").AsString() +
            "_I" + newLibOwner.GetMeta("Instance").AsString();
        newLib.SetMeta("Instance", newLibOwner.GetMeta("Instance").AsInt32());
        newLibOwner.animationPlayer.AddAnimationLibrary(newLibName, newLib);
    }

    private static void AddNewAnimation(AnimationLibrary lib, Animation oldAnim, Animation newAnim, Thing newAnimOwner) {
        newAnim.LoopMode = Animation.LoopModeEnum.Linear;
        string animationGroup = oldAnim.GetMeta("AnimationGroup").AsString();
        string newAnimName =
            "Animation" +
            newAnimOwner.GetMeta("ThingName").AsString() +
            newAnimOwner.GetMeta("PartName").AsString() +
            (newAnimOwner.GetMeta("IsMirror").AsBool() ? "Mirror" : "") +
            animationGroup +
            "_V" + newAnimOwner.GetMeta("PartVariant").AsString() +
            "_I" + newAnimOwner.GetMeta("Instance").AsString();
        newAnim.SetMeta("AnimationGroup", animationGroup);
        newAnim.SetMeta("Instance", newAnimOwner.GetMeta("Instance").AsInt32());
        lib.AddAnimation(newAnimName, newAnim);
    }

    public static void MergeAnimations(Thing receiver, Thing connector) {
        while (connector.animationPlayer.GetAnimationLibraryList().Count > 0) {
            StringName conLibStr = connector.animationPlayer.GetAnimationLibraryList().First();
            AnimationLibrary conLib = connector.animationPlayer.GetAnimationLibrary(conLibStr);

            foreach (StringName conAnimStr in conLib.GetAnimationList()) {
                //StringName conAnimStr = conLib.GetAnimationList().First();
                Animation conAnim = conLib.GetAnimation(conAnimStr);
                string conAnimGroup = conAnim.GetMeta("AnimationGroup").AsString();
                bool match = false;

                // Match by animation group
                foreach (string recAnimStr in receiver.animationPlayer.GetAnimationList()) {
                    Animation recAnim = receiver.animationPlayer.GetAnimation(recAnimStr);
                    string recAnimGroup = recAnim.GetMeta("AnimationGroup").AsString();

                    if (conAnimGroup.Equals(recAnimGroup)) {
                        // Merge
                        Print("Animation Merge");
                        for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                            NodePath oldPath = conAnim.TrackGetPath(i);
                            string newPath = receiver.GetPathTo(connector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                            conAnim.TrackSetPath(i, newPath);
                            conAnim.CopyTrack(i, recAnim);
                            conAnim.TrackSetPath(i, oldPath);
                        }
                        match = true;
                        break;
                    }
                }

                // There is no matching animation group in the receiver's animation list so create create new receiver animation
                if (!match) {

                    Godot.Collections.Array<StringName> recLibs = receiver.animationPlayer.GetAnimationLibraryList();
                    AnimationLibrary recLib;

                    if (recLibs.Count > 1) { }
                    switch (recLibs.Count) {
                        case 0:
                            recLib = new();
                            AddNewLibrary(recLib, receiver);
                            break;
                        case 1:
                            recLib = receiver.animationPlayer.GetAnimationLibrary(recLibs.First());
                            break;
                        default:
                            Print("Why in the hell is there more than one library");
                            recLib = receiver.animationPlayer.GetAnimationLibrary(recLibs.First());
                            break;
                    }

                    Animation newRecAnim = new();
                    Print("New Animation");
                    // Copy tracks
                    for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                        NodePath oldPath = conAnim.TrackGetPath(i);
                        string newPath = receiver.GetPathTo(connector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                        conAnim.TrackSetPath(i, newPath);
                        conAnim.CopyTrack(i, newRecAnim);
                        conAnim.TrackSetPath(i, oldPath);
                    }

                    AddNewAnimation(recLib, conAnim, newRecAnim, receiver);
                }

                // All duplicated parts share the same library. Only delete animations if this is the last remaining copy referencing the library
                conLib.RemoveAnimation(conAnimStr);
            }

            connector.animationPlayer.RemoveAnimationLibrary(conLibStr);
        }

        //foreach (defConnector.animationPlayer.GetAnimationLibraryList()
        connector.RemoveChild(connector.animationPlayer);
        connector.animationPlayer.QueueFree();
        connector.animationPlayer = null;
    }

    public static void SplitAnimations(Thing receiver, Thing connector) {
        receiver.animationPlayer.Pause();
        AnimationPlayer connectorAnimator = new();
        connector.animationPlayer = connectorAnimator;
        foreach (string library in receiver.animationPlayer.GetAnimationLibraryList()) {
            AnimationLibrary receiverLib = receiver.animationPlayer.GetAnimationLibrary(library);
            AnimationLibrary newConLib = new();
            AddNewLibrary(newConLib, connector);

            List<string> conChildren = [connector.Name.ToString()];
            foreach (Thing p in connector.connectedThings) {
                conChildren.Add(p.Name.ToString());
            }

            foreach (string anim in receiverLib.GetAnimationList()) {
                Animation receiverAnim = receiverLib.GetAnimation(anim);
                Animation newConnectorAnim = new();
                int index = 0;
                while (index < receiverAnim.GetTrackCount()) {
                    NodePath trackPath = receiverAnim.TrackGetPath(index);
                    List<string> pathNames = [.. trackPath.GetConcatenatedNames().Split("/")];
                    bool removal = false;
                    foreach (string connectorName in conChildren) {
                        if (pathNames.Contains(connectorName)) {

                            // Fix path name for detached part
                            int partNameIndex = pathNames.IndexOf(connectorName);
                            string[] p = pathNames.Select((item, index) => new { Item = item, Index = index })
                                .Where(x => x.Index > partNameIndex)
                                .Select(x => x.Item)
                                .ToArray();
                            string newPathName = String.Join("/", p);
                            newPathName += ":" + trackPath.GetConcatenatedSubNames();

                            receiverAnim.TrackSetPath(index, newPathName);
                            receiverAnim.CopyTrack(index, newConnectorAnim);
                            Print("Animation added to: ", connectorName, " ", anim, " ", newPathName);
                            removal = true;
                            receiverAnim.RemoveTrack(index);
                            break;
                        }
                    }

                    if (removal == false) { index++; }

                }
                if (newConnectorAnim.GetTrackCount() > 0) {
                    AddNewAnimation(newConLib, receiverAnim, newConnectorAnim, connector);
                }
            }
        }
        connector.AddChild(connectorAnimator);
    }
}
