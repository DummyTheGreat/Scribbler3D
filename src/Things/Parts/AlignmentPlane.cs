using Godot;
using static Godot.GD;
using System;
using System.Linq;

public partial class AlignmentPlane : MeshInstance3D {
    private Vector3[] vertices;
    private int[] vertexIndices;
    private Vector3 localNormal;
    private Basis localFrame;
    private Vector3 centroid;
    private Node3D frontMarker;
    private Vector3 front;
    private Vector2 dimensions;
    private int frontIndex;

    private Vector3 old;

    static Vector3 Centroid(Vector3[] v) {
        Vector3 sum = Vector3.Zero;
        foreach (var p in v) sum += p;
        return sum / v.Length;
    }

    public Vector3 GetLocalNormal() { return this.localNormal; }
    public Basis GetLocalFrame() { return this.localFrame; }
    public Vector3 GetCentroid() { return this.centroid; }
    public Vector3 GetFront() { return this.front; }
    public Vector2 GetDimensions() { return this.dimensions; }

    public void ShiftFrontToNext(float degrees) {

        Basis normalMatrix = this.GlobalTransform.Basis.Inverse().Transposed();
        Vector3 globalNormal = (normalMatrix * this.localNormal).Normalized();

        float angle = Mathf.DegToRad(degrees);
        Vector3 globalCentroid = this.GlobalTransform * this.centroid;
        Transform3D t = this.frontMarker.GlobalTransform;
        t.Origin -= globalCentroid;
        t = t.Rotated(globalNormal, angle);
        t.Origin += globalCentroid;
        // This should only affect position
        this.frontMarker.GlobalTransform = t;

        ComputeLocalBasis();
        ComputeMaxDimensionExtents();

    }

    public void FlipNormal() { 
        this.localNormal = -this.localNormal;
        ComputeLocalBasis();
        ComputeMaxDimensionExtents();
    }

    private int ComputeFrontmostIndex() {

        Vector3[] globalVertices = new Vector3[this.vertices.Length];
        for (int i = 0; i < this.vertices.Length; i++)
            globalVertices[i] = this.GlobalTransform * this.vertices[i];

        Vector3 globalCentroid = this.GlobalTransform * this.centroid;

        int frontIndex = 0;
        float maxDot = float.MinValue;
        for (int i = 0; i < globalVertices.Length; i++) {
            Vector3 vertex = (globalVertices[i] - globalCentroid).Normalized();
            Vector3 marker = (this.frontMarker.GlobalPosition - globalCentroid).Normalized();
            float calc = vertex.Dot(marker);
            if (calc > maxDot) {
                maxDot = calc;
                frontIndex = i;
            }
        }
        this.frontIndex = frontIndex;
        return frontIndex;
    }

    private void ComputeLocalNormalFromSurface() {
        // First triangle
        int i0 = this.vertexIndices[0];
        int i1 = this.vertexIndices[1];
        int i2 = this.vertexIndices[2];

        this.localNormal = (this.vertices[i1] - this.vertices[i0]).Cross(this.vertices[i2] - this.vertices[i0]).Normalized();
    }

    // Basically find the connecting part's local basis (relative to normal) so that its inverse can be used to calculate the proper 
    // rotational destination of the part
    private void ComputeLocalBasis() {

        static Vector3 ProjectOntoPlane(Vector3 v, Vector3 n) => v - n * n.Dot(v);

        this.centroid = Centroid(this.vertices);
        this.front = this.vertices[this.ComputeFrontmostIndex()];

        Vector3 up = this.GetLocalNormal().Normalized();
        Vector3 forward = ProjectOntoPlane(this.front - this.centroid, up).Normalized();
        Vector3 right = up.Cross(forward).Normalized();

        this.localFrame = new(right, up, forward);
    }

    // Get the max dimensions (extents) of a set of vertices that are equal along the y-axis. Used primarily for scale conversions
    private void ComputeMaxDimensionExtents() {
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

        for (int i = 0; i < this.vertices.Length; i++) {
            Vector3 d = this.vertices[i] - this.centroid;
            float x = d.Dot(this.localFrame.X);
            float z = d.Dot(-this.localFrame.Z);

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        this.dimensions = new Vector2(maxX - minX, maxZ - minZ);
    }

    public override void _Ready() {
        Godot.Collections.Array arrays = this.Mesh.SurfaceGetArrays(0);
        this.vertices = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        this.vertexIndices = (int[])arrays[(int)Mesh.ArrayType.Index];
        this.frontMarker = this.GetChild<Node3D>(0);
        ComputeLocalNormalFromSurface();
        ComputeLocalBasis();
        ComputeMaxDimensionExtents();
    }
}
