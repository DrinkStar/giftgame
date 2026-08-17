// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   Enemy base node (Iter6 plan Decision 7). A CharacterBody3D driven by a
///   <see cref="EnemyData"/> resource with three movement archetypes:
///   MeleeChase (walk at the player, wolves speed up at night), Charge (boar:
///   ×3 dash inside ChargeRange while the cooldown is ready), Swimmer (shark:
///   full 3-axis tracking, no gravity). Attack cooldown, red hit flash, death
///   events and loot drops all live here; spider/bat/storm_beast/mutant ship
///   as MeleeChase placeholders until their behaviors land.
///
///   Contracts (plan F2): a missing player leaves the enemy idle with zero
///   velocity; a missing DayNightService disables the night boost; materials
///   are duplicated per instance before tinting; TakeDamage on a dead or
///   dataless enemy is a no-op; death fires EnemyDied exactly once.
/// </summary>
public partial class EnemyBase : CharacterBody3D
{
  private const float FlashDuration = 0.2f;
  private static readonly Color FlashColor = new(1f, 0f, 0f);

  #region Exports (Decision 7)

  [Export] public EnemyData? EnemyData;

  /// <summary>Path to the player CharacterBody3D (wired per scene).</summary>
  [Export] public NodePath Player = new();

  /// <summary>Path to the DayNightService (optional; night boost needs it).</summary>
  [Export] public NodePath DayNightServicePath = new();

  [Export] public float Gravity = -20f;
  [Export] public float ChargeRange = 6f;
  [Export] public float ChargeSpeedMultiplierValue = 3f;
  [Export] public float ChargeCooldown = 2f;

  /// <summary>
  ///   T8.5.5: damage multiplier while it is night (default 1.25), applied at
  ///   the attack damage calculation via
  ///   <see cref="CombatLogic.NightDamageMultiplier"/>. Day = 1. Speed uses
  ///   <see cref="CombatLogic.NightSpeedMultiplier"/> (wolf 1.5, others 1.25).
  /// </summary>
  [Export] public float NightDamageMultiplierValue = 1.25f;

  /// <summary>
  ///   Optional path to a FloatingBody (or any Node3D) the player stands on.
  ///   SeaBeast enemies only pursue while the player is within
  ///   <see cref="FloatAggroRadius"/> of it. Empty path = always pursue.
  /// </summary>
  [Export] public NodePath FloatTargetPath = new();

  /// <summary>SeaBeast aggro radius around the float target, in meters.</summary>
  [Export] public float FloatAggroRadius = 8f;

  /// <summary>
  ///   Optional path to a real model node (e.g. a GLB instance child) that
  ///   replaces the default capsule Visual. When resolved, the capsule is
  ///   hidden and the override becomes the tint/flash target. Used by
  ///   storm_zone.tscn to mount the shark model on the boss without changing
  ///   enemy.tscn; Game.tscn's capsule enemies leave it empty (Iter6.1).
  /// </summary>
  [Export] public NodePath ModelPath = new();

  #endregion Exports

  /// <summary>Slow applied to the player on a webbing hit (Iter6.1 todo 3).</summary>
  private const float WebbingSlowDuration = 2f;
  private const float WebbingSlowFactor = 0.5f;

  /// <summary>Small per-enemy hover sine amplitude/frequency (flyer anti-stall).</summary>
  private const float FlyerHoverAmplitude = 0.15f;
  private const float FlyerHoverFrequency = 2.5f;

  private CharacterBody3D? _player;
  private PlayerStats? _playerStats;
  private DayNightService? _dayNight;
  private EnemyHealth? _health;
  private StandardMaterial3D? _material;
  private Color _tintColor = new(0.8f, 0.8f, 0.8f);
  private Node3D? _floatTarget;
  private MeshInstance3D? _modelVisual;
  private BossPhaseController? _phaseController;

  private float _attackCooldown;
  private float _chargeCooldownRemaining;
  private float _chargeRemaining;
  private float _flashRemaining;
  private float _hoverTime;
  private float _hoverPhase;

