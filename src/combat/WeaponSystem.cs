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
  public const string UseItemAction = "use_item";
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

  /// <summary>
  ///   R1 (Iter8p): optional progression service. Null (the shipped default)
  ///   keeps every damage multiplier at ×1; a real talent tree lands later.
  /// </summary>
  [Export] public ProgressionService? Progression;

  /// <summary>
  ///   T8p.6 (iter8p-plan Decision 7): the torch light node under the
  ///   player. Resolved null-safely — when the node is missing the torch
  ///   simply has no light. FIX(iter8p-plan): T8p.6 torch light — still
  ///   weak melee, matching unchanged (a torch IS a tool).
  /// </summary>
  [Export] public NodePath TorchLightPath = new NodePath("../TorchLight");

  #endregion Exports

  private const uint EnemyCollisionMask = 128u;

  private Camera3D? _camera;
  private InventorySystem? _inventory;
  private OmniLight3D? _torchLight;
  private bool _buildMode;
  private bool _secondarySlotActive;
  private float _attackElapsed = 1f;

  public override void _Ready()
  {
    _camera = GetNodeOrNull<Camera3D>(CameraPath);
    _inventory = GetNodeOrNull<InventorySystem>(InventoryPath);
    _torchLight = GetNodeOrNull<OmniLight3D>(TorchLightPath);

    GameEvents.BuildModeChanged += OnBuildModeChanged;
  }

  public override void _ExitTree()
  {
    GameEvents.BuildModeChanged -= OnBuildModeChanged;
  }

  public override void _Process(double delta)
  {
    _attackElapsed += (float)delta;
    UpdateTorchLight();
  }

  /// <summary>
  ///   T8p.6 (iter8p-plan Decision 7): light-update seam — the TorchLight
  ///   OmniLight3D (a sibling of this node under the player) is visible
  ///   exactly while the hotbar's SelectedItem is a torch; switching away
  ///   (or empty hands) turns it off. Torch acquisition is out of scope
  ///   this iteration (no recipe, no starting item) — tests inject the
  ///   item directly, per the plan.
  /// </summary>
  private void UpdateTorchLight()
  {
    if (_torchLight != null)
      _torchLight.Visible = _inventory?.SelectedItem?.Id == "torch";
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    // FIX(iter7-plan): T7.0 — no attacks while dead. WeaponSystem's parent IS
    // the Player node, so no new export is needed to reach the controller's
    // stats; unrelated parents (test fixtures) resolve to null and pass through.
    if (GetParentOrNull<PlayerController>()?.Stats is { IsAlive: false })
      return;

    // FIX(iter8-plan): T8.4 — a modal overlay (CraftUI) may lock gameplay
    // input; consume/attack must not fire underneath it.
    if (GameEvents.GameplayInputLocked)
      return;

    if (@event.IsActionPressed(SlotSwitchAction))
    {
      ToggleWeaponSlot();
      return;
    }

    // T8p.1 (iter8p-plan Decision 3): consume the hotbar-selected item with F.
    // Placed AFTER the dead check but BEFORE the build-mode guard, so eating
    // and drinking keep working while build mode is active.
    if (@event.IsActionPressed(UseItemAction))
    {
      TryUseItem();
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

  #region R1 (Iter8p) damage resolution seams

  /// <summary>
  ///   R1 (Iter8p): base melee damage × the melee_damage multiplier. Bows
  ///   melee weakly (BowMeleeDamage) just like <see cref="TryMelee"/>. Public
  ///   test seam — the multiplier is ×1 until a real talent tree is injected.
  /// </summary>
  public float ResolveMeleeDamage()
  {
    var sel = ResolveEffectiveItem();
    var baseDamage = sel?.Id.Contains("bow") == true ? BowMeleeDamage : MeleeDamage;
    return baseDamage * (Progression?.GetMultiplier("melee_damage") ?? 1f);
  }

  /// <summary>
  ///   R1 (Iter8p): base spear throw damage × the throw_damage multiplier.
  ///   Public test seam — the multiplier is ×1 until a real talent tree is
  ///   injected.
  /// </summary>
  public float ResolveThrowDamage() =>
    SpearThrowDamage * (Progression?.GetMultiplier("throw_damage") ?? 1f);

  /// <summary>
  ///   R1 (Iter8p): base arrow damage × the ranged_damage multiplier. Public
  ///   test seam — the multiplier is ×1 until a real talent tree is injected.
  /// </summary>
  public float ResolveRangedDamage() =>
    ArrowDamage * (Progression?.GetMultiplier("ranged_damage") ?? 1f);

  #endregion R1 (Iter8p) damage resolution seams

  /// <summary>
  ///   T8p.1 (iter8p-plan): consume the hotbar's SelectedItem (the primary
  ///   hotbar slot — NOT the secondary weapon slot). Food feeds hunger (and
  ///   health when the item restores it), drink quenches thirst; each use
  ///   consumes one unit. FIX(iter8p-plan): T8p.1 use item — RequiresCooking
  ///   gate comes later (T8.4b).
  /// </summary>
  private void TryUseItem()
  {
    var sel = _inventory?.SelectedItem;
    if (sel == null)
      return;

    var stats = GetParentOrNull<PlayerController>()?.Stats;
    if (stats == null)
      return;

    switch (sel.Type)
    {
      case ItemType.Food:
        // FIX(iter8-plan): T8.4b — cooking gate: raw food cannot be eaten raw.
        // The hint rides the existing GuideLine subtitle so the player sees
        // why the item was refused (no new UI).
        if (sel.RequiresCooking)
        {
          GameEvents.RaiseGuideLine($"需要烹饪：{sel.DisplayName}");
          return;
        }

        stats.Eat(sel.HungerRestore, sel.HealthRestore);
        _inventory?.RemoveItem(sel.Id, 1);
        break;

      case ItemType.Drink:
        stats.Drink(sel.ThirstRestore);
        _inventory?.RemoveItem(sel.Id, 1);
        break;
    }
  }

  /// <summary>LMB: melee swing for every tool, weak for bows (Decision 1).</summary>
  private void TryMelee()
  {
    if (!CombatLogic.IsReady(_attackElapsed, MeleeCooldown))
      return;

    var sel = ResolveEffectiveItem();
    if (sel == null || sel.Type != ItemType.Tool)
      return;

    // R1 (Iter8p): damage flows through the public resolution seam so the
    // progression multiplier applies (×1 until a talent tree is wired).
    var damage = ResolveMeleeDamage();

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
          // R1 (Iter8p): through the resolution seam (throw_damage multiplier).
          SpawnProjectile(ResolveThrowDamage(), SpearSpeed, SpearGravity);
        }
        break;

      case AttackType.Shoot:
        if (
          CombatLogic.IsReady(_attackElapsed, MeleeCooldown)
          && _inventory.RemoveItem(ArrowItemId, 1)
        )
        {
          _attackElapsed = 0f;
          // R1 (Iter8p): through the resolution seam (ranged_damage multiplier).
          SpawnProjectile(ResolveRangedDamage(), ArrowSpeed, ArrowGravity);
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
      // FIX(iter8p-plan): T8p.5 sfx hook — melee hit event
      GameEvents.RaiseMeleeHit(ResolveEffectiveItem()?.Id ?? "");
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
