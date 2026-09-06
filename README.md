# C# Raylib Pong 

A performance-focused Pong clone built from scratch in C# using the Raylib-cs framework. 

https://github.com/user-attachments/assets/d99809ed-3332-4ed5-bd9b-c6af91f51074

## 🧠 Project Overview
This project marks a transition from tutorial-based learning to independent, logic-driven development. Rather than relying on a heavy game engine like Unity, this game was built by manually managing the game loop, rendering pipeline, and physics state frame-by-frame using C# and Raylib.

### ⚙️ Core Technical Mechanics
* **Entity-Component Architecture:** Separated the "brain" (decision-making) from the "physics" (movement and collision) to ensure clean state management.
* **Custom AI Tracking:** Built a speed-deficit AI opponent that dynamically calculates paddle midpoints to track the ball's X-coordinate, intentionally throttled to allow player counter-play.
* **Math-Based Boundary Logic:** Implemented exact coordinate limiters (`&&` condition checks) to restrict paddle movement strictly within the rendering window without relying on expensive geometric collision calculations.
* **Custom Physics & State:** Engineered manual ball trajectory routing (X/Y velocity manipulation) and automated respawn triggers tied directly to out-of-bounds screen coordinates.

## 🚀 How to Run
1. Clone the repository.
2. Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed.
3. Open `PongGame.sln` in Visual Studio.
4. Restore NuGet packages (specifically `Raylib-cs` by MiniJack).
5. Build and Run.

**Developer:** Flamex



