using Godot;
using System;

public partial class InstructionsOverlay : CanvasLayer
{
    private TextureRect instructionsImage;
    private Label promptLabel;
    private float elapsedTime = 0;
    
    public override void _Ready()
    {
        // Create a control that fills the entire screen
        var control = new Control();
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(control);
        
        // Add black background
        var background = new ColorRect();
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        background.Color = Colors.Black;
        control.AddChild(background);
        
        // Create the image display with exact settings
        instructionsImage = new TextureRect();
        instructionsImage.Texture = GD.Load<Texture2D>("res://Assets/Sprites/Regles2.png");
        instructionsImage.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
        instructionsImage.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
        instructionsImage.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        instructionsImage.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        instructionsImage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        control.AddChild(instructionsImage);
        
        // Add prompt text at bottom
        var textBg = new ColorRect();
        textBg.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        textBg.Size = new Vector2(0, 50);
        textBg.Color = new Color(0, 0, 0, 0.8f);
        control.AddChild(textBg);
        
        promptLabel = new Label();
        promptLabel.Text = "Tap or Press Any Key to Start";
        promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
        promptLabel.VerticalAlignment = VerticalAlignment.Center;
        promptLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        promptLabel.Size = new Vector2(0, 50);
        control.AddChild(promptLabel);
    }
    
    public override void _Process(double delta)
    {
        // Make the text pulse
        elapsedTime += (float)delta;
        promptLabel.Modulate = new Color(1, 1, 1, 0.5f + 0.5f * Mathf.Sin(elapsedTime * 3));
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed ||
            @event is InputEventKey keyEvent && keyEvent.Pressed ||
            @event is InputEventScreenTouch touchEvent && touchEvent.Pressed)
        {
            EmitSignal(SignalName.InstructionsCompleted);
            QueueFree();
        }
    }
    
    [Signal]
    public delegate void InstructionsCompletedEventHandler();
}