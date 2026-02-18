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
    public delegate void PartConnectEventHandler(PartCollider newCollider);

    [Signal]
    public delegate void PartDisconnectEventHandler();

    //private ColliderType colliderType;

    public AlignmentPlane plane;
    public Basis planeBasis;
    public Part associatedPart;

    private PartCollider boundCollider;
    private List<PartCollider> intersectingColliders;
    private MeshInstance3D link;
    private ImmediateMesh linkMesh;
    private Vector3[] vertices;
    private int frontIndex;
    private bool initPhase;


    public PartCollider GetBoundCollider() { return this.boundCollider; }
    public void SetBoundCollider(PartCollider newBound) { this.boundCollider = newBound; }
    public void SetPlane(AlignmentPlane p) {
        this.plane = p;
    }

    public void ToggleLinkVisibility(bool enable) {
        this.link.Visible = enable;
    }

    public void ToggleAreaDetection(bool enable) {
        
        if (initPhase) {
            this.AreaEntered -= InitFirstContact;
            initPhase = false;
        }

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
        if (externalArea is PartCollider collider && collider.GetBoundCollider() == null) {
            this.intersectingColliders.Add(collider);
            Print("Init Overlap", collider);
        }

        //if (this.intersectingColliders.Count > 0 && externalArea is PartCollider col) {
        //    this.AddChild(this.link);
        //    col.SetBoundCollider(this);
        //    this.SetBoundCollider(col);
        //    this.EmitSignal(SignalName.PartConnect, this, true);
        //}

    }

    private void HandleOverlap(Area3D externalArea) {
        // Ensure that the intersecting area is a PartCollider and is not already bound to another collider
        if (externalArea is PartCollider collider && collider.GetBoundCollider() == null) {
            this.intersectingColliders.Add(collider);
            Print("Overlap ", collider);
        }
    }

    private void HandleSeparation(Area3D externalArea) {
        if (externalArea is PartCollider collider && this.intersectingColliders.Contains(collider)) {
            this.intersectingColliders.Remove(collider);
            Print("Separation ", collider);
        }
    }

    public override void _Ready() {
        this.link = new MeshInstance3D {
            Visible = false,
            Name = "Link"
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
        Node root = this;
        while (root.GetParent() != null) {
            if (root.GetParent() is Part pp) {
                this.associatedPart = pp;
                break;
            }

            root = root.GetParent();
        }

        if (root.GetParent() == null) {
            PushError("Collider does not have associated part");
        }

        Print(this.associatedPart.depth);
        this.CollisionLayer = (uint)(Math.Pow(2, this.associatedPart.depth));
        this.CollisionMask = (uint)(this.associatedPart.depth > 0 ? Math.Pow(2, this.associatedPart.depth - 1) : 0);

        if (this.CollisionMask != 0) {
            initPhase = true;
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
            this.EmitSignal(SignalName.PartConnect, this);
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

