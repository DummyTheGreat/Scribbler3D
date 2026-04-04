using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.Control;
using static Godot.GD;

public partial class Thing : Node3D
{
    public Thing[] ancestors;

    [Export]
    public Resource thingData;

    public enum State {
        Editor,
        Active
    }

    public State state;

    private Dictionary<StringName, List<Part>> partTracker;

    public void AddToTracker(Part part) {
        if (!this.partTracker.TryAdd(part.UID, [part])) {
            this.partTracker[part.UID].Add(part);
        }
    }

    public int GetTrackedCount(Part part) {
        this.partTracker.TryGetValue(part.UID, out List<Part> list);
        if (list == null) { return 0; }
        return list.Count;
    }

    public bool RemoveFromTracker(Part part) {
        this.partTracker.TryGetValue(part.UID, out List<Part> list);
        return list.Remove(part);
    }

    public static Thing Create(Resource thingData) {
        return new() { thingData = thingData };
    }

    public void Assemble(Node parent = null) {

        static AlignmentPlane FindConnector(Node node) {
            if (node is AlignmentPlane plane && plane.GetMeta("MeshType").AsString() == "Connector") {
                return plane;
            }
            AlignmentPlane connectorPlane = null;
            foreach (Node child in node.GetChildren()) {
                connectorPlane = FindConnector(child);
                if (connectorPlane != null) {
                    return connectorPlane;
                }
            }
            return null;
        }

        // Extract required parts from thing data resource
        List<Part> partList = [];
        Godot.Collections.Array<Resource> requirements = this.thingData.Get("parts").AsGodotArray<Resource>();
        foreach (Resource requirement in requirements) {
            Godot.Collections.Array<Resource> partdataArr = requirement.Get("partData").AsGodotArray<Resource>();
            foreach (Resource partdata in partdataArr) {
                PackedScene partPack = requirement.Get("partScene").As<PackedScene>();
                Part pScene = partPack.Instantiate<Part>();
                pScene.duplicateScene = partPack;
                pScene.CreateImportData(partdata);
                partList.Add(pScene);
            }
        }

        this.partTracker = [];

        // Build the Thing according to matching connectors and receivers
        Part[] partArray = [..partList];
        for (int i = 0; i < partArray.Length; i++) {
            if (!partArray[i].importData.receiverPath.IsEmpty) {
                for (int j = 0; j < partArray.Length; j++) {
                    AlignmentPlane receiver = partArray[j].GetNodeOrNull<AlignmentPlane>(partArray[i].importData.receiverPath);
                    if (receiver != null) {
                        AlignmentPlane connector = FindConnector(partArray[i]);
                        string conOrientation = connector.HasMeta("Orientation") ? connector.GetMeta("Orientation").AsString() : "";
                        string recOrientation = receiver.HasMeta("Orientation") ? receiver.GetMeta("Orientation").AsString() : "";

                        // Only mirror if 180 flip
                        if ((conOrientation == "Left" && recOrientation == "Right") || (conOrientation == "Right" && recOrientation == "Left") ||
                            (conOrientation == "Front" && recOrientation == "Rear") || (conOrientation == "Rear" && recOrientation == "Front") ||
                            (conOrientation == "Top" && recOrientation == "Bottom") || (conOrientation == "Bottom" && recOrientation == "Top")) {

                            Part oldPart = partArray[i];
                            if (partArray[i].HasMeta("Mirror")) {
                                PackedScene mirrorPartScene = partArray[i].GetMeta("Mirror").As<PackedScene>();
                                partArray[i] = mirrorPartScene.Instantiate<Part>();
                                partArray[i].duplicateScene = mirrorPartScene;
                                connector = FindConnector(partArray[i]);
                                partArray[i].CreateImportData(oldPart.importData);
                                oldPart.QueueFree();
                            }
                        }

                        Part childPart = partArray[i];
                        Part parentPart = partArray[j];
                        if (parentPart.IsInsideTree()) {
                            childPart.PreparePart(connector, receiver, parentPart, this);
                        }
                        else {
                            parentPart.PartPrepared += () => childPart.PreparePart(connector, receiver, parentPart, this);
                        }
                        break;
                    }
                }
            }
            else {
                partArray[i].PreparePart(null, null, parent ?? this, this);
            }
        }
    }

    public override void _Ready() { }

    public override void _Process(double delta) { }
}
