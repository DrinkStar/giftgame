// Original — swim overlay math (PlayerMotion stays untouched)
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class PlayerSwimTest : TestClass
{
  public PlayerSwimTest(Node testScene) : base(testScene) { }

  private const double Tolerance = 0.0001;

  [Test]
  public void IsInWater_WhenCenterBelowSurfacePlusOffset()
  {
    PlayerSwim.IsInWater(bodyY: 0f, surfaceY: 0f).ShouldBeTrue();
    PlayerSwim.IsInWater(bodyY: 2f, surfaceY: 0f).ShouldBeFalse();
  }

  [Test]
  public void Apply_SpaceGoesUp_CtrlGoesDown()
  {
    var falling = new Vector3(4f, -20f, 0f);

    var up = PlayerSwim.Apply(falling, swimUp: true, swimDown: false, bodyY: -1f, surfaceY: 0f);
    up.Y.ShouldBe(PlayerSwim.VerticalSpeed, Tolerance);
    up.X.ShouldBe(4f * PlayerSwim.HorizontalScale, Tolerance);

    var down = PlayerSwim.Apply(falling, swimUp: false, swimDown: true, bodyY: -1f, surfaceY: 0f);
    down.Y.ShouldBe(-PlayerSwim.VerticalSpeed, Tolerance);
  }

  [Test]
  public void Apply_DoesNotDivePastMaxDepth()
  {
    var bodyY = 0f - PlayerSwim.MaxDiveDepth;
    var velocity = PlayerSwim.Apply(
      Vector3.Zero, swimUp: false, swimDown: true, bodyY, surfaceY: 0f
    );
    velocity.Y.ShouldBe(0f, Tolerance);
  }

  [Test]
  public void Apply_ReplacesGravityWithBuoyancyTowardFloatDepth()
  {
    var falling = new Vector3(0f, -20f, 0f);
    var velocity = PlayerSwim.Apply(
      falling, swimUp: false, swimDown: false, bodyY: -2f, surfaceY: 0f
    );
    velocity.Y.ShouldBeGreaterThan(0f);
    velocity.Y.ShouldBeLessThanOrEqualTo(PlayerSwim.VerticalSpeed);
  }
}
