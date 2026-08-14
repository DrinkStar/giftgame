// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Survival stats (health/hunger/thirst/stamina), ported from upstream
///   SurvivalIsland's PlayerStats with these adaptations:
///   - the Warmth chain is intentionally dropped (no body temperature in the
///     iteration design; ambient temperature stays available as environment
///     data via <see cref="DayNightMath"/>);
///   - stamina replaces warmth: sprinting drains it, jumping costs a fixed
///     amount, and it regenerates when not draining (plan Decision 2);
///   - upstream bug fixed: upstream emitted PlayerDied from the Health setter
///     every time health was <= 0, so repeated damage after death fired the
///     signal once per frame. The _deathNotified guard emits it EXACTLY once.
///   All signals are published through the static <see cref="GameEvents"/>
///   bus. This node only publishes; it subscribes to nothing, so no
///   _ExitTree unsubscribe is needed.
/// </summary>
public partial class PlayerStats : Node
{
  [Export] public float MaxHealth = 100f;
  [Export] public float MaxHunger = 100f;
  [Export] public float MaxThirst = 100f;
  [Export] public float StaminaMax = 100f;

  [Export] public float HungerDecreaseRate = 1f / 30f; // Per second
  [Export] public float ThirstDecreaseRate = 1f / 20f; // Per second

  [Export] public float DamageFromNoHunger = 2f; // Per second
  [Export] public float DamageFromNoThirst = 3f; // Per second

  [Export] public float StaminaRegenRate = 30f; // Per second, when not draining
  [Export] public float SprintStaminaDrain = 20f; // Per second, read by PlayerController
  [Export] public float JumpStaminaCost = 10f;

  /// <summary>
  ///   How long stamina regeneration stays suppressed after a
  ///   <see cref="DrainStamina"/> call, in milliseconds. This is a
  ///   simplification of the upstream continuous-drain model: the sprint
  ///   controller drains every physics tick, so each tick keeps pushing the
  ///   suppression window forward and regen stays paused while sprinting;
  ///   after the last drain the window expires and regen resumes.
  /// </summary>
  private const ulong StaminaDrainSuppressionMs = 500;

  private float _health;
  private float _hunger;
  private float _thirst;
  private float _stamina;

  /// <summary>Seconds left on the active slow (0 = no slow).</summary>
  private float _slowRemaining;

  /// <summary>Speed factor applied while <see cref="_slowRemaining"/> is active.</summary>
  private float _slowFactor = 1f;

  private bool _deathNotified;
  private ulong _staminaDrainUntilMs;

  public float Health
  {
    get => _health;
    set
    {
      _health = Mathf.Clamp(value, 0, MaxHealth);
      GameEvents.RaiseHealthChanged(_health, MaxHealth);
      if (_health <= 0 && !_deathNotified)
      {
        // Upstream bug fix: upstream emitted PlayerDied on every write to
        // Health while health was 0 (e.g. starvation damage each frame).
        _deathNotified = true;
        GameEvents.RaisePlayerDied();
      }
    }
  }

  public float Hunger
  {
    get => _hunger;
    set
    {
      _hunger = Mathf.Clamp(value, 0, MaxHunger);
      GameEvents.RaiseHungerChanged(_hunger, MaxHunger);
    }
  }

  public float Thirst
  {
    get => _thirst;
    set
    {
      _thirst = Mathf.Clamp(value, 0, MaxThirst);
      GameEvents.RaiseThirstChanged(_thirst, MaxThirst);
    }
  }

  public float Stamina
  {
    get => _stamina;
    set
    {
      _stamina = Mathf.Clamp(value, 0, StaminaMax);
      GameEvents.RaiseStaminaChanged(_stamina, StaminaMax);
    }
  }

  public bool IsAlive => Health > 0;

