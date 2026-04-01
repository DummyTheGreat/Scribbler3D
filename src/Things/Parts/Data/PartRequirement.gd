extends Resource
class_name PartRequirement

@export var partScene : PackedScene = null
@export var partData : Array[PartData] = []

static func create(scene : PackedScene) -> PartRequirement:
	var newReq = PartRequirement.new()
	newReq.partScene = scene
	return newReq
