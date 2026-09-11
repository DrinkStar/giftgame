// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Tests for the shark king 3-phase boss fight (Iter6.1 todo 4): pure
///   phase thresholds and minion-spawn gating in <see cref="CombatLogic"/>,
///   the phase-3 rage multipliers, the <see cref="BossPhaseController"/>
///   transition event (raised exactly once per transition), and the
///   shark_king.tres boss asset.
/// </summary>
public class BossPhaseTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private EnemyBase _enemy = default!;
  private BossPhaseController _controller = default!;

  public BossPhaseTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/enemy.tscn");
    packed.ShouldNotBeNull();
    _enemy = packed!.Instantiate<EnemyBase>();
    _enemy.Name = "TestBoss";
    _fixture.AddToRoot(_enemy, autoRemoveFromRoot: true);

    _controller = new BossPhaseController { BossId = "shark_king" };
    _enemy.AddChild(_controller);
    // Idempotent: sets the boss reference even if the tree has not called
    // _Ready on the controller yet.
    _controller._Ready();
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose()
  {
    if (_enemy == null)
      return;

    _enemy.Dispose();
    _enemy = null!;
    GC.SuppressFinalize(this);
  }

  private static EnemyData CreateSharkKingData() =>
    new()
    {
      Id = "shark_king",
      MaxHealth = 400f,
      Damage = 36f,
      MoveSpeed = 7f,
      AttackRange = 3.8f,
      AttackCooldown = 1.1f,
      Behavior = EnemyBehavior.MeleeChase,
      Boss = true,
      Scale = 2f
    };

  [Test]
  public void BossPhase_AboveSeventyPercent_ReturnsPhaseOne()
  {
    CombatLogic.BossPhase(0.75f).ShouldBe(1);
    CombatLogic.BossPhase(1f).ShouldBe(1);
    CombatLogic.BossPhase(0.71f).ShouldBe(1);
  }

  [Test]
  public void BossPhase_BetweenFortyAndSeventy_ReturnsPhaseTwo()
  {
    CombatLogic.BossPhase(0.55f).ShouldBe(2);
    // The 70% boundary itself belongs to phase 2 (phase 1 is strictly > 70%).
    CombatLogic.BossPhase(0.7f).ShouldBe(2);
    CombatLogic.BossPhase(0.41f).ShouldBe(2);
  }

  [Test]
  public void BossPhase_AtOrBelowForty_ReturnsPhaseThree()
  {
    CombatLogic.BossPhase(0.15f).ShouldBe(3);
    // The 40% boundary itself belongs to phase 3 (phase 2 is strictly > 40%).
    CombatLogic.BossPhase(0.4f).ShouldBe(3);
    CombatLogic.BossPhase(0f).ShouldBe(3);
  }

  [Test]
  public void ShouldSpawnMinion_ElapsedBelowInterval_ReturnsFalse()
  {
    CombatLogic.ShouldSpawnMinion(5.9f, 6f, 0, 5).ShouldBeFalse();
    CombatLogic.ShouldSpawnMinion(0f, 6f, 0, 5).ShouldBeFalse();
  }

  [Test]
  public void ShouldSpawnMinion_TimerReadyUnderAliveCap_ReturnsTrue()
  {
    CombatLogic.ShouldSpawnMinion(6f, 6f, 0, 5).ShouldBeTrue();
    CombatLogic.ShouldSpawnMinion(15f, 6f, 4, 5).ShouldBeTrue();
  }

  [Test]
  public void ShouldSpawnMinion_AliveAtOrAboveCap_ReturnsFalse()
  {
    CombatLogic.ShouldSpawnMinion(6f, 6f, 5, 5).ShouldBeFalse();
    CombatLogic.ShouldSpawnMinion(99f, 6f, 6, 5).ShouldBeFalse();
  }

  [Test]
  public void BossRage_PhaseThree_ReturnsRageMultipliers()
  {
    var rage = CombatLogic.BossRage(3);
    rage.SpeedMultiplier.ShouldBe(2.2f);
    rage.DamageMultiplier.ShouldBe(1.8f);

    // Phase 3 also shortens the attack interval.
    CombatLogic.BossRageCooldownMultiplier(3).ShouldBe(0.45f);
  }

  [Test]
  public void BossRage_BelowPhaseThree_ReturnsBaseMultipliers()
  {
    foreach (var phase in new[] { 0, 1, 2 })
    {
      var rage = CombatLogic.BossRage(phase);
      rage.SpeedMultiplier.ShouldBe(1f);
      rage.DamageMultiplier.ShouldBe(1f);
      CombatLogic.BossRageCooldownMultiplier(phase).ShouldBe(1f);
    }
  }

  [Test]
  public void BossPhaseChanged_RaisedExactlyOncePerTransition()
  {
    _enemy.EnemyData = CreateSharkKingData();

    var events = new List<(string BossId, int Phase)>();
    Action<string, int> handler = (id, phase) => events.Add((id, phase));
    GameEvents.BossPhaseChanged += handler;
    try
    {
      // 400 hp: 120 damage → 0.7 (phase 2), 120 more → 0.4 (phase 3).
      _enemy.TakeDamage(120f);
      _controller._PhysicsProcess(0.016);

      _enemy.TakeDamage(120f);
      _controller._PhysicsProcess(0.016);

      // Same phase again: no further transition event.
      _controller._PhysicsProcess(0.016);
    }
    finally
    {
      GameEvents.BossPhaseChanged -= handler;
    }

    events.Count.ShouldBe(2);
    events[0].BossId.ShouldBe("shark_king");
    events[0].Phase.ShouldBe(2);
    events[1].BossId.ShouldBe("shark_king");
    events[1].Phase.ShouldBe(3);
    _controller.CurrentPhase.ShouldBe(3);
  }

  [Test]
  public void BossPhaseChanged_NoEventWhileStayingInPhaseOne()
  {
    _enemy.EnemyData = CreateSharkKingData();

    var count = 0;
    Action<string, int> handler = (_, _) => count++;
    GameEvents.BossPhaseChanged += handler;
    try
    {
      // 50 damage on 400 hp stays above 70%: phase 1, no event.
      _enemy.TakeDamage(50f);
      _controller._PhysicsProcess(0.016);
      _controller._PhysicsProcess(0.016);
    }
    finally
    {
      GameEvents.BossPhaseChanged -= handler;
    }

    count.ShouldBe(0);
    _controller.CurrentPhase.ShouldBe(1);
  }

  [Test]
  public void SharkKingResource_IsBossAtScaleTwo()
  {
    var king = GD.Load<EnemyData>("res://assets/enemies/shark_king.tres");
    king.ShouldNotBeNull();
    king!.Boss.ShouldBeTrue();
    king.Scale.ShouldBe(2f);
    king.Behavior.ShouldBe(EnemyBehavior.MeleeChase);
    king.MaxHealth.ShouldBe(400f);
    king.Damage.ShouldBe(36f);
    king.MoveSpeed.ShouldBe(7f);
    king.AttackRange.ShouldBe(3.8f);
    king.AttackCooldown.ShouldBe(1.1f);
  }

  /// <summary>
  ///   FIX(code-review P2-05): a minion scene whose root is NOT an EnemyBase
  ///   must fail closed (warn + skip) instead of throwing from the generic
  ///   Instantiate cast. Drives the controller into phase 2 with the interval
  ///   elapsed, then proves no minion was added and no exception escaped.
  /// </summary>
  [Test]
  public void SpawnMinion_NonEnemyScene_FailsClosedWithoutThrowing()
  {
    _enemy.EnemyData = CreateSharkKingData();
    _enemy.TakeDamage(120f); // 400 → 0.7 = phase 2

    // A scene whose root is a plain Node3D (not an EnemyBase).
    var packed = new PackedScene();
    packed.Pack(new Node3D { Name = "NotAnEnemy" });
    _controller.MinionScene = packed;
    _controller.MinionData = CreateSharkKingData();
    _controller.MinionSpawnInterval = 0f;
    _controller.MaxAliveMinions = 5;

    // Interval elapsed → spawn attempt; must not throw.
    Should.NotThrow(() => _controller._PhysicsProcess(1.0));

    _controller.CurrentPhase.ShouldBe(2);
  }

  [Test]
  public void SpawnMinion_WithModelScene_MountsModelBeforeReady()
  {
    _enemy.EnemyData = CreateSharkKingData();
    _enemy.TakeDamage(120f); // 400 → 0.7 = phase 2

    var spawnParent = new Node3D { Name = "MinionSpawnParent" };
    _enemy.GetParent()!.AddChild(spawnParent);

    _controller.MinionScene =
      GD.Load<PackedScene>("res://scenes/combat/enemy.tscn");
    _controller.MinionData =
      GD.Load<EnemyData>("res://assets/enemies/shark_pup.tres");
    _controller.MinionModelScene = GD.Load<PackedScene>(
      "res://assets/models/enemies/shark_king/Shark.glb"
    );
    _controller.MinionModelFootLiftY =
      BossPhaseController.GobkitSharkFootLiftY;
    _controller.MinionSpawnParentPath = new NodePath(spawnParent.GetPath());
    _controller.MinionSpawnInterval = 0f;
    _controller.MaxAliveMinions = 1;

    try
    {
      _controller._PhysicsProcess(1.0);

      var minion = spawnParent.GetNodeOrNull<EnemyBase>("Enemy");
      minion.ShouldNotBeNull();
      ReferenceEquals(minion!.GetParent(), spawnParent).ShouldBeTrue();
      ReferenceEquals(minion.GetParent(), _enemy).ShouldBeFalse();
      minion.EnemyData.ShouldNotBeNull();
      minion.EnemyData!.Id.ShouldBe("shark_pup");
      minion.ModelPath.ToString().ShouldBe("EnemyModel");
      var model = minion.GetNodeOrNull<Node3D>("EnemyModel");
      model.ShouldNotBeNull();
      model!.Position.Y.ShouldBe(
        BossPhaseController.GobkitSharkFootLiftY, tolerance: 0.001f
      );
      var offset = minion.GlobalPosition - _enemy.GlobalPosition;
      new Vector2(offset.X, offset.Z).Length().ShouldBe(
        BossPhaseController.MinionSpawnRadius, tolerance: 0.001f
      );
      offset.Y.ShouldBe(
        BossPhaseController.MinionSpawnHeight, tolerance: 0.001f
      );
    }
    finally
    {
      spawnParent.Free();
    }
  }

  [Test]
  public void MinionSpawnOffset_UsesFiveDeterministicRingSlots()
  {
    var first = BossPhaseController.MinionSpawnOffset(0);
    for (var i = 0; i < BossPhaseController.MinionSpawnSlots; i++)
    {
      var offset = BossPhaseController.MinionSpawnOffset(i);
      new Vector2(offset.X, offset.Z).Length().ShouldBe(
        BossPhaseController.MinionSpawnRadius, tolerance: 0.001f
      );
      offset.Y.ShouldBe(
        BossPhaseController.MinionSpawnHeight, tolerance: 0.001f
      );
    }

    BossPhaseController.MinionSpawnOffset(
      BossPhaseController.MinionSpawnSlots
    ).ShouldBe(first);
  }

  [Test]
  public void StormZone_WiresLandPupAsBossSibling()
  {
    var packed = GD.Load<PackedScene>("res://scenes/storm_zone.tscn");
    packed.ShouldNotBeNull();
    var scene = packed!.Instantiate<Node3D>();
    try
    {
      var phase = scene.GetNodeOrNull<BossPhaseController>(
        "SharkKing/BossPhaseController"
      );
      phase.ShouldNotBeNull();
      phase!.MinionSpawnParentPath.ToString().ShouldBe("../..");
      phase.MinionData.ShouldNotBeNull();
      phase.MinionData!.Id.ShouldBe("shark_pup");
      phase.MinionModelScene.ShouldNotBeNull();
      phase.MinionModelFootLiftY.ShouldBe(
        BossPhaseController.GobkitSharkFootLiftY, tolerance: 0.001f
      );
    }
    finally
    {
      scene.Free();
    }
  }
}
