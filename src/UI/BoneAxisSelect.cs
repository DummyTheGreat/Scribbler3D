using Godot;
using System;
using System.Linq;

public partial class BoneAxisSelect : HBoxContainer
{
    private Button x;
    private Button y;
    private Button z;

    private void ToggleOthersOff(bool toggle, Button b) {
        if (toggle) {
            foreach (Button button in this.GetChildren().OfType<Button>()) {
                if (button != b && button.ButtonPressed) {
                    button.SetPressedNoSignal(false);
                }
            }
        }
        else {
            b.SetPressedNoSignal(true);
        }
    }

    public override void _Ready() {
        this.x = this.GetChild<Button>(0);
        this.y = this.GetChild<Button>(1);
        this.z = this.GetChild<Button>(2);

        this.x.Toggled += (toggle) => ToggleOthersOff(toggle, this.x);
        this.y.Toggled += (toggle) => ToggleOthersOff(toggle, this.y);
        this.z.Toggled += (toggle) => ToggleOthersOff(toggle, this.z);

    }
}
