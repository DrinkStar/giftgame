// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Asset tests for the 9 EnemyData .tres files (Iter6 plan Decision 6):
///   every row of the full table loads with its fields intact, shark_king is
///   the boss at scale 2, and boar's drop item actually exists.
/// </summary>
public class EnemyDataTest : TestClass
{
  /// <summary>Decision 6 table: id, health, damage, speed, range, cd, behavior, boss, scale, drop.</summary>
  private static readonly (string Id, float Health, float Damage, float Speed,
    float Range, float Cooldown, EnemyBehavior Behavior, bool Boss, float Scale,
    string Drop, int DropAmount)[] ExpectedEnemies =
  {
    ("crab", 30f, 5f, 2f, 1.5f, 1f, EnemyBehavior.MeleeChase, false, 1f, "", 0),
    ("boar", 60f, 12f, 4f, 1.5f, 1f, EnemyBehavior.Charge, false, 1f, "raw_meat", 1),
    ("wolf", 40f, 10f, 4.5f, 1.5f, 1f, EnemyBehavior.MeleeChase, false, 1f, "", 0),
    ("shark", 80f, 15f, 5f, 2f, 1f, EnemyBehavior.Swimmer, false, 1f, "", 0),
    ("spider", 35f, 8f, 3f, 1.5f, 1f, EnemyBehavior.Webbing, false, 1f, "", 0),
    ("bat", 20f, 6f, 4f, 1.5f, 1f, EnemyBehavior.Flyer, false, 1f, "", 0),
    ("storm_beast", 120f, 20f, 4f, 1.5f, 1f, EnemyBehavior.SeaBeast, false, 1f, "", 0),
    ("mutant", 100f, 18f, 4f, 1.5f, 1f, EnemyBehavior.Mutant, false, 1f, "", 0),
    ("shark_king", 400f, 30f, 5f, 3f, 1.5f, EnemyBehavior.Swimmer, true, 2f, "", 0)
  };

  public EnemyDataTest(Node testScene) : base(testScene) { }

  [Test]
  public void AllNineEnemyResourcesLoadWithFullFields()
  {
    foreach (var (id, health, damage, speed, range, cooldown, behavior, boss,
      scale, drop, dropAmount) in ExpectedEnemies)
    {
      var data = GD.Load<EnemyData>($"res://assets/enemies/{id}.tres");
      data.ShouldNotBeNull($"{id}.tres must load");

      data!.Id.ShouldBe(id);
      data.DisplayName.ShouldNotBeNullOrEmpty();
      data.MaxHealth.ShouldBe(health);
      data.Damage.ShouldBe(damage);
      data.MoveSpeed.ShouldBe(speed);
      data.AttackRange.ShouldBe(range);
      data.AttackCooldown.ShouldBe(cooldown);
      data.Behavior.ShouldBe(behavior);
      data.Boss.ShouldBe(boss);
      data.Scale.ShouldBe(scale);
      data.DropItemId.ShouldBe(drop);
      data.DropAmount.ShouldBe(dropAmount);
    }
  }

  [Test]
  public void SharkKingIsTheOnlyBossAtScaleTwo()
  {
    var king = GD.Load<EnemyData>("res://assets/enemies/shark_king.tres");
    king.ShouldNotBeNull();
    king!.Boss.ShouldBeTrue();
    king.Scale.ShouldBe(2f);
    king.MaxHealth.ShouldBe(400f);
    king.AttackRange.ShouldBe(3f);

    foreach (var (id, _, _, _, _, _, _, boss, _, _, _) in ExpectedEnemies)
    {
      if (id != "shark_king")
        GD.Load<EnemyData>($"res://assets/enemies/{id}.tres")!.Boss.ShouldBeFalse();
    }
  }

  [Test]
  public void BoarDropItemResourceExists()
  {
    var boar = GD.Load<EnemyData>("res://assets/enemies/boar.tres");
    boar.ShouldNotBeNull();
    boar!.DropItemId.ShouldBe("raw_meat");
    boar.DropAmount.ShouldBe(1);

    var drop = GD.Load<ItemData>("res://assets/items/raw_meat.tres");
    drop.ShouldNotBeNull();
    drop!.Id.ShouldBe("raw_meat");
  }
}
