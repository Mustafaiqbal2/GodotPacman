extends Node

# Signals
signal swiped(gesture)
signal swipe_ended(gesture) # alias for `swiped`
signal swipe_started(partial_gesture)
signal swipe_updated(partial_gesture)
signal swipe_updated_with_delta(partial_gesture, delta)
signal swipe_failed()

# Configuration variables
@export var detect_gesture: bool = true
@export_enum("Idle", "Fixed") var process_method: String = "Fixed"
@export var distance_threshold: float = 5.0  # ULTRA LOW for better responsiveness
@export var duration_threshold: float = 0.01  # LOWERED for better responsiveness
@export var minimum_points: int = 2
@export_enum("Four Directions", "Eight Directions") var directions_mode: String = "Four Directions"
@export var debug_mode: bool = true
@export var detect_mouse: bool = true  # NEW: Toggle to explicitly enable mouse input
@export var use_window_coordinates: bool = true  # NEW: Use window coordinates instead of viewport

# Direction constants
const DIRECTION_RIGHT = "right"
const DIRECTION_DOWN_RIGHT = "down-right"
const DIRECTION_DOWN = "down"
const DIRECTION_DOWN_LEFT = "down-left"
const DIRECTION_LEFT = "left"
const DIRECTION_UP_LEFT = "up-left"
const DIRECTION_UP = "up"
const DIRECTION_UP_RIGHT = "up-right"

const DIRECTIONS = [
	DIRECTION_RIGHT,
	DIRECTION_DOWN_RIGHT,
	DIRECTION_DOWN,
	DIRECTION_DOWN_LEFT,
	DIRECTION_LEFT,
	DIRECTION_UP_LEFT,
	DIRECTION_UP,
	DIRECTION_UP_RIGHT
]

# Implementation
var gesture_history = []
var states = {}
var was_swiping = false
var detection_areas = []

# Input tracking - made more explicit for debugging
var input_pressed = false
var input_start_position = Vector2()
var input_current_position = Vector2()
var input_last_update_position = Vector2()

# Viewport tracking
var main_viewport = null
var viewport_name = "unknown"
var gameScaledViewport = null  # NEW: Reference to GameScaledViewport

# Debugging - flag to disable physics input spam
var ignore_physics_this_frame = false
var last_mouse_down_time = 0.0
var mouse_down_cooldown = 0.1  # Seconds to ignore redundant mouse down events

# Debug function - using print_err instead of push_error
func debug(message, more1='', more2='', more3=''):
	if debug_mode:
		var debug_str = '[SwipeDetector] ' + str(message) + ' ' + str(more1) + ' ' + str(more2) + ' ' + str(more3)
		print(debug_str)  # Regular console output
		printerr(debug_str)  # Error console without stack trace

# NEW FUNCTION: Get real mouse position using the best available method
func get_real_mouse_position():
	if use_window_coordinates:
		# Use window coordinates directly
		var window_pos = DisplayServer.mouse_get_position()
		var window_size = DisplayServer.window_get_size()
		var viewport_size = get_viewport().size
		
		# Scale window coordinates to viewport coordinates
		var scale_x = float(viewport_size.x) / float(window_size.x)
		var scale_y = float(viewport_size.y) / float(window_size.y)
		
		return Vector2(window_pos.x * scale_x, window_pos.y * scale_y)
	elif gameScaledViewport != null:
		# Use GameScaledViewport if available
		return gameScaledViewport.get_mouse_position()
	else:
		# Fallback to current viewport
		return get_viewport().get_mouse_position()

