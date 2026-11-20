// Original — camera look gate (menus must not spin the pivot)
namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

public class PlayerCameraTest : TestClass
{
  private Fixture _fixture = default!;

  public PlayerCameraTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    GameEvents.RaiseGameplayInputLockChanged(false);
    Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
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

  /// <summary>
  ///   Integration path: the boom ray must actually shorten the lens local
  ///   offset when a World body sits between the pivot and the rest position,
  ///   and must never go closer than <see cref="PlayerCamera.MinBoomLength"/>.
  /// </summary>
  [Test]
  public async Task PhysicsProcess_PullsBoomInBeforeWorldHit()
  {
    var root = new Node3D { Name = "BoomRig" };
    var pivot = new Node3D { Name = "Pivot", Position = new Vector3(0f, 1.8f, 0f) };
    var camera = new PlayerCamera
    {
      Name = "Camera3D",
      Position = new Vector3(0f, 0f, 6f),
      Current = false,
      CollisionMargin = 0.25f,
      MinBoomLength = 0.6f,
      BoomSmoothSpeed = 1000f
    };
    camera.SetPhysicsProcess(false); // driven manually for determinism
    pivot.AddChild(camera);
    root.AddChild(pivot);

    // Thick wall whose front face sits at z = 2.8 (box centered at z = 3).
    var wall = new StaticBody3D
    {
      Name = "Wall",
      CollisionLayer = 1u,
      CollisionMask = 0u,
      Position = new Vector3(0f, 1.8f, 3f)
    };
    wall.AddChild(
      new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10f, 10f, 0.4f) } }
    );
    root.AddChild(wall);

    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    await TestScene.ProcessFrame(2);

    const double frame = 0.05;
    for (var i = 0; i < 20; i++)
      camera._PhysicsProcess(frame);

    // 2.8 hit distance - 0.25 margin = 2.55 target.
    camera.Position.Z.ShouldBeLessThan(2.8f);
    camera.Position.Z.ShouldBeGreaterThan(2.2f);
    camera.Position.Length().ShouldBeGreaterThanOrEqualTo(camera.MinBoomLength - 0.001f);
  }

  /// <summary>
  ///   Without a hit the boom must rest at the authored offset (no drift from
  ///   repeated smoothing writes).
  /// </summary>
  [Test]
  public async Task PhysicsProcess_KeepsRestBoomWhenNothingBlocks()
  {
    var root = new Node3D { Name = "OpenSkyRig" };
    var pivot = new Node3D { Name = "Pivot", Position = new Vector3(0f, 400f, 0f) };
    var camera = new PlayerCamera
    {
      Name = "Camera3D",
      Position = new Vector3(0f, 0f, 6f),
      Current = false
    };
    camera.SetPhysicsProcess(false);
    pivot.AddChild(camera);
    root.AddChild(pivot);

    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    await TestScene.ProcessFrame(2);

    for (var i = 0; i < 20; i++)
      camera._PhysicsProcess(0.05);

    camera.Position.DistanceTo(new Vector3(0f, 0f, 6f)).ShouldBeLessThan(0.001f);
    camera.Fov.ShouldBe(camera.WalkFov);
  }
}
