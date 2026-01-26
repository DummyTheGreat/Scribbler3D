using Godot;
using static Godot.GD;

using System;
using System.Collections.Generic;
using System.Linq;

public partial class ThingEditor : Control {
    private ItemList things;
    private Node3D worldRoot;
    private List<Part> partsForEdit;
    private Part selectedPart;
    private int highestSelectionIndex;
    private ExportModelTransformData exportButton;

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {
        Thing thing = Load<PackedScene>("src/Things/" + things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        worldRoot.AddChild(thing);

        partsForEdit.AddRange(thing.parts);
        this.exportButton.AddParts([.. thing.parts]);

        MeshInstance3D mesh;
        foreach (Part part in thing.parts) {
            if (part is StaticPart) {
                mesh = part.GetChildren().OfType<MeshInstance3D>().Where(x => !x.HasMeta("IsReceiver")).FirstOrDefault();
            }
            else {
                mesh = part.GetChildren().OfType<Skeleton3D>().FirstOrDefault().GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
            }
            part.ToggleEditorMode();
        }
    }

    public override void _Ready() {
        things = GetChild<ItemList>(0);
        worldRoot = GetChild<SubViewportContainer>(2).GetChild<SubViewport>(0).GetChild<Node3D>(0);
        things.MultiSelected += ThingSelected;
        highestSelectionIndex = 0;
        selectedPart = null;
        partsForEdit = [];
        exportButton = GetChild<ExportModelTransformData>(1);
    }

    public override void _Process(double delta) {
        //foreach (Thing thing in selectedThings) {
        //    thing.Position
    }
}