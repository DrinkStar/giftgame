// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   R1 (Iter8p) reserve contract: the null talent tree/skill host are
///   no-ops, an injected modifier source doubles resolved melee damage
///   through the public WeaponSystem seam, the ProgressionService defaults
///   every multiplier to 1, and PlayerStats/PlayerController keep ×1
///   behavior (no crash) when no service is wired.
/// </summary>
public class ProgressionReserveTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private Node _player = default!;
  private InventorySystem _inventory = default!;
  private WeaponSystem _weapon = default!;
  private PlayerStats? _stats;
  private PlayerController? _controller;

  public ProgressionReserveTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Spear in hotbar slot 0 so ResolveMeleeDamage uses the plain
    // MeleeDamage base (only bows switch to BowMeleeDamage).
    _inventory = new InventorySystem
    {
      Name = "InventorySystem",
      StartingItems = new Godot.Collections.Array<Godot.Collections.Dictionary>
      {
        BuildEntry("wooden_spear", 1)
      }
    };

    _player = new Node { Name = "Player" };
    _player.AddChild(_inventory);
    _weapon = new WeaponSystem
    {
      Name = "WeaponSystem",
      InventoryPath = "../InventorySystem"
    };
    _player.AddChild(_weapon);

    _fixture.AddToRoot(_player, autoRemoveFromRoot: true);
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
    if (_player == null)
      return;

    _player.Dispose();
    _player = null!;
    _stats?.Dispose();
    _stats = null;
    _controller?.Dispose();
    _controller = null;
    GC.SuppressFinalize(this);
  }

  private static Godot.Collections.Dictionary BuildEntry(string id, int amount) =>
    new()
    {
      ["item"] = GD.Load<ItemData>($"res://assets/items/{id}.tres"),
      ["amount"] = amount
    };

  [Test]
  public void NullTalentTreeAndSkillHostAreNoOps()
  {
    var tree = NullTalentTree.Instance;

    tree.IsUnlocked("any_talent").ShouldBeFalse();
    tree.Unlock("any_talent").ShouldBeFalse(); // no-op, never throws
    tree.AllIds.ShouldBeEmpty();
    ((IModifierSource)tree).GetMultiplier("melee_damage").ShouldBe(1f);
    ((IModifierSource)tree).GetMultiplier("move_speed").ShouldBe(1f);

    NullSkillHost.Instance.TryActivate("any_skill").ShouldBeFalse();
  }

  [Test]
  public void InjectedMeleeDamageMultiplierDoublesResolvedDamage()
  {
    // No service → ×1 baseline through the public seam.
    _weapon.Progression.ShouldBeNull();
    _weapon.ResolveMeleeDamage().ShouldBe(_weapon.MeleeDamage);

    // Inject a service whose tree doubles melee_damage only.
    var service = new ProgressionService { TalentTree = new DoubleMeleeTree() };
    _weapon.Progression = service;

    _weapon.ResolveMeleeDamage().ShouldBe(_weapon.MeleeDamage * 2f);

    // The fake tree only knows melee_damage — the other seams stay ×1.
    _weapon.ResolveThrowDamage().ShouldBe(_weapon.SpearThrowDamage);
    _weapon.ResolveRangedDamage().ShouldBe(_weapon.ArrowDamage);

    _weapon.Progression = null;
    service.Dispose();
  }

  [Test]
  public void ProgressionServiceDefaultsEveryMultiplierToOne()
  {
    var service = new ProgressionService();

    service.TalentTree.ShouldBeSameAs(NullTalentTree.Instance);
    service.SkillHost.ShouldBeSameAs(NullSkillHost.Instance);

    foreach (var statId in new[]
             {
               "melee_damage", "throw_damage", "ranged_damage", "stamina_cost",
               "hunger_rate", "thirst_rate", "move_speed", "max_health",
               "totally_unknown_stat"
             })
    {
      service.GetMultiplier(statId).ShouldBe(1f);
    }

    service.Dispose();
  }

  [Test]
  public void PlayerStatsWithNullProgressionScalesByOne()
  {
    _stats = new PlayerStats { Progression = null };
    _fixture.AddToRoot(_stats, autoRemoveFromRoot: true);

    // max_health: the computed property mirrors the untouched field and the
    // node initializes to exactly that value.
    _stats.EffectiveMaxHealth.ShouldBe(_stats.MaxHealth);
    _stats.Health.ShouldBe(_stats.MaxHealth);

    // hunger_rate/thirst_rate: one 0.5 s tick drains exactly rate × 0.5 × 1
    // (a small tolerance absorbs real engine frames interleaving _Process).
    _stats.Hunger = _stats.MaxHunger;
    _stats.Thirst = _stats.MaxThirst;
    _stats._Process(0.5);
    _stats.Hunger.ShouldBe(
      _stats.MaxHunger - _stats.HungerDecreaseRate * 0.5f, 0.1f
    );
    _stats.Thirst.ShouldBe(
      _stats.MaxThirst - _stats.ThirstDecreaseRate * 0.5f, 0.1f
    );

    // stamina_cost: one DrainStamina call spends exactly the amount × 1
    // (the 0.5 s suppression window keeps regen out of the picture).
    _stats.DrainStamina(10f);
    _stats.Stamina.ShouldBe(_stats.StaminaMax - 10f, 0.01f);
  }

  [Test]
  public void PlayerControllerWithNullProgressionStillTicksPhysics()
  {
    _controller = new PlayerController { Progression = null };
    _fixture.AddToRoot(_controller, autoRemoveFromRoot: true);

    // One full physics tick with no input: no crash, gravity accumulates
    // (the ×1 move_speed copy into PlayerMotion also ran unmodified).
    _controller._PhysicsProcess(0.016);
    _controller.Velocity.Y.ShouldBeLessThan(0f);
  }

  /// <summary>
  ///   Test double: unlocks nothing but reports a ×2 multiplier for the
  ///   melee_damage stat only.
  /// </summary>
  private sealed class DoubleMeleeTree : ITalentTree, IModifierSource
  {
    public bool IsUnlocked(string talentId) => false;

    public bool Unlock(string talentId) => false;

    public IEnumerable<string> AllIds => Array.Empty<string>();

    public float GetMultiplier(string statId) => statId == "melee_damage" ? 2f : 1f;
  }
}
