using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System;

public partial class Pacman : Actor
{
	private static readonly int[] animationFramePhase = new int[] { 1, 0, 1, 2 };
	private static Stopwatch stopwatch = Stopwatch.StartNew();

	// Direction tracking
	private Direction? queuedDirectionFromQuadrant = null;
	private int queuedDirectionAge = 0;
	private int maxQueueAge = 20;

	// Screen quadrant tracking
	private bool quadrantTouchActive = false;
	private double touchStartTime;
	private bool touchDetectedThisTick = false;
	
	// Screen regions (will be initialized in Ready)
	private Rect2 upRegion;
	private Rect2 downRegion;
	private Rect2 leftRegion;
	private Rect2 rightRegion;
	
	// Debug
	private bool coordinatesInitialized = false;
	private Vector2 screenSize;

	private bool isPoweredUp = false;
	private Sprite2D hatSprite;
	private Node swipeDetector;

	public void SetStartState()
	{
		Position = new Vector2I(112, 188);
		direction = Direction.Left;
		animationTick = 0;
		SetStartRoundSprite();
	}

	private Direction GetInputDirection()
	{
		if (Input.IsActionPressed("Right"))
			return Direction.Right;
		else if (Input.IsActionPressed("Left"))
			return Direction.Left;
		else if (Input.IsActionPressed("Up"))
			return Direction.Up;
		else if (Input.IsActionPressed("Down"))
			return Direction.Down;
		
		return direction;
	}

	// Get direct touch position from Game node
	private Vector2 GetRawPosition()
	{
		return GetNode<Game>("../").LatestMousePos;
	}

	// Process quadrant-based input
	private Direction ProcessQuadrantTouch()
	{
		touchDetectedThisTick = false;
		
		 // Check for any active touch or mouse button
		if (Input.IsActionPressed("swipe_start") || Input.IsMouseButtonPressed(MouseButton.Left))
		{
			if (!quadrantTouchActive)
			{
				quadrantTouchActive = true;
				touchStartTime = stopwatch.Elapsed.TotalSeconds;
				
				// Get touch position
				Vector2 touchPos = GetRawPosition();
				
				// Make sure screen regions are initialized
				if (!coordinatesInitialized)
				{
					InitializeScreenRegions();
				}
				
				// Process the region of the touch
				Direction regionDir = GetRegionDirection(touchPos);
				GD.PrintErr($"TOUCH at ({touchPos.X:F1}, {touchPos.Y:F1}) -> {regionDir}");
				touchDetectedThisTick = true;
				
				// Return the detected direction immediately
				return regionDir;
			}
		}
		// End touch
		else if (quadrantTouchActive)
		{
			quadrantTouchActive = false;
		}
		
		// If no new touch detected, use current direction
		return direction;
	}
	
	// Initialize screen regions based on the first touch
	private void InitializeScreenRegions()
	{
		screenSize = DisplayServer.WindowGetSize();
		
		// Debug print the screen size
		GD.PrintErr($"Screen size: {screenSize.X} x {screenSize.Y}");
		
		// Calculate region sizes based on screen dimensions
		float centerX = screenSize.X / 2;
		float centerY = screenSize.Y / 2;
		
		// Create touch regions - but make UP region larger
		// Horizontal division: 25% left, 50% middle, 25% right
		float horizontalMidStart = screenSize.X * 0.25f;
		float horizontalMidEnd = screenSize.X * 0.75f;
		
		// Vertical division: 55% up, 45% down (making UP larger)
		float verticalDivision = screenSize.Y * 0.55f; // 55% of screen height for UP
		
		// Define regions
		leftRegion = new Rect2(0, 0, horizontalMidStart, screenSize.Y);
		rightRegion = new Rect2(horizontalMidEnd, 0, screenSize.X - horizontalMidEnd, screenSize.Y);
		
		// Make UP region larger
		upRegion = new Rect2(horizontalMidStart, 0, horizontalMidEnd - horizontalMidStart, verticalDivision);
		downRegion = new Rect2(horizontalMidStart, verticalDivision, horizontalMidEnd - horizontalMidStart, screenSize.Y - verticalDivision);
		
		// Debug print the regions
		GD.PrintErr($"LEFT region: {leftRegion}");
		GD.PrintErr($"RIGHT region: {rightRegion}");
		GD.PrintErr($"UP region: {upRegion} - LARGER");
		GD.PrintErr($"DOWN region: {downRegion}");
		
		coordinatesInitialized = true;
	}
	