  /// <summary>Exposed for tests (null until health is initialized).</summary>
  public EnemyHealth? HealthTracker => _health;

  public override void _Ready()
  {
    if (!Player.IsEmpty)
    {
      _player = GetNodeOrNull<CharacterBody3D>(Player);
      // FIX(code-review P2-04): resolve the player's stats ONCE and cache it
      // — the old code re-looked-up "PlayerStats" by name on every attack,
      // a second addressing path that drifted from PlayerController's
      // exported Stats property and silently no-op'd the damage/slow when the
      // child name or position changed. PlayerStats sits under the player in
      // every shipped scene (Game.tscn, storm_zone.tscn), so this matches the
      // node-name convention those scenes already rely on.
      _playerStats = _player?.GetNodeOrNull<PlayerStats>("PlayerStats");
    }
    if (!DayNightServicePath.IsEmpty)
      _dayNight = GetNodeOrNull<DayNightService>(DayNightServicePath);
    if (!FloatTargetPath.IsEmpty)
      _floatTarget = GetNodeOrNull<Node3D>(FloatTargetPath);

    // Boss phase state machine (Iter6.1 todo 4): attached as a child named
    // "BossPhaseController"; null for regular enemies (phase reads 0 → 1×).
    _phaseController = GetNodeOrNull<BossPhaseController>("BossPhaseController");

    if (EnemyData != null)
      _health = new EnemyHealth(EnemyData.MaxHealth);

    // Deterministic per-instance hover phase (flyer anti-stall): the same
    // enemy always bobs on the same sine offset so tests stay reproducible.
    _hoverPhase = GetInstanceId() % 6283 / 6283f * Mathf.Tau;

    ApplyModelOverride();
    ApplyScale();
    SetupMaterial();
  }

  public override void _PhysicsProcess(double delta)
  {
    if (EnemyData == null)
      return;

    var dt = (float)delta;
    UpdateFlash(dt);

    // Decision 7 contract: without a player the enemy is idle — zero the
    // velocity and bail out BEFORE gravity is applied.
    if (_player == null)
    {
      Velocity = Vector3.Zero;
      return;
    }

    _attackCooldown = Mathf.Max(0f, _attackCooldown - dt);
    var dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

    var behavior = EnemyData.Behavior;
    var seaBeastAggro = behavior == EnemyBehavior.SeaBeast && IsSeaBeastAggro();

    switch (behavior)
    {
      case EnemyBehavior.MeleeChase:
      case EnemyBehavior.Webbing:
        MoveMeleeChase(dt);
        break;
      case EnemyBehavior.Charge:
        MoveCharge(dt, dist);
        break;
      case EnemyBehavior.Swimmer:
        MoveSwimmer();
        break;
      case EnemyBehavior.Flyer:
        MoveFlyer(dt);
        break;
      case EnemyBehavior.SeaBeast:
        if (seaBeastAggro)
          MoveSwimmer();
        else
          Velocity = Vector3.Zero; // Idle until the player boards the float.
        break;
      case EnemyBehavior.Mutant:
        MoveMeleeChase(dt);
        break;
    }

    // SeaBeast only attacks while aggroed; the mutant's slam widens the
    // trigger radius into an AoE (CombatLogic.MutantAoeRadius).
    var canAttack = behavior != EnemyBehavior.SeaBeast || seaBeastAggro;
    var attackRange = behavior == EnemyBehavior.Mutant
      ? CombatLogic.MutantAoeRadius(EnemyData.AttackRange, HealthRatio())
      : EnemyData.AttackRange;

    if (canAttack && CombatLogic.ShouldAttack(dist, attackRange, _attackCooldown <= 0f))
    {
      // Phase-3 boss rage boosts damage (1× for regular enemies); T8.5.5:
      // night multiplies damage for every enemy (default 1.25), day = 1.
      var rage = CombatLogic.BossRage(BossPhase());
      _playerStats?
        .TakeDamage(
          EnemyData.Damage
          * rage.DamageMultiplier
          * CombatLogic.NightDamageMultiplier(IsNight(), NightDamageMultiplierValue)
        );

      // Webbing hit: the spider slows the player for 2 s at 0.5× speed.
      if (behavior == EnemyBehavior.Webbing)
        _playerStats?.ApplySlow(WebbingSlowDuration, WebbingSlowFactor);

      _attackCooldown = EnemyData.AttackCooldown
        * CombatLogic.BossRageCooldownMultiplier(BossPhase());
    }
  }

