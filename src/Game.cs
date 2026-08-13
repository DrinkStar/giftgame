namespace SeaAnomaly;

using Godot;

/// <summary>
///   Root of the 3D game scene (src/Game.tscn). The scene owns the world
///   environment, sun, ground, and player; gameplay behavior lives in the
///   child nodes, so this class stays minimal.
/// </summary>
public partial class Game : Node3D
{
}