	// Determine direction based on which region of the screen was touched
	private Direction GetRegionDirection(Vector2 touchPosition)
	{
		// Check if touch is in any of our regions
		if (leftRegion.HasPoint(touchPosition))
		{
			return Direction.Left;
		}
		else if (rightRegion.HasPoint(touchPosition)) 
		{
			return Direction.Right;
		}
		else if (upRegion.HasPoint(touchPosition))
		{
			return Direction.Up;
		}
		else // Default to down if outside any region
		{
			return Direction.Down;
		}
	}

	public void SetStartRoundSprite()
	{
		FrameCoords = new Vector2I(2, (int)Direction.Left);
	}

	public void SetDefaultSpriteAnimation()
	{
		int phase = (animationTick / 2) & 3;
		FrameCoords = new Vector2I(animationFramePhase[phase], (int)direction);
	}

	public void SetDeathSpriteAnimation(int tick)
	{
		int phase = 3 + tick / 8;
		if (phase >= 14)
		{
			Visible = false;
		}
		phase = Mathf.Clamp(phase, 3, 13);
		FrameCoords = new Vector2I(phase, 0);
	}

	public override void _Ready()
	{
		SetProcessInput(true);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		
		// Debug info
		GD.PrintErr("Pacman ready - USING COMPLETE SWIPE DETECTOR");
		
		// Try multiple paths to find SwipeDetector
		string[] possiblePaths = new string[] {
			"../SwipeDetector",          // Sibling in Game node
			"../../SwipeDetector",       // Child of Game's parent
			"/root/Game/SwipeDetector"   // Absolute path
		};
		
		foreach (string path in possiblePaths)
		{
			try
			{
				swipeDetector = GetNode(path);
				if (swipeDetector != null)
				{
					GD.PrintErr($"Found SwipeDetector at path: {path}");
					break;
				}
			}
			catch (Exception)
			{
				// Path not valid, continue to next path
			}
		}
		
		if (swipeDetector != null)
		{
			// Connect to the swiped signal
			swipeDetector.Connect("swiped", Callable.From((Godot.GodotObject gesture) => {
				HandleSwipe(gesture);
			}));
			
			GD.PrintErr("Successfully connected to SwipeDetector");
			
			// Enable debug mode on the swipe detector for troubleshooting
			swipeDetector.Set("debug_mode", true);
		}
		else
		{
			GD.PrintErr("ERROR: SwipeDetector not found in any tried path!");
			GD.PrintErr("Scene tree hierarchy for debugging:");
			PrintSceneTree();
		}

		// Hat code remains the same
		hatSprite = new Sprite2D();
		hatSprite.Texture = GD.Load<Texture2D>("res://Assets/Sprites/hat.png");
		hatSprite.ZIndex = 10;
		float hatScale = 0.04f;
		hatSprite.Scale = new Vector2(hatScale, hatScale);
		hatSprite.Visible = false;
		AddChild(hatSprite);

		// Call this method after connecting to the swipeDetector in _Ready
		SetupMouseDebug();

		// Call this after connecting to the swiped signal
		ConnectToAllSwipeSignals();
	}

