// Ported/adapted from chickensoft-games/GameDemo (MIT License) —
// see godot-refs/chickensoft-GameDemo/LICENSE
namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Unit tests for the pure-C# <see cref="PlayerMotion"/> movement math.
///   Written tests-after style: behavior first implemented in
///   <see cref="PlayerMotion"/>, then locked here.
/// </summary>
public class PlayerMotionTest : TestClass
{
  public PlayerMotionTest(Node testScene) : base(testScene) { }

  private const double Tolerance = 0.0001;

  [Test]
  public void ForwardInputMovesAlongCameraForward()
  {
    var motion = new PlayerMotion();

    // Vector2.Up is (0, -1): forward input in the GameDemo convention.
    var direction = motion.GetMoveDirection(Vector2.Up, Basis.Identity);

    direction.X.ShouldBe(0f, Tolerance);
    direction.Z.ShouldBe(-1f, Tolerance);
    direction.Y.ShouldBe(0f, Tolerance);
  }

  [Test]
  public void InputDirectionFollowsCameraYaw()
  {
    var motion = new PlayerMotion();

    // Camera yawed 90° to the right (negative rotation about +Y):
    // its forward is world +X.
    var cameraBasis = Basis.Identity.Rotated(
      Vector3.Up, Mathf.DegToRad(-90f)
    );
    var direction = motion.GetMoveDirection(Vector2.Up, cameraBasis);

    direction.X.ShouldBe(1f, Tolerance);
    direction.Z.ShouldBe(0f, Tolerance);
    direction.Y.ShouldBe(0f, Tolerance);
  }

  [Test]
  public void WalkingSpeedIsClamped()
  {
    var motion = new PlayerMotion();

    // A large delta saturates the lerp weight, so the horizontal velocity
    // reaches the target speed exactly — and is clamped there.
    var velocity = motion.ComputeVelocity(
      Vector3.Zero, Vector2.Up, Basis.Identity, delta: 1f, running: false
    );

    var horizontalSpeed = (velocity with { Y = 0f }).Length();
    horizontalSpeed.ShouldBe(motion.WalkSpeed, Tolerance);
    horizontalSpeed.ShouldBeLessThanOrEqualTo(motion.WalkSpeed + 0.0001f);
  }

  [Test]
  public void RunningSpeedIsClampedAtRunMultiplier()
  {
    var motion = new PlayerMotion();

    var velocity = motion.ComputeVelocity(
      Vector3.Zero, Vector2.Up, Basis.Identity, delta: 1f, running: true
    );

    var horizontalSpeed = (velocity with { Y = 0f }).Length();
    horizontalSpeed.ShouldBe(motion.RunSpeed, Tolerance);
    motion.RunSpeed.ShouldBe(motion.WalkSpeed * motion.RunMultiplier);
  }

  [Test]
  public void DiagonalInputIsNotStrongerThanAxisAligned()
  {
    var motion = new PlayerMotion();

    // Diagonal input is corrected so its direction is never longer than a
    // full-strength axis-aligned direction.
    var diagonal = new Vector2(1f, -1f).Normalized();
    var diagonalDirection = motion.GetMoveDirection(diagonal, Basis.Identity);
    var axisDirection = motion.GetMoveDirection(Vector2.Up, Basis.Identity);

    diagonalDirection.Length().ShouldBeLessThanOrEqualTo(
      axisDirection.Length() + 0.0001f
    );
  }

  [Test]
  public void GravityAccumulatesEachTick()
  {
    var motion = new PlayerMotion();

    var velocity = motion.ComputeVelocity(
      Vector3.Zero, Vector2.Zero, Basis.Identity, delta: 0.5f, running: false
    );
    velocity.Y.ShouldBe(motion.Gravity * 0.5f, Tolerance);

    velocity = motion.ComputeVelocity(
      velocity, Vector2.Zero, Basis.Identity, delta: 0.5f, running: false
    );
    velocity.Y.ShouldBe(motion.Gravity * 1f, Tolerance);
  }

  [Test]
  public void JumpAppliesImpulseAndPreservesHorizontalVelocity()
  {
    var motion = new PlayerMotion();

    var velocity = motion.Jump(new Vector3(3f, 0f, 2f));

    velocity.Y.ShouldBe(motion.JumpImpulseForce, Tolerance);
    velocity.X.ShouldBe(3f, Tolerance);
    velocity.Z.ShouldBe(2f, Tolerance);
  }

  [Test]
  public void GroundedTransitionsAreDetected()
  {
    var motion = new PlayerMotion();

    // A fresh motion object starts grounded.
    motion.IsGrounded.ShouldBeTrue();

    motion.UpdateGrounded(isOnFloor: false)
      .ShouldBe(PlayerMotion.GroundTransition.LeftGround);
    motion.IsGrounded.ShouldBeFalse();

    // No change while airborne.
    motion.UpdateGrounded(isOnFloor: false)
      .ShouldBe(PlayerMotion.GroundTransition.None);

    motion.UpdateGrounded(isOnFloor: true)
      .ShouldBe(PlayerMotion.GroundTransition.Landed);
    motion.IsGrounded.ShouldBeTrue();

    // No change while standing.
    motion.UpdateGrounded(isOnFloor: true)
      .ShouldBe(PlayerMotion.GroundTransition.None);
  }
}
