// Original — third-person aim reach
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class AimQueryTest : TestClass
{
  public AimQueryTest(Node testScene) : base(testScene) { }

  private const double Tolerance = 0.0001;

  [Test]
  public void MaxDistance_AddsFollowGapToGameplayRange()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var range = 3f;

    var reach = AimQuery.MaxDistance(camera, body, range);

    reach.ShouldBe(camera.DistanceTo(body) + range, Tolerance);
    reach.ShouldBeGreaterThan(range);
  }

  [Test]
  public void MaxDistance_WhenCameraOnBody_EqualsRange()
  {
    var origin = new Vector3(1f, 2f, 3f);
    AimQuery.MaxDistance(origin, origin, 3f).ShouldBe(3f, Tolerance);
  }

  [Test]
  public void OriginAtBody_WhenLookAtBody_LandsOnBody()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var origin = AimQuery.OriginAtBody(camera, body, body - camera);
    origin.DistanceTo(body).ShouldBe(0f, Tolerance);
  }

  [Test]
  public void OriginAtBody_AlongLook_EqualsFollowGap()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var look = new Vector3(0f, 0f, -1f);
    var origin = AimQuery.OriginAtBody(camera, body, look);
    origin.DistanceTo(camera).ShouldBe(camera.DistanceTo(body), Tolerance);
  }
}
