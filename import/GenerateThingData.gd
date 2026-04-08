extends Node
class_name GenerateThingData

static func _scriptCheck(node : Node) -> String:
	var script = node.get_script()
	var className = script.resource_path.get_file().get_basename()
	if script is CSharpScript:
		return className
	return ""
	
static func _checkFolder(folderName : String) -> bool:
	var dir = DirAccess.open("res://src/Things/Data/Resources")
	if not dir.dir_exists(folderName):
		dir.make_dir(folderName)
		return false
	return true
	

static func createThingData(mesh : Node, nameChange : String = "") -> ThingSingletonData:
	if mesh == null:
		return null
	
	# Check if data already exists. Update if already existing. Create if not
	var thingName : String = mesh.get_meta("ThingName") if nameChange.is_empty() else nameChange
	var partName : String = mesh.get_meta("PartName")
	var orienation : String = mesh.get_meta("Orientation") if mesh.has_meta("Orientation") else ""
	var vari : String = "V" + str(mesh.get_meta("PartVariant")) if mesh.has_meta("PartVariant") else ""
	var instance : int = mesh.get_meta("Instance")
	
	var resFileName : String = thingName + partName + orienation + vari + "I" + str(instance)
	# data for the current part being imported
	var sceneFileName : String = mesh.get_meta("ThingName") + "_" + partName + "_Model.glb"
	if mesh.get_meta("MeshType") == "Part" and mesh.get_meta("IsMirror"):
		sceneFileName = "Mirrors/" + mesh.get_meta("ThingName") + "_" + partName + "_Mirror_Model.glb"
	 
	var thisScene : PackedScene = load("res://assets/Models/" + mesh.get_meta("ThingName") + "/" + sceneFileName)
	
	var data : ThingSingletonData
	var thisSkeleton : Skeleton3D
	if mesh.get_meta("MeshType") == "Part":
		var idx : int = mesh.get_children().find_custom(func(x : Node): return x is Skeleton3D)
		thisSkeleton = mesh.get_child(idx) if idx != -1 else null
	else:
		thisSkeleton = thisScene.instantiate().get_children().filter(func(x : Node): return x is Skeleton3D).front()
	
	var filePath : String = "res://src/Things/Data/Resources/" + thingName + "/" + resFileName + ".tres"
	if FileAccess.file_exists(filePath):
		data = load(filePath)
	else:
		data = ThingSingletonData.create(
			partName, 
			thingName, 
			thisScene, 
			thisSkeleton)
		data.fileName = resFileName
	return data


static func GenerateImport(scene : Node) -> void:
	var data = createThingData(scene)
	var connectedThings : Array[ThingSingletonData] = []
	# Add a part requirement for each receiver. If all planes
	# are receivers add self with 1 amount
	var sceneType = _scriptCheck(scene)
	for sceneChild in scene.get_children():
		if sceneType == "DeformingThing" and sceneChild is Skeleton3D:
			for skelChild in sceneChild.get_children():
				if skelChild is BoneAttachment3D:
					var planeMesh : MeshInstance3D = skelChild.get_child(0)
					if planeMesh.get_meta("MeshType") == "Receiver":
						var childData : ThingSingletonData = createThingData(planeMesh)
						childData.pathToReceiver = scene.get_path_to(planeMesh)
						_checkFolder(childData.complexName)
						ResourceSaver.save(childData, "res://src/Things/Data/Resources/" + childData.complexName + "/" + childData.fileName + ".tres")
						connectedThings.append(childData)
		elif sceneType == "StaticThing" and sceneChild.get_child_count() > 0:
			var planeMesh = sceneChild.get_child(0)
			if planeMesh.has_meta("MeshType") and planeMesh.get_meta("MeshType") == "Receiver":
				var childData = createThingData(planeMesh)
				childData.pathToReceiver = scene.get_path_to(planeMesh)
				_checkFolder(childData.complexName)
				ResourceSaver.save(childData, "res://src/Things/Data/Resources/" + childData.complexName + "/" + childData.fileName + ".tres")
				connectedThings.append(childData)
				
	data.connectedThings = connectedThings
	_checkFolder(data.complexName)
	return ResourceSaver.save(data, "res://src/Things/Data/Resources/" + data.complexName + "/" + data.fileName + ".tres")


static func TraversePartTree(thing : Node, thingName : String, path : NodePath) -> ThingSingletonData:
	var data : ThingSingletonData = createThingData(thing, thingName)
	data.pathToReceiver = path
	var connectedThings : Array[ThingSingletonData] = []

	var sceneType = _scriptCheck(thing)
	for sceneChild in thing.get_children():
		if sceneType == "DeformingThing" and sceneChild is Skeleton3D:
			for skelChild in sceneChild.get_children():
				if skelChild is BoneAttachment3D:
					var idx = skelChild.get_children().find_custom(func(x : Node): 
							return x.has_meta("MeshType") and x.get_meta("MeshType") == "Part")
					var childPart = skelChild.get_child(idx) if idx != -1 else null
					if childPart != null:
						var childData : ThingSingletonData = TraversePartTree(childPart, thingName, thing.get_path_to(skelChild.get_child(0)))
						connectedThings.append(childData)
		elif sceneType == "StaticThing":
			var idx = sceneChild.get_children().find_custom(func(x : Node): 
					return x.has_meta("MeshType") and x.get_meta("MeshType") == "Part")
			var childPart = sceneChild.get_child(idx) if idx != -1 else null
			if childPart != null:
				var childData : ThingSingletonData = TraversePartTree(childPart, thingName, thing.get_path_to(sceneChild.get_child(0)))
				connectedThings.append(childData)
			
	data.connectedThings = connectedThings
	ResourceSaver.save(data, "res://src/Things/Data/Resources/" + data.complexName + "/" + data.fileName + ".tres")
	return data

static func GenerateRuntime(thing : Node, newThingName : String):
	if _checkFolder(newThingName):
		print("Thing already exists")
		return
	TraversePartTree(thing, newThingName, ^"")