  /// <summary>
  ///   Applies damage through the pure <see cref="EnemyHealth"/> tracker,
  ///   flashes red for 0.2s and dies when health reaches zero.
  /// </summary>
  public void TakeDamage(float damage)
  {
    if (EnemyData == null || damage <= 0f)
      return;

    var health = EnsureHealth();
    if (health.IsDead)
      return;

    health.TakeDamage(damage);
    StartFlash();

    if (health.IsDead)
      Die();
  }

  private void Die()
  {
    // Decision 9: EnemyDied fires for every enemy; bosses additionally fire
    // BossDefeated. QueueFree happens immediately after.
    GameEvents.RaiseEnemyDied(EnemyData!.Id);
    if (EnemyData.Boss)
      GameEvents.RaiseBossDefeated(EnemyData.Id);

    TryDropLoot();
    QueueFree();
  }

  /// <summary>
  ///   T8.5.7: kill drops no longer go straight into the player's inventory —
  ///   they spawn as ground loot at the enemy's position via
  ///   <see cref="GroundLoot.Spawn"/>, the same flow the player death drop
  ///   (GameManager.DropDeathLoot) already uses, so the player must walk over
  ///   and pick them up. Best-effort: a missing loot scene is silently
  ///   skipped (Spawn returns null). No player/inventory reference is needed
  ///   anymore, so off-screen or player-less kills still drop.
  /// </summary>
  private void TryDropLoot()
  {
    if (string.IsNullOrEmpty(EnemyData!.DropItemId))
      return;

    GroundLoot.Spawn(EnemyData.DropItemId, EnemyData.DropAmount, GlobalPosition);
  }

  private EnemyHealth EnsureHealth() =>
    _health ??= new EnemyHealth(EnemyData!.MaxHealth);

  private void ApplyScale()
  {
    if (EnemyData == null)
      return;

    var s = Mathf.Max(0.01f, EnemyData.Scale);
    Scale = new Vector3(s, s, s);
  }

  /// <summary>
  ///   Duplicates the Visual material per instance and tints it by enemy id
  ///   (Decision 7: wolf gray / boar brown / crab red / shark cyan /
  ///   shark_king dark-cyan / spider dark-gray / bat black / storm_beast
  ///   blue-gray / mutant purple). The duplicate is kept as the flash
  ///   reference so the hit flash never leaks across instances. When a
  ///   model override is mounted (<see cref="ModelPath"/>) it becomes the
  ///   tint target instead of the capsule.
  /// </summary>
  private void SetupMaterial()
  {
    var visual = _modelVisual ?? GetNodeOrNull<MeshInstance3D>("Visual");
    if (visual == null)
      return;

    _material = visual.MaterialOverride is StandardMaterial3D existing
      ? (StandardMaterial3D)existing.Duplicate()
      : new StandardMaterial3D();
    visual.MaterialOverride = _material;

    _tintColor = GetTint(EnemyData?.Id ?? "");
    _material.AlbedoColor = _tintColor;
  }

  /// <summary>
  ///   Permanently overrides the tint color (BossPhaseController phase 3:
  ///   dark red). The hit flash afterwards reverts to this color instead of
  ///   the default tint.
  /// </summary>
  public void OverrideTint(Color color)
  {
    _tintColor = color;
    if (_material != null)
      _material.AlbedoColor = color;
  }

