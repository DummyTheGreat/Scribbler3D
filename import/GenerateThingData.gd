extends Node
class_name GenerateThingData

static func addPartRequirement(parts : Array[PartRequirement], thingName : String, partName: String, path : NodePath) -> void:
	var fileName = thingName + "_" + partName + "_Model.glb"
	var thisScene : PackedScene = load("res://assets/Models/" + thingName + "/" + fileName)
	# Check if requirement for the part already exists
	var i = parts.find_custom(func(x : PartRequirement): return x.partScene == thisScene)
	if i != -1:
		# Prevent duplicate from reimport of same part
		if parts[i].receiver.any(func(x : NodePath): 
			return x.get_concatenated_names() == path.get_concatenated_names()) or parts[i].receiver.is_empty():
			return
			
		parts[i].amount += 1
		if not path.is_empty():
			parts[i].receiver.append(path)
	else:
		var pReq : PartRequirement = PartRequirement.create(thisScene)
		pReq.amount = 1
		if not path.is_empty():
			pReq.receiver.append(path)
		parts.append(pReq)

static func generate(scene : Node) -> void:
# Check if data already exists. Update if already existing. Create if not
	var thingName : String = scene.get_meta("ThingName")
	var partName : String = scene.get_meta("PartName")
	var data : ThingData
	var newParts : Array[PartRequirement] = []
	if FileAccess.file_exists("res://src/Things/Data/Resources/" + thingName + ".tres"):
		data = load("res://src/Things/Data/Resources/" + thingName + ".tres")
		newParts.append_array(data.parts)
	else:
		data = ThingData.new()
	# Add a part requirement for each receiver. If all planes
	# are receivers add self with 1 amount
	var hasConnector : bool = false
	for sceneChild in scene.get_children():
		if sceneChild is Skeleton3D:
			for skelChild in sceneChild.get_children():
				if skelChild is BoneAttachment3D:
					var planeMesh : MeshInstance3D = skelChild.get_child(0)
					if planeMesh.get_meta("MeshType") == "Receiver":
						addPartRequirement(
							newParts,
							planeMesh.get_meta("ThingName"),
							planeMesh.get_meta("PartName"),
							scene.get_path_to(planeMesh)
						)
					elif planeMesh.get_meta("MeshType") == "Connector":
						hasConnector = true
	
	if not hasConnector:
		addPartRequirement(newParts, thingName, partName, "")
	
	data.parts = newParts
	return ResourceSaver.save(data, "res://src/Things/Data/Resources/" + thingName + ".tres")