# Built-in _ready function
func _ready():
	# Initialize states dictionary
	states = {}
	states['_singleton'] = {'capturing': false, 'was_swiping': false, 'last_update_delta': 0.0, 'gesture': null}
	
	# Set up processing with priority
	set_process(true)
	set_physics_process(true)
	set_process_input(true)
	
	# Find GameScaledViewport
	var all_viewports = get_all_viewports()
	for vp in all_viewports:
		if vp.name == "GameScaledViewport":
			gameScaledViewport = vp
			debug("Found GameScaledViewport: ", vp.name, " Size: ", vp.size)
	
	# Debugging setup information
	debug("SwipeDetector initialized and ready - MOUSE INPUT " + ("ENABLED" if detect_mouse else "DISABLED"))
	debug("Mouse Processing: Distance threshold = " + str(distance_threshold) + ", Direction Mode = " + directions_mode)
	debug("Using coordinates from: " + ("Window" if use_window_coordinates else (gameScaledViewport.name if gameScaledViewport else "Current Viewport")))
	printerr("[IMPORTANT] SwipeDetector ready - CLICK AND DRAG TO TEST")
	
	# Debug the viewport size
	var viewport_size = get_viewport().size
	debug("Viewport size: ", viewport_size)

	# Debug viewport info
	main_viewport = get_viewport()
	if main_viewport:
		viewport_name = main_viewport.get_name() if main_viewport.get_name() != "" else "DefaultViewport"
		var viewport_path = main_viewport.get_path()
		viewport_size = main_viewport.size
		var viewport_world = main_viewport.world_2d
		
		debug("VIEWPORT INFO:")
		debug("- Name: ", viewport_name)
		debug("- Path: ", viewport_path)
		debug("- Size: ", viewport_size)
		debug("- World2D: ", viewport_world)
		debug("- Parent: ", get_parent().get_name())
		
		# Try different methods to get mouse position
		var mouse_pos_viewport = main_viewport.get_mouse_position()
		var mouse_pos_input = Vector2(0,0)  # Placeholder
		var window_mouse_pos = DisplayServer.mouse_get_position()
		var real_mouse_pos = get_real_mouse_position()  # NEW: Use our custom function
		
		debug("MOUSE POSITION:")
		debug("- Viewport method: ", mouse_pos_viewport)
		debug("- Input method: ", mouse_pos_input)
		debug("- Window method: ", window_mouse_pos)
		debug("- REAL METHOD: ", real_mouse_pos, " (Our custom function)")

	# Debugging all available viewports
	debug("ALL VIEWPORTS: " + str(all_viewports.size()))
	for i in range(all_viewports.size()):
		var vp = all_viewports[i]
		debug("Viewport[" + str(i) + "]: Name=" + str(vp.get_name()) + " Size=" + str(vp.size))
	
	printerr("[IMPORTANT] SwipeDetector ready - WINDOW COORDINATES MODE")

# Get all viewports in the scene
func get_all_viewports():
	var viewports = []
	var root = get_tree().get_root()
	_collect_viewports(root, viewports)
	return viewports

func _collect_viewports(node, result_array):
	if node is Viewport:
		result_array.append(node)
	
	for child in node.get_children():
		_collect_viewports(child, result_array)

# Using standard _input to catch direct mouse events
func _input(event):
	# Only handle mouse button and motion events
	if event is InputEventMouseButton and detect_mouse:
		if event.button_index == MOUSE_BUTTON_LEFT:
			# Get the actual mouse position using our custom function
			var real_mouse_pos = get_real_mouse_position()
			
			debug("ACTUAL MOUSE EVENT AT: ", real_mouse_pos, " (event position: " + str(event.position) + ")")
			debug("Window position: ", DisplayServer.mouse_get_position())
			
			if event.pressed:
				# Only process if not already pressed
				if not input_pressed:
					input_pressed = true
					input_start_position = real_mouse_pos
					input_current_position = real_mouse_pos
					input_last_update_position = real_mouse_pos
					last_mouse_down_time = Time.get_ticks_msec() / 1000.0
					
					# Start the gesture
					var area = null  # Use null for the _singleton state
					swipe_start(area, real_mouse_pos)
					was_swiping = true
					
					debug("MOUSE DOWN - Starting swipe at: ", real_mouse_pos)
					ignore_physics_this_frame = true  # Avoid duplicate detection
			else:
				# Mouse button released
				if input_pressed and was_swiping:
					input_pressed = false
					input_current_position = real_mouse_pos
					
					# End the gesture
					var area = null  # Use null for the _singleton state
					swipe_stop(area)
					was_swiping = false
					
					debug("MOUSE UP - Ending swipe at: ", real_mouse_pos)
					ignore_physics_this_frame = true  # Avoid duplicate detection
					
	# Track mouse movement while pressed
	elif event is InputEventMouseMotion and input_pressed and was_swiping and detect_mouse:
		var real_mouse_pos = get_real_mouse_position()
		input_current_position = real_mouse_pos
		
		var distance = input_last_update_position.distance_to(real_mouse_pos)
		if distance >= distance_threshold:
			var area = null  # Use null for the _singleton state
			swipe_update(get_process_delta_time(), area, real_mouse_pos)
			input_last_update_position = real_mouse_pos
			
			debug("MOUSE MOVE - Updating swipe to: ", real_mouse_pos, " (distance: " + str(distance) + ")")

	if event is InputEventMouseButton:
		debug("_input: Mouse button event received")
		debug("- Position (event): ", event.position)
		debug("- Position (real): ", get_real_mouse_position())
		debug("- Window Position: ", DisplayServer.mouse_get_position())
		debug("- Pressed: ", event.pressed)
		debug("- Button index: ", event.button_index)