  /// <summary>
  ///   Mounts the optional real-model override: hides the capsule Visual and
  ///   makes the first MeshInstance3D under the resolved node the tint
  ///   target. Missing/unresolvable overrides fall back to the capsule.
  /// </summary>
  private void ApplyModelOverride()
  {
    if (ModelPath.IsEmpty)
      return;

    var visual = FindMeshInstance(GetNodeOrNull<Node>(ModelPath));
    if (visual == null)
    {
      GD.PushWarning(
        $"EnemyBase: ModelPath '{ModelPath}' resolves to no MeshInstance3D; keeping capsule."
      );
      return;
    }

    GetNodeOrNull<MeshInstance3D>("Visual")?.SetVisible(false);
    _modelVisual = visual;
  }

  /// <summary>Breadth-first search for the first MeshInstance3D under a node.</summary>
  private static MeshInstance3D? FindMeshInstance(Node? root)
  {
    if (root == null)
      return null;

    var queue = new Queue<Node>();
    queue.Enqueue(root);

    while (queue.Count > 0)
    {
      var node = queue.Dequeue();
      if (node is MeshInstance3D mesh)
        return mesh;

      foreach (var child in node.GetChildren())
        queue.Enqueue(child);
    }

    return null;
  }

  private static Color GetTint(string id)
  {
    return id switch
    {
      "wolf" => new Color(0.5f, 0.5f, 0.5f),
      "boar" => new Color(0.55f, 0.35f, 0.2f),
      "crab" => new Color(0.8f, 0.2f, 0.2f),
      "shark" => new Color(0.2f, 0.7f, 0.8f),
      "shark_king" => new Color(0.1f, 0.4f, 0.5f),
      "spider" => new Color(0.3f, 0.3f, 0.3f),
      "bat" => new Color(0.1f, 0.1f, 0.1f),
      "storm_beast" => new Color(0.4f, 0.45f, 0.6f),
      "mutant" => new Color(0.5f, 0.2f, 0.6f),
      _ => new Color(0.8f, 0.8f, 0.8f)
    };
  }

  private void StartFlash()
  {
    _flashRemaining = FlashDuration;
    if (_material != null)
      _material.AlbedoColor = FlashColor;
  }

  private void UpdateFlash(float dt)
  {
    if (_flashRemaining <= 0f)
      return;

    _flashRemaining -= dt;
    if (_flashRemaining <= 0f && _material != null)
      _material.AlbedoColor = _tintColor;
  }

  private bool IsNight() => _dayNight?.IsNight == true;

  private Vector3 HorizontalDirectionToPlayer()
  {
    var toPlayer = _player!.GlobalPosition - GlobalPosition;
    toPlayer.Y = 0f;
    return toPlayer.LengthSquared() > 0.0001f ? toPlayer.Normalized() : Vector3.Zero;
  }

  private void MoveMeleeChase(float dt)
  {
    // T8.5.5: night speed — wolves keep their 1.5× hunt boost, every other
    // melee chaser gets the general 1.25× (CombatLogic.NightSpeedMultiplier);
    // mutants add their rage speed on top (Iter6.1).
    var speed = EnemyData.MoveSpeed
      * CombatLogic.NightSpeedMultiplier(EnemyData!.Id, IsNight())
      * RageSpeedMultiplier();

    var dir = HorizontalDirectionToPlayer();
    Velocity = new Vector3(dir.X * speed, Velocity.Y + Gravity * dt, dir.Z * speed);
    MoveAndSlide();
  }

  private void MoveCharge(float dt, float dist)
  {
    _chargeCooldownRemaining = Mathf.Max(0f, _chargeCooldownRemaining - dt);
    _chargeRemaining = Mathf.Max(0f, _chargeRemaining - dt);

    // Pure logic decides engagement; once engaged the boost holds for the
    // whole ChargeCooldown window so the charge is a real dash, and the
    // cooldown pauses the next one (Decision 7/8).
    if (
      _chargeRemaining <= 0f
      && CombatLogic.ChargeSpeedMultiplier(dist, ChargeRange, _chargeCooldownRemaining <= 0f) > 1f
    )
    {
      _chargeRemaining = ChargeCooldown;
      _chargeCooldownRemaining = ChargeCooldown;
    }

    var speed = EnemyData!.MoveSpeed
      * CombatLogic.NightSpeedMultiplier(EnemyData.Id, IsNight())
      * (_chargeRemaining > 0f ? ChargeSpeedMultiplierValue : 1f);

    var dir = HorizontalDirectionToPlayer();
    Velocity = new Vector3(dir.X * speed, Velocity.Y + Gravity * dt, dir.Z * speed);
    MoveAndSlide();
  }