	// Handle swipe gestures from the SwipeDetector
	private void HandleSwipe(Godot.GodotObject swipeGesture)
	{
		try
			{
				// Debug: Print the gesture details including class type
				GD.PrintErr($"HandleSwipe called with gesture type: {swipeGesture.GetType()}");
				
				// Get basic gesture data
				Vector2 firstPoint = Vector2.Zero;
				Vector2 lastPoint = Vector2.Zero;
				float distance = 0f;
				string swipeDir = "none";

				try
				{
					firstPoint = (Vector2)swipeGesture.Call("first_point");
					lastPoint = (Vector2)swipeGesture.Call("last_point");
					distance = (float)swipeGesture.Call("get_distance");
					swipeDir = (string)swipeGesture.Call("get_direction");
				}
				catch (Exception e)
				{
					GD.PrintErr($"Error getting gesture data: {e.Message}");
					return;
				}
				
				GD.PrintErr($"SWIPE DETECTED: {swipeDir}, distance={distance}, from {firstPoint} to {lastPoint}");
				
				// Only process valid swipes with some minimum distance
				if (distance < 5f || swipeDir == "none")
				{
					GD.PrintErr("Swipe too short or invalid - ignoring");
					return;
				}
				
				// Convert string direction to game Direction enum
				Direction pacmanDir;
				switch (swipeDir)
				{
					case "up":
					case "up-left":
					case "up-right":
						pacmanDir = Direction.Up;
						break;
					case "down":
					case "down-left":
					case "down-right":
						pacmanDir = Direction.Down;
						break;
					case "left":
						pacmanDir = Direction.Left;
						break;
					case "right":
						pacmanDir = Direction.Right;
						break;
					default:
						GD.PrintErr($"Unknown swipe direction: {swipeDir}");
						return;
				}
				
				// Apply or queue the direction
				if (CanMoveInDirection(pacmanDir))
				{
					direction = pacmanDir;
					queuedDirectionFromQuadrant = null;
					GD.PrintErr($"APPLIED SWIPE: {pacmanDir}");
				}
				else
				{
					queuedDirectionFromQuadrant = pacmanDir;
					queuedDirectionAge = 0;
					maxQueueAge = 30;
					GD.PrintErr($"QUEUED SWIPE: {pacmanDir}");
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"Error in HandleSwipe: {e.Message}");
				GD.PrintErr($"Stack trace: {e.StackTrace}");
			}
	}
	
	// Convert string direction from SwipeDetector to our Direction enum
	private Direction ConvertStringToDirection(string swipeDirection)
	{
		switch (swipeDirection)
		{
			case "up":
			case "up-left":
			case "up-right":
				return Direction.Up;
				
			case "down":
			case "down-left":
			case "down-right":
				return Direction.Down;
				
			case "left":
				return Direction.Left;
				
			case "right":
				return Direction.Right;
				
			default:
				GD.PrintErr($"Unknown swipe direction: {swipeDirection}");
				return direction; // Keep current direction as fallback
		}
	}
	
	// Utility method to print the scene tree for debugging
	private void PrintSceneTree()
	{
		Node current = this;
		string nodePath = "";
		
		// Build path from this node to root
		while (current != null)
		{
			nodePath = current.Name + " / " + nodePath;
			current = current.GetParent();
		}
		
		GD.PrintErr("Path from this node to root: " + nodePath);
		
		// Print children of Game node
		Node gameNode = GetParent();
		if (gameNode != null)
		{
			GD.PrintErr($"Children of {gameNode.Name} node:");
			foreach (Node child in gameNode.GetChildren())
			{
				GD.PrintErr($"- {child.Name}");
			}
		}
	}

	// Add a public method that Game.cs can call when power mode changes
	public void SetPoweredUp(bool isPowered)
	{
		if (isPowered != isPoweredUp)
		{
			isPoweredUp = isPowered;
			
			// Show or hide the hat
			hatSprite.Visible = isPowered;
			
			GD.PrintErr($"Pacman hat visibility changed: {isPowered}");
		}
	}

	// Update the hat position to follow Pacman in every frame
	public override void _Process(double delta)
	{
		// Position the hat properly on Pacman's head
		if (isPoweredUp && hatSprite != null)
		{
			 // Properly position the hat slightly above pacman's center
			// Exact position depends on the hat image's center point
			hatSprite.Position = new Vector2(0, -8); // Slightly above Pacman's center
			
			// Optional: Make the hat bob slightly up and down for a fun effect
			float bobAmount = Mathf.Sin((float)Time.GetTicksMsec() / 300.0f) * 1.0f;
			hatSprite.Position = new Vector2(0, -8 + bobAmount);
		}
	}

