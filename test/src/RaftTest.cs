// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8.5.2 (iter8.5): raft modes (paddle → sail → anchor), code-registered
///   input actions, and the player's raft-platform velocity inheritance.
///   Rafts are instantiated from res://scenes/raft/raft.tscn un-wired (no
///   WaterMesh), so these tests also exercise _Ready's default cells and the
///   null-provider safety path (base buoyancy early-returns, no crash).
/// </summary>
public class RaftTest : TestClass, IDisposable
{
  private const string RaftScenePath = "res://scenes/raft/raft.tscn";

  private Fixture _fixture = default!;
  private Raft _raft = default!;

  public RaftTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    // Sweep leaked scene nodes from earlier test classes (BedTest pattern):
    // leftover physics bodies at the root would pollute the raft-carry
    // downward probe (it can hit a stale body before the raft).
    SweepLeakedNodes(TestScene.GetTree().Root);

    // No test may leave the raft actions pressed (global input state leaks
    // between tests otherwise).
    InjectInput(RaftInput.AnchorAction, pressed: false);
    InjectInput(RaftInput.ModeAction, pressed: false);

    _fixture = new Fixture(TestScene.GetTree());
    _raft = GD.Load<PackedScene>(RaftScenePath).Instantiate<Raft>();
    await _fixture.AddToRoot(_raft, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    InjectInput(RaftInput.AnchorAction, pressed: false);
    InjectInput(RaftInput.ModeAction, pressed: false);
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node field satisfies CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_raft == null)
      return;

