using Godot;
using static Godot.GD;

using System;
using System.Collections.Generic;

public partial class PartCollider : Area3D {
    //public enum ColliderType {
    //    Detector,
    //    Reciever
    //}

    [Signal]
    public delegate void PartConnectEventHandler(PartCollider newCollider, bool init);

    [Signal]
    public delegate void PartDisconnectEventHandler();

    //private ColliderType colliderType;

    public MeshInstance3D plane;

    private PartCollider boundCollider;
    private List<PartCollider> intersectingColliders;
    private MeshInstance3D link;
    private ImmediateMesh linkMesh;
    private Vector3 localNormal;
    private Vector3[] vertices;
    private int frontIndex;

    public void ComputeLocalNormalFromSurface(int surface) {
        var arrays = this.plane.Mesh.SurfaceGetArrays(surface);
        var verts = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        var indices = (int[])arrays[(int)Mesh.ArrayType.Index];

        // Take the first triangle
        int i0 = indices[0];
        int i1 = indices[1];
        int i2 = indices[2];

        this.localNormal = (verts[i1] - verts[i0]).Cross(verts[i2] - verts[i0]).Normalized();
    }

    public int GetFrontIndex() { return this.frontIndex; }
    public void SetFrontIndex(int i) { this.frontIndex = i; }
    public Vector3[] GetVertices() { return this.vertices; }
    public void SetVertices(Vector3[] v) { this.vertices = v; }
    public Vector3 GetLocalNormal() { return this.localNormal; }
    public void SetLocalNormal(Vector3 v) { this.localNormal = v; }
    public PartCollider GetBoundCollider() { return this.boundCollider; }
    public void SetBoundCollider(PartCollider newBound) { this.boundCollider = newBound; }
    public Part GetAssociatedPart() { return this.GetParentOrNull<Part>(); }

    public void ToggleLinkVisibility(bool enable) {
        this.link.Visible = enable;
    }

    public void ToggleAreaDetection(bool enable) {
        if (enable) {
            this.AreaEntered += HandleOverlap;
            this.AreaExited += HandleSeparation;
        }
        else {
            this.AreaEntered -= HandleOverlap;
            this.AreaExited -= HandleSeparation;
        }
    }

    private void InitFirstContact(Area3D externalArea) {
        HandleOverlap(externalArea);
        this.AreaEntered -= InitFirstContact;

        if (this.intersectingColliders.Count > 0 && externalArea is PartCollider collider) {
            this.AddChild(link);
            collider.SetBoundCollider(this);
            this.SetBoundCollider(collider);
            this.EmitSignal(SignalName.PartConnect, this, true);
        }

    }

    private void HandleOverlap(Area3D externalArea) {
        // Ensure that the intersecting area is a PartCollider and is not already bound to another collider
        if (externalArea is PartCollider collider && collider.GetBoundCollider() == null) {
            this.intersectingColliders.Add(collider);
        }
    }

    private void HandleSeparation(Area3D externalArea) {
        if (externalArea is PartCollider collider && this.intersectingColliders.Contains(collider)) {
            this.intersectingColliders.Remove(collider);
        }
    }

    public override void _Ready() {
        this.link = new MeshInstance3D {
            Visible = false
        };
        linkMesh = new ImmediateMesh();
        this.link.Mesh = linkMesh;
        linkMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        linkMesh.SurfaceSetNormal(Vector3.Zero);
        linkMesh.SurfaceSetUV(Vector2.Zero);
        linkMesh.SurfaceAddVertex(Vector3.Zero);
        linkMesh.SurfaceAddVertex(Vector3.Zero);
        linkMesh.SurfaceEnd();

        this.intersectingColliders = [];

        int depth = 0; Node root = this;
        while (root.GetParent() is Part) { depth++; root = root.GetParent(); }

        this.CollisionLayer = (uint)(Math.Pow(2, depth));
        this.CollisionMask = (uint)(depth > 1 ? Math.Pow(2, depth - 1) : 0);

        if (this.CollisionMask != 0) {
            this.AreaEntered += InitFirstContact;
        }
    }

    public override void _Process(double delta) {

        if (this.CollisionMask == 0) {
            return;
        }

        // Change to Signal --
        PartCollider closestCollider = null;
        if (this.intersectingColliders.Count > 1) {
            float shortestDistance = float.MaxValue;
            for (int i = 0; i < this.intersectingColliders.Count; i++) {

                PartCollider externalCollider = this.intersectingColliders[i];
                float distance = externalCollider.GlobalPosition.DistanceTo(this.GlobalPosition);

                if (distance < shortestDistance) {
                    closestCollider = externalCollider;
                    shortestDistance = distance;
                }
            }
        }
        else if (this.intersectingColliders.Count == 1) {
            closestCollider = this.intersectingColliders[0];
        }

        // -- End
        if (closestCollider == null && this.boundCollider != null) {
            // BoundArea collider -> null
            this.boundCollider.SetBoundCollider(null);
            this.SetBoundCollider(null);

            this.ToggleLinkVisibility(false);
            this.EmitSignal(SignalName.PartDisconnect);
            this.RemoveChild(link);
        }
        else if (closestCollider != this.boundCollider) {
            // BoundArea null -> collider
            if (this.boundCollider == null) {
                this.ToggleLinkVisibility(true);
                this.AddChild(link);
            }
            // Disconnect the collider that was previously bound to THIS collider by unassociating it from THIS collider
            this.boundCollider?.SetBoundCollider(null);

            // BoundArea collider -> collider
            closestCollider.SetBoundCollider(this);
            this.SetBoundCollider(closestCollider);
            this.EmitSignal(SignalName.PartConnect, this, false);
        }

        if (this.boundCollider != null) {
            linkMesh.ClearSurfaces();
            linkMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            //linkMesh.SurfaceSetNormal(Vector3.Zero);
            //linkMesh.SurfaceSetUV(Vector2.Zero);
            linkMesh.SurfaceAddVertex(Vector3.Zero);
            linkMesh.SurfaceAddVertex(ToLocal(this.boundCollider.GlobalPosition));
            linkMesh.SurfaceEnd();
        }
    }
}

