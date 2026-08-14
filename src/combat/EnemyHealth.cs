// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure enemy health tracker (Iter6 plan Decision 7). No scene tree access —
///   <see cref="EnemyBase"/> owns one instance per enemy and reads
///   <see cref="IsDead"/> to decide when to die. Fully unit-testable.
/// </summary>
public class EnemyHealth
{
  public float MaxHealth { get; }

  public float Health { get; private set; }

  public bool IsDead { get; private set; }

  public EnemyHealth(float maxHealth)
  {
    MaxHealth = Mathf.Max(0f, maxHealth);
    Health = MaxHealth;
    IsDead = false;
  }

  /// <summary>
  ///   Applies damage and returns the clamped remaining health. Negative or
  ///   zero damage is ignored, and damage after death is ignored (no double
  ///   death transitions).
  /// </summary>
  public float TakeDamage(float damage)
  {
    if (IsDead || damage <= 0f)
      return Health;

    Health = Mathf.Max(0f, Health - damage);
    if (Health <= 0f)
      IsDead = true;

    return Health;
  }

  /// <summary>Marks the enemy dead and zeroes its health.</summary>
  public void Die()
  {
    Health = 0f;
    IsDead = true;
  }
}