  private void MoveSwimmer()
  {
    var toPlayer = _player!.GlobalPosition - GlobalPosition;

    var horizontal = new Vector3(toPlayer.X, 0f, toPlayer.Z);
    var horizontalDir =
      horizontal.LengthSquared() > 0.0001f ? horizontal.Normalized() : Vector3.Zero;

    // Track the player Y too; no gravity while swimming (Decision 7).
    var verticalDir = Mathf.Abs(toPlayer.Y) < 0.25f ? 0f : Mathf.Sign(toPlayer.Y);

    var speed = EnemyData!.MoveSpeed
      * CombatLogic.NightSpeedMultiplier(EnemyData.Id, IsNight())
      * RageSpeedMultiplier();

    Velocity = new Vector3(
      horizontalDir.X * speed,
      verticalDir * speed,
      horizontalDir.Z * speed
    );
    MoveAndSlide();
  }

  /// <summary>
  ///   Flyer behavior (Iter6.1): no gravity — the Y velocity is entirely
  ///   seek-driven toward the player's Y, plus a small deterministic per-enemy
  ///   sine bob so converging flyers never stall on top of each other.
  /// </summary>
  private void MoveFlyer(float dt)
  {
    var toPlayer = _player!.GlobalPosition - GlobalPosition;

    var horizontal = new Vector3(toPlayer.X, 0f, toPlayer.Z);
    var horizontalDir =
      horizontal.LengthSquared() > 0.0001f ? horizontal.Normalized() : Vector3.Zero;

    var speed = EnemyData!.MoveSpeed
      * CombatLogic.FlyerSpeedBoost
      * CombatLogic.NightSpeedMultiplier(EnemyData.Id, IsNight());
    var verticalVelocity = CombatLogic.FlyerVerticalSeek(toPlayer.Y, speed, dt);

    _hoverTime += dt;
    var hover = Mathf.Sin(_hoverTime * FlyerHoverFrequency + _hoverPhase)
      * FlyerHoverAmplitude;

    Velocity = new Vector3(
      horizontalDir.X * speed,
      verticalVelocity + hover,
      horizontalDir.Z * speed
    );
    MoveAndSlide();
  }

  /// <summary>Current hp ratio (1 = full); safe when health is absent.</summary>
  private float HealthRatio()
  {
    var health = EnsureHealth();
    return health.MaxHealth <= 0f ? 1f : health.Health / health.MaxHealth;
  }

  /// <summary>
  ///   Extra speed multiplier from a mutant rage or a phase-3 boss rage
  ///   (Iter6.1); other enemies run at 1f.
  /// </summary>
  private float RageSpeedMultiplier()
  {
    if (EnemyData!.Behavior == EnemyBehavior.Mutant)
      return CombatLogic.MutantRage(HealthRatio()).SpeedMultiplier;

    return CombatLogic.BossRage(BossPhase()).SpeedMultiplier;
  }

  /// <summary>
  ///   Boss phase read from the attached <see cref="BossPhaseController"/>
  ///   child; 0 (no controller) maps to base multipliers.
  /// </summary>
  private int BossPhase() => _phaseController?.CurrentPhase ?? 0;

  /// <summary>
  ///   SeaBeast aggro (Iter6.1): pursue while the player is on the watched
  ///   float, or always when no float is wired (treated as permanently at
  ///   sea).
  /// </summary>
  private bool IsSeaBeastAggro() =>
    CombatLogic.SeaBeastAggro(PlayerIsOnFloat(), _floatTarget == null);

  private bool PlayerIsOnFloat()
  {
    if (_floatTarget == null || _player == null)
      return false;

    return _player.GlobalPosition.DistanceTo(_floatTarget.GlobalPosition)
      <= FloatAggroRadius;
  }
}
