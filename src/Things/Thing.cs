using Godot;
using Godot.Collections;
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

    public static Thing Create(Resource thingData) {
        Thing thing = new();
        thing.thingData = thingData;
        return thing;
    }

    public void Assemble(Node parent, ThingEditor editor) {

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
                            childPart.PreparePart(connector, receiver, parentPart, this, editor);
                        }
                        else {
                            parentPart.PartPrepared += () => childPart.PreparePart(connector, receiver, parentPart, this, editor);
                        }
                        break;
                    }
                }
            }
            else {
                partArray[i].PreparePart(null, null, parent, this, editor);
            }
        }
    }

    public override void _Ready() { }

    public override void _Process(double delta) { }
}
