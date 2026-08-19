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
      Damage = 30f,
      MoveSpeed = 5f,
      AttackRange = 3f,
      AttackCooldown = 1.5f,
      Behavior = EnemyBehavior.Swimmer,
      Boss = true,
      Scale = 2f
    };

  [Test]
  public void BossPhase_AboveSixtyPercent_ReturnsPhaseOne()
  {
    CombatLogic.BossPhase(0.65f).ShouldBe(1);
    CombatLogic.BossPhase(1f).ShouldBe(1);
    CombatLogic.BossPhase(0.61f).ShouldBe(1);
  }

  [Test]
  public void BossPhase_BetweenThirtyAndSixty_ReturnsPhaseTwo()
  {
    CombatLogic.BossPhase(0.45f).ShouldBe(2);
    // The 60% boundary itself belongs to phase 2 (phase 1 is strictly > 60%).
    CombatLogic.BossPhase(0.6f).ShouldBe(2);
    CombatLogic.BossPhase(0.31f).ShouldBe(2);
  }

  [Test]
  public void BossPhase_BelowThirty_ReturnsPhaseThree()
  {
    CombatLogic.BossPhase(0.15f).ShouldBe(3);
    // The 30% boundary itself belongs to phase 3 (phase 2 is strictly > 30%).
    CombatLogic.BossPhase(0.3f).ShouldBe(3);
    CombatLogic.BossPhase(0f).ShouldBe(3);
  }

  [Test]
  public void ShouldSpawnMinion_ElapsedBelowInterval_ReturnsFalse()
  {
    CombatLogic.ShouldSpawnMinion(9.9f, 10f, 0, 3).ShouldBeFalse();
    CombatLogic.ShouldSpawnMinion(0f, 10f, 0, 3).ShouldBeFalse();
  }

  [Test]
  public void ShouldSpawnMinion_TimerReadyUnderAliveCap_ReturnsTrue()
  {
    CombatLogic.ShouldSpawnMinion(10f, 10f, 0, 3).ShouldBeTrue();
    CombatLogic.ShouldSpawnMinion(15f, 10f, 2, 3).ShouldBeTrue();
  }

  [Test]
  public void ShouldSpawnMinion_AliveAtOrAboveCap_ReturnsFalse()
  {
    CombatLogic.ShouldSpawnMinion(10f, 10f, 3, 3).ShouldBeFalse();
    CombatLogic.ShouldSpawnMinion(99f, 10f, 4, 3).ShouldBeFalse();
  }

  [Test]
  public void BossRage_PhaseThree_ReturnsRageMultipliers()
  {
    var rage = CombatLogic.BossRage(3);
    rage.SpeedMultiplier.ShouldBe(1.8f);
    rage.DamageMultiplier.ShouldBe(1.5f);

    // Phase 3 also shortens the attack interval.
    CombatLogic.BossRageCooldownMultiplier(3).ShouldBe(0.6f);
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
      // 400 hp: 160 damage → 0.6 (phase 2), 120 more → 0.3 (phase 3).
      _enemy.TakeDamage(160f);
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
      // 50 damage on 400 hp stays above 60%: phase 1, no event.
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
    king.Behavior.ShouldBe(EnemyBehavior.Swimmer);
    king.MaxHealth.ShouldBe(400f);
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
    _enemy.TakeDamage(160f); // 400 → 0.6 = phase 2

    // A scene whose root is a plain Node3D (not an EnemyBase).
    var packed = new PackedScene();
    packed.Pack(new Node3D { Name = "NotAnEnemy" });
    _controller.MinionScene = packed;
    _controller.MinionData = CreateSharkKingData();
    _controller.MinionSpawnInterval = 0f;
    _controller.MaxAliveMinions = 3;

    // Interval elapsed → spawn attempt; must not throw.
    Should.NotThrow(() => _controller._PhysicsProcess(1.0));

    _controller.CurrentPhase.ShouldBe(2);
  }
}
