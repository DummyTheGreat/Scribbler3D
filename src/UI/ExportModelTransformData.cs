using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public partial class ExportModelTransformData : Button
{
    public class ModelTransform {
        public string Name { get; set; }
        public float[] Origin { get; set; }
        public float[][] Basis { get; set; }
    }

    private List<ModelTransform> transforms;
    private List<Thing> parts;

    public void AddParts(List<Thing> p) {
        this.parts.AddRange(p);
    }
    private void OnClick() {
        transforms = [];
        foreach (var part in this.parts) {
            float[] o = [
                part.GlobalTransform.Origin.X,
                part.GlobalTransform.Origin.Y,
                part.GlobalTransform.Origin.Z
                ];

            float[][] b = [
                    [
                        part.GlobalBasis.X.X,
                        part.GlobalBasis.Y.X,
                        part.GlobalBasis.Z.X,
                    ],
                    [
                        part.GlobalBasis.X.Y,
                        part.GlobalBasis.Y.Y,
                        part.GlobalBasis.Z.Y,
                    ],
                    [
                        part.GlobalBasis.X.Z,
                        part.GlobalBasis.Y.Z,
                        part.GlobalBasis.Z.Z,
                    ],
                ];

            ModelTransform t = new() {
                Name = part.Name,
                Origin = o,
                Basis = b,
            };

            transforms.Add(t);
        }

        JsonSerializerOptions options = new() { WriteIndented = true };
        Dictionary<string, object> wrapper = new() {
            ["objects"] = transforms
        };

        string jsonString = JsonSerializer.Serialize(wrapper, options);

        string dirVirtual = "user://transforms";
        string fileVirtual = "user://transforms/transforms.json";

        string dirPath = ProjectSettings.GlobalizePath(dirVirtual);
        string filePath = ProjectSettings.GlobalizePath(fileVirtual);

        Directory.CreateDirectory(dirPath);

        File.WriteAllText(filePath, jsonString);

        GD.Print($"Wrote {this.transforms.Count} transforms to:\n{filePath}");
    }

    public override void _Ready() {
        this.Pressed += OnClick;
        this.parts = [];
    }
}
