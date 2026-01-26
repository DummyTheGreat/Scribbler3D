extends Node
class_name EditorTransform

static func validate_normal_direction(
	plane : MeshInstance3D, 
	alignment : AlignmentPlaneGD, 
	partGlobalTransform : Transform3D):
	var quadCentroidWorld : Vector3 = plane.global_transform * alignment.GetCentroid()
	var partCenterWorld : Vector3 = partGlobalTransform.origin
	var partOutWorld : Vector3 = (quadCentroidWorld - partCenterWorld).normalized()
	
	var normalMatrix : Basis = plane.global_transform.basis.inverse().transposed()
	var planeNormal : Vector3 = (normalMatrix * alignment.GetLocalNormal()).normalized()
	
	if planeNormal.dot(partOutWorld) < 0.0:
		alignment.FlipNormal()

static func calculate_join_transform(
	connecting_plane: MeshInstance3D,
	receiving_plane: MeshInstance3D,
	connector_part_global: Transform3D, 
	receiver_part_global : Transform3D) -> Transform3D:
	# Local helper: project vector v onto plane whose normal is n (assumes n is normalized or close to it)
	var project_onto_plane := func(v: Vector3, n: Vector3) -> Vector3:
		return v - n * n.dot(v)
	
	var planeA : AlignmentPlaneGD = AlignmentPlaneGD.new(connecting_plane)
	validate_normal_direction(connecting_plane, planeA, connector_part_global)
	var planeB : AlignmentPlaneGD = AlignmentPlaneGD.new(receiving_plane)
	validate_normal_direction(receiving_plane, planeB, receiver_part_global)

	var center_b_world: Vector3 = receiving_plane.global_transform * planeB.GetCentroid()
	var front_b_world: Vector3  = receiving_plane.global_transform * planeB.GetFront()
	var nm_b: Basis = receiving_plane.global_transform.basis.inverse().transposed()
	var normal_b_world: Vector3 = (nm_b * planeB.GetLocalNormal()).normalized()

	# Plane A should face plane B -> A's "up" should point opposite B's normal
	var up_b_world: Vector3 = (-normal_b_world).normalized()
	var forward_b_world: Vector3 = project_onto_plane.call(front_b_world - center_b_world, normal_b_world).normalized()
	var right_b_world: Vector3 = up_b_world.cross(forward_b_world).normalized()
	
	var target_basis_w := Basis(right_b_world, up_b_world, forward_b_world)

	# Local frame of plane A
	var a_local_frame: Basis = planeA.GetLocalFrame()

	# Scale factors (in-plane only)
	var dims_a: Vector2 = planeA.GetDimensions()
	var dims_b: Vector2 = planeB.GetDimensions()

	var sx: float = (abs(dims_a.x) > 1e-8) if (dims_b.x / dims_a.x) else 1.0
	var sz: float = (abs(dims_a.y) > 1e-8) if (dims_b.y / dims_a.y) else 1.0

	var s := Basis(
		Vector3(sx, 0.0, 0.0),
		Vector3(0.0, 1.0, 0.0),
		Vector3(0.0, 0.0, sz)
	)

	var desired_quad_a_basis_w: Basis = target_basis_w * s * a_local_frame.inverse()
	
	# Match front vertices and origins together for positional correctness
	var desired_quad_a_origin_w: Vector3 = front_b_world - (desired_quad_a_basis_w * planeA.GetFront())
	var desired_quad_a_global := Transform3D(desired_quad_a_basis_w, desired_quad_a_origin_w)

	# Convert from the quad's desired global transform to the *part* transform
	# so the part moves correctly (same as your C# comment)
	var quad_in_part: Transform3D = connector_part_global.affine_inverse() * connecting_plane.global_transform
	return desired_quad_a_global * quad_in_part.affine_inverse()
