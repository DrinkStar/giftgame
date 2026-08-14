// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Weapon controller mounted under the player (Iter6 plan Decision 1/2/3):
///   tools ARE weapons. LMB melee-swings (weak for bows), RMB throws the held
///   spear (consumes 1) or fires an arrow (consumes 1); no ammo, no attack.
///   Dispatch is delegated to the pure <see cref="CombatLogic.ResolveAttack"/>
///   so the rules stay unit-testable, and all attacks are cooldown-gated by
///   <see cref="CombatLogic.IsReady"/>.
///
///   Iter6.1 todo 5 (Decision D3): a second weapon slot on the inventory is
///   toggled with the <c>weapon_slot_switch</c> input (Q). When active, the
///   secondary slot item drives attacks instead of the hotbar selection
///   (empty secondary falls back to primary). The toggle publishes
///   <see cref="GameEvents.WeaponSlotChanged"/>.
///
///   Decision 11: while build mode is active every attack input is ignored
///   (subscribed via GameEvents.BuildModeChanged, unsubscribed in _ExitTree).
/// </summary>
public partial class WeaponSystem : Node
{
  #region Input action names (secondary_attack added to project.godot by W2)

  public const string AttackAction = "attack";
  public const string SecondaryAttackAction = "secondary_attack";
  public const string SlotSwitchAction = "weapon_slot_switch";
  public const string ArrowItemId = "arrow";

  #endregion Input action names

  #region Exports (Decision 2)

  [Export] public NodePath CameraPath = "../CameraPivot/Camera3D";
  [Export] public NodePath InventoryPath = "../InventorySystem";

  [Export] public float MeleeDamage = 15f;
  [Export] public float MeleeRange = 3f;
  [Export] public float MeleeCooldown = 0.5f;
  [Export] public float BowMeleeDamage = 5f;
  [Export] public float SpearThrowDamage = 25f;
  [Export] public float SpearSpeed = 20f;
  [Export] public float SpearGravity = 2f;
  [Export] public float ArrowDamage = 20f;
  [Export] public float ArrowSpeed = 25f;
  [Export] public float ArrowGravity = 9.8f;
  [Export] public float ProjectileLifetime = 5f;

  #endregion Exports

  private const uint EnemyCollisionMask = 128u;

  private Camera3D? _camera;
  private InventorySystem? _inventory;
  private bool _buildMode;
  private bool _secondarySlotActive;
  private float _attackElapsed = 1f;

  public override void _Ready()
  {
    _camera = GetNodeOrNull<Camera3D>(CameraPath);
    _inventory = GetNodeOrNull<InventorySystem>(InventoryPath);

    GameEvents.BuildModeChanged += OnBuildModeChanged;
  }

  public override void _ExitTree()
  {
    GameEvents.BuildModeChanged -= OnBuildModeChanged;
  }

  public override void _Process(double delta)
  {
    _attackElapsed += (float)delta;
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    // FIX(iter7-plan): T7.0 — no attacks while dead. WeaponSystem's parent IS
    // the Player node, so no new export is needed to reach the controller's
    // stats; unrelated parents (test fixtures) resolve to null and pass through.
    if (GetParentOrNull<PlayerController>()?.Stats is { IsAlive: false })
      return;

    if (@event.IsActionPressed(SlotSwitchAction))
    {
      ToggleWeaponSlot();
      return;
    }

    // Decision 11: no attack input while build mode is active.
    if (_buildMode)
      return;

    if (@event.IsActionPressed(AttackAction))
      TryMelee();
    else if (@event.IsActionPressed(SecondaryAttackAction))
      TrySecondary();
  }

  /// <summary>
  ///   True while the secondary weapon slot drives attacks (Iter6.1 todo 5).
  ///   Flipped by the <c>weapon_slot_switch</c> input (Q) via
  ///   <see cref="ToggleWeaponSlot"/>; publishing
  ///   <see cref="GameEvents.WeaponSlotChanged"/> on every change.
  /// </summary>
  public bool SecondarySlotActive
  {
    get => _secondarySlotActive;
    set
    {
      _secondarySlotActive = value;
      GameEvents.RaiseWeaponSlotChanged(
        _inventory?.SelectedItem, _inventory?.SecondaryItem
      );
    }
  }

  /// <summary>Q: toggles between the primary hotbar slot and the secondary slot.</summary>
  public void ToggleWeaponSlot() => SecondarySlotActive = !_secondarySlotActive;

