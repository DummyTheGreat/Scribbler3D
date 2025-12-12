using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static Godot.GD;

public partial class Thing : Node3D
{
    public List<Part> parts;
    public override void _Ready() {
        parts = [];
        // Get all top level parts (There should only be 1 usually but why not check)
        Stack<Part> stack = new([.. this.GetChild(0).GetChildren().ToList().OfType<Part>()]);

        while (stack.Count > 0) {
            Part part = stack.Pop();
            parts.Add(part);

            foreach (Node child in part.GetChildren()) {
                if (child is Part childPart) {
                    stack.Push(childPart);
                }
            }
        }
    }
}
