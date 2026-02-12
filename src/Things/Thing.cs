using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class Thing : Node3D
{
    [Export]
    public Godot.Collections.Array<Part> parts;
    public Thing[] ancestors;
    public override void _Ready() {

    }

    public override void _Process(double delta) {
        //AnimationPlayer p = this.GetChildren().OfType<AnimationPlayer>().FirstOrDefault();
        //p.GetAnimationLibrary()
        //p?.Play("LegWiggler");
    }
}
