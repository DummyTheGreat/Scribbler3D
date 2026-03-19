using Godot;
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
            int amount = requirement.Get("amount").AsInt32();
            for (int i = 0; i < amount; i++) {
                PackedScene partPack = requirement.Get("partScene").As<PackedScene>();
                Part pScene = partPack.Instantiate<Part>();
                pScene.duplicateScene = partPack;
                Godot.Collections.Array<NodePath> pathArray = requirement.Get("receiver").AsGodotArray<NodePath>();
                if (pathArray.Count > 0) { pScene.pathToReceiver = pathArray[i]; }
                partList.Add(pScene);
            }
        }

        // Build the Thing according to matching connectors and receivers
        Part[] partArray = partList.ToArray();
        for (int i = 0; i < partArray.Length; i++) {
            if (partArray[i].pathToReceiver != null) {
                //Print(partArray[i].Name);
                for (int j = 0; j < partArray.Length; j++) {
                    AlignmentPlane receiver = partArray[j].GetNodeOrNull<AlignmentPlane>(partArray[i].pathToReceiver);
                    if (receiver != null) {
                        //Print(receiver.Name);
                        AlignmentPlane connector = FindConnector(partArray[i]);
                        string conOrientation = "";
                        string recOrientation = "";

                        if (connector.HasMeta("Orientation")) {
                            conOrientation = connector.GetMeta("Orientation").AsString();
                        }
                        if (receiver.HasMeta("Orientation")) {
                            recOrientation = receiver.GetMeta("Orientation").AsString();
                        }
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
                                oldPart.QueueFree();
                            }
                        }

                        Part childPart = partArray[i];
                        Part parentPart = partArray[j];
                        if (parentPart.IsInsideTree()) {
                            childPart.PreparePart(connector, receiver, parentPart, this, editor, null);
                        }
                        else {
                            parentPart.PartPrepared += () => childPart.PreparePart(connector, receiver, parentPart, this, editor, null);
                        }
                        break;
                    }
                }
            }
            else {
                partArray[i].PreparePart(null, null, parent, this, editor, null);
            }
        }
    }

    public override void _Ready() { }

    public override void _Process(double delta) { }
}
