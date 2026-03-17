using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Godot.GD;

/*
 * Deforming Parts are parts that will have changes to their mesh's form through bone manipulation.
 * 
 * They possess a node hierarchy like such:
 * 
 * Root (DeformingPart)
 * ├── Bounds (MeshInstance3D)
 * ├── Skeleton (Skeleton3D)
 * ├───── SkinMesh (MeshInstance3D)
 * ├───── Connector/Receiver Bone Socket (BoneAttachment3D)
 * ├──────── Connector/Receiver (AlignmentPlane)
 * ├───── ...
 * ├── Child Part (Part)
 * ├── ...
 */
public partial class DeformingPart : Part {

    public Skeleton3D skeleton;

    public override void DoAnimation(StringName animationLibrary, StringName animation) {
        string name = animationLibrary + "/" + animation;
        if (this.animationPlayer.IsPlaying() && this.animationPlayer.CurrentAnimation.Equals(name)) {
            this.animationPlayer.Stop();
        }
        else {
            this.animationPlayer.Play(name);
        }
    }

    public override MeshInstance3D GetSkinMesh() {
        return this.skeleton.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
    }

    public override void MergeAnimations(Part connector) {
        string conLibName = connector.UID + "Lib";

        AnimationLibrary matchingLibrary = this.editor.GetPartAnimationLibrary(connector.UID, conLibName, connector);
        Print(connector.animationPlayer.GetAnimationList());
        if (connector is DeformingPart defConnector) {
            while (defConnector.animationPlayer.GetAnimationLibraryList().Count > 0) {
                StringName conLibStr = defConnector.animationPlayer.GetAnimationLibraryList().First();
                AnimationLibrary conLib = defConnector.animationPlayer.GetAnimationLibrary(conLibStr);
                Print("Library: ", conLibStr);
                Print("Animations: ", conLib.GetAnimationListSize());

                foreach (StringName conAnimStr in conLib.GetAnimationList()) {
                    //StringName conAnimStr = conLib.GetAnimationList().First();
                    Animation conAnim = conLib.GetAnimation(conAnimStr);
                    string conAnimGroup = conAnim.GetMeta("AnimationGroup").AsString();
                    bool match = false;
                    foreach (string recAnimStr in this.animationPlayer.GetAnimationList()) {
                        Animation recAnim = this.animationPlayer.GetAnimation(recAnimStr);
                        string recAnimGroup = recAnim.GetMeta("AnimationGroup").AsString();

                        if (conAnimGroup.Equals(recAnimGroup)) {
                            // Merge
                            Print("Animation Merge");
                            for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                                NodePath oldPath = conAnim.TrackGetPath(i);
                                string newPath = this.GetPathTo(defConnector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                                Print(newPath.ToString());
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

                        Godot.Collections.Array<StringName> recLibs = this.animationPlayer.GetAnimationLibraryList();
                        if (recLibs.Count > 1) { Print("Why in the hell is there more than one library"); }
                        AnimationLibrary recLib = this.animationPlayer.GetAnimationLibrary(recLibs.First());
                        Animation newRecAnim = new();
                        Print("New Animation");
                        // New
                        for (int i = 0; i < conAnim.GetTrackCount(); i++) {
                            NodePath oldPath = conAnim.TrackGetPath(i);
                            string newPath = this.GetPathTo(defConnector).GetConcatenatedNames() + "/" + conAnim.TrackGetPath(i).ToString();
                            Print(newPath.ToString());
                            conAnim.TrackSetPath(i, newPath);
                            conAnim.CopyTrack(i, newRecAnim);
                            conAnim.TrackSetPath(i, oldPath);
                        }

                        newRecAnim.LoopMode = Animation.LoopModeEnum.Linear;
                        newRecAnim.SetMeta("AnimationGroup", conAnim.GetMeta("AnimationGroup"));
                        recLib.AddAnimation(this.UID + conAnim.GetMeta("AnimationGroup"), newRecAnim);
                    }

                    // All duplicated parts share the same library. Only delete animations if this is the last remaining copy referencing the library
                    if (matchingLibrary == null) {
                        Print("Delete Animtion: ", conAnimStr);
                        conLib.RemoveAnimation(conAnimStr);
                    }

                    Print("Animation added to: ", this.Name);
                    Print(conAnimStr);
                }

                defConnector.animationPlayer.RemoveAnimationLibrary(conLibStr);
            }

            //foreach (defConnector.animationPlayer.GetAnimationLibraryList()
            defConnector.RemoveChild(defConnector.animationPlayer);
            defConnector.animationPlayer.QueueFree();
            defConnector.animationPlayer = null;
        }
    }

    // Receiver
    public override void AttachPart(Part connector) {
        base.AttachPart(connector);
        BoneAttachment3D receiverSocket = connector.activeCollider.GetBoundCollider().GetParentOrNull<BoneAttachment3D>();
        connector.Reparent(receiverSocket);
        MergeAnimations(connector);
    }

    // Receiver = this
    public override void DetachPart(Part connector) {
        if (connector is DeformingPart defConnector) {
            this.animationPlayer.Pause();
            AnimationPlayer connectorAnimator = new();
            foreach (string library in this.animationPlayer.GetAnimationLibraryList()) {
                AnimationLibrary receiverLib = this.animationPlayer.GetAnimationLibrary(library);
                AnimationLibrary tempConLib = new();
                string conLibName = connector.UID + "Lib";
                AnimationLibrary matchingLibrary = this.editor.GetPartAnimationLibrary(connector.UID, conLibName, connector);
                Print(conLibName);
                Print("Matching library: ", matchingLibrary);
                connectorAnimator.AddAnimationLibrary(conLibName, matchingLibrary != null ? matchingLibrary : tempConLib);

                List<string> conChildren = [defConnector.Name.ToString()];
                foreach (NodePath p in defConnector.connectedParts) {
                    conChildren.Add(defConnector.GetNode<Part>(p).Name.ToString());
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

                                if (matchingLibrary == null) {
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
                                }
                                removal = true;
                                receiverAnim.RemoveTrack(index);
                                break;
                            }
                        }

                        if (removal == false) { index++; }
 
                    }
                    if (newConnectorAnim.GetTrackCount() > 0 && matchingLibrary == null) {
                        string conAnimName = connector.UID + (string)receiverAnim.GetMeta("AnimationGroup");
                        newConnectorAnim.SetMeta("AnimationGroup", receiverAnim.GetMeta("AnimationGroup"));
                        connectorAnimator.GetAnimationLibrary(conLibName).AddAnimation(conAnimName, newConnectorAnim);
                    }
                }
            }
            defConnector.animationPlayer = connectorAnimator;
            defConnector.AddChild(connectorAnimator);
        }
        base.DetachPart(connector);
    }

    public override void Selected() {
        this.editor.AddPartSlidersToToolList(this.skeleton);
        base.Selected();
    }

    public override void Unselected() {
        this.editor.RemovePartSlidersFromToolList(this.skeleton);
        base.Unselected();
    }

    public override void StopSelected() {
        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(false);
            }
        }
        base.StopSelected();
    }

    public override void MoveSelected() {
        base.MoveSelected();

        foreach (Node node in this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren())) {
            if (node is PartCollider collider) {
                collider.ToggleAreaDetection(true);
            }
            this.activeCollider?.ToggleLinkVisibility(true);
        }
    }

    public override void AddPartCollider(PartCollider collider, MeshInstance3D quad) {
        BoneAttachment3D b = this.skeleton.GetChildren().OfType<BoneAttachment3D>().Where(x => x.GetChildren().Contains(quad)).FirstOrDefault();
        b.AddChild(collider);
    }

    public override void _Ready() {
        this.skeleton = this.GetChildren().OfType<Skeleton3D>().FirstOrDefault();
        this.bindingQuads = [..
            this.skeleton.GetChildren().OfType<BoneAttachment3D>().SelectMany(x => x.GetChildren()).OfType<AlignmentPlane>()
            ];
        base._Ready();
    }
}
