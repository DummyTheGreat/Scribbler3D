using Godot;
using System;
using System.Linq;
using static WorldRoot;

public partial class WorldSpace : Node
{
    public Thing selected;
    public virtual void CreateThingChild(Resource thingData) {
    }
}