# Physics process - for backup detection only
func _physics_process(delta):
	if ignore_physics_this_frame:
		ignore_physics_this_frame = false
		return
		
	# Only check for missed events
	var current_time = Time.get_ticks_msec() / 1000.0
	var real_mouse_pos = get_real_mouse_position()
	
	# Debug mouse position in physics process
	if debug_mode and current_time - last_mouse_down_time > 1.0:
		debug("PHYSICS: Mouse at ", real_mouse_pos)
		debug("PHYSICS: Window mouse at ", DisplayServer.mouse_get_position())
		last_mouse_down_time = current_time  # Update to avoid spam
	
	# Missed mouse down
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT) and not input_pressed:
		if current_time - last_mouse_down_time > mouse_down_cooldown:
			real_mouse_pos = get_real_mouse_position()
			last_mouse_down_time = current_time
			
			input_pressed = true
			input_start_position = real_mouse_pos
			input_current_position = real_mouse_pos
			input_last_update_position = real_mouse_pos
			
			var area = null
			swipe_start(area, real_mouse_pos)
			was_swiping = true
			
			debug("PHYSICS RECOVERY: Missed mouse down detected at: ", real_mouse_pos)
	
	# Missed mouse up
	elif !Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT) and input_pressed and was_swiping:
		input_pressed = false
		input_current_position = real_mouse_pos
		
		var area = null
		swipe_stop(area)
		was_swiping = false
		
		debug("PHYSICS RECOVERY: Missed mouse up detected at: ", real_mouse_pos)
	
	# Missed mouse move
	elif input_pressed and was_swiping:
		var distance = input_last_update_position.distance_to(real_mouse_pos)
		if distance >= distance_threshold:
			var area = null
			swipe_update(delta, area, real_mouse_pos)
			input_last_update_position = real_mouse_pos
			
			debug("PHYSICS RECOVERY: Missed mouse move detected to: ", real_mouse_pos)

# Start a swipe
func swipe_start(area, point):
	var state_key = area.name if area else '_singleton'
	var state = states[state_key]
	
	state.capturing = true
	state.last_update_delta = 0.0
	state.gesture = SwipeGesture.new(area, [point], directions_mode)
	
	emit_signal("swipe_started", state.gesture)
	debug("SWIPE STARTED at: ", point)
	return self

# Update a swipe
func swipe_update(delta, area, point):
	var state_key = area.name if area else '_singleton'
	var state = states[state_key]
	
	if state.gesture != null:
		state.last_update_delta += delta
		
		state.gesture.add_point(point)
		state.gesture.add_duration(state.last_update_delta)
		
		emit_signal("swipe_updated", state.gesture)
		emit_signal("swipe_updated_with_delta", state.gesture, state.last_update_delta)
		
		state.last_update_delta = 0.0
		debug("SWIPE UPDATED to: ", point)
	else:
		debug("ERROR: Cannot update swipe - gesture is null")
	
	return self

