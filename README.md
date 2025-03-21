# PacMatsa

A modern Pacman clone built with Godot Engine 4.4, featuring both desktop and mobile support.

<img src="Assets/Logo/logo.png" width="200" alt="PacMatsa Logo">

## 🎮 Game Features

- Classic Pacman gameplay with a modern twist
- Fully responsive controls (keyboard and touch screen support)
- Smooth swipe controls for mobile devices
- Multiple ghosts with unique AI behaviors
- Power-ups and special items
- Classic arcade-style visuals and sound effects

## 📱 Supported Platforms

- Windows Desktop
- Android
- Web (HTML5)

## 🚀 Installation

### Desktop
1. Download the latest release from the [Releases](https://github.com/Mustafaiqbal2/GodotPacman/releases) page
2. Extract the zip file
3. Run the executable file

### Android
1. Download the APK from the [Releases](https://github.com/Mustafaiqbal2/GodotPacman/releases) page
2. Install the APK on your Android device
3. Enjoy the game!

### Building from Source
1. Clone the repository: `git clone https://github.com/Mustafaiqbal2/GodotPacman.git`
2. Open the project in Godot Engine 4.4 or later
3. Export the project for your desired platform

## 🕹️ Controls

### Desktop
- **Arrow Keys / WASD**: Move Pacman
- **P**: Pause the game
- **ESC**: Exit/Back

### Mobile
- **Swipe**: Swipe in any direction to move Pacman
- **Tap**: Various UI interactions

## 🛠️ Technical Details

### Built With
- [Godot Engine 4.4](https://godotengine.org/)
- C# (Mono) for gameplay logic
- GDScript for UI and utilities

### Architecture
The game is structured with the following main components:

- **Game**: Main game scene controller
- **Pacman**: Player character with movement and collision logic
- **Ghost**: Enemy AI with different behavior patterns
- **Maze**: Level layout and dot/power-up placement
- **SwipeDetector**: Custom touch input handling for mobile devices

## 👨‍💻 Development

### Requirements
- Godot Engine 4.4 or later
- .NET SDK 6.0 or later (for C# development)
- Android SDK (for Android builds)

### Project Structure
- Scenes: Main game scenes
- Scripts: C# and GDScript code
- Assets: Game assets (sprites, sounds, fonts)
- android: Android-specific build files

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgements

- Original Pacman game by Namco
- [Godot Engine](https://godotengine.org/) development team
- Arcade font by [FontSpace](https://www.fontspace.com/arcade-classic-font-f3284)