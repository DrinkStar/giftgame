// Original — camera look gate (menus must not spin the pivot)
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

public class PlayerCameraTest : TestClass
{
  public PlayerCameraTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  [Cleanup]
  public void Cleanup()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Visible;
  }

  [Test]
  public void ShouldAcceptLook_WhenUnlockedAndCursorNotVisible()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    // Prefer Captured; headless may leave Confined — either is non-Visible.
    Input.MouseMode = Input.MouseModeEnum.Captured;
    if (Input.MouseMode == Input.MouseModeEnum.Visible)
      Input.MouseMode = Input.MouseModeEnum.Confined;

    if (Input.MouseMode == Input.MouseModeEnum.Visible)
    {
      // Headless ignored both modes — gate still blocks Visible.
      PlayerCamera.ShouldAcceptLook().ShouldBeFalse();
      return;
    }

    PlayerCamera.ShouldAcceptLook().ShouldBeTrue();
  }

  [Test]
  public void ShouldAcceptLook_FalseWhenGameplayLocked()
  {
    Input.MouseMode = Input.MouseModeEnum.Captured;
    if (Input.MouseMode == Input.MouseModeEnum.Visible)
      Input.MouseMode = Input.MouseModeEnum.Confined;

    GameEvents.RaiseGameplayInputLockChanged(true);
    PlayerCamera.ShouldAcceptLook().ShouldBeFalse();
  }

  [Test]
  public void ShouldAcceptLook_FalseWhenCursorVisible()
  {
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Visible;
    PlayerCamera.ShouldAcceptLook().ShouldBeFalse();
  }

  [Test]
  public void DefaultsFavorReadableThirdPerson()
  {
    var cam = new PlayerCamera();
    cam.MouseSensitivity.ShouldBe(0.12f);
    cam.VerticalMin.ShouldBe(-55f);
    cam.VerticalMax.ShouldBe(55f);
    cam.CollisionMargin.ShouldBe(0.25f);
    cam.MinBoomLength.ShouldBe(0.6f);
    cam.BoomSmoothSpeed.ShouldBe(18f);
    cam.WalkFov.ShouldBe(75f);
    cam.SprintFov.ShouldBe(82f);
    cam.Free();
  }

  [Test]
  public void ResolveBoomLength_PullsInBeforeHitWithMargin()
  {
    PlayerCamera.ResolveBoomLength(
      restLength: 5.6f,
      hitDistance: 3f,
      margin: 0.25f,
      minBoom: 0.6f
    ).ShouldBe(2.75f);
  }

  [Test]
  public void ResolveBoomLength_ClampsToRestWhenHitBeyondBoom()
  {
    PlayerCamera.ResolveBoomLength(5.6f, 100f, 0.25f, 0.6f).ShouldBe(5.6f);
  }

  [Test]
  public void ResolveBoomLength_AppliesMarginWhenHitNearRest()
  {
    PlayerCamera.ResolveBoomLength(5.6f, 5.6f, 0.25f, 0.6f).ShouldBe(5.35f);
  }

  [Test]
  public void ResolveBoomLength_RespectsMinBoom()
  {
    PlayerCamera.ResolveBoomLength(5.6f, 0.4f, 0.25f, 0.6f).ShouldBe(0.6f);
  }

  [Test]
  public void SmoothBoomLength_PullsInFasterThanRelease()
  {
    var pull = PlayerCamera.SmoothBoomLength(5f, 2f, 0.1f, 10f);
    var release = PlayerCamera.SmoothBoomLength(2f, 5f, 0.1f, 10f);
    (5f - pull).ShouldBe(1f); // full 10 * 0.1
    (release - 2f).ShouldBe(0.5f); // half rate
  }
}
