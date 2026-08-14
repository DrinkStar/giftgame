// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure <see cref="EnemyHealth"/> tracker (Iter6 plan
///   Decision 7): damage, death, overkill clamping and boundary behavior.
/// </summary>
public class EnemyHealthTest : TestClass
{
  public EnemyHealthTest(Node testScene) : base(testScene) { }

  [Test]
  public void Constructor_StartsAtFullHealthAndAlive()
  {
    var health = new EnemyHealth(30f);
    health.Health.ShouldBe(30f);
    health.MaxHealth.ShouldBe(30f);
    health.IsDead.ShouldBeFalse();
  }

  [Test]
  public void TakeDamage_ReducesHealthAndReturnsRemaining()
  {
    var health = new EnemyHealth(30f);
    health.TakeDamage(10f).ShouldBe(20f);
    health.Health.ShouldBe(20f);
    health.IsDead.ShouldBeFalse();
  }

  [Test]
  public void TakeDamage_Overkill_ClampsToZeroAndMarksDead()
  {
    var health = new EnemyHealth(30f);
    health.TakeDamage(999f).ShouldBe(0f);
    health.Health.ShouldBe(0f);
    health.IsDead.ShouldBeTrue();
  }

  [Test]
  public void TakeDamage_ExactlyMaxHealth_KillsAtBoundary()
  {
    var health = new EnemyHealth(30f);
    health.TakeDamage(30f).ShouldBe(0f);
    health.IsDead.ShouldBeTrue();
  }

  [Test]
  public void TakeDamage_AfterDeath_IsIgnored()
  {
    var health = new EnemyHealth(30f);
    health.TakeDamage(999f);
    health.TakeDamage(50f).ShouldBe(0f);
    health.Health.ShouldBe(0f);
    health.IsDead.ShouldBeTrue();
  }

  [Test]
  public void TakeDamage_ZeroOrNegative_IsIgnored()
  {
    var health = new EnemyHealth(30f);
    health.TakeDamage(0f).ShouldBe(30f);
    health.TakeDamage(-5f).ShouldBe(30f);
    health.IsDead.ShouldBeFalse();
  }

  [Test]
  public void Die_MarksDeadAndZeroesHealth()
  {
    var health = new EnemyHealth(30f);
    health.Die();
    health.Health.ShouldBe(0f);
    health.IsDead.ShouldBeTrue();
  }
}
