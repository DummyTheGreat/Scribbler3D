using Godot;
using System;

public partial class AlignmentPlane : MeshInstance3D
{
    private Vector3[] vertices;
    private int[] vertexIndices;
    private Vector3 localNormal;
    private Basis localFrame;
    private Vector3 centroid;
    private Vector3 front;
    private Vector2 dimensions;

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

    public void FlipNormal() { 
        this.localNormal = -this.localNormal;
        ComputeLocalBasis();
        ComputeMaxDimensionExtents();
    }

    private int ComputeFrontmostIndex() {
        Vector3[] globalVertices = new Vector3[this.vertices.Length];
        for (int i = 0; i < this.vertices.Length; i++)
            globalVertices[i] = this.GlobalTransform * this.vertices[i];

        float lowestX = float.MaxValue;
        int frontIndex = 0;

        for (int i = 0; i < globalVertices.Length; i++) {
            bool isTieX = Mathf.IsEqualApprox(globalVertices[i].X, lowestX);
            if ((isTieX && globalVertices[i].Y > globalVertices[frontIndex].Y) ||
                (!isTieX && globalVertices[i].X < lowestX)) {
                lowestX = globalVertices[i].X;
                frontIndex = i;
            }
        }
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
        Vector3 forward = ProjectOntoPlane(front - this.centroid, up).Normalized();
        //if (fA_L.LengthSquared() < 1e-10f) fA_L = Vector3.Right;
        //fA_L = fA_L.Normalized();

        //forward = ProjectOntoPlane(forward, up).Normalized(); // <-- is this necessary? no?
        Vector3 right = up.Cross(forward).Normalized();
        //if (rA_L.LengthSquared() < 1e-10f) {
        //    fA_L = ProjectOntoPlane(Vector3.Forward, upA_L).Normalized();
        //    rA_L = upA_L.Cross(fA_L).Normalized();
        //}
        //fA_L = rA_L.Cross(upA_L).Normalized();

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
        ComputeLocalNormalFromSurface();
        ComputeLocalBasis();
        ComputeMaxDimensionExtents();
    }
}
