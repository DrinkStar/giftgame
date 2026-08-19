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

  /// <summary>
  ///   Beyond this distance the AI switch is skipped (idle). Does not change
  ///   the state machine — far-island enemies still exist, they just don't
  ///   chase every physics tick.
  /// </summary>
  public const float SleepDistance = 80f;

  private CharacterBody3D? _player;
  private PlayerStats? _playerStats;
  private DayNightService? _dayNight;
  private EnemyHealth? _health;
  private StandardMaterial3D? _material;
  private Color _tintColor = new(0.8f, 0.8f, 0.8f);
  private Node3D? _floatTarget;
  private MeshInstance3D? _modelVisual;
  private BossPhaseController? _phaseController;
  private CharacterAnimator? _animator;
  private bool _dying;
  private SphereShape3D? _attackQueryShape;

  private Vector3 _homePosition;
  private Vector2 _attractorXz;
  private Vector3 _wanderTarget;
  private float _wanderRadius;
  private float _wanderClock;
  private int _wanderSlot = -1;

  private float _attackCooldown;
  private float _chargeCooldownRemaining;
  private float _chargeRemaining;
  private float _flashRemaining;
  private float _hoverTime;
  private float _hoverPhase;

  /// <summary>Exposed for tests (null until health is initialized).</summary>
  public EnemyHealth? HealthTracker => _health;

  /// <summary>True while a Charge dash is in progress (visual layer only).</summary>
  public bool IsCharging => _chargeRemaining > 0f;

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
    Hurtbox.EnsureOn(this, Hurtbox.Kind.Enemy);

    // Visual layer: bind clips after the model child is in the tree. Does
    // not touch the AI switch below.
    _animator = GetNodeOrNull<CharacterAnimator>("CharacterAnimator");
    _animator?.BindFromTree();

    CaptureHabitat();
  }

  public override void _PhysicsProcess(double delta)
  {
    if (EnemyData == null || _dying)
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
    var distSq = GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
    // Habitat species still wander their den when the player is far; others
    // keep the SleepDistance idle skip. The behavior switch is unchanged.
    if (!HasHabitat() && distSq > SleepDistance * SleepDistance)
    {
      Velocity = Vector3.Zero;
      return;
    }

    var dist = Mathf.Sqrt(distSq);

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
      // AttackRange / cooldown / behavior switch are unchanged. Damage is
      // applied only when the attack volume overlaps the player Hurtbox.
      if (OverlapsPlayerHurtbox(attackRange))
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
      }

      _attackCooldown = EnemyData.AttackCooldown
        * CombatLogic.BossRageCooldownMultiplier(BossPhase());
      _animator?.NotifyAttack();
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
    if (!health.IsDead)
      _animator?.NotifyHit();

    if (health.IsDead)
      Die();
  }

  private void Die()
  {
    if (_dying)
      return;
    _dying = true;

    // Decision 9: EnemyDied fires for every enemy; bosses additionally fire
    // BossDefeated. Loot and events stay immediate so tests and drops do not
    // wait on the death clip; QueueFree waits for the visual when present.
    GameEvents.RaiseEnemyDied(EnemyData!.Id);
    if (EnemyData.Boss)
      GameEvents.RaiseBossDefeated(EnemyData.Id);

    TryDropLoot();
    CollisionLayer = 0;
    CollisionMask = 0;
    GetNodeOrNull<Hurtbox>(Hurtbox.NodeName)?.Disable();
    Velocity = Vector3.Zero;

    var hold = _animator?.NotifyDeath() ?? 0f;
    if (hold <= 0.05f)
    {
      QueueFree();
      return;
    }

    var tree = GetTree();
    if (tree == null)
    {
      QueueFree();
      return;
    }

    tree.CreateTimer(Mathf.Min(hold, 1.5f))
      .Timeout += OnDeathClipFinished;
  }

  private void OnDeathClipFinished()
  {
    if (IsInstanceValid(this))
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

  /// <summary>
  ///   True when a sphere of <paramref name="attackRange"/> around this enemy
  ///   overlaps the player's Hurtbox. AttackRange remains the AI gate;
  ///   this only decides whether damage is applied. Geometric fallback keeps
  ///   crabs/wolves connecting at the same range when physics is not yet
  ///   synced (headless tests).
  /// </summary>
  private bool OverlapsPlayerHurtbox(float attackRange)
  {
    var hurtbox = _player?.GetNodeOrNull<Hurtbox>(Hurtbox.NodeName);
    if (hurtbox == null || !GodotObject.IsInstanceValid(hurtbox))
      return false;
    if (hurtbox.CollisionLayer == 0 || !hurtbox.Monitorable)
      return false;

    var space = GetWorld3D()?.DirectSpaceState;
    if (space != null)
    {
      _attackQueryShape ??= new SphereShape3D();
      _attackQueryShape.Radius = attackRange;
      var query = new PhysicsShapeQueryParameters3D
      {
        Shape = _attackQueryShape,
        Transform = new Transform3D(Basis.Identity, GlobalPosition),
        CollisionMask = CombatLayers.PlayerHurtboxMask,
        CollideWithAreas = true,
        CollideWithBodies = false
      };
      var hits = space.IntersectShape(query, 8);
      foreach (var hit in hits)
      {
        if (
          hit.TryGetValue("collider", out var collider)
          && Hurtbox.IsPlayerHurtbox(collider.AsGodotObject())
        )
        {
          return true;
        }
      }
    }

    return GlobalPosition.DistanceTo(_player!.GlobalPosition) <= attackRange;
  }

  private bool HasHabitat() =>
    EnemyData != null
    && EnemyData.Habitat != EnemyHabitatKind.None
    && _wanderRadius > 0f;

  private void CaptureHabitat()
  {
    _homePosition = GlobalPosition;
    _wanderTarget = _homePosition;
    _attractorXz = new Vector2(_homePosition.X, _homePosition.Z);
    _wanderRadius = 0f;
    if (EnemyData == null || EnemyData.Habitat == EnemyHabitatKind.None)
      return;

    _wanderRadius = EnemyHabitat.EffectiveWanderRadius(
      EnemyData.Habitat, EnemyData.WanderRadius);

    var tree = GetTree();
    if (tree == null)
      return;

    long seed = 12345;
    if (tree.Root.FindChild("IslandBuilder", recursive: true, owned: false)
        is IslandBuilder builder)
      seed = builder.WorldSeed;

    _attractorXz = EnemyHabitat.WorldAttractor(
      new Vector2(_homePosition.X, _homePosition.Z),
      EnemyData.Habitat,
      WorldLayout.Generate(seed));
  }

  private Vector3 HorizontalToward(Vector3 world)
  {
    var d = world - GlobalPosition;
    d.Y = 0f;
    return d.LengthSquared() > 0.0001f ? d.Normalized() : Vector3.Zero;
  }

  /// <summary>
  ///   Direction used by the existing chase movers: chase while the player
  ///   is in the den disk, otherwise den / forage / drink-beach. Does not
  ///   add AI states.
  /// </summary>
  private Vector3 HabitatMoveDirection(float dt)
  {
    if (!HasHabitat())
      return HorizontalDirectionToPlayer();

    _wanderClock += dt;
    bool leaking = EnemyHabitat.IsLeakingLeash(
      _homePosition, _wanderRadius, GlobalPosition);
    bool chase = !leaking && EnemyHabitat.ShouldChase(
      _homePosition, _wanderRadius, GlobalPosition,
      _player!.GlobalPosition, EnemyData!.AttackRange);

    Vector3 desired;
    if (chase)
    {
      desired = HorizontalDirectionToPlayer();
    }
    else if (leaking)
    {
      desired = HorizontalToward(_homePosition);
    }
    else
    {
      int slot = (int)(_wanderClock / EnemyHabitat.WanderSlotSeconds) % 4;
      if (slot != _wanderSlot)
      {
        _wanderSlot = slot;
        var attractor = new Vector3(_attractorXz.X, GlobalPosition.Y, _attractorXz.Y);
        _wanderTarget = EnemyHabitat.PickWanderTarget(
          _homePosition, attractor, _wanderRadius, slot);
      }

      var to = _wanderTarget - GlobalPosition;
      to.Y = 0f;
      if (to.LengthSquared() < 0.64f)
        return Vector3.Zero;

      desired = to.Normalized();
    }

    return ConstrainDirToHabitat(desired);
  }

  private Vector3 ConstrainDirToHabitat(Vector3 desired)
  {
    if (!HasHabitat() || desired.LengthSquared() < 0.0001f)
      return desired;

    var offset = new Vector3(
      GlobalPosition.X - _homePosition.X, 0f, GlobalPosition.Z - _homePosition.Z);
    float dist = offset.Length();
    if (dist < _wanderRadius * 0.98f)
      return desired;
    if (dist < 0.0001f)
      return desired;

    var outward = offset / dist;
    float radial = desired.Dot(outward);
    if (radial <= 0f)
      return desired;

    var tangent = desired - outward * radial;
    return tangent.LengthSquared() < 0.0001f ? -outward : tangent.Normalized();
  }

  private void MoveMeleeChase(float dt)
  {
    // T8.5.5: night speed — wolves keep their 1.5× hunt boost, every other
    // melee chaser gets the general 1.25× (CombatLogic.NightSpeedMultiplier);
    // mutants add their rage speed on top (Iter6.1).
    var speed = EnemyData!.MoveSpeed
      * CombatLogic.NightSpeedMultiplier(EnemyData.Id, IsNight())
      * RageSpeedMultiplier();

    var dir = HabitatMoveDirection(dt);
    Velocity = new Vector3(dir.X * speed, Velocity.Y + Gravity * dt, dir.Z * speed);
    MoveAndSlide();
  }

  private void MoveCharge(float dt, float dist)
  {
    _chargeCooldownRemaining = Mathf.Max(0f, _chargeCooldownRemaining - dt);
    _chargeRemaining = Mathf.Max(0f, _chargeRemaining - dt);

    bool chasing = HasHabitat()
      && EnemyHabitat.ShouldChase(
        _homePosition, _wanderRadius, GlobalPosition,
        _player!.GlobalPosition, EnemyData!.AttackRange)
      && !EnemyHabitat.IsLeakingLeash(_homePosition, _wanderRadius, GlobalPosition);
    if (!HasHabitat())
      chasing = true;

    // Pure logic decides engagement; once engaged the boost holds for the
    // whole ChargeCooldown window so the charge is a real dash, and the
    // cooldown pauses the next one (Decision 7/8). Habitat species only
    // dash while actually chasing (not while walking back to the den).
    if (
      chasing
      && _chargeRemaining <= 0f
      && CombatLogic.ChargeSpeedMultiplier(dist, ChargeRange, _chargeCooldownRemaining <= 0f) > 1f
    )
    {
      _chargeRemaining = ChargeCooldown;
      _chargeCooldownRemaining = ChargeCooldown;
    }

    var speed = EnemyData!.MoveSpeed
      * CombatLogic.NightSpeedMultiplier(EnemyData.Id, IsNight())
      * (chasing && _chargeRemaining > 0f ? ChargeSpeedMultiplierValue : 1f);

    var dir = HabitatMoveDirection(dt);
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
