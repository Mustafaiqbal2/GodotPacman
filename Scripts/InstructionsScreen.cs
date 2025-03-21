using Godot;
using System;

public partial class InstructionsScreen : CanvasLayer
{
	[Signal]
	public delegate void InstructionsDoneEventHandler();
	
	public override void _Ready()
	{
		// Create a simple full-screen ColorRect as background
		var background = new ColorRect();
		background.Color = Colors.Black;
		background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(background);
		
		// Load the texture
		var texture = ResourceLoader.Load<Texture2D>("res://Assets/Sprites/Regles2.png");
		if (texture == null)
		{
			GD.PrintErr("Failed to load instructions texture!");
			EmitSignal(SignalName.InstructionsDone);
			QueueFree();
			return;
		}
		
		// Create a simple sprite (not using Control nodes)
		var sprite = new Sprite2D();
		sprite.Texture = texture;
		sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
		
		// Center the sprite on screen and scale it to fit
		var viewport = GetViewport();
		var viewportSize = viewport.GetVisibleRect().Size;
		var windowSize = DisplayServer.WindowGetSize();
		
		// UPDATED: Always use portrait-optimized values
		var textureSize = texture.GetSize();
		float scaleX = viewportSize.X * 0.98f / textureSize.X; // Use almost full width
		float scaleY = viewportSize.Y * 0.92f / textureSize.Y; // Leave room for prompt
		float scale = Mathf.Min(scaleX, scaleY);
		
		// Set the scale and center it
		sprite.Scale = new Vector2(scale, scale);
		
		// UPDATED: Always use portrait positioning
		sprite.Position = new Vector2(
			viewportSize.X / 2, 
			viewportSize.Y / 2 - 40 // Always use the portrait offset
		);
		
		AddChild(sprite);
		
		// SIMPLE APPROACH: Just use a Label directly without containers
		var promptLabel = new Label();
		promptLabel.Text = "Tap to Start";
		
		// CRITICAL FIX: Set both horizontal and vertical alignment
		promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
		promptLabel.VerticalAlignment = VerticalAlignment.Center;
		
		// FIX: Use bottom-wide preset that's meant for this exact purpose
		promptLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		
		// UPDATED: Always use portrait position for prompt
		promptLabel.Position = new Vector2(0, -80);
		
		// Style the label
		promptLabel.AddThemeColorOverride("font_color", Colors.White);
		promptLabel.AddThemeConstantOverride("outline_size", 2);
		promptLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		
		AddChild(promptLabel);
		
		// Auto-close timer
		GetTree().CreateTimer(5.0).Timeout += () => {
			EmitSignal(SignalName.InstructionsDone);
			QueueFree();
		};
		
		GD.Print($"Using basic Sprite2D approach: Viewport size {viewportSize}, Scale {scale}");
		GD.Print($"Window size: {windowSize.X}x{windowSize.Y}, Portrait-only mode");
	}
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed ||
			@event is InputEventKey keyEvent && keyEvent.Pressed ||
			@event is InputEventScreenTouch touchEvent && touchEvent.Pressed)
		{
			GD.Print("Input detected, closing instructions");
			EmitSignal(SignalName.InstructionsDone);
			QueueFree();
		}
	}
}
