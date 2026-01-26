@tool
extends EditorScript
class_name AdjustNodeTree

func _scriptCheck(script) -> bool:
	var className = script.resource_path.get_file().get_basename()
	if script is CSharpScript:
		return className == "Part" or \
			className == "StaticPart" or \
			className == "DeformingPart"
	return false
	

func _dive(part : Node, parent : Node, scene : Node, urManager : EditorUndoRedoManager) -> void:
	var script = part.get_script()
	var isPart = (script and self._scriptCheck(script))
	if (isPart and parent != self.get_scene()):
		if part.parentPart == null:
			part.parentPart = part.get_path_to(parent)
		# At this point this is a child Part
		# Transform part here
		var nodeParent : Node3D = part.get_parent()
		# Find receiver plane
		var receiver = null
		for sibling in nodeParent.get_children():
			if sibling.get_script() and \
			sibling.get_script().resource_path\
			.get_file().get_basename() == "AlignmentPlane":
				receiver = sibling
				break
		
		# Find connector plane
		var connector = null
		for att in part.find_child("Skeleton3D", false).get_children()\
		.filter(func(x): return x is BoneAttachment3D):
			for child in att.get_children():
				var childScript = child.get_script()
				var childIsConnector = (childScript and \
				childScript.resource_path.get_file()\
				.get_basename() == "AlignmentPlane" and \
				child.name.contains("Connector"))
				if (childIsConnector):
					connector = child
					break
		#var temp : Node3D = Node3D.new()
		#temp.global_tran
		print(part.global_transform)
		var newTransform = EditorTransform.calculate_join_transform(
			connector,
			receiver,
			part.global_transform,
			parent.global_transform
		)
		part.global_transform = newTransform
	
	if (isPart):
		if not scene.parts.has(part):
			scene.parts.append(part)

		for attachment : BoneAttachment3D in part\
		.find_child("Skeleton3D", false)\
		.get_children()\
		.filter(func(x): return x is BoneAttachment3D):
			for child in attachment.get_children():
				var childScript = child.get_script()
				var childIsPart = (childScript and self._scriptCheck(childScript))
				if childIsPart:
					self._dive(child, part, scene, urManager)
			

func _run() -> void:
	var undoRedoManager : EditorUndoRedoManager = get_editor_interface().get_editor_undo_redo()
	var scene = self.get_scene()
	if scene.parts == null:
		scene.parts = []
	var sceneScript = scene.get_script()
	var className = sceneScript.resource_path.get_file().get_basename()
	if (sceneScript and className == "Thing"):
		for node in scene.get_children():
			self._dive(node, scene, scene, undoRedoManager)

			
