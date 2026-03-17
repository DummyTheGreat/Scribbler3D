extends Resource
class_name PartRequirement

@export var partScene : PackedScene = null
@export var amount : int = 0
@export var receiver : Array[NodePath] = []

static func create(scene : PackedScene) -> PartRequirement:
	var newReq = PartRequirement.new()
	newReq.partScene = scene
	return newReq
