extends Node
class_name GenerateThingData

static func _scriptCheck(node : Node) -> String:
	var script = node.get_script()
	var className = script.resource_path.get_file().get_basename()
	if script is CSharpScript:
		return className
	return ""

static func addPartRequirement(mesh : Node, parts : Array[PartRequirement], thingName : String, partName: String, path : NodePath) -> void:
	var fileName = thingName + "_" + partName + "_Model.glb"
	var thisScene : PackedScene = load("res://assets/Models/" + thingName + "/" + fileName)
	var thisSkeleton : Skeleton3D
	if mesh.get_meta("MeshType") == "Part":
		var idx : int = mesh.get_children().find_custom(func(x : Node): return x is Skeleton3D)
		thisSkeleton = mesh.get_child(idx) if idx != -1 else null
	else:
		thisSkeleton = thisScene.instantiate().get_children().filter(func(x : Node): return x is Skeleton3D).front()
	# Check if requirement for the part already exists
	var i = parts.find_custom(func(x : PartRequirement): return x.partScene == thisScene)
	if i != -1:
		var existingPaths : Array = parts[i].partData.map(func(x : PartData): return x.receiver)
		# Prevent duplicate from reimport of same part
		if existingPaths.any(func(x : NodePath): 
			return x.get_concatenated_names() == path.get_concatenated_names()) or existingPaths.is_empty():
			return
			
		if not path.is_empty():
			parts[i].partData.append(PartData.create(partName, path, thisSkeleton))
	else:
		var pReq : PartRequirement = PartRequirement.create(thisScene)
		pReq.partData.append(PartData.create(partName, path, thisSkeleton))
		parts.append(pReq)

static func GenerateImport(scene : Node) -> void:
	# Check if data already exists. Update if already existing. Create if not
	var thingName : String = scene.get_meta("ThingName")
	var partName : String = scene.get_meta("PartName")
	var data : ThingData
	var newParts : Array[PartRequirement] = []
	if FileAccess.file_exists("res://src/Things/Data/Resources/" + thingName + ".tres"):
		data = load("res://src/Things/Data/Resources/" + thingName + ".tres")
		newParts.append_array(data.parts)
	else:
		data = ThingData.create(thingName)
	# Add a part requirement for each receiver. If all planes
	# are receivers add self with 1 amount
	var hasConnector : bool = false
	var sceneType = _scriptCheck(scene)
	for sceneChild in scene.get_children():
		if sceneType == "DeformingPart" and sceneChild is Skeleton3D:
			for skelChild in sceneChild.get_children():
				if skelChild is BoneAttachment3D:
					var planeMesh : MeshInstance3D = skelChild.get_child(0)
					if planeMesh.get_meta("MeshType") == "Receiver":
						addPartRequirement(
							planeMesh,
							newParts,
							planeMesh.get_meta("ThingName"),
							planeMesh.get_meta("PartName"),
							scene.get_path_to(planeMesh)
						)
					elif planeMesh.get_meta("MeshType") == "Connector":
						hasConnector = true
		elif sceneType == "StaticPart" and sceneChild.get_child_count() > 0:
			var plane = sceneChild.get_child(0)
			if plane.has_meta("MeshType") and plane.get_meta("MeshType") == "Receiver":
				addPartRequirement(
					plane,
					newParts,
					plane.get_meta("ThingName"),
					plane.get_meta("PartName"),
					scene.get_path_to(plane)
				)
			elif plane.has_meta("MeshType") and plane.get_meta("MeshType") == "Connector":
				hasConnector = true
	
	if not hasConnector:
		addPartRequirement(scene, newParts, thingName, partName, "")
	
	data.parts = newParts
	return ResourceSaver.save(data, "res://src/Things/Data/Resources/" + thingName + ".tres")


static func TraversePartTree(part : Node, partParent : Node, newParts : Array[PartRequirement], rp : Node):
	if part == null:
		return
	# Create requirement from current part's base info
	addPartRequirement(
		part,
		newParts,
		part.get_meta("ThingName"),
		part.get_meta("PartName"),
		partParent.get_path_to(rp) if rp != null else ^""
	)
	# Traverse through node tree
	# Different technique for part types
	var sceneType = _scriptCheck(part)
	for sceneChild in part.get_children():
		if sceneType == "DeformingPart" and sceneChild is Skeleton3D:
			for skelChild in sceneChild.get_children():
				if skelChild is BoneAttachment3D:
					var idx = skelChild.get_children().find_custom(func(x : Node): 
							return x.has_meta("MeshType") and x.get_meta("MeshType") == "Part")
					var childPart = skelChild.get_child(idx) if idx != -1 else null
					TraversePartTree(childPart, part, newParts, skelChild.get_child(0))
		elif sceneType == "StaticPart":
			var idx = sceneChild.get_children().find_custom(func(x : Node): 
					return x.has_meta("MeshType") and x.get_meta("MeshType") == "Part")
			var childPart = sceneChild.get_child(idx) if idx != -1 else null
			TraversePartTree(childPart, part, newParts, sceneChild.get_child(0))


static func GenerateRuntime(part : Node, newThingName : String):
	var data : ThingData
	var newParts : Array[PartRequirement] = []
	if FileAccess.file_exists("res://src/Things/Data/Resources/" + newThingName + ".tres"):
		print("Thing already exists")
		return
	data = ThingData.create(newThingName)
	TraversePartTree(part, null, newParts, null)
	data.parts = newParts
	ResourceSaver.save(data, "res://src/Things/Data/Resources/" + newThingName + ".tres")
