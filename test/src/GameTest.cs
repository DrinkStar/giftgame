namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Integration test for the 3D game scene (src/Game.tscn): the scene must
///   load cleanly and contain the player node used by the controller cluster.
/// </summary>
public class GameTest : TestClass
{
  private Game _game = default!;
  private Fixture _fixture = default!;

  public GameTest(Node testScene) : base(testScene) { }

  [SetupAll]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _game = await _fixture.LoadAndAddScene<Game>();
  }

  [CleanupAll]
  public void Cleanup() => _fixture.Cleanup();

  [Test]
  public void GameSceneLoadsWithRootNode()
  {
    _game.ShouldNotBeNull();
    _game.ShouldBeOfType<Game>();
    _game.ShouldBeAssignableTo<Node3D>();
  }

  [Test]
  public void PlayerNodeExists()
  {
    var player = _game.GetNode("Player");
    player.ShouldNotBeNull();
    player.ShouldBeAssignableTo<CharacterBody3D>();
  }
}
