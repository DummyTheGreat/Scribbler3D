extends Node3D
class_name AlignmentPlaneGD

@onready var vertices : PackedVector3Array
@onready var vertexIndices : PackedInt32Array
var plane : MeshInstance3D
var localNormal : Vector3
var localFrame : Basis
var centroid : Vector3
var front : Vector3
var dimensions : Vector2

func _init(p : MeshInstance3D) -> void:
	var arrays : Array = p.mesh.surface_get_arrays(0);
	self.vertices = arrays[Mesh.ArrayType.ARRAY_VERTEX]
	self.vertexIndices = arrays[Mesh.ArrayType.ARRAY_INDEX]
	self.plane = p
	ComputeLocalNormalFromSurface();
	ComputeLocalBasis();
	ComputeMaxDimensionExtents();

static func Centroid(v : Array[Vector3]) -> Vector3:
	var sum : Vector3 = Vector3.ZERO
	for p in v: sum += p
	return sum / v.size()
	
static func ProjectOntoPlane(v : Vector3, n : Vector3): return v - n * n.dot(v)

func GetLocalNormal() -> Vector3: return self.localNormal
func GetLocalFrame() -> Basis: return self.localFrame
func GetCentroid() -> Vector3: return self.centroid
func GetFront() -> Vector3: return self.front
func GetDimensions() -> Vector2: return self.dimensions

func FlipNormal() -> void:
	self.localNormal = -self.localNormal
	ComputeLocalBasis()
	ComputeMaxDimensionExtents()

func ComputeFrontmostIndex() -> int:
	var globalVertices : Array[Vector3] = []
	for i in range(self.vertices.size()):
		globalVertices.append(self.plane.global_transform * self.vertices[i])
		
	var lowestX : float = INF
	var frontIndex : int = 0
	
	for i in range(globalVertices.size()):
		var isTieX : bool = is_equal_approx(globalVertices[i].x, lowestX)
		if ((isTieX && globalVertices[i].y > globalVertices[frontIndex].y) || \
		(not isTieX && globalVertices[i].x < lowestX)):
			lowestX = globalVertices[i].x
			frontIndex = i
			
	return frontIndex
	
func ComputeLocalNormalFromSurface() -> void:
	var i0 = self.vertexIndices[0]
	var i1 = self.vertexIndices[1]
	var i2 = self.vertexIndices[2]
	
	self.localNormal = (self.vertices[i1] - self.vertices[i0])\
	.cross(self.vertices[i2] - self.vertices[i0]).normalized()
	
func ComputeLocalBasis() -> void:
	self.centroid = Centroid(self.vertices)
	self.front = self.vertices[self.ComputeFrontmostIndex()]
	
	var up : Vector3 = self.GetLocalNormal().normalized()
	var forward : Vector3 = self.ProjectOntoPlane(self.front - self.centroid, up).normalized()
	var right : Vector3 = up.cross(forward).normalized()
	
	self.localFrame = Basis(right, up, forward)

func ComputeMaxDimensionExtents() -> void:
	var minX : float = INF
	var maxX : float = -INF
	var minZ : float = INF
	var maxZ : float = -INF
	
	for i in range(self.vertices.size()):
		var d : Vector3 = self.vertices[i] - self.centroid
		var x : float = d.dot(self.localFrame.x)
		var z : float = d.dot(self.localFrame.z)
		
		if x < minX: minX = x
		if x > maxX: maxX = x
		if z < minZ: minZ = z
		if z > maxZ: maxZ = z
		
	self.dimensions = Vector2(maxX - minX, maxZ - minZ)
	

func _ready() -> void:
	pass
