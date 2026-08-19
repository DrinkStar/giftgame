// Original — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   Hurtboxes on both sides: player melee/projectiles hit EnemyHurtbox
///   (layer 9); enemy attacks still use AttackRange but apply damage only
///   when overlapping PlayerHurtbox (layer 10). PlayerDied remains ≠ GameOver.
/// </summary>
public class CombatHurtboxTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;

  public CombatHurtboxTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup() => _fixture = new Fixture(TestScene.GetTree());

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  public void Dispose() => GC.SuppressFinalize(this);

  private static EnemyData CreateCrabData(float damage = 5f) =>
    new()
    {
      Id = "crab",
      MaxHealth = 30f,
      Damage = damage,
      MoveSpeed = 0f,
      AttackRange = 1.5f,
      AttackCooldown = 1f,
      Behavior = EnemyBehavior.MeleeChase,
      Boss = false,
      Scale = 1f
    };

  [Test]
  public void LayersAreDedicatedAndDoNotCollideWithInteractRay()
  {
    CombatLayers.EnemyHurtboxMask.ShouldBe(256u);
    CombatLayers.PlayerHurtboxMask.ShouldBe(512u);
    CombatLayers.MeleeRayMask.ShouldBe(256u);
    CombatLayers.ProjectileMask.ShouldBe(257u);

    const uint interactMask = 0b11101;
    (interactMask & CombatLayers.EnemyHurtboxMask).ShouldBe(0u);
    (interactMask & CombatLayers.PlayerHurtboxMask).ShouldBe(0u);
    (interactMask & CombatLayers.EnemiesMask).ShouldBe(0u);
  }

  [Test]
  public void ResolveEnemy_WalksHurtboxToEnemyBase_AndAcceptsBodyFallback()
  {
    var enemy = new EnemyBase { Name = "Enemy" };
    var hurtbox = new Hurtbox { Name = "Hurtbox" };
    enemy.AddChild(hurtbox);

    Hurtbox.ResolveEnemy(hurtbox).ShouldBe(enemy);
    Hurtbox.ResolveEnemy(enemy).ShouldBe(enemy);
    Hurtbox.ResolveEnemy(new Node()).ShouldBeNull();
  }

  [Test]
  public void SpeciesProfiles_FitRestPoseNotSharedCapsule()
  {
    var player = Hurtbox.ForSpecies("player");
    player.UseBox.ShouldBeFalse();
    player.Radius.ShouldBe(0.4f);
    player.TopY.ShouldBeGreaterThan(1.7f);
    player.Offset.Y.ShouldBe(Hurtbox.PlayerOffsetY);

    var crab = Hurtbox.ForSpecies("crab");
    crab.UseBox.ShouldBeTrue();
    crab.BoxSize.X.ShouldBeGreaterThan(1.5f);
    crab.BoxSize.X.ShouldBeGreaterThan(Hurtbox.EnemyRadius * 2f);

    var boar = Hurtbox.ForSpecies("boar");
    boar.UseBox.ShouldBeTrue();
    boar.BoxSize.Z.ShouldBeLessThan(5f);
    boar.BoxSize.ShouldNotBe(crab.BoxSize);

    var wolf = Hurtbox.ForSpecies("wolf");
    wolf.BoxSize.ShouldNotBe(crab.BoxSize);
    wolf.BoxSize.Z.ShouldBeLessThan(4f);

    Hurtbox.ForSpecies("shark").BoxSize.ShouldBe(Hurtbox.ForSpecies("shark_king").BoxSize);
  }

  [Test]
  public async Task AppliedHurtbox_UsesSpeciesShapeAndPlayerCoversHead()
  {
    var (_, stats, crab) = await SpawnAttackRig(playerOffset: 1f);
    var crabBox = crab.GetNode<Hurtbox>("Hurtbox")
      .GetNode<CollisionShape3D>("CollisionShape3D").Shape as BoxShape3D;
    crabBox.ShouldNotBeNull();
    crabBox!.Size.X.ShouldBeGreaterThan(1.5f);

    var boarData = CreateCrabData();
    boarData.Id = "boar";
    var boar = ResourceLoader.Load<PackedScene>("res://scenes/combat/enemy.tscn")!
      .Instantiate<EnemyBase>();
    boar.Name = "Boar";
    boar.EnemyData = boarData;
    crab.GetParent().AddChild(boar);
    await TestScene.ProcessFrame(1);
    var boarBox = boar.GetNode<Hurtbox>("Hurtbox")
      .GetNode<CollisionShape3D>("CollisionShape3D").Shape as BoxShape3D;
    boarBox.ShouldNotBeNull();
    boarBox!.Size.ShouldNotBe(crabBox.Size);

    var player = stats.GetParent().GetNode<Hurtbox>("Hurtbox");
    var cap = player.GetNode<CollisionShape3D>("CollisionShape3D").Shape as CapsuleShape3D;
    cap.ShouldNotBeNull();
    (player.Position.Y + cap!.Height * 0.5f).ShouldBeGreaterThan(1.7f);
    stats.ShouldNotBeNull();
  }

  [Test]
  public async Task MeleeRayHitsEnemyHurtboxAndDamagesEnemyBase()
  {
    var (weapon, enemy) = await SpawnMeleeRig(new Vector3(0f, 0f, -2f));
    await TestScene.ProcessFrame(2);

    weapon.FireMelee(10f);

    enemy.HealthTracker.ShouldNotBeNull();
    enemy.HealthTracker!.Health.ShouldBe(20f);
  }

  [Test]
  public async Task MeleeRayHittingOnlyCharacterBodyDoesNotDamage()
  {
    var (weapon, enemy) = await SpawnMeleeRig(new Vector3(0f, 0f, -2f));
    enemy.TakeDamage(1f);
    enemy.HealthTracker!.Health.ShouldBe(29f);

    var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
    enemy.RemoveChild(hurtbox);
    hurtbox.Free();
    await TestScene.ProcessFrame(2);

    weapon.FireMelee(10f);

    // Primary path is Hurtbox-only. Grazing the body capsule is a miss.
    // ResolveEnemy still accepts an EnemyBase collider as a documented
    // thin-miss fallback if some other query includes layer 8.
    enemy.HealthTracker.Health.ShouldBe(29f);
  }

  [Test]
  public async Task ProjectileHitsEnemyHurtbox()
  {
    var (_, enemy) = await SpawnMeleeRig(new Vector3(0f, 0f, -2f));

    var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/projectile.tscn");
    packed.ShouldNotBeNull();
    var projectile = packed!.Instantiate<Projectile>();
    projectile.Damage = 10f;
    projectile.Gravity = 0f;
    projectile.Lifetime = 5f;
    projectile.SetVelocity(new Vector3(0f, 0f, -20f));
    enemy.GetParent().AddChild(projectile);
    projectile.GlobalPosition = new Vector3(0f, 0f, -0.5f);
    projectile.SetPhysicsProcess(false);
    await TestScene.ProcessFrame(2);

    projectile._PhysicsProcess(0.1);

    enemy.HealthTracker.ShouldNotBeNull();
    enemy.HealthTracker!.Health.ShouldBe(20f);
  }

  [Test]
  public async Task EnemyAttackDamagesPlayerOnlyViaHurtboxOverlap()
  {
    var (_, stats, enemy) = await SpawnAttackRig(playerOffset: 1f);
    await TestScene.ProcessFrame(2);

    CombatLogic.ShouldAttack(1f, 1.5f, true).ShouldBeTrue();
    var before = stats.Health;
    before.ShouldBe(100f);
    enemy._PhysicsProcess(1.0 / 60.0);
    stats.Health.ShouldBe(before - 5f);

    // Second tick is still on cooldown (AttackCooldown=1s) — no extra hit.
    enemy._PhysicsProcess(1.0 / 60.0);
    stats.Health.ShouldBe(before - 5f);
  }

  [Test]
  public async Task EnemyAttackDoesNotDamageWhenPlayerHurtboxDisabled()
  {
    var (player, stats, enemy) = await SpawnAttackRig(playerOffset: 1f);
    player.GetNode<Hurtbox>("Hurtbox").Disable();
    await TestScene.ProcessFrame(2);

    CombatLogic.ShouldAttack(1f, 1.5f, true).ShouldBeTrue();
    var before = stats.Health;
    enemy._PhysicsProcess(1.0 / 60.0);
    stats.Health.ShouldBe(before);
  }

  [Test]
  public async Task EnemyKillViaHurtboxRaisesPlayerDiedNotGameOver()
  {
    var (_, stats, enemy) = await SpawnAttackRig(playerOffset: 1f, damage: 200f);
    stats.Revive();
    await TestScene.ProcessFrame(2);

    var died = 0;
    var gameOver = 0;
    Action onDied = () => died++;
    Action onOver = () => gameOver++;
    GameEvents.PlayerDied += onDied;
    GameEvents.GameOver += onOver;
    try
    {
      stats.IsAlive.ShouldBeTrue();
      enemy._PhysicsProcess(1.0 / 60.0);
    }
    finally
    {
      GameEvents.PlayerDied -= onDied;
      GameEvents.GameOver -= onOver;
    }

    stats.IsAlive.ShouldBeFalse();
    died.ShouldBe(1);
    gameOver.ShouldBe(0);
  }

  private async Task<(WeaponSystem Weapon, EnemyBase Enemy)> SpawnMeleeRig(
    Vector3 enemyPosition
  )
  {
    var root = new Node3D { Name = "HurtboxRig" };
    var holder = new Node3D { Name = "Player" };
    var camera = new Camera3D { Name = "Camera3D" };
    var weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      CameraPath = "../Camera3D"
    };
    holder.AddChild(camera);
    holder.AddChild(weapon);
    root.AddChild(holder);

    var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/enemy.tscn");
    packed.ShouldNotBeNull();
    var enemy = packed!.Instantiate<EnemyBase>();
    enemy.Name = "Enemy";
    enemy.EnemyData = CreateCrabData();
    enemy.Position = enemyPosition;
    root.AddChild(enemy);

    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    enemy.SetPhysicsProcess(false);
    return (weapon, enemy);
  }

  private async Task<(PlayerController Player, PlayerStats Stats, EnemyBase Enemy)>
    SpawnAttackRig(float playerOffset, float damage = 5f)
  {
    var root = new Node3D { Name = "AttackRig" };

    var player = new PlayerController { Name = "Player" };
    var stats = new PlayerStats { Name = "PlayerStats" };
    player.AddChild(stats);
    player.Stats = stats;
    player.Position = new Vector3(playerOffset, 0f, 0f);
    root.AddChild(player);

    var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/enemy.tscn");
    packed.ShouldNotBeNull();
    var enemy = packed!.Instantiate<EnemyBase>();
    enemy.Name = "Enemy";
    enemy.EnemyData = CreateCrabData(damage);
    enemy.Player = new NodePath("../Player");
    enemy.Position = Vector3.Zero;
    enemy.ProcessMode = Node.ProcessModeEnum.Disabled;
    root.AddChild(enemy);

    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    enemy.ProcessMode = Node.ProcessModeEnum.Disabled;
    player.SetPhysicsProcess(false);
    return (player, stats, enemy);
  }
}
