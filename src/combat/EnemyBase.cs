// Original (Iter6) — no upstream port
namespace SeaAnomaly;

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
  [Export] public float NightSpeedMultiplierValue = 1.5f;

  #endregion Exports

  private CharacterBody3D? _player;
  private DayNightService? _dayNight;
  private EnemyHealth? _health;
  private StandardMaterial3D? _material;
  private Color _tintColor = new(0.8f, 0.8f, 0.8f);

  private float _attackCooldown;
  private float _chargeCooldownRemaining;
  private float _chargeRemaining;
  private float _flashRemaining;

  /// <summary>Exposed for tests (null until health is initialized).</summary>
  public EnemyHealth? HealthTracker => _health;

  public override void _Ready()
  {
    if (!Player.IsEmpty)
      _player = GetNodeOrNull<CharacterBody3D>(Player);
    if (!DayNightServicePath.IsEmpty)
      _dayNight = GetNodeOrNull<DayNightService>(DayNightServicePath);

    if (EnemyData != null)
      _health = new EnemyHealth(EnemyData.MaxHealth);

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

    switch (EnemyData.Behavior)
    {
      case EnemyBehavior.MeleeChase:
        MoveMeleeChase(dt);
        break;
      case EnemyBehavior.Charge:
        MoveCharge(dt, dist);
        break;
      case EnemyBehavior.Swimmer:
        MoveSwimmer();
        break;
    }

    if (CombatLogic.ShouldAttack(dist, EnemyData.AttackRange, _attackCooldown <= 0f))
    {
      _player.GetNodeOrNull<PlayerStats>("PlayerStats")?.TakeDamage(EnemyData.Damage);
      _attackCooldown = EnemyData.AttackCooldown;
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

  private void TryDropLoot()
  {
    if (string.IsNullOrEmpty(EnemyData!.DropItemId) || _player == null)
      return;

    var inventory = _player.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    var drop = GD.Load<ItemData>($"res://assets/items/{EnemyData.DropItemId}.tres");
    if (drop != null)
      inventory.AddItem(drop, EnemyData.DropAmount);
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
  ///   reference so the hit flash never leaks across instances.
  /// </summary>
  private void SetupMaterial()
  {
    var visual = GetNodeOrNull<MeshInstance3D>("Visual");
    if (visual == null)
      return;

    _material = visual.MaterialOverride is StandardMaterial3D existing
      ? (StandardMaterial3D)existing.Duplicate()
      : new StandardMaterial3D();
    visual.MaterialOverride = _material;

    _tintColor = GetTint(EnemyData?.Id ?? "");
    _material.AlbedoColor = _tintColor;
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
    // Wolves get the night boost (Decision 7/8).
    var boost = CombatLogic.NightSpeedMultiplier(EnemyData!.Id, IsNight());
    var speed = EnemyData.MoveSpeed * (boost > 1f ? NightSpeedMultiplierValue : 1f);

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

    var speed = EnemyData!.MoveSpeed * (_chargeRemaining > 0f ? ChargeSpeedMultiplierValue : 1f);

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

    Velocity = new Vector3(
      horizontalDir.X * EnemyData!.MoveSpeed,
      verticalDir * EnemyData.MoveSpeed,
      horizontalDir.Z * EnemyData.MoveSpeed
    );
    MoveAndSlide();
  }
}
