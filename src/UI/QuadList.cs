using Godot;
using System;

public partial class QuadList : HBoxContainer {
    private VBoxContainer list;
    private Button close;
    private VScrollBar scroll;

    private void Close() {
        this.QueueFree();
    }

    private void UpdateScroll(Node node) {

    }

    public override void _Ready() {
        this.list = this.GetChild<VBoxContainer>(0);
        this.list.ChildEnteredTree += UpdateScroll;
        this.close = this.GetChild(1).GetChild<Button>(0);
        this.close.Pressed += Close;
        this.scroll = this.GetChild(1).GetChild<VScrollBar>(1);
        this.scroll.MaxValue = 0f;
    }
}
