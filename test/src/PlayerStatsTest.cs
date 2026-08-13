// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Behavioral tests for <see cref="PlayerStats"/>. The node is added to the
///   tree root so _Ready runs (publishing initial values) and engine _Process
///   frames tick it in real time; assertions either happen in the same
///   synchronous block (immune to frame interleaving) or account for the
///   real-time drain/regen explicitly.
/// </summary>
public class PlayerStatsTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private PlayerStats _stats = default!;

  public PlayerStatsTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _stats = new PlayerStats();
    _fixture.AddToRoot(_stats, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable <see cref="PlayerStats"/> field satisfies CA1001. The
  ///   null-out makes double disposal safe.
  /// </summary>
  public void Dispose()
  {
    if (_stats == null)
      return;

    _stats.Dispose();
    _stats = null!;
    GC.SuppressFinalize(this);
  }

  [Test]
  public void EatClampsHungerAtMax()
  {
    _stats.Hunger = 50f;
    _stats.Eat(1000f);
    _stats.Hunger.ShouldBe(_stats.MaxHunger);
  }

  [Test]
  public void DrinkClampsThirstAtMax()
  {
    _stats.Thirst = 50f;
    _stats.Drink(1000f);
    _stats.Thirst.ShouldBe(_stats.MaxThirst);
  }

  [Test]
  public void TakeDamagePastZeroEmitsPlayerDiedExactlyOnce()
  {
    var deathCount = 0;
    Action onDeath = () => deathCount++;
    GameEvents.PlayerDied += onDeath;
    try
    {
      // Upstream bug fix (locked here): upstream emitted PlayerDied on every
      // Health write while health was 0 — damaging twice must fire it once.
      _stats.TakeDamage(200f);
      _stats.Health.ShouldBe(0f);

      _stats.TakeDamage(200f);
      deathCount.ShouldBe(1);
    }
    finally
    {
      GameEvents.PlayerDied -= onDeath;
    }
  }

  [Test]
  public void DrainStaminaToZeroAndRegenSuppressedDuringWindow()
  {
    _stats.DrainStamina(1000f);
    _stats.Stamina.ShouldBe(0f);

    // Within the 0.5 s suppression window regeneration is paused, even if
    // the process tick runs.
    _stats._Process(0.1);
    _stats.Stamina.ShouldBe(0f);
  }

  [Test]
  public async Task StaminaRegeneratesAfterDrainSuppressionWindow()
  {
    _stats.DrainStamina(20f);
    var drained = _stats.Stamina;

    // Wait out the 0.5 s window (engine frames regen on their own meanwhile).
    await Task.Delay(600);

    _stats._Process(0.5);
    _stats.Stamina.ShouldBeGreaterThan(drained);
  }

  [Test]
  public void CanSprintIsFalseAtZeroStamina()
  {
    _stats.CanSprint().ShouldBeTrue();

    _stats.DrainStamina(1000f);
    _stats.CanSprint().ShouldBeFalse();
  }
}