  public override void _Ready()
  {
    _health = MaxHealth;
    _hunger = MaxHunger;
    _thirst = MaxThirst;
    _stamina = StaminaMax;
    _deathNotified = false;

    GameEvents.RaiseHealthChanged(_health, MaxHealth);
    GameEvents.RaiseHungerChanged(_hunger, MaxHunger);
    GameEvents.RaiseThirstChanged(_thirst, MaxThirst);
    GameEvents.RaiseStaminaChanged(_stamina, StaminaMax);
  }

  public override void _Process(double delta)
  {
    if (!IsAlive)
      return;

    var dt = (float)delta;

    // Decrease hunger and thirst over time.
    Hunger -= HungerDecreaseRate * dt;
    Thirst -= ThirstDecreaseRate * dt;

    // Apply damage from empty stats.
    if (Hunger <= 0)
      Health -= DamageFromNoHunger * dt;

    if (Thirst <= 0)
      Health -= DamageFromNoThirst * dt;

    // Stamina regenerates only outside the drain-suppression window
    // (simplification — see StaminaDrainSuppressionMs).
    if (Time.GetTicksMsec() >= _staminaDrainUntilMs)
      Stamina += StaminaRegenRate * dt;

    // Weakening slow from enemy hits (Iter6.1: spider webbing) ticks down.
    if (_slowRemaining > 0f)
      _slowRemaining = Mathf.Max(0f, _slowRemaining - dt);
  }

  public void Eat(float hungerRestore, float healthRestore = 0)
  {
    Hunger += hungerRestore;
    if (healthRestore > 0)
      Health += healthRestore;
  }

  public void Drink(float thirstRestore) => Thirst += thirstRestore;

  public void TakeDamage(float damage) => Health -= damage;

  public void Heal(float amount) => Health += amount;

  /// <summary>
  ///   FIX(iter7-plan): T7.0 respawn contract — death is recoverable. Restores
  ///   full health and stamina, raises hunger/thirst to the 30-point safety
  ///   line, clears any active movement slow (spider webbing) and re-arms the
  ///   death hook so PlayerDied can fire again on the next death. Values are
  ///   written directly and the four stat events are raised once each, mirroring
  ///   <see cref="_Ready"/>. This method does NOT raise GameOver — the death
  ///   contract keeps GameOver reserved for the true ending.
  /// </summary>
  public void Revive()
  {
    _health = MaxHealth;
    _stamina = StaminaMax;
    _hunger = Mathf.Max(_hunger, 30f);
    _thirst = Mathf.Max(_thirst, 30f);
    _slowRemaining = 0f;
    _deathNotified = false;

    GameEvents.RaiseHealthChanged(_health, MaxHealth);
    GameEvents.RaiseStaminaChanged(_stamina, StaminaMax);
    GameEvents.RaiseHungerChanged(_hunger, MaxHunger);
    GameEvents.RaiseThirstChanged(_thirst, MaxThirst);
  }

  /// <summary>
  ///   Applies a movement slow for <paramref name="duration"/> seconds at
  ///   <paramref name="factor"/> speed (e.g. 2 s at 0.5× from spider webbing,
  ///   Iter6.1). Re-applying overwrites the previous slow; a non-positive
  ///   duration is ignored.
  /// </summary>
  public void ApplySlow(float duration, float factor)
  {
    if (duration <= 0f)
      return;

    _slowRemaining = duration;
    _slowFactor = factor;
  }

  /// <summary>
  ///   Current movement speed multiplier: <see cref="_slowFactor"/> while a
  ///   slow is active, 1f otherwise. Exposed for PlayerController (and other
  ///   movement consumers) to read — wiring lands with the dual-weapon task.
  /// </summary>
  public float SpeedMultiplier => _slowRemaining > 0f ? _slowFactor : 1f;

  /// <summary>
  ///   Spends stamina (sprint tick or jump) and starts the 0.5 s suppression
  ///   window during which stamina does not regenerate.
  /// </summary>
  public void DrainStamina(float amount)
  {
    Stamina -= amount;
    _staminaDrainUntilMs = Time.GetTicksMsec() + StaminaDrainSuppressionMs;
  }

  public bool CanSprint() => Stamina > 0;
}
