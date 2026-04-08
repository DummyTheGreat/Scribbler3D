extends Resource
class_name ThingSingletonData

# The state of a thing that exists on its own or is part of a whole. Can be
# part of a complex thing.

@export var singletonName : String = "" # Ex: A leg or arm
@export var variant : int = 1
@export var instance : int = 1
@export var boneData : Array[ThingBoneData] = []
@export var scene : PackedScene = null
@export var materialData : ThingMaterialData = null

@export var complexName : String = "" # Ex: Human
@export var connectedThings : Array[ThingSingletonData] = []
@export var pathToReceiver : NodePath = ""
@export var fileName : String = ""

@export var volume : float # This is determined either ahead of time or by taking the volume of the scene
@export var mass : float # Calculated by getting average density from materials and its volume

static func addBoneData(boneIdx : int, data : Array[ThingBoneData], skeleton : Skeleton3D) -> void:
	var scale : Vector3 = skeleton.get_bone_pose_scale(boneIdx)
	var bd = ThingBoneData.create(boneIdx, scale.x, scale.y, scale.z)
	data.append(bd)
	for childIdx in skeleton.get_bone_children(boneIdx):
		addBoneData(childIdx, data, skeleton)
		
static func create(
	name : String, 
	cName : String,
	sce : PackedScene,
	skeleton : Skeleton3D) -> ThingSingletonData:
	var newData = ThingSingletonData.new()
	newData.singletonName = name
	newData.complexName = cName
	newData.scene = sce
	
	if (skeleton != null):
		var parentless : PackedInt32Array = skeleton.get_parentless_bones()
		for idx in parentless:
			addBoneData(idx, newData.boneData, skeleton)
	return newData