	public override void Tick(int ticks)
	{
		Direction oldDirection = direction;
		
		 // Process queued direction from swipes
		if (queuedDirectionFromQuadrant.HasValue)
		{
			// Check if we can now move in the queued direction
			if (CanMoveInDirection(queuedDirectionFromQuadrant.Value))
			{
				direction = queuedDirectionFromQuadrant.Value;
				queuedDirectionFromQuadrant = null;
				GD.PrintErr($"APPLIED QUEUED DIRECTION: {direction}");
			}
			else
			{
				// Age the queue
				queuedDirectionAge++;
				if (queuedDirectionAge > maxQueueAge)
				{
					queuedDirectionFromQuadrant = null;
				}
			}
		}
		// Keep keyboard input as fallback
		else
		{
			Direction keyDir = GetInputDirection();
			if (keyDir != direction)
			{
				if (CanMoveInDirection(keyDir))
				{
					direction = keyDir;
				}
				else
				{
					queuedDirectionFromQuadrant = keyDir;
					queuedDirectionAge = 0;
				}
			}
		}
		
		// Validate movement
		if (!CanMove(true))
		{
			direction = oldDirection;
		}
		
		// Move if possible
		if (CanMove(true))
		{
			Move(true);
			animationTick++;
		}
	}

	private bool CanMoveInDirection(Direction testDir)
	{
		Direction backup = direction;
		direction = testDir;
		bool can = CanMove(true);
		direction = backup;
		return can;
	}

	// Add to Pacman.cs after the swipeDetector connection
	// Add monitoring for mouse events to help debug
	private void SetupMouseDebug()
	{
		if (swipeDetector != null)
		{
			// Monitor all signals
			swipeDetector.Connect("swipe_started", Callable.From((Godot.GodotObject gesture) => {
				GD.PrintErr($"SIGNAL: swipe_started");
			}));
			
			swipeDetector.Connect("swipe_updated", Callable.From((Godot.GodotObject gesture) => {
				GD.PrintErr($"SIGNAL: swipe_updated");
			}));
			
			swipeDetector.Connect("swipe_failed", Callable.From(() => {
				GD.PrintErr($"SIGNAL: swipe_failed");
			}));
		}
	}

	// Add to Pacman.cs - public method to manually test swipe directions
	public override void _Input(InputEvent @event)
	{
		// Only handle mouse events for testing if SwipeDetector isn't working
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseEvent.Pressed)
			{
				GD.PrintErr($"Manual mouse tracking - button down at {mouseEvent.Position}");
			}
			else 
			{
				GD.PrintErr($"Manual mouse tracking - button up at {mouseEvent.Position}");
			}
		}
		else if (@event is InputEventMouseMotion motionEvent && Input.IsMouseButtonPressed(MouseButton.Left))
		{
			// Uncomment for detailed tracking (will be verbose)
			// GD.PrintErr($"Manual mouse tracking - motion at {motionEvent.Position}");
		}
	}

	// Add to the _Ready method after connecting to SwipeDetector

	// Connect to all SwipeDetector signals for detailed debugging
	private void ConnectToAllSwipeSignals()
	{
		if (swipeDetector != null)
		{
			swipeDetector.Connect("swipe_started", Callable.From((Godot.GodotObject gesture) => {
				Vector2 firstPoint = (Vector2)gesture.Call("first_point");
				GD.PrintErr($"SIGNAL: swipe_started at {firstPoint}");
			}));
			
			swipeDetector.Connect("swipe_updated", Callable.From((Godot.GodotObject gesture) => {
				Vector2 lastPoint = (Vector2)gesture.Call("last_point");
				GD.PrintErr($"SIGNAL: swipe_updated to {lastPoint}");
			}));
			
			swipeDetector.Connect("swipe_failed", Callable.From(() => {
				GD.PrintErr($"SIGNAL: swipe_failed");
			}));
		}
	}
}
