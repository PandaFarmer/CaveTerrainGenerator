extends MeshInstance3D

@onready var point_view := $"../PointView";
var surface_tool := SurfaceTool.new();


func _ready():
	surface_tool.begin(Mesh.PRIMITIVE_TRIANGLES);
	surface_tool.set_color(Colors.BLUE_B);
	surface_tool.set_smooth_group(-1)
	create_surface_mesh();
	mesh = surface_tool.commit();

const CENTER := Vector3.ZERO;
const RADIUS: float = 5.0;	
const HEIGHT: float = 10.0;

const a : Vector3 = Vector3(30, -20, 0);
const b : Vector3 = Vector3(-30, 20, 0);

func get_sample_value(index: Vector3i) -> float:	

	var q = Vector3(index.x, index.y, index.z) - Vector3(1.0,0.0,1.0);
	var d = .00001
	d = min( d, sdRhombus(opRevolution(q, 0.15), Vector2(0.5,0.15)) );
	return d;

func opRevolution(p : Vector3, w: float ) -> Vector2:
	#var p = Vector3(index.x, index.y, index.z) - CENTER;
	var p_xz = Vector2(p.x, p.z);
	return Vector2( (p_xz).length() - w, p.y );

# func opRevolution( p : Vector3, w : float) -> Vector2:
# 	return vec2( length(p.xz) - w, p.y );

func ndot(a : Vector2, b : Vector2) -> float:
	return a.x*b.x - a.y*b.y;

func sdRhombus(p : Vector2, b : Vector2) -> float: 
	# var p = Vector2(index.x, index.z) - Vector2(CENTER.x, CENTER.y);
	# p = abs(p);

	var h = clamp( ndot(b-2.0*p,b)/b.dot(b), -1.0, 1.0 );
	var d = ( p-0.5*b*Vector2(1.0-h,1.0+h) ).length();
	return d * sign( p.x*b.y + p.y*b.x - b.x*b.y );
	
func create_surface_mesh(size: int = 16):
	for x in range(-size, size):
		for y in range(-size, size):
			for z in range(-size, size):
				create_surface_mesh_quad(Vector3i(x,y,z));

func create_surface_mesh_quad(index: Vector3i):
	for axis_index in range(AXIS.size()):
		var axis = AXIS[axis_index];
		var sample_value1 = get_sample_value(index);
		var sample_value2 = get_sample_value(index + axis);
		
		#reversed from original since we want caves
		if sample_value1 < 0 and sample_value2 >= 0:
			add_reversed_quad(index, axis_index);
		elif sample_value1 >= 0 and sample_value2 < 0:
			add_quad(index, axis_index);
			

const AXIS := [
	Vector3i(1,0,0),	
	Vector3i(0,1,0),	
	Vector3i(0,0,1),	
];

func add_quad(index: Vector3i, axis_index: int):
	var points = get_quad_points(index, axis_index);
	
	surface_tool.set_normal(AXIS[axis_index]);
	
	add_vertex(points[0])
	add_vertex(points[1])
	add_vertex(points[2])

	add_vertex(points[0])
	add_vertex(points[2])
	add_vertex(points[3])
	
func add_reversed_quad(index: Vector3i, axis_index: int):
	var points = get_quad_points(index, axis_index);
	
	surface_tool.set_normal(-AXIS[axis_index]);
	
	add_vertex(points[0])
	add_vertex(points[2])
	add_vertex(points[1])

	add_vertex(points[0])
	add_vertex(points[3])
	add_vertex(points[2])
			
func get_quad_points(index: Vector3i, axis_index: int):
	return [
		index + QUAD_POINTS[axis_index][0],
		index + QUAD_POINTS[axis_index][1],
		index + QUAD_POINTS[axis_index][2],
		index + QUAD_POINTS[axis_index][3],
	];
	
# The 4 relative indexes of the corners of a Quad that is orthogonal to each axis
const QUAD_POINTS := [
	# x axis
	[
		Vector3i(0,0,-1),
		Vector3i(0,-1,-1),
		Vector3i(0,-1,0),
		Vector3i(0,0,0)
	],	
	# y axis
	[
		Vector3i(0,0,-1),
		Vector3i(0,0,0),
		Vector3i(-1,0,0),
		Vector3i(-1,0,-1)
	],	
	# z axis
	[
		Vector3i(0,0,0),
		Vector3i(0,-1,0),
		Vector3i(-1,-1,0),
		Vector3i(-1,0,0)
	],	
];

# Step 7 Shift the surface points and generate normals			
func add_vertex(index: Vector3i):	
	var surface_position = get_surface_position(index);
	var surface_normal = get_surface_gradient(index, get_sample_value(index))
	
	surface_tool.set_normal(surface_normal);# needs reversal?
	surface_tool.add_vertex(surface_position);
	point_view.add_point(surface_position, Colors.GOLD_B);
	
#note surface_gradient auto generated in deformation mode?
func get_surface_gradient(index: Vector3i, sample_value: float) -> Vector3:
	return Vector3(
		sample_value - get_sample_value(index + AXIS[0]), 0,
		# sample_value - get_sample_value(index + AXIS[1]),
		sample_value - get_sample_value(index + AXIS[2])
	).normalized();	
	
func get_surface_position(index: Vector3i):
	var total := Vector3.ZERO;
	var surface_edge_count = 0;
	
	for edge_offsets in EDGES:
		var position_a = Vector3(index + edge_offsets[0]);
		var sample_a = get_sample_value(position_a);
		var position_b = Vector3(index + edge_offsets[1])
		var sample_b = get_sample_value(position_b);
		
		if sample_a * sample_b <= 0:
			# if different signs
			surface_edge_count += 1;
			total += position_a.lerp(position_b, abs(sample_a) / (abs(sample_a) + abs(sample_b)));
	
	if surface_edge_count == 0:
		return Vector3(index) + Vector3.ONE * 0.5;
	
	return total / surface_edge_count;

const EDGES := [
	# Edges on min Z axis
	[Vector3i(0,0,0),Vector3i(1,0,0)],
	[Vector3i(1,0,0),Vector3i(1,1,0)],
	[Vector3i(1,1,0),Vector3i(0,1,0)],
	[Vector3i(0,1,0),Vector3i(0,0,0)],
	# Edges on max Z axis
	[Vector3i(0,0,1),Vector3i(1,0,1)],
	[Vector3i(1,0,1),Vector3i(1,1,1)],
	[Vector3i(1,1,1),Vector3i(0,1,1)],
	[Vector3i(0,1,1),Vector3i(0,0,1)],
	# Edges connecting min Z to max Z
	[Vector3i(0,0,0),Vector3i(0,0,1)],
	[Vector3i(1,0,0),Vector3i(1,0,1)],
	[Vector3i(1,1,0),Vector3i(1,1,1)],
	[Vector3i(0,1,0),Vector3i(0,1,1)],
]
