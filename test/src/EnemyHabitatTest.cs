// Original — habitat leash / wander helpers (contract 3: no new AI states)
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Pure tests for <see cref="EnemyHabitat"/>: chase leash, wander slots,
///   river/beach attractors. Does not drive EnemyBase's behavior switch.
/// </summary>
public class EnemyHabitatTest : TestClass
{
  public EnemyHabitatTest(Node testScene) : base(testScene) { }

  [Test]
  public void HabitatEnumValuesStayStable()
  {
    ((int)EnemyHabitatKind.None).ShouldBe(0);
    ((int)EnemyHabitatKind.ForestRiver).ShouldBe(1);
    ((int)EnemyHabitatKind.BeachShore).ShouldBe(2);
  }

  [Test]
  public void ShouldChase_ZeroWanderRadius_AlwaysTrue()
  {
    EnemyHabitat.ShouldChase(
      Vector3.Zero, 0f, new Vector3(10f, 0f, 0f), new Vector3(80f, 0f, 0f), 1.5f)
      .ShouldBeTrue();
  }

  [Test]
  public void ShouldChase_PlayerInsideDenDisk_True()
  {
    var home = new Vector3(50f, 0f, 0f);
    EnemyHabitat.ShouldChase(
      home, 18f, home, home + new Vector3(5f, 0f, 0f), 1.5f)
      .ShouldBeTrue();
  }

  [Test]
  public void ShouldChase_PlayerFarFromDen_FalseUnlessMelee()
  {
    var home = new Vector3(50f, 0f, 0f);
    var enemy = home;
    var spawn = Vector3.Zero;
    EnemyHabitat.ShouldChase(home, 18f, enemy, spawn, 1.5f).ShouldBeFalse();

    var melee = enemy + new Vector3(2f, 0f, 0f);
    EnemyHabitat.ShouldChase(home, 18f, enemy, melee, 1.5f).ShouldBeTrue();
  }

  [Test]
  public void IsLeakingLeash_BeyondSlack_True()
  {
    var home = new Vector3(40f, 0f, 0f);
    EnemyHabitat.IsLeakingLeash(home, 10f, home + new Vector3(9f, 0f, 0f))
      .ShouldBeFalse();
    EnemyHabitat.IsLeakingLeash(home, 10f, home + new Vector3(12f, 0f, 0f))
      .ShouldBeTrue();
  }

  [Test]
  public void ClampToHabitat_PullsOutwardPointOntoCircle()
  {
    var home = new Vector3(10f, 1f, 0f);
    var far = new Vector3(40f, 2f, 0f);
    var clamped = EnemyHabitat.ClampToHabitat(home, 8f, far);
    EnemyHabitat.Horizontal(home, clamped).ShouldBe(8f, 0.001);
    clamped.Y.ShouldBe(2f);
  }

  [Test]
  public void PickWanderTarget_DenDrinkAndForageSlots()
  {
    var home = new Vector3(20f, 1f, 10f);
    var river = new Vector3(30f, 1f, 10f);
    EnemyHabitat.PickWanderTarget(home, river, 12f, 0).ShouldBe(home);

    var drink = EnemyHabitat.PickWanderTarget(home, river, 12f, 2);
    EnemyHabitat.Horizontal(home, drink).ShouldBeLessThanOrEqualTo(12f + 0.001f);

    var forage = EnemyHabitat.PickWanderTarget(home, river, 12f, 1);
    forage.ShouldNotBe(home);
    EnemyHabitat.Horizontal(home, forage).ShouldBe(12f * 0.55f, 0.05);
  }

  [Test]
  public void WorldAttractor_ForestUsesRiverBank_BeachUsesCoast()
  {
    var specs = WorldLayout.Generate(12345);
    var forest = EnemyHabitat.WorldAttractor(
      new Vector2(48f, 0f), EnemyHabitatKind.ForestRiver, specs);
    forest.Length().ShouldBeGreaterThan(IslandBuilder.SpawnSafeRadius * 0.5f);

    var same = EnemyHabitat.WorldAttractor(
      new Vector2(48f, 0f), EnemyHabitatKind.ForestRiver, specs);
    same.ShouldBe(forest);

    var beach = EnemyHabitat.WorldAttractor(
      new Vector2(75f, 11f), EnemyHabitatKind.BeachShore, specs);
    beach.Length().ShouldBeGreaterThan(WorldLayout.MainRadius * 0.7f);
  }

  [Test]
  public void RiverSampleIsDeterministicForSameSpec()
  {
    var spec = WorldLayout.Generate(12345)[0];
    var a = IslandHeightmap.SampleRiverLocal(spec, 0.45f);
    var b = IslandHeightmap.SampleRiverLocal(spec, 0.45f);
    a.ShouldBe(b);

    var other = IslandHeightmap.SampleRiverLocal(
      WorldLayout.Generate(54321)[0], 0.45f);
    (a == other).ShouldBeFalse();
  }
}