    _raft.Dispose();
    _raft = null!;
    GC.SuppressFinalize(this);
  }

  private static void InjectInput(string action, bool pressed)
  {
    Input.ParseInputEvent(new InputEventAction
    {
      Action = action,
      Pressed = pressed
    });
    // Events are applied at the next main-loop flush — force them through so
    // the action state is updated before the manual physics tick.
    Input.FlushBufferedEvents();
  }

  /// <summary>
  ///   T8.5.2 (1): defaults — Paddle mode, 5 default cells, 3×0.3×2 body —
  ///   and un-wired physics ticks (no WaterMesh, no input) do not crash and
  ///   do not move the raft.
  /// </summary>
  [Test]
  public void DefaultsToPaddleWithDefaultCellsAndSurvivesUnwiredPhysics()
  {
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);
    _raft.Cells.Length.ShouldBe(5);
    _raft.BodySize.ShouldBe(new Vector3(3f, 0.3f, 2f));
    _raft.WaveHeightProvider.ShouldBeNull();
    _raft.Freeze.ShouldBeFalse();

    // Two physics ticks with no wave provider and no input: no crash, no
    // mode change, and the raft is not frozen.
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);
    _raft.Freeze.ShouldBeFalse();
  }

  /// <summary>
  ///   T8.5.2 (2): Anchored freezes the body (and zeroes leftover velocity
  ///   on entry); returning to Paddle unfreezes it.
  /// </summary>
  [Test]
  public void AnchoredFreezesAndReturningToPaddleUnfreezes()
  {
    _raft.LinearVelocity = new Vector3(2f, 0f, 0f);
    _raft.Mode = Raft.RaftMode.Anchored;
    _raft._PhysicsProcess(1.0 / 60.0);

    _raft.Freeze.ShouldBeTrue();
    _raft.LinearVelocity.ShouldBe(Vector3.Zero);

    _raft.Mode = Raft.RaftMode.Paddle;
    _raft._PhysicsProcess(1.0 / 60.0);

    _raft.Freeze.ShouldBeFalse();
  }

  /// <summary>
  ///   T8.5.2 (3): the M key cycles Paddle → Sail → Paddle. Each press is
  ///   released before the next so the just-pressed edge is re-armed.
  /// </summary>
  [Test]
  public void ModeKeyCyclesPaddleAndSail()
  {
    InjectInput(RaftInput.ModeAction, pressed: true);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Sail);

    InjectInput(RaftInput.ModeAction, pressed: false);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Sail);

    InjectInput(RaftInput.ModeAction, pressed: true);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);

    InjectInput(RaftInput.ModeAction, pressed: false);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);
  }

  /// <summary>
  ///   T8.5.2 (3): the G key toggles the anchor (Paddle → Anchored → Paddle),
  ///   freezing the body while anchored.
  /// </summary>
  [Test]
  public void AnchorKeyTogglesAnchored()
  {
    InjectInput(RaftInput.AnchorAction, pressed: true);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Anchored);
    _raft.Freeze.ShouldBeTrue();

    InjectInput(RaftInput.AnchorAction, pressed: false);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Anchored);

    InjectInput(RaftInput.AnchorAction, pressed: true);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);
    _raft.Freeze.ShouldBeFalse();

    InjectInput(RaftInput.AnchorAction, pressed: false);
    _raft._PhysicsProcess(1.0 / 60.0);
    _raft.Mode.ShouldBe(Raft.RaftMode.Paddle);
  }

  /// <summary>
  ///   T8.5.2 (4): RaftInput registration is idempotent — Raft._Ready already
  ///   registered the actions, so calling again must not throw, and both
  ///   actions exist in the input map.
  /// </summary>
  [Test]
  public void RegisterInputActionsIsIdempotent()
  {
    RaftInput.RegisterInputActions();
    RaftInput.RegisterInputActions();

    InputMap.HasAction(RaftInput.AnchorAction).ShouldBeTrue();
    InputMap.HasAction(RaftInput.ModeAction).ShouldBeTrue();
  }

  /// <summary>
  ///   T8.5.2 (5): the raft-carry detection seam
  ///   (<see cref="PlayerController.ResolveRaftCarry"/>) reports the raft's
  ///   horizontal velocity when the raft is under the player's feet (the
  ///   0.5 m downward probe reaches the deck) and zero when nothing is below.
  ///   The probe needs a physics space and real collision shapes, but not a
  ///   full physics integration — deterministic and independent of
  ///   PlayerMotion/MoveAndSlide behaviour.
  /// </summary>
  [Test]
  public async Task RaftCarryDetectsRaftBelowFeet()
  {
    var player = new PlayerController { Name = "Player", Gravity = 0f };
    player.Position = new Vector3(0f, 1.25f, 0f); // feet at 0.25 — probe reaches the deck (top 0.15)
    player.AddChild(
      new CollisionShape3D
      {
        Shape = new CapsuleShape3D { Radius = 0.5f, Height = 2f }
      }
    );
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    // No raft under the feet yet → no carry.
    player.ResolveRaftCarry().ShouldBe(Vector3.Zero);

    _raft.LinearVelocity = new Vector3(3f, 0f, 0f);
    player.ResolveRaftCarry().ShouldBe(new Vector3(3f, 0f, 0f));
  }

  /// <summary>T8.5.2 (5): a player far above the deck gets no carry.</summary>
  [Test]
  public async Task RaftCarryIsZeroWhenRaftIsFarBelow()
  {
    var player = new PlayerController { Name = "Player", Gravity = 0f };
    player.Position = new Vector3(0f, 3f, 0f); // feet at 2.0 — outside the 0.5 m probe
    player.AddChild(
      new CollisionShape3D
      {
        Shape = new CapsuleShape3D { Radius = 0.5f, Height = 2f }
      }
    );
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    _raft.LinearVelocity = new Vector3(3f, 0f, 0f);
    player.ResolveRaftCarry().ShouldBe(Vector3.Zero);
  }

  /// <summary>
  ///   Removes every node under <paramref name="root"/> except the test scene
  ///   and its current scene (BedTest/QuestServiceTest pattern) — leaked
  ///   physics bodies would otherwise pollute the carry probe.
  /// </summary>
  private void SweepLeakedNodes(Node root)
  {
    var current = TestScene.GetTree().CurrentScene;
    foreach (var child in root.GetChildren())
    {
      if (ReferenceEquals(child, TestScene) || ReferenceEquals(child, current))
        continue;

      if (child.Name == "Main")
        continue;

      root.RemoveChild(child);
      child.Free();
    }
  }
}
