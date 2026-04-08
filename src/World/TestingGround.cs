using Godot;
using System;
using System.Linq;

public partial class TestingGround : WorldSpace
{
    public override void CreateThingChild(Resource thingData) {
        base.CreateThingChild(thingData);
        Thing thing = Thing.Create(thingData);
        thing.Assemble(this);
        // Animation stuff
        Thing[] newChildren = [.. this.GetChildren().OfType<Thing>().Where(x => x.importData.complexName == thing.importData.complexName)];
        foreach (Thing childThing in newChildren) {
            if (childThing.animationPlayer == null) {
                AnimationPlayer placeholder = new();
                childThing.AddChild(placeholder);
                childThing.animationPlayer = placeholder;
            }
        }
    }
}
