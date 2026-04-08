extends Resource
class_name ThingBoneData

@export var boneIndex : int = 0
@export var xScale : float = 1.0
@export var yScale : float = 1.0
@export var zScale : float = 1.0

static func create(index : int, x : float, y : float, z : float) -> ThingBoneData:
	var data = ThingBoneData.new()
	data.boneIndex = index
	data.xScale = x
	data.yScale = y
	data.zScale = z
	return data
