extends Resource
class_name PartData

@export var name : String = ""
@export var variant : int = 1
@export var instance : int = 1
@export var receiver : NodePath = ""
@export var boneData : Array[PartBoneData] = []

static func addBoneData(boneIdx : int, data : Array[PartBoneData], skeleton : Skeleton3D) -> void:
	var scale : Vector3 = skeleton.get_bone_pose_scale(boneIdx)
	print("bone scale: ", scale)
	data.append(PartBoneData.create(boneIdx, scale.x, scale.y, scale.z))
	for childIdx in skeleton.get_bone_children(boneIdx):
		addBoneData(childIdx, data, skeleton)


static func create(partName : String, path : NodePath, skeleton : Skeleton3D) -> PartData:
	var newData = PartData.new()
	newData.name = partName
	newData.receiver = path
	if (skeleton != null):
		var parentless : PackedInt32Array = skeleton.get_parentless_bones()
		for idx in parentless:
			
			addBoneData(idx, newData.boneData, skeleton)
		#newData.boneData.sort_custom(
			#func(a : PartBoneData, b : PartBoneData): 
				#if a.boneIndex < b.boneIndex
				#)
	return newData
