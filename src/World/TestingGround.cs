using Godot;
using System;
using System.Linq;

public partial class TestingGround : WorldSpace
{
    public override void CreateThingChild(Resource thingData) {
        base.CreateThingChild(thingData);
        Thing thing = Thing.Create(thingData);
        this.AddChild(thing);
        thing.Assemble();
        // Animation stuff
        Part[] newChildren = [.. this.GetChildren().OfType<Part>().Where(x => x.thing == thing)];
        foreach (Part part in newChildren) {
            if (part.animationPlayer == null) {
                AnimationPlayer placeholder = new();
                part.AddChild(placeholder);
                part.animationPlayer = placeholder;
            }
        }
    }
}
