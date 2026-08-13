namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Behavioral tests that simulate player input at the engine level and
///   drive <see cref="PlayerController._PhysicsProcess"/> directly, verifying
///   the full input → motion pipeline (project.godot input map → controller →
///   PlayerMotion) without a manual play session.
/// </summary>
public class PlayerInputSimulationTest : TestClass
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;

  public PlayerInputSimulationTest(Node testScene) : base(testScene) { }

  /// <summary>
  ///   Loads a fresh Game scene per test so every test starts from the same
  ///   initial player state (spawned at (0, 2, 0), zero velocity).
  /// </summary>
  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    var game = await _fixture.LoadAndAddScene<Game>();
    _player = game.GetNode<PlayerController>("Player");
  }

  [Cleanup]
  public void Cleanup()
  {
    ReleaseMoveForward();
    _fixture.Cleanup();
  }

  [Test]
  public void HoldingMoveForwardProducesHorizontalVelocity()
  {
    InjectMoveForward(pressed: true);

    _player._PhysicsProcess(1.0 / 60.0);

    // The player spawns airborne at (0, 2, 0), so only the horizontal
    // component of the velocity is asserted.
    var horizontalVelocity = _player.Velocity with { Y = 0f };
    horizontalVelocity.Length().ShouldBeGreaterThan(0.1f);
  }

  [Test]
  public void NoInputKeepsHorizontalVelocityAtZero()
  {
    // Defensive reset: the global input state is shared between tests, so
    // make sure no movement action is still marked pressed.
    ReleaseMoveForward();

    for (var tick = 0; tick < 5; tick++)
    {
      _player._PhysicsProcess(1.0 / 60.0);
    }

    var horizontalVelocity = _player.Velocity with { Y = 0f };
    horizontalVelocity.Length().ShouldBe(0f, 0.001);
  }

  private static void InjectMoveForward(bool pressed)
  {
    Input.ParseInputEvent(new InputEventAction
    {
      Action = PlayerController.MoveForwardAction,
      Pressed = pressed
    });

    // Godot 4 accumulates parsed input and only applies it at the next
    // main-loop flush — force it through so the action state is updated
    // before we drive the physics tick.
    Input.FlushBufferedEvents();
  }

  private static void ReleaseMoveForward() => InjectMoveForward(pressed: false);
}