  /// <summary>
  ///   The item the next attack uses: the secondary slot item when the
  ///   secondary slot is active and equipped, otherwise the hotbar's
  ///   <see cref="InventorySystem.SelectedItem"/>. Pure resolution lives in
  ///   <see cref="CombatLogic.EffectiveItem"/> (test seam).
  /// </summary>
  public ItemData? ResolveEffectiveItem() =>
    CombatLogic.EffectiveItem(
      _inventory?.SelectedItem, _inventory?.SecondaryItem, _secondarySlotActive
    );

  /// <summary>
  ///   Resolves what the effective item can perform (Decision 1
  ///   ammo-gated dispatch). Public test seam — the input handlers funnel
  ///   through the same rules.
  /// </summary>
  public AttackType ResolveCurrentAttack()
  {
    if (_inventory == null)
      return AttackType.None;

    var sel = ResolveEffectiveItem();
    if (sel == null)
      return AttackType.None;

    var isTool = sel.Type == ItemType.Tool;
    return CombatLogic.ResolveAttack(
      sel.Id,
      _inventory.HasItem(sel.Id, 1),
      _inventory.HasItem(ArrowItemId, 1),
      isTool
    );
  }

  private void OnBuildModeChanged(bool enabled) => _buildMode = enabled;

  /// <summary>LMB: melee swing for every tool, weak for bows (Decision 1).</summary>
  private void TryMelee()
  {
    if (!CombatLogic.IsReady(_attackElapsed, MeleeCooldown))
      return;

    var sel = ResolveEffectiveItem();
    if (sel == null || sel.Type != ItemType.Tool)
      return;

    // Bows melee weakly; everything else (spear/axe/other tools) hits hard.
    var damage = sel.Id.Contains("bow") ? BowMeleeDamage : MeleeDamage;

    _attackElapsed = 0f;
    PerformMelee(damage);
  }

  /// <summary>RMB: spear throw / bow shot depending on the resolved attack.</summary>
  private void TrySecondary()
  {
    if (_inventory == null)
      return;

    var sel = ResolveEffectiveItem();
    if (sel == null)
      return;

    switch (ResolveCurrentAttack())
    {
      case AttackType.Throw:
        if (
          CombatLogic.IsReady(_attackElapsed, MeleeCooldown)
          && _inventory.RemoveItem(sel.Id, 1)
        )
        {
          _attackElapsed = 0f;
          SpawnProjectile(SpearThrowDamage, SpearSpeed, SpearGravity);
        }
        break;

      case AttackType.Shoot:
        if (
          CombatLogic.IsReady(_attackElapsed, MeleeCooldown)
          && _inventory.RemoveItem(ArrowItemId, 1)
        )
        {
          _attackElapsed = 0f;
          SpawnProjectile(ArrowDamage, ArrowSpeed, ArrowGravity);
        }
        break;
    }
  }

  /// <summary>
  ///   Decision 3: a ray from the camera forward by MeleeRange, masked to the
  ///   Enemies layer (8) only — the player body (layer 2) can never be hit.
  /// </summary>
  private void PerformMelee(float damage)
  {
    if (_camera == null)
      return;

    var from = _camera.GlobalPosition;
    var forward = -_camera.GlobalTransform.Basis.Z;
    var query = PhysicsRayQueryParameters3D.Create(
      from, from + forward * MeleeRange, EnemyCollisionMask
    );

    var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (
      hit.TryGetValue("collider", out var collider)
      && collider.AsGodotObject() is EnemyBase enemy
    )
    {
      enemy.TakeDamage(damage);
    }
  }

  /// <summary>
  ///   Spawns a projectile from scenes/combat/projectile.tscn 1m in front of
  ///   the camera with an initial velocity along the camera forward vector.
  /// </summary>
  private void SpawnProjectile(float damage, float speed, float gravity)
  {
    if (_camera == null)
      return;

    var packed = GD.Load<PackedScene>("res://scenes/combat/projectile.tscn");
    var projectile = packed?.Instantiate<Projectile>();
    if (projectile == null)
      return;

    var forward = -_camera.GlobalTransform.Basis.Z;

    projectile.Damage = damage;
    projectile.Speed = speed;
    projectile.Gravity = gravity;
    projectile.Lifetime = ProjectileLifetime;
    projectile.GlobalPosition = _camera.GlobalPosition + forward;
    projectile.SetVelocity(forward * speed);

    GetTree().CurrentScene?.AddChild(projectile);
  }
}
