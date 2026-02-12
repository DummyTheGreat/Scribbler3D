@tool
extends EditorScript
class_name AdjustNodeTree

var partScriptNames

func _scriptCheck(script) -> String:
	var className = script.resource_path.get_file().get_basename()
	if script is CSharpScript:
		return className
	return ""
	
func _isAlignmentPlane(node : Node, planeType : String) -> bool:
	var nodeScript = node.get_script()
	return nodeScript and nodeScript.resource_path.get_file()\
	.get_basename() == "AlignmentPlane" and node.name.contains(planeType)
	
func _validatePartChild(childNodeParent : Node, part : Node, scene : Node, urManager : EditorUndoRedoManager) -> void:
	for child in childNodeParent.get_children():
		var childScript = child.get_script()
		if childScript == null:
			return
		var childScriptName = self._scriptCheck(childScript)
		var childIsPart = (childScript and childScriptName in partScriptNames)
		if childIsPart:
			if child not in part.connectedParts:
				part.connectedParts.append(child)
			self._dive(child, part, scene, urManager)
	

func _dive(part : Node, parent : Node, scene : Node, urManager : EditorUndoRedoManager) -> void:
	var script = part.get_script()
	if script == null:
		return
	var scriptName = self._scriptCheck(script)
	var isPart = (script and scriptName in partScriptNames)
	if (isPart and parent != self.get_scene()):
		part.parentPart = part.get_path_to(parent)
		# At this point this is a child Part
		# Transform part here
		var nodeParent : Node3D = part.get_parent()
		
		var partName = part.get_meta("extras")["PartName"]
		# Find receiver plane
		var receiver = null
		for sibling in nodeParent.get_children():
			if self._isAlignmentPlane(sibling, "Receiver") and sibling.name.contains(partName):
				receiver = sibling
				break			
		
		# Find connector plane
		var connector = null
		if scriptName == "DeformingPart":
			for att in part.find_child("Skeleton3D", false).get_children()\
			.filter(func(x): return x is BoneAttachment3D):
				for child in att.get_children():
					if self._isAlignmentPlane(child, "Connector"):
						connector = child
						break
		else:
			for child in part.get_children():
				if self._isAlignmentPlane(child, "Connector"):
					connector = child
					break
				
		print(part.name)

		var newTransform = EditorTransform.calculate_join_transform(
			connector,
			receiver,
			part.global_transform,
			parent.global_transform,
			scriptName
		)
		#print(newTransform)
		part.global_transform = newTransform
	
	if isPart:
		if not scene.parts.has(part):
			scene.parts.append(part)
			
		part.connectedParts = []

		if scriptName == "DeformingPart":
			for attachment : BoneAttachment3D in part\
			.find_child("Skeleton3D", false)\
			.get_children()\
			.filter(func(x): return x is BoneAttachment3D):
				_validatePartChild(attachment, part, scene, urManager)
		else:
			_validatePartChild(part, part, scene, urManager)


func _run() -> void:
	self.partScriptNames = ["Part", "DeformingPart", "StaticPart"]
	var undoRedoManager : EditorUndoRedoManager = get_editor_interface().get_editor_undo_redo()
	var scene = self.get_scene()
	scene.parts = []
	var sceneScript = scene.get_script()
	var className = sceneScript.resource_path.get_file().get_basename()
	if (sceneScript and className == "Thing"):
		for node in scene.get_children():
			self._dive(node, scene, scene, undoRedoManager)

			
