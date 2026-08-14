// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Integration test for scenes/combat/enemy.tscn (Iter6 plan Decision 13):
///   the scene instantiates on the Enemies layer, an EnemyData assigned in
///   code drives the pure EnemyHealth tracker, and death raises GameEvents
///   EnemyDied EXACTLY once (handler unsubscribed in finally).
/// </summary>
public class CombatIntegrationTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private EnemyBase _enemy = default!;

  public CombatIntegrationTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/enemy.tscn");
    packed.ShouldNotBeNull();
    _enemy = packed!.Instantiate<EnemyBase>();
    _enemy.Name = "TestEnemy";
    _fixture.AddToRoot(_enemy, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_enemy == null)
      return;

    _enemy.Dispose();
    _enemy = null!;
    GC.SuppressFinalize(this);
  }

  private static EnemyData CreateCrabData() =>
    new()
    {
      Id = "crab",
      DisplayName = "Test Crab",
      MaxHealth = 30f,
      Damage = 5f,
      MoveSpeed = 2f,
      AttackRange = 1.5f,
      AttackCooldown = 1f,
      Behavior = EnemyBehavior.MeleeChase,
      Boss = false,
      Scale = 1f
    };

  private static EnemyData CreateSharkKingData() =>
    new()
    {
      Id = "shark_king",
      DisplayName = "Test Shark King",
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
  public void EnemySceneInstantiatesOnEnemiesLayer()
  {
    _enemy.ShouldNotBeNull();
    // Decision 7: collision_layer=128 (Enemies, editor layer_8), collision_mask=1 (World).
    _enemy.CollisionLayer.ShouldBe(128u);
    _enemy.CollisionMask.ShouldBe(1u);
    _enemy.GetNodeOrNull<CollisionShape3D>("CollisionShape3D").ShouldNotBeNull();
    _enemy.GetNodeOrNull<MeshInstance3D>("Visual").ShouldNotBeNull();
  }

  [Test]
  public void TakeDamage_KillsEnemyAndRaisesEnemyDiedExactlyOnce()
  {
    _enemy.EnemyData = CreateCrabData();

    var count = 0;
    string? diedId = null;
    Action<string> handler = id =>
    {
      count++;
      diedId = id;
    };
    GameEvents.EnemyDied += handler;
    try
    {
      _enemy.TakeDamage(999f);
    }
    finally
    {
      GameEvents.EnemyDied -= handler;
    }

    count.ShouldBe(1);
    diedId.ShouldBe("crab");
    _enemy.HealthTracker.ShouldNotBeNull();
    _enemy.HealthTracker!.IsDead.ShouldBeTrue();
    _enemy.HealthTracker.Health.ShouldBe(0f);
  }

  [Test]
  public void BossDeath_RaisesEnemyDiedAndBossDefeatedOnceEach()
  {
    _enemy.EnemyData = CreateSharkKingData();

    var diedCount = 0;
    var bossCount = 0;
    Action<string> diedHandler = _ => diedCount++;
    Action<string> bossHandler = _ => bossCount++;
    GameEvents.EnemyDied += diedHandler;
    GameEvents.BossDefeated += bossHandler;
    try
    {
      _enemy.TakeDamage(999f);
    }
    finally
    {
      GameEvents.EnemyDied -= diedHandler;
      GameEvents.BossDefeated -= bossHandler;
    }

    diedCount.ShouldBe(1);
    bossCount.ShouldBe(1);
    _enemy.HealthTracker.ShouldNotBeNull();
    _enemy.HealthTracker!.IsDead.ShouldBeTrue();
  }

  [Test]
  public void NonLethalDamage_KeepsHealthAndRaisesNoDeathEvent()
  {
    _enemy.EnemyData = CreateCrabData();

    var count = 0;
    Action<string> handler = _ => count++;
    GameEvents.EnemyDied += handler;
    try
    {
      _enemy.TakeDamage(10f);
    }
    finally
    {
      GameEvents.EnemyDied -= handler;
    }

    count.ShouldBe(0);
    _enemy.HealthTracker.ShouldNotBeNull();
    _enemy.HealthTracker!.IsDead.ShouldBeFalse();
    _enemy.HealthTracker.Health.ShouldBe(20f);
  }
}
