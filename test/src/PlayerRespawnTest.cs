// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T7.0 respawn contract (iter7-plan, frozen in iter6-code-review):
///   PlayerDied is a recoverable character-death hook — NOT game over.
///   Death stops all input (controller + weapon system), GameManager defers
///   a RespawnPlayer that teleports the player to the spawn marker and
///   revives it via <see cref="PlayerStats.Revive"/>; GameOver is never
///   raised, and the player can die again after reviving.
/// </summary>
public class PlayerRespawnTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerStats _stats = default!;

  public PlayerRespawnTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _stats = new PlayerStats();
    _fixture.AddToRoot(_stats, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable <see cref="PlayerStats"/> field satisfies CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_stats == null)
      return;

    _stats.Dispose();
    _stats = null!;
    GC.SuppressFinalize(this);
  }

  private static Godot.Collections.Dictionary BuildEntry(string id, int amount) =>
    new()
    {
      ["item"] = GD.Load<ItemData>($"res://assets/items/{id}.tres"),
      ["amount"] = amount
    };

  private static void InjectMoveForward(bool pressed)
  {
    Input.ParseInputEvent(new InputEventAction
    {
      Action = PlayerController.MoveForwardAction,
      Pressed = pressed
    });

    // Godot 4 accumulates parsed input and only applies it at the next
    // main-loop flush — force it through before driving the physics tick.
    Input.FlushBufferedEvents();
  }

  /// <summary>
  ///   T7.0 (a): Revive on a dead, drained, slowed player restores full
  ///   health/stamina, raises hunger/thirst to the 30-point safety line and
  ///   clears the movement slow.
  /// </summary>
  [Test]
  public void ReviveRestoresFullStatsAndClearsDeath()
  {
    _stats.TakeDamage(1000f);
    _stats.Health.ShouldBe(0f);
    _stats.IsAlive.ShouldBeFalse();

    _stats.Hunger = 10f;
    _stats.Thirst = 5f;
    _stats.DrainStamina(1000f);
    _stats.ApplySlow(5f, 0.5f);

    _stats.Revive();

    _stats.IsAlive.ShouldBeTrue();
    _stats.Health.ShouldBe(_stats.MaxHealth);
    _stats.Stamina.ShouldBe(_stats.StaminaMax);
    _stats.Hunger.ShouldBeGreaterThanOrEqualTo(30f);
    _stats.Thirst.ShouldBeGreaterThanOrEqualTo(30f);
    _stats.SpeedMultiplier.ShouldBe(1f);
  }

  /// <summary>
  ///   T7.0 (b): PlayerDied fires exactly once per death (upstream bug fix
  ///   still holds) and fires AGAIN after Revive — dying is recoverable, not
  ///   a one-shot terminal state.
  /// </summary>
  [Test]
  public void PlayerDiedFiresOncePerDeathAndAgainAfterRevive()
  {
    var deathCount = 0;
    Action onDeath = () => deathCount++;
    GameEvents.PlayerDied += onDeath;
    try
    {
      _stats.TakeDamage(200f);
      _stats.TakeDamage(200f);
      deathCount.ShouldBe(1);

      _stats.Revive();
      _stats.IsAlive.ShouldBeTrue();

      _stats.TakeDamage(200f);
      deathCount.ShouldBe(2);
    }
    finally
    {
      GameEvents.PlayerDied -= onDeath;
    }
  }

  /// <summary>
  ///   T7.0 (c): a dead player produces zero velocity even with movement
  ///   input held — the controller gate stops the full movement pipeline.
  /// </summary>
  [Test]
  public async Task DeadPlayerIgnoresMovementInput()
  {
    var game = await _fixture.LoadAndAddScene<Game>();
    try
    {
      var player = game.GetNode<PlayerController>("Player");
      var stats = player.GetNode<PlayerStats>("PlayerStats");

      stats.TakeDamage(1000f);
      stats.IsAlive.ShouldBeFalse();

      InjectMoveForward(pressed: true);
      player._PhysicsProcess(1.0 / 60.0);

      player.Velocity.Length().ShouldBe(0f);
    }
    finally
    {
      InjectMoveForward(pressed: false);
    }
  }

  /// <summary>
  ///   T7.0 (d): death must NOT raise GameOver — GameOver stays reserved for
  ///   the true ending (post-shark-king epilogue) in a future iteration.
  /// </summary>
  [Test]
  public async Task GameManagerDoesNotRaiseGameOverOnDeath()
  {
    var player = new PlayerController { Name = "Player" };
    player.AddChild(new PlayerStats { Name = "PlayerStats" });
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    var spawn = new Marker3D { Name = "Spawn" };
    await _fixture.AddToRoot(spawn, autoRemoveFromRoot: true);

    var manager = new GameManager { Player = player, PlayerSpawnPoint = spawn };
    await _fixture.AddToRoot(manager, autoRemoveFromRoot: true);

    var gameOverCount = 0;
    Action onGameOver = () => gameOverCount++;
    GameEvents.GameOver += onGameOver;
    try
    {
      GameEvents.RaisePlayerDied();
      await TestScene.ProcessFrame(2);

      gameOverCount.ShouldBe(0);
    }
    finally
    {
      GameEvents.GameOver -= onGameOver;
    }
  }

  /// <summary>
  ///   T7.0 (e): the full death → respawn chain (Stats setter → PlayerDied →
  ///   GameManager deferred RespawnPlayer) teleports the player to the spawn
  ///   marker and revives it with the T7.0 stat guarantees.
  /// </summary>
  [Test]
  public async Task RespawnTeleportsToSpawnPointAndRevives()
  {
    var player = new PlayerController { Name = "Player" };
    player.AddChild(new PlayerStats { Name = "PlayerStats" });
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    var spawn = new Marker3D { Name = "Spawn" };
    await _fixture.AddToRoot(spawn, autoRemoveFromRoot: true);
    spawn.Position = new Vector3(10, 1.5f, 10);

    var manager = new GameManager { Player = player, PlayerSpawnPoint = spawn };
    await _fixture.AddToRoot(manager, autoRemoveFromRoot: true);

    player.GlobalPosition = new Vector3(5, 0, 5);

    var stats = player.GetNode<PlayerStats>("PlayerStats");
    stats.TakeDamage(1000f);
    stats.Hunger = 5f;
    stats.Thirst = 5f;

    await TestScene.ProcessFrame(2);

    // The teleport happened: X/Z land exactly on the marker; Y drifts by a
    // few physics-frame gravity steps because the revived player falls from
    // the marker until it reaches the ground.
    player.GlobalPosition.X.ShouldBe(spawn.GlobalPosition.X, 0.001);
    player.GlobalPosition.Z.ShouldBe(spawn.GlobalPosition.Z, 0.001);
    player.GlobalPosition.Y.ShouldBe(spawn.GlobalPosition.Y, 0.5);
    stats.IsAlive.ShouldBeTrue();
    stats.Health.ShouldBe(stats.MaxHealth);
    stats.Stamina.ShouldBe(stats.StaminaMax);
    // Revive raised both to exactly 30; the remaining frames drained a few
    // 1/30-units, so assert the safety line with a small tolerance.
    stats.Hunger.ShouldBe(30f, 0.1);
    stats.Thirst.ShouldBe(30f, 0.1);
  }

  /// <summary>
  ///   T7.0: the weapon system gate blocks attack input while the parent
  ///   player is dead — a spear throw must NOT consume the held spear.
  /// </summary>
  [Test]
  public void WeaponSystemIgnoresAttackInputWhilePlayerIsDead()
  {
    var player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    player.AddChild(stats);
    player.Stats = stats;

    var inventory = new InventorySystem
    {
      Name = "InventorySystem",
      StartingItems = new Godot.Collections.Array<Godot.Collections.Dictionary>
      {
        BuildEntry("wooden_spear", 1)
      }
    };
    player.AddChild(inventory);

    var weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem"
    };
    player.AddChild(weapon);

    _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    stats.TakeDamage(1000f);
    stats.IsAlive.ShouldBeFalse();

    weapon._UnhandledInput(new InputEventAction
    {
      Action = WeaponSystem.SecondaryAttackAction,
      Pressed = true
    });

    inventory.HasItem("wooden_spear", 1).ShouldBeTrue();
  }

  /// <summary>
  ///   FIX(code-review P2-07): the T7.0 death gate also covers the hotbar —
  ///   while the player is dead, number-key / wheel selection must not switch
  ///   the equipped slot (mirrors WeaponSystem's own gate).
  /// </summary>
  [Test]
  public void InventorySystemIgnoresHotbarInputWhilePlayerIsDead()
  {
    var player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    player.AddChild(stats);
    player.Stats = stats;
    var inventory = new InventorySystem { Name = "InventorySystem" };
    player.AddChild(inventory);
    _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    inventory.SelectedHotbarSlot = 0;

    stats.TakeDamage(1000f);
    stats.IsAlive.ShouldBeFalse();

    // Wheel-down while dead must NOT move the selection.
    inventory._Input(new InputEventMouseButton
    {
      ButtonIndex = MouseButton.WheelDown,
      Pressed = true
    });
    inventory.SelectedHotbarSlot.ShouldBe(0);

    // Revive → input works again.
    stats.Revive();
    inventory._Input(new InputEventMouseButton
    {
      ButtonIndex = MouseButton.WheelDown,
      Pressed = true
    });
    inventory.SelectedHotbarSlot.ShouldBe(1);
  }

  /// <summary>
  ///   FIX(code-review P2-07): the T7.0 death gate also covers interaction —
  ///   while the player is dead, the E-key ray/interact path is skipped and
  ///   any held target is cleared (no prompt/progress leaks past death).
  /// </summary>
  [Test]
  public async Task PlayerInteractionClearsTargetWhilePlayerIsDead()
  {
    var player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    player.AddChild(stats);
    player.Stats = stats;
    var cameraPivot = new Node3D { Name = "CameraPivot" };
    player.AddChild(cameraPivot);
    var camera = new Camera3D { Name = "Camera3D" };
    cameraPivot.AddChild(camera);
    var interaction = new PlayerInteraction
    {
      Name = "PlayerInteraction",
      CameraPath = "../CameraPivot/Camera3D"
    };
    player.AddChild(interaction);
    await _fixture.AddToRoot(player, autoRemoveFromRoot: true);

    // Any target the previous frame may have found is dropped on death.
    stats.TakeDamage(1000f);
    stats.IsAlive.ShouldBeFalse();

    interaction._Process(1.0 / 60.0);

    interaction.CurrentPrompt.ShouldBe("");
    interaction.CurrentProgress.ShouldBe(0f);
  }
}