# End a swipe
func swipe_stop(area):
	var state_key = area.name if area else '_singleton'
	var state = states[state_key]
	var gesture = state.gesture
	
	if gesture != null:
		# Get swipe details
		var points_count = gesture.point_count()
		var start_point = gesture.first_point()
		var end_point = gesture.last_point()
		var swipe_distance = gesture.get_distance()
		var direction = gesture.get_direction()
		
		debug("SWIPE ENDED - Points: " + str(points_count) + " Distance: " + str(swipe_distance))
		
		# SUPER lenient criteria for swipe detection
		if swipe_distance > 3.0 and direction != "none":
			# Calculate and print the swipe vector for debugging
			var swipe_vector = end_point - start_point
			debug("VALID SWIPE - direction: " + direction + " vector: " + str(swipe_vector))
			
			# Store in history and emit signals
			gesture_history.append(gesture)
			emit_signal("swiped", gesture)
			emit_signal("swipe_ended", gesture)
			printerr("SWIPE DETECTED: " + direction + " (Distance: " + str(swipe_distance) + ")")
		else:
			debug("SWIPE FAILED - Too short or invalid direction")
			emit_signal("swipe_failed")
	else:
		debug("SWIPE FAILED - No gesture data")
		emit_signal("swipe_failed")
	
	# Reset state
	state.capturing = false
	state.gesture = null
	state.last_update_delta = 0.0
	
	return self

# Get gesture history
func history():
	return gesture_history

# Set thresholds
func set_duration_threshold(value):
	duration_threshold = value
	return self

func set_distance_threshold(value):
	distance_threshold = value
	return self

# Gesture class implementation
class SwipeGesture:
	var area
	var points = []
	var duration = 0.0
	var directions_mode
	
	func _init(p_area, p_points, p_directions_mode="Four Directions"):
		area = p_area
		for point in p_points:
			points.append(point)
		duration = 0
		directions_mode = p_directions_mode
	
	func get_area():
		return area
	
	func get_duration():
		return duration
	
	func add_duration(delta):
		duration += delta
	
	func add_point(point):
		points.append(point)
	
	func get_points():
		return points
	
	func point_count():
		return points.size()
	
	func first_point():
		if points.size() > 0:
			return points[0]
		return Vector2()
	
	func last_point():
		if points.size() > 0:
			return points[points.size() - 1]
		return Vector2()
	
	func get_distance():
		return first_point().distance_to(last_point())
	
	func get_speed():
		if duration > 0:
			return get_distance() / duration
		return 0.0
	
	func get_direction():
		if points.size() < 2:
			return "none"
		
		var vector = last_point() - first_point()
		var angle_rad = vector.angle()
		var angle_deg = rad_to_deg(angle_rad)
		
		if directions_mode == "Four Directions":
			# Four-direction mode (up, right, down, left)
			if angle_deg >= -45 and angle_deg < 45:
				return "right"
			elif angle_deg >= 45 and angle_deg < 135:
				return "down"
			elif angle_deg >= 135 or angle_deg < -135:
				return "left"
			else:
				return "up"
		else:
			# Eight-direction mode
			if angle_deg >= -22.5 and angle_deg < 22.5:
				return "right"
			elif angle_deg >= 22.5 and angle_deg < 67.5:
				return "down-right"
			elif angle_deg >= 67.5 and angle_deg < 112.5:
				return "down"
			elif angle_deg >= 112.5 and angle_deg < 157.5:
				return "down-left"
			elif angle_deg >= 157.5 or angle_deg < -157.5:
				return "left"
			elif angle_deg >= -157.5 and angle_deg < -112.5:
				return "up-left"
			elif angle_deg >= -112.5 and angle_deg < -67.5:
				return "up"
			else:
				return "up-right"
	
	func get_direction_vector():
		return last_point() - first_point()
	
	func get_direction_angle():
		return get_direction_vector().angle()
