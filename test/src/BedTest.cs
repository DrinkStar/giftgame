// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8p.4 (iter8p-plan Decision 6): the bed. Interacting sleeps until
///   morning — SetTime(6) jumps the clock to dawn WITHOUT rolling the day
///   (SetTime does not increment CurrentDay, intended), stamina refills to
///   full, and the respawn slot moves to the bed (exposed for tests through
///   <see cref="GameManager.RespawnSlot"/>). RespawnPlayer prefers the slot
///   over the spawn marker.
/// </summary>
public class BedTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerController _player = default!;
  private PlayerStats _stats = default!;
  private GameManager _manager = default!;
  private DayNightService _dayNight = default!;

  public BedTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Sweep leaked scene nodes from earlier test classes (see
    // QuestServiceTest/GroundLootTest — leaked Game scenes keep their
    // GameManager subscribed to the static PlayerDied bus).
    SweepLeakedNodes(TestScene.GetTree().Root);

    // The bed resolves GameManager via GetTree().CurrentScene — the manager
    // must live under the current scene. A leaked scene may already own a
    // "GameManager" sibling name, so remove it first (BedInteract looks the
    // name up and must reach OUR manager, not a stale one).
    var scene = TestScene.GetTree().CurrentScene;
    var stale = scene.GetNodeOrNull("GameManager");
    if (stale != null)
    {
      scene.RemoveChild(stale);
      stale.Free();
    }

    _player = new PlayerController { Name = "Player" };
    _stats = new PlayerStats { Name = "PlayerStats" };
    _player.AddChild(_stats);
    _player.Stats = _stats;

    await _fixture.AddToRoot(_player, autoRemoveFromRoot: true);

    // DayNightService stays OUT of the tree on purpose: SetTime/CurrentHour
    // work standalone and no _Process tick can drift the hour between the
    // Interact call and the assertion.
    _dayNight = new DayNightService();

    // C#-constructed nodes default to an empty Name (Godot renames them to
    // "@Node@N" on AddChild) — the bed looks the manager up BY NAME under
    // the current scene, so the fixture must name it explicitly (the real
    // Game.tscn names it "GameManager").
    _manager = new GameManager
    {
      Name = "GameManager",
      Player = _player,
      DayNightService = _dayNight
    };
    scene.AddChild(_manager);
  }

  [Cleanup]
  public void Cleanup()
  {
    if (_manager != null && _manager.IsInsideTree())
      _manager.GetParent()!.RemoveChild(_manager);
    if (_player != null && _player.IsInsideTree())
      _player.GetParent()!.RemoveChild(_player);

    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it
  ///   so the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    _manager?.Dispose();
    _manager = null!;
    _dayNight?.Dispose();
    _dayNight = null!;

    if (_player == null)
      return;

    _player.Dispose();
    _player = null!;
    GC.SuppressFinalize(this);
  }

  /// <summary>
  ///   Removes and frees leftover nodes from earlier test classes (same
  ///   contract as GroundLootTest.SweepLeakedNodes): everything except the
  ///   test runner scene and the current scene is test garbage.
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

  private async Task<BedInteract> AddBedAt(Vector3 position)
  {
    var bed = new BedInteract { Name = "Bed" };
    await _fixture.AddToRoot(bed, autoRemoveFromRoot: true);
    bed.GlobalPosition = position;
    return bed;
  }

  /// <summary>
  ///   T8p.4 (a): sleeping jumps the clock to dawn (06:00) without rolling
  ///   the day — SetTime moves the hour but never increments CurrentDay.
  /// </summary>
  [Test]
  public async Task InteractSetsTimeToDawn()
  {
    var bed = await AddBedAt(new Vector3(4, 0, 4));
    _dayNight.SetTime(23f);
    _dayNight.CurrentHour.ShouldBe(23f);

    bed.Interact(_player);

    _dayNight.CurrentHour.ShouldBe(6f);
    // No day roll: SetTime does not increment CurrentDay, intended.
    _dayNight.CurrentDay.ShouldBe(1);
  }

  /// <summary>
  ///   T8p.4 (b): sleeping refills stamina to full.
  /// </summary>
  [Test]
  public async Task InteractRefillsStamina()
  {
    var bed = await AddBedAt(new Vector3(4, 0, 4));
    _stats.Stamina = 10f;

    bed.Interact(_player);

    _stats.Stamina.ShouldBe(_stats.StaminaMax);
  }

  /// <summary>
  ///   T8p.4 (c): sleeping moves GameManager's respawn slot to the bed
  ///   position plus the +1 Y anti-collider offset (read through the public
  ///   RespawnSlot getter).
  /// </summary>
  [Test]
  public async Task InteractMovesRespawnSlotToBed()
  {
    var bed = await AddBedAt(new Vector3(6, 0.5f, -2));

    bed.Interact(_player);

    _manager.RespawnSlot.ShouldNotBeNull();
    _manager.RespawnSlot!.Value.X.ShouldBe(bed.GlobalPosition.X, 0.001);
    // +Y offset lifts the revived player above the bed's generated collider.
    _manager.RespawnSlot!.Value.Y.ShouldBe(bed.GlobalPosition.Y + 1f, 0.001);
    _manager.RespawnSlot!.Value.Z.ShouldBe(bed.GlobalPosition.Z, 0.001);
  }

  /// <summary>
  ///   T8p.4 (c): the full death → respawn chain teleports the player to
  ///   the bed slot (not the spawn marker) and revives it.
  /// </summary>
  [Test]
  public async Task RespawnTeleportsToBedSlot()
  {
    var slot = new Vector3(7, 1.5f, 7);
    _manager.SetRespawnPoint(slot);
    _manager.RespawnSlot.ShouldBe(slot);

    _player.GlobalPosition = new Vector3(0, 0, 0);

    _stats.TakeDamage(1000f);
    await TestScene.ProcessFrame(2);

    _stats.IsAlive.ShouldBeTrue();
    // Teleport to the bed slot: X/Z land exactly; Y drifts by a few physics
    // gravity steps as the revived player falls from the slot (same contract
    // as PlayerRespawnTest).
    _player.GlobalPosition.X.ShouldBe(slot.X, 0.001);
    _player.GlobalPosition.Z.ShouldBe(slot.Z, 0.001);
    _player.GlobalPosition.Y.ShouldBe(slot.Y, 0.5);
  }
}
