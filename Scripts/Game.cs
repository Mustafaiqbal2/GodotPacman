using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	private enum FreezeType
	{
		None	 = 0,
		Ready	 = (1 << 1),  // New round started	
		EatGhost = (1 << 2),  // Pacman has eaten a ghost
		Dead     = (1 << 3),  // Pacman was eaten by a ghost
		Won		 = (1 << 4),  // round won (all dots eaten)
		GameOver = (1 << 5),  // round lost and no lifes left
		Reset    = (1 << 6),  // for freeze the game when reset is called (to avoid update actors in the first tick)
		Instructions = (1 << 7), // Instructions screen
	}

	public enum FruitType
	{
		Cherries,
		Strawberry,
		Peach,
		Apple,
		Grapes,
		Galaxian,
		Bell,
		Key
	}

	// game constants

	private readonly Vector2I fruitTile = new Vector2I(112, 140) / Maze.TileSize;

	private readonly int[] ghostEatenScores = new int[] { 200, 400, 800, 1600 };
	private readonly int[] fruitScores = new int[] { 100, 300, 500, 700, 1000, 2000, 3000, 5000 };
	private readonly int dotScore = 10;
	private readonly int pillScore = 50;

	private readonly int ghostEatenFreezeTicks = 60;
	private readonly int pacmanEatenFreezeTicks = 60;
	private readonly int pacmanDeathTicks = 150;
	private readonly int roundWonFreezeTicks = 4 * 60;
	private readonly int fruitActiveTicks = 560;

	// scenes pacman and ghosts

	[Export]
	private PackedScene pacmanScene;

	[Export]
	private PackedScene ghostScene;

	[Export]
	private Texture2D dotsTexture;

	[Export]
	private Texture2D readyTextTexture;

	[Export]
	private Texture2D gameOverTextTexture;

	[Export]
	private Texture2D lifeTexture;

	[Export]
	private Texture2D fruitTexture;

	[Export]
	private Texture2D instructionsTexture;  // Will be loaded from "Regles2.png"

	private Trigger instructionsStartedTrigger;
	private bool firstGameStart = true;  // To only show instructions on first run
	private Label scoreText;
	private Label highScoreText;
	private Sprite2D mazeSprite;
	private ColorRect ghostDoorSprite; // "sprite"

	public Pacman pacman;
	private Ghost[] ghosts = new Ghost[4];

	// sounds

	private AudioStreamPlayer munch1Sound;
	private AudioStreamPlayer munch2Sound;
	private AudioStreamPlayer fruitSound;
	private AudioStreamPlayer ghostEatenSound;
	private AudioStreamPlayer sirenSound;
	private AudioStreamPlayer powerPelletSound;
	private AudioStreamPlayer pacmanDeathSound;
	private AudioStreamPlayer gameOverSound;

	// game control variables

	private int ticks;
	private int freeze;
	private int level;
	private int score;
	private int highScore;
	private int numGhostsEaten;
	private int numLifes;
	private int numDotsEaten;
	private int numDotsEatenThisRound;

	// triggers

	private List<Trigger> triggers = new List<Trigger>();
	private Trigger dotEatenTrigger;
	private Trigger pillEatenTrigger;
	private Trigger ghostEatenUnFreezeTrigger;
	private Trigger pacmanEatenTrigger;
	private Trigger readyStartedTrigger;
	private Trigger roundStartedTrigger;
	private Trigger roundWonTrigger;
	private Trigger gameOverTrigger;
	private Trigger resetTrigger;
	private Trigger fruitActiveTrigger;
	private Trigger fruitEatenTrigger;
	private Trigger[] ghostFrightenedTrigger = new Trigger[4];
	private Trigger[] ghostEatenTrigger = new Trigger[4];

	// debug

	private List<Vector2I>[] ghostsPaths = new List<Vector2I>[4];

	// Add these fields near the top of the Game class
	private float currentDotSizeMultiplier = 0.4f;  // 3.2/8 = 0.4 - this was one of the correct values
	private float currentSourceSizeMultiplier = 0.75f;  // 6/8 = 0.75

	// Add these fields at the top of your Game class
	private float[] pillSourceXOffsets = new float[] { 7 }; // X offset from texture start - fixed at 8 
	private float[] pillWidths = new float[] { 9 };        // Width of source rectangle - fixed at 9
	private float[] pillHeights = new float[] { 10 };      // Height of source rectangle - fixed at 10
	private int pillSourceXIndex = 0;
	private int pillWidthIndex = 0;
	private int pillHeightIndex = 0;
	private double pillTestTimer = 0;

	// Add these variables to your class
	private bool showSplashScreen = true;
	private float splashScreenTimer = 0f;
	private const float SPLASH_SCREEN_DURATION = 5.0f; // 5 seconds

	/* LOAD AND SAVE HIGHSCORE */

	private void LoadHighScore()
	{
		GD.Print("Loading high score");
		string filePath = "user://highscore.data";
		
		// Force reset high score for testing (remove this in production)
		// FileAccess.Delete(filePath);
		
		if (FileAccess.FileExists(filePath))
		{
			try
			{
				var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
				if (file != null)
				{
					highScore = (int)file.Get32();
					file.Close();
					GD.Print("Loaded high score: " + highScore);
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr("Error reading high score: " + ex.Message);
				highScore = 0;
			}
		}
		else
		{
			GD.Print("No high score file found, creating new one");
			highScore = 0;
			SaveHighScore();
		}
		
		// Make sure the high score text is updated
		if (highScoreText != null)
		{
			highScoreText.Text = "Meilleur score: " + highScore.ToString();
		}
	}

	private void SaveHighScore()
	{
		GD.Print("Attempting to save high score: " + highScore);
		
		try
		{
			string filePath = "user://highscore.data";
			var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
			if (file != null)
			{
				file.Store32((uint)highScore);
				file.Close();
				GD.Print("High score saved successfully");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("Error saving high score: " + ex.Message);
		}
	}

	/* RESET */

	private void StopSounds()
	{
		sirenSound.Stop();
		powerPelletSound.Stop();
	}

	private void ResetTriggers()
	{
		foreach (Trigger t in triggers)
		{
			t.Reset(); // disables the trigger and reset the games ticks of the trigger to 0
		}
	}

	private void DisableTriggers()
	{
		foreach (Trigger t in triggers)
		{
			t.Disable();
		}
	}

	private void ResetActors()
	{
		// pacman

		pacman.Visible = true;
		pacman.SetStartState();
		// Make sure hat is hidden on reset
		pacman.SetPoweredUp(false);

		// ghosts

		foreach (Ghost g in ghosts)
		{
			g.Visible = true;
			g.SetStartState();
		}
	}

	private void Reset()
	{
		// reset some control variables
		ticks = 0;
		level = 1;
		score = 0;
		numGhostsEaten = 0;
		numLifes = 3;
		numDotsEaten = 0;
		numDotsEatenThisRound = 0;

		StopSounds();

		// disable triggers & reset actors and maze
		ResetTriggers();
		ResetActors();
		Maze.Reset();

		// reset maze color and show door
		mazeSprite.SelfModulate = new Color("417ae2");
		ghostDoorSprite.Visible = true;

		// For the initial game start, show instructions using a proper scene
		if (firstGameStart)
		{
			firstGameStart = false;
			
			// Hide game elements
			pacman.Visible = false;
			foreach (Ghost g in ghosts)
			{
				g.Visible = false;
			}
			
			// Completely stop game processing
			freeze = (int)FreezeType.Reset;
			
			try {
				// Load and instance the instructions scene
				var instructionsScene = GD.Load<PackedScene>("res://Scenes/InstructionsScreen.tscn");
				if (instructionsScene != null)
				{
					// CRITICAL FIX: Add to ROOT of tree, not to Game
					var instructionsScreen = instructionsScene.Instantiate<InstructionsScreen>();
					
					// Connect the signal
					instructionsScreen.InstructionsDone += OnInstructionsCompleted;
					
					// Add to ROOT, bypassing the viewport constraints
					GetTree().Root.CallDeferred("add_child", instructionsScreen);
					GD.Print("Adding instructions to ROOT instead of Game node");
				}
				else
				{
					GD.PrintErr("Failed to load instructions scene!");
					readyStartedTrigger.Start();
				}
			}
			catch (Exception ex) {
				GD.PrintErr("Exception loading instructions: " + ex.Message);
				readyStartedTrigger.Start();
			}
			
			// Make sure nothing else happens
			showSplashScreen = false;
		}
		else
		{
			// For subsequent games, go directly to ready state
			SetFreezeTo(FreezeType.Reset);
			readyStartedTrigger.Start();
		}
	}

	// Add this method to handle when instructions are done
	private void OnInstructionsCompleted()
	{
		// Start the game
		freeze = (int)FreezeType.None;
		readyStartedTrigger.Start();
		
		// Make actors visible
		pacman.Visible = true;
		foreach (Ghost g in ghosts)
		{
			g.Visible = true;
		}
	}

	// Add this method
	private void ForceResetHighScore()
	{
		// Delete the file to ensure a clean start
		if (FileAccess.FileExists("user://highscore.data"))
		{
			DirAccess.RemoveAbsolute("user://highscore.data");
			GD.Print("Deleted existing high score file");
		}
		
		// Reset high score to 0
		highScore = 0;
		SaveHighScore();
		
		// Update display
		if (highScoreText != null)
		{
			highScoreText.Text = "Meilleur score: 0";
		}
		
		GD.Print("High score forcibly reset to 0");
	}

	/* GAME */

	private bool IsFrozen()
	{
		return freeze != (int)FreezeType.None;
	}

	private bool IsFrozenBy(FreezeType freezeType)
	{
		return (freeze & (int)freezeType) == (int)freezeType;
	}

	private void SetFreezeTo(FreezeType freezeType)
	{
		freeze = (int)freezeType;
	}

	private void FreezeBy(FreezeType freezeType)
	{
		freeze |= (int)freezeType;
	}

	private void UnFreeze()
	{
		freeze = (int)FreezeType.None;
	}

	private void UnFreezeBy(FreezeType freezeType)
	{
		freeze &= ~(int)freezeType;
	}

	// init a new round (ready msg only) this occurs when pacman loses a life or at the start of the game

	private void InitRound()
	{
		// disable timers

		DisableTriggers();

		// reset actors

		ResetActors();

		// set freeze to ready

		SetFreezeTo(FreezeType.Ready);

		// check if the last game has been lost or won

		if (numDotsEaten >= Maze.NumDots)
		{
			numDotsEaten = 0;
			level++;
			Maze.Reset();
		}
		else
		{
			numLifes--;
		}

		// reset number of dots eaten this round

		numDotsEatenThisRound = 0;

		// reset maze color and show door

		mazeSprite.SelfModulate = new Color("417ae2");
		ghostDoorSprite.Visible = true;
	}

	/* PACMAN RELATED */

	// check if pacman should move this tick

	private bool PacmanShouldMove()
	{
		if (dotEatenTrigger.IsActive())
		{
			return false;
		}
		else if (pillEatenTrigger.IsActive())
		{
			return false;
		}

		return true;
	}

	/* GHOST RELATED */

	// ghost mode

	private Ghost.Mode GhostScatterChasePhase()
	{
		int s = roundStartedTrigger.TicksSinceStarted();

		 // Use consistent timing for all levels
		if (s < 10 * 60) return Ghost.Mode.Scatter;      // 10 seconds scatter
		else if (s < 20 * 60) return Ghost.Mode.Chase;   // 10 seconds chase
		else if (s < 30 * 60) return Ghost.Mode.Scatter; // 10 seconds scatter 
		else if (s < 40 * 60) return Ghost.Mode.Chase;   // 10 seconds chase
		else if (s < 50 * 60) return Ghost.Mode.Scatter; // 10 seconds scatter
		else if (s < 60 * 60) return Ghost.Mode.Chase;   // 10 seconds chase
		else if (s < 70 * 60) return Ghost.Mode.Scatter; // 10 seconds scatter
		
		return Ghost.Mode.Chase;
	}

	// update the ghost mode

	private bool GhostLeaveHouse(Ghost g)
	{
		// Use consistent values for all levels
		switch (g.type)
		{
			case Ghost.Type.Pinky:
				if (numDotsEatenThisRound >= 15)
				{
					return true;
				}
				break;
			case Ghost.Type.Inky:
				if (numDotsEatenThisRound >= 30)
				{
					return true;
				}
				break;
			case Ghost.Type.Clyde:
				if (numDotsEatenThisRound >= 60)
				{
					return true;
				}
				break;
		}
		
		return false;
	}

	private bool IsGhostFrightened(Ghost g)
	{
		return ghostFrightenedTrigger[(int)g.type].IsActive();
	}

	private int GetGhostFrightenedTicks()
	{
		 // Same duration for all levels
		return 10 * 60; // 10 seconds at 60fps
	}

	/* ACTORS RELATED (BOTH PACMAN AND GHOSTS) */

	private void UpdateDotsEaten()
	{
		numDotsEaten++;
		numDotsEatenThisRound++;

		// check if there are no dots left

		if (numDotsEaten >= Maze.NumDots)
		{
			roundWonTrigger.Start();
		}

		// spawn fruits

		if (numDotsEaten == 70 || numDotsEaten == 170)
		{
			fruitActiveTrigger.Start();
		}

		// play munch sound

		if ((numDotsEaten & 1) != 0)
		{
			munch1Sound.Play();
		}
		else
		{
			munch2Sound.Play();
		}
	}

	private void UpdateActors()
	{
		/* TICK PACMAN */

		if (PacmanShouldMove())
		{
			pacman.Tick(ticks);
		}

		// Handle dot and pill eating

		Vector2I pacmanTile = pacman.PositionToTile();
		Maze.Tile mazeTile = Maze.GetTile(pacmanTile);

		if (mazeTile == Maze.Tile.Dot || mazeTile == Maze.Tile.Pill)
		{
			switch (mazeTile)
			{
				case Maze.Tile.Dot:
					dotEatenTrigger.Start();

					// increment score and number of dots eaten

					score += dotScore;

					break;
				case Maze.Tile.Pill:
					pillEatenTrigger.Start();
					
					// reset num of ghost eaten
					numGhostsEaten = 0;
					
					// Debug to track execution
					GD.Print("Power pill eaten, making ghosts frightened");
					
					// set ghosts to be in frightened mode
					StopSounds();
					powerPelletSound.Play();
					
					// IMPORTANT: Tell Pacman to show the hat when powered up
					pacman.SetPoweredUp(true);
					
					foreach (Ghost g in ghosts)
					{
						if (g.mode == Ghost.Mode.Chase || g.mode == Ghost.Mode.Scatter)
						{
							ghostFrightenedTrigger[(int)g.type].Start();
						}
					}
					
					// increment score and number of dots eaten
					score += pillScore;
					
					break;
			}

			// clear tile, increment number of dots and check if there are no dots left

			Maze.SetTile(pacmanTile, Maze.Tile.Empty);

			UpdateDotsEaten();
		}

		// check if pacman eats fruit

		if (fruitActiveTrigger.IsActive())
		{
			if (pacmanTile == fruitTile)
			{
				fruitActiveTrigger.Disable();
				fruitEatenTrigger.Start();

				// score increment

				score += fruitScores[(int)GetFruitTypeFromLevel(level)];

				// play sound

				fruitSound.Play();
			}
		}

		// check if pacman eats a ghost (or viceversa)

		foreach (Ghost g in ghosts)
		{
			if (pacman.PositionToTile() == g.PositionToTile())
			{
				if (g.mode == Ghost.Mode.Frightened)
				{
					// ghost has been eaten

					// freeze the game

					FreezeBy(FreezeType.EatGhost);

					// swap ghost mode to eyes

					g.mode = Ghost.Mode.Eyes;

					// disable frightened trigger

					ghostFrightenedTrigger[(int)g.type].Disable();

					// start ghost eaten and eaten trigger

					ghostEatenUnFreezeTrigger.Start(ghostEatenFreezeTicks);
					ghostEatenTrigger[(int)g.type].Start();

					// increment the score

					score += ghostEatenScores[numGhostsEaten];

					// increment the number of ghosts eaten by one

					numGhostsEaten++;

					// play sound

					ghostEatenSound.Play();
				}
				else if (g.mode == Ghost.Mode.Chase || g.mode == Ghost.Mode.Scatter)
				{
					// pacman has been eaten
					
					// Make sure hat is hidden when Pacman dies
					pacman.SetPoweredUp(false);
					
					// freeze the game
					FreezeBy(FreezeType.Dead);
					
					// start pacman eaten trigger
					pacmanEatenTrigger.Start(pacmanEatenFreezeTicks);
					
					// check number of lifes

					if (numLifes >= 1)
					{
						// start readystarted trigger after (pacmanEatenFreezeTicks + pacmanDeathTicks) ticks

						readyStartedTrigger.Start(pacmanEatenFreezeTicks + pacmanDeathTicks);
					}
					else
					{
						// game over

						gameOverTrigger.Start(pacmanEatenFreezeTicks + pacmanDeathTicks);
					}

					// stop sounds

					StopSounds();

					// Play pacman death sound
					pacmanDeathSound.Play();
				}
			}
		}

		/* TICK GHOSTS */

		foreach (Ghost g in ghosts)
		{
			g.UpdateGhostMode(GhostLeaveHouse, IsGhostFrightened, GhostScatterChasePhase);
			g.UpdateTargetTile(pacman, ghosts);
			g.Tick(ticks);
		}
	}

	private void UpdatePacmanSprite()
	{
		if (IsFrozenBy(FreezeType.EatGhost))
		{
			pacman.Visible = false;
		}
		else if (IsFrozenBy(FreezeType.Dead))
		{
			pacman.Visible = true;

			if (pacmanEatenTrigger.IsActive())
			{
				int tick = pacmanEatenTrigger.TicksSinceStarted();
				pacman.SetDeathSpriteAnimation(tick);
			}
		}
		else if (IsFrozenBy(FreezeType.Ready))
		{
			pacman.Visible = true;
			pacman.SetStartRoundSprite();
		}
		else if (IsFrozenBy(FreezeType.GameOver))
		{
			pacman.Visible = false;
		}
		else
		{
			pacman.Visible = true;
			pacman.SetDefaultSpriteAnimation();
		}
	}

	private void UpdateGhostSprite(Ghost g)
	{
		// check if it has just been eaten

		if (ghostEatenTrigger[(int)g.type].IsActive())
		{
			g.Visible = true;
			g.SetScoreSprite(numGhostsEaten - 1);
		}
		else if (IsFrozenBy(FreezeType.Dead))
		{
			g.Visible = true;

			if (pacmanEatenTrigger.IsActive())
			{
				g.Visible = false;
			}
		}
		else if (IsFrozenBy(FreezeType.Won) || IsFrozenBy(FreezeType.GameOver))
		{
			g.Visible = false;
		}
		else
		{
			g.Visible = true;

			// choose the sprite and animation to show

			switch (g.mode)
			{
				case Ghost.Mode.Frightened:
					int ticksSinceFrightened = ghostFrightenedTrigger[(int)g.type].TicksSinceStarted();
					int phase = (ticksSinceFrightened / 4) & 1;
					g.SetFrightenedSpriteAnimation(phase, ticksSinceFrightened > GetGhostFrightenedTicks() - 60 && (ticksSinceFrightened & 0x10) != 0);
					break;
				case Ghost.Mode.EnterHouse:
				case Ghost.Mode.Eyes:
					g.SetEyesSprite();
					break;
				default:
					g.SetDefaultSpriteAnimation();
					break;
			}
		}
	}

	private FruitType GetFruitTypeFromLevel(int levelNumber)
	{
		switch (levelNumber)
		{
			case 1:
				return FruitType.Cherries;
			case 2:
				return FruitType.Strawberry;
			case 3:
			case 4:
				return FruitType.Peach;
			case 5:
			case 6:
				return FruitType.Apple;
			case 7:
			case 8:
				return FruitType.Grapes;
			case 9:
			case 10:
				return FruitType.Galaxian;
			case 11:
			case 12:
				return FruitType.Bell;
			default:
				return FruitType.Key;
		}
	}

	private void UpdateActorsSprites()
	{
		// pacman

		UpdatePacmanSprite();

		// ghosts

		foreach (Ghost g in ghosts)
		{
			UpdateGhostSprite(g);
		}
	}

	// score update display

	private void UpdateScore()
	{
		scoreText.Text = (score == 0) ? "00" : score.ToString();
		
		// Check if high score is beaten and update it immediately
		if (score > highScore)
		{
			highScore = score;
			highScoreText.Text = "Meilleur score: " + highScore.ToString();
			SaveHighScore();
			GD.Print("New high score updated: " + highScore);
		}
	}

	/* DEBUG */

	private void DrawGhostsPaths()
	{
		Color[] pathColors = new Color[4] { Color.Color8(255, 0, 0, 255), Color.Color8(252, 181, 255, 255), Color.Color8(0, 255, 255, 255), Color.Color8(248, 187, 85, 255) };
		int pathLineWidth = 2;

		for (int i = 0; i < 4; i++)
		{
			List<Vector2I> path = ghostsPaths[i];

			if (path.Count > 0)
			{
				for (int j = 0; j < path.Count - 1; j++)
				{
					Vector2I p1 = path[j];
					Vector2I p2 = path[j + 1];
					Vector2I pathDirection = p2 - p1;

					Vector2I pathLineSize = Vector2I.Zero;

					switch (pathDirection.X)
					{
						case 0:
							pathLineSize.X = pathLineWidth;
							break;
						case 1:
							pathLineSize.X = 8 + pathLineWidth;
							break;
						case -1:
							pathLineSize.X = -8;
							break;
					}

					switch (pathDirection.Y)
					{
						case 0:
							pathLineSize.Y = pathLineWidth;
							break;
						case 1:
							pathLineSize.Y = 8 + pathLineWidth;
							break;
						case -1:
							pathLineSize.Y = -8;
							break;
					}

					DrawRect(new Rect2I(p1 * 8 + new Vector2I(3, 3), pathLineSize), pathColors[i]);
				}

				DrawRect(new Rect2I(path[path.Count - 1] * 8 + Vector2I.One * ((8 - pathLineWidth * 2) >> 1), new Vector2I(pathLineWidth, pathLineWidth) * 2), pathColors[i]);
			}
		}
	}

	private void CalculateGhostsPaths()
	{
		for (int i = 0; i < 4; i++)
		{
			if (ghosts[i].DistanceToTileMid() == Vector2I.Zero)
			{
				ghosts[i].GetCurrentPath(ghostsPaths[i], 17);
			}
		}
	}

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		OS.SetLowProcessorUsageMode(false);
		// create triggers

		triggers.Add(dotEatenTrigger = new Trigger());
		triggers.Add(pillEatenTrigger = new Trigger(3));
		triggers.Add(readyStartedTrigger = new Trigger(Callable.From(() =>
		{
			InitRound();
			roundStartedTrigger.Start(2 * 60);
		})));
		triggers.Add(roundStartedTrigger = new Trigger(Callable.From(() =>
		{
			UnFreeze();

			StopSounds();
			sirenSound.Play();
		})));
		triggers.Add(roundWonTrigger = new Trigger(Callable.From(() =>
		{
			FreezeBy(FreezeType.Won);
			readyStartedTrigger.Start(roundWonFreezeTicks);

			StopSounds();
		})));
		triggers.Add(gameOverTrigger = new Trigger(Callable.From(() =>
		{
			DisableTriggers();
			SetFreezeTo(FreezeType.GameOver);
			StopSounds();
			
			// Make sure the game over sound is played
			if (gameOverSound != null && gameOverSound.Stream != null)
			{
				GD.Print("Playing game over sound");
				gameOverSound.VolumeDb = 0; // Reset volume to make sure it's audible
				gameOverSound.Play();
			}
			else
			{
				GD.PrintErr("Game over sound not available");
			}
			
			// Save high score
			if (score > highScore)
			{
				highScore = score;
				SaveHighScore();
				GD.Print("New high score saved: " + highScore);
			}
			
			resetTrigger.Start(3 * 60);
		})));
		triggers.Add(resetTrigger = new Trigger(Callable.From(() =>
		{
			Reset();
		})));
		triggers.Add(fruitActiveTrigger = new Trigger(fruitActiveTicks));
		triggers.Add(fruitEatenTrigger = new Trigger(2 * 60)); // show fruit score for 2 secs
		triggers.Add(pacmanEatenTrigger = new Trigger(pacmanDeathTicks));
		triggers.Add(ghostEatenUnFreezeTrigger = new Trigger(Callable.From(() =>
		{
			UnFreezeBy(FreezeType.EatGhost);
		})));

		for (int i = 0; i < 4; i++)
		{
			triggers.Add(ghostFrightenedTrigger[i] = new Trigger(GetGhostFrightenedTicks()));
			triggers.Add(ghostEatenTrigger[i] = new Trigger(ghostEatenFreezeTicks));
		}

		// get nodes

		scoreText = GetNode<Label>("Score");
		highScoreText = GetNode<Label>("HighScore");
		mazeSprite = GetNode<Sprite2D>("Maze");
		ghostDoorSprite = GetNode<ColorRect>("GhostDoor");

		munch1Sound = GetNode<AudioStreamPlayer>("Munch1Sound");
		munch2Sound = GetNode<AudioStreamPlayer>("Munch2Sound");
		fruitSound = GetNode<AudioStreamPlayer>("FruitSound");
		ghostEatenSound = GetNode<AudioStreamPlayer>("GhostEatenSound");
		sirenSound = GetNode<AudioStreamPlayer>("SirenSound");
		powerPelletSound = GetNode<AudioStreamPlayer>("PowerPelletSound");
		
		// Add new audio players
		pacmanDeathSound = new AudioStreamPlayer();
		gameOverSound = new AudioStreamPlayer();
		
		// Set audio streams (you'll need to add these files to your project)
		pacmanDeathSound.Stream = GD.Load<AudioStream>("res://Assets/Sounds/pacman_death.mp3");
		gameOverSound.Stream = GD.Load<AudioStream>("res://Assets/Sounds/game_over.mp3");
		
		// Add to scene tree
		AddChild(pacmanDeathSound);
		AddChild(gameOverSound);

		// Set the volume a bit higher than other sounds
		pacmanDeathSound.VolumeDb = 3.0f;
		gameOverSound.VolumeDb = 3.0f;

		// create pacman

		pacman = (Pacman)pacmanScene.Instantiate();
		AddChild(pacman);

		// create ghosts

		for (int i = 0; i < 4; i++)
		{
			ghosts[i] = (Ghost)ghostScene.Instantiate();
			ghosts[i].type = (Ghost.Type)i;
			AddChild(ghosts[i]);
			}

		// ghost paths

		for (int i = 0; i < 4; i++)
		{
			ghostsPaths[i] = new List<Vector2I>();
		}

		// reset state & set high score

		LoadHighScore();
		Reset();

		// hide mouse cursor

		DisplayServer.MouseSetMode(DisplayServer.MouseMode.Visible);

		GD.Print("Added simple control buttons");

		// Manually load the instructions texture
		try
		{
			 // Use the CORRECT path in both places
			string texturePath = "res://Assets/Sprites/Regles2.png";
			
			// Try to preload the texture to ensure it's ready
			ResourceLoader.LoadThreadedRequest(texturePath, "Texture2D");
			OS.DelayMsec(100); // Small delay to help with loading
			
			instructionsTexture = GD.Load<Texture2D>(texturePath);
			if (instructionsTexture != null) 
			{
				Vector2 size = instructionsTexture.GetSize();
				GD.Print("Instructions texture loaded successfully - size: " + size);
			} 
			else 
			{
				GD.PrintErr("Failed to load instructions texture from path: " + texturePath);
				// Try alternate path as fallback
				instructionsTexture = GD.Load<Texture2D>("res://Assets/Regles2.png");
				if (instructionsTexture != null)
					GD.Print("Loaded from alternate path instead");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("Error loading instructions texture: " + ex.Message);
		}

		// Create the instructions trigger - after this time elapses, normal game start occurs
		triggers.Add(instructionsStartedTrigger = new Trigger(Callable.From(() =>
		{
			GD.Print("Instructions timer finished");
			
			// Make sure we only unfreeze if still in instructions mode
			if (IsFrozenBy(FreezeType.Instructions))
			{
				GD.Print("Transitioning from instructions to game start");
				
				// Make actors visible again
				pacman.Visible = true;
				foreach (Ghost g in ghosts)
				{
					g.Visible = true;
				}
				
				// Clear instructions state and start game
				UnFreezeBy(FreezeType.Instructions);
				readyStartedTrigger.Start();
			}
		})));
	}

	// draw (for debug)

	private const float PILL_SCALE_FACTOR = 1.0f;  // Full size for pills

	public override void _Draw()
	{
		 // Check if we're in instructions mode FIRST
		if (showSplashScreen && instructionsTexture != null)
		{
			 // Step 1: Get viewport size
			Vector2 viewportSize = GetViewportRect().Size;
			
			// Step 2: Fill entire screen with black background
			DrawRect(new Rect2(0, 0, viewportSize.X, viewportSize.Y), Colors.Black);
			
			// Step 3: Create a TextureRect that doesn't rely on our drawing code
			TextureRect instructionsRect = new TextureRect();
			instructionsRect.Texture = instructionsTexture;
			instructionsRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			instructionsRect.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			instructionsRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
			instructionsRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			instructionsRect.Size = viewportSize;
			AddChild(instructionsRect);
			
			// Step 4: Add a timer to remove this when needed
			Timer cleanupTimer = new Timer();
			cleanupTimer.OneShot = true;
			cleanupTimer.WaitTime = 0.01;
			cleanupTimer.Timeout += () => {
				QueueRedraw(); // Force a redraw
				
				// Add prompt text over the image
				Label promptLabel = new Label();
				promptLabel.Text = "Tap or Press Any Key to Start";
				promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
				promptLabel.Position = new Vector2(viewportSize.X / 2 - 100, viewportSize.Y - 50);
				AddChild(promptLabel);
			};
			AddChild(cleanupTimer);
			cleanupTimer.Start();
			
			// Step 5: Replace EndSplashScreen to clean up our elements
			GetTree().CreateTimer(0.1).Timeout += () => {
				EndSplashScreenDelegate = () => {
					if (instructionsRect != null && IsInstanceValid(instructionsRect))
						instructionsRect.QueueFree();
					
					var promptLabel = GetNode<Label>("Label");
					if (promptLabel != null && IsInstanceValid(promptLabel))
						promptLabel.QueueFree();
						
					showSplashScreen = false;
					readyStartedTrigger.Start();
					pacman.Visible = true;
					foreach (Ghost g in ghosts)
						g.Visible = true;
				};
			};
			
			return;
		}

		// draw ghost paths

		// DrawGhostsPaths();

		// draw dots and pills

		for (int j = 0; j < Maze.Height; j++)
		{
			for (int i = 0; i < Maze.Width; i++)
			{
				switch (Maze.GetTile(new Vector2I(i, j)))
				{
					case Maze.Tile.Dot:
						// Create a properly sized rectangle for dots with centering
						float dotSize = Maze.TileSize * currentDotSizeMultiplier; // 3.2 pixels
						Vector2 dotOffset = new Vector2(
							(Maze.TileSize - dotSize) / 2,
							(Maze.TileSize - dotSize) / 2
						);
						
						// Position dot in the center of the tile
						Rect2 dotRect = new Rect2(
							new Vector2(i * Maze.TileSize, j * Maze.TileSize) + dotOffset, 
							new Vector2(dotSize, dotSize)
						);
						
						// Source size of 6 pixels (0.75 * 8)
						float sourceSize = Maze.TileSize * currentSourceSizeMultiplier;
						
						DrawTextureRectRegion(
							dotsTexture, 
							dotRect, 
							new Rect2(new Vector2(0, 0), new Vector2(sourceSize, sourceSize))
						);
						break;
						
					case Maze.Tile.Pill:
						if ((ticks & 8) != 0 || freeze != 0)
							{
								// Get current test values
								float xOffset = pillSourceXOffsets[pillSourceXIndex];
								float width = pillWidths[pillWidthIndex];
								float height = pillHeights[pillHeightIndex];
								
								// Position centered in tile
								float pillSize = Maze.TileSize;
								Vector2 pillOffset = new Vector2(
									(Maze.TileSize - pillSize) / 2,
									(Maze.TileSize - pillSize) / 2
								);
								
								// Create the destination rectangle - full size
								Rect2 pillRect = new Rect2(
									new Vector2(i * Maze.TileSize, j * Maze.TileSize),
									new Vector2(pillSize, pillSize)
								);
								
								// Create a source rectangle using test values
								DrawTextureRectRegion(
									dotsTexture,
									pillRect,
									new Rect2(
										new Vector2(xOffset, 0),  // Test different X positions
										new Vector2(width, height) // Test different widths and heights
									)
								);
								
								// Add visual indicator of the current test case
								if (j == 11 && i == 1)
								{
									DrawString(
										ThemeDB.FallbackFont,
										pillRect.Position + new Vector2(10, -10),
										$"{pillSourceXIndex},{pillWidthIndex},{pillHeightIndex}",
										HorizontalAlignment.Left,
										-1,
										12,
										Colors.Yellow
									);
								}
							}
						break;
				}
			}
		}

		// draw ready text

		if (IsFrozenBy(FreezeType.Ready))
		{
			DrawTexture(readyTextTexture, new Vector2I(89, 131));
		}

		// draw game over text

		if (IsFrozenBy(FreezeType.GameOver))
		{
			DrawTexture(gameOverTextTexture, new Vector2I(73, 131));
		}

		// maze animation when round won

		if (IsFrozenBy(FreezeType.Won))
		{
			int ticksSinceWon = roundWonTrigger.TicksSinceStarted();
			mazeSprite.SelfModulate = (ticksSinceWon & 16) != 0 ? new Color("417ae2") : new Color("ffffff");
			ghostDoorSprite.Visible = false;
		}

		// draw lifes

		for (int i = 0; i < numLifes; i++)
		{
			DrawTexture(lifeTexture, new Vector2I(16 + 16 * i, 248));
		}

		// draw the fruits that represent the level number

		int levelStart = level - 7 > 0 ? level - 7 : 0;

		for (int i = levelStart; i < level; i++)
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(i + 1);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(188 - 16 * (i - levelStart), 248), new Vector2I(24, 16)), new Rect2I(new Vector2I(0, fruitIndex * 16), new Vector2I(24, 16)));
		}

		// draw fruit

		if (fruitActiveTrigger.IsActive())
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(level);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(100, 132), new Vector2I(24, 16)), new Rect2I(new Vector2I(0, fruitIndex * 16), new Vector2I(24, 16)));
		}
		else if (fruitEatenTrigger.IsActive())
		{
			int fruitIndex = (int)GetFruitTypeFromLevel(level);
			DrawTextureRectRegion(fruitTexture, new Rect2I(new Vector2I(100, 132), new Vector2I(24, 16)), new Rect2I(new Vector2I(24, fruitIndex * 16), new Vector2I(24, 16)));
		}

		// Draw instructions if in instructions state
		if (IsFrozenBy(FreezeType.Instructions) && instructionsTexture != null)
		{
			// Get the viewport size to center the image
			Vector2 viewportSize = GetViewportRect().Size;
			Vector2 textureSize = instructionsTexture.GetSize();
			
			// Scale factor to fit the screen while maintaining aspect ratio
			float scaleFactor = Math.Min(
				viewportSize.X / textureSize.X,
				viewportSize.Y / textureSize.Y
			) * 0.9f;  // Scale to 90% of available space
			
			// Handle extremely large textures
			if (textureSize.X > 2000 || textureSize.Y > 2000)
			{
				// For very large textures, use a more aggressive scaling
				scaleFactor *= 0.5f;
				GD.Print("Large texture detected - using reduced scale: " + scaleFactor);
			}
			
			// Calculate centered position
			Vector2 position = new Vector2(
				(viewportSize.X - (textureSize.X * scaleFactor)) / 2,
				(viewportSize.Y - (textureSize.Y * scaleFactor)) / 2
			);
			
			// Draw the texture with scaling
			DrawTextureRectRegion(
				instructionsTexture,
				new Rect2(position, textureSize * scaleFactor),
				new Rect2(0, 0, textureSize.X, textureSize.Y)
			);
			
			// Optional: Make sure the instruction text is visible against the background
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(viewportSize.X / 2, viewportSize.Y - 30),
				"Press any key to start",
				HorizontalAlignment.Center,
				-1,
				16,
				Colors.White
			);
		}
	}

	// runs at 60 fps

	public override void _PhysicsProcess(double delta)
	{
		 // IMPORTANT: Skip normal game processing when showing splash screen
		if (showSplashScreen)
		{
			// Only increment timer and force redraw
			splashScreenTimer += (float)delta;
			QueueRedraw();
			return; // Skip all other processing
		}
		
		// toggle fullscreen

		if (Input.IsActionJustPressed("ToggleFullscreen"))
		{
			Window window = GetWindow();

			if (window.Mode != Window.ModeEnum.ExclusiveFullscreen)
			{
				window.Mode = Window.ModeEnum.ExclusiveFullscreen;
			}
			else
			{
				window.Mode = Window.ModeEnum.Windowed;
			}
		}

		// reset

		if (Input.IsActionJustPressed("Reset"))
		{
			Reset();
		}

		// update triggers

		foreach (Trigger t in triggers)
		{
			t.Tick(ticks);
		}

		// sound change from power pellet back to siren

		if (powerPelletSound.Playing)
		{
			bool changeToSiren = true;
			
			foreach (Ghost g in ghosts)
			{
				if (IsGhostFrightened(g))
				{
					changeToSiren = false;
					break;
				}
			}
			
			if (changeToSiren)
			{
				// IMPORTANT: Remove Pacman's hat when power effect ends
				pacman.SetPoweredUp(false);
				
				StopSounds();
				sirenSound.Play();
			}
		}

		// update actors if the game is not frozen

		if (!IsFrozen())
		{
			UpdateActors();
		}

		// update score

		UpdateScore();

		// update sprites

		UpdateActorsSprites();

		// debug ghost paths

		// CalculateGhostsPaths();

		// redraw

		QueueRedraw();

		// increment number of ticks

		ticks++;
		
		// Add this inside _PhysicsProcess right after ticks++
		if (IsFrozenBy(FreezeType.Instructions))
		{
			// Add this line to debug EVERY frame
			if (ticks % 30 == 0) // Log every half second
			{
				GD.Print($"Frame {ticks}: Still in instructions mode, freeze={freeze}, texture exists={instructionsTexture != null}");
			}
		}
	}
	public Vector2 LatestMousePos { get; private set; }

	// Replace _Process with this version
	public override void _Process(double delta)
	{
		// Handle splash screen logic
		if (showSplashScreen)
		{
			splashScreenTimer += (float)delta;
			
			// Force redraw every frame to ensure splash screen is visible
			QueueRedraw();
			
			// Only handle input to skip splash screen
			return;
		}
		
		// Normal game processing (your existing code)
		LatestMousePos = DisplayServer.MouseGetPosition();
	}

	// Replace _Input with this version
	public override void _Input(InputEvent @event)
	{
		// First handle splash screen skip
		if (showSplashScreen)
		{
			if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed ||
				@event is InputEventKey keyEvent && keyEvent.Pressed ||
				@event is InputEventScreenTouch touchEvent && touchEvent.Pressed)
			{
				GD.Print("Input received - ending splash screen");
				EndSplashScreen();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		
		// Handle game input (your existing code)
		if (IsFrozenBy(FreezeType.Instructions))
		{
			// This code won't be needed anymore
		}
	}

	// Add this helper method
	private void EndSplashScreen()
	{
		GD.Print("Ending splash screen NOW");
		
		showSplashScreen = false;
		
		// Start the game directly
		readyStartedTrigger.Start();
		
		// Make actors visible
		pacman.Visible = true;
		foreach (Ghost g in ghosts)
		{
			g.Visible = true;
		}
		
		// Force a redraw
		QueueRedraw();
	}

	// Add this delegate outside any method at class level
	private Action EndSplashScreenDelegate;
}
