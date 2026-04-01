extends Resource
class_name ThingData

@export var name : String = ""
@export var parts : Array[PartRequirement] = []

static func create(n : String) -> ThingData:
	var newData = ThingData.new()
	newData.name = n
	return newData
