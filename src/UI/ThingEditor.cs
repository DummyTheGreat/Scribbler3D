using Godot;
using static Godot.GD;

using System;
using System.Collections.Generic;

public partial class ThingEditor : Control {
    private ItemList things;
    private Node3D worldRoot;
    private List<Part> partsForEdit;
    private Part selectedPart;
    private int highestSelectionIndex;

    /** 
     * Signal Function
     * 
     * Triggers on ItemList | MultiSelected
    **/
    private void ThingSelected(long index, bool selected) {
        Thing thing = Load<PackedScene>("src/Things/" + things.GetItemText((int)index) + ".tscn").Instantiate<Thing>();
        Print(thing.Name);
        worldRoot.AddChild(thing);

        partsForEdit.AddRange(thing.parts);

        foreach (Part part in thing.parts) {
            Print(part.Name);
            part.ToggleEditorMode();
            part.PartSelected += PartSelected;
        }
        PrintTreePretty();
    }


    // Signal Function Received from Parts in the editor
    private void PartSelected(Part part, int index) {

        //// Cancel click
        //if (index < 0 && this.selectedPart != part) {
        //    part.SetDrag(false);
        //    return;
        //}
        //// Unpressed mouse left
        //else if (index < 0 && this.selectedPart == part) {
        //    part.SetDrag(false);
        //    selectedPart = null;
        //}
        //// Press mouse left, no selected part
        //else if (this.selectedPart == null) {
        //    this.selectedPart = part;
        //    part.SetDrag(true);
        //}
        //// Multi click, highest z-index -> sibling index wins
        //else if (part.ZIndex > this.selectedPart.ZIndex || (part.ZIndex == this.selectedPart.ZIndex && part.siblingIndex < this.selectedPart.siblingIndex)) {
        //    Part old = this.selectedPart;
        //    this.selectedPart = part;
        //    this.selectedPart.SetDrag(true);
        //    old.CancelClick();
        //}
        //// Multi click, failed above condition, cancel
        //else {
        //    part.CancelClick();
        //}

    }

    public override void _Ready() {
        things = GetChild<ItemList>(0);
        worldRoot = GetChild<SubViewportContainer>(1).GetChild<SubViewport>(0).GetChild<Node3D>(0);
        things.MultiSelected += ThingSelected;
        highestSelectionIndex = 0;
        selectedPart = null;
        partsForEdit = [];
    }

    public override void _Process(double delta) {
        //foreach (Thing thing in selectedThings) {
        //    thing.Position
    }
}