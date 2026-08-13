// Ported/adapted from chickensoft-games/GameDemo (MIT License) —
// see godot-refs/chickensoft-GameDemo/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure C# movement math for the player controller. Contains no Godot node
///   dependencies — only GodotSharp math structs — so it can be unit-tested
///   without a scene tree. All tunables are plain fields; the controller
///   copies its [Export] values into an instance of this class.
/// </summary>
public class PlayerMotion
{
  /// <summary>Walking speed (meters/sec).</summary>
  public float WalkSpeed = 8f;

  /// <summary>Running speed multiplier applied to <see cref="WalkSpeed"/>.</summary>
  public float RunMultiplier = 1.5f;

  /// <summary>Horizontal acceleration, used as a lerp weight per second.</summary>
  public float Acceleration = 4f;

  /// <summary>
  ///   Speed (meters/sec) below which horizontal velocity snaps to zero when
  ///   no input is given.
  /// </summary>
  public float StoppingSpeed = 1f;

  /// <summary>Gravity (meters/sec²). Negative = down.</summary>
  public float Gravity = -20f;

  /// <summary>Initial vertical velocity (meters/sec) applied on jump.</summary>
  public float JumpImpulseForce = 8f;

  /// <summary>Running speed (meters/sec).</summary>
  public float RunSpeed => WalkSpeed * RunMultiplier;

  /// <summary>
  ///   True while the player is on the floor. Updated via
  ///   <see cref="UpdateGrounded"/>.
  /// </summary>
  public bool IsGrounded { get; private set; } = true;

  /// <summary>Result of a grounded-state evaluation.</summary>
  public enum GroundTransition
  {
    /// <summary>The grounded state did not change.</summary>
    None,
    /// <summary>The player just landed on the floor.</summary>
    Landed,
    /// <summary>The player just left the floor.</summary>
    LeftGround
  }

  /// <summary>
  ///   Converts a 2D input vector into a camera-relative 3D move direction,
  ///   flattened to the horizontal plane. Diagonal input is corrected so it
  ///   is not stronger than axis-aligned input.
  /// </summary>
  /// <param name="input">
  ///   2D input vector where X is right/left and Y is back/forward
  ///   (forward = -Y), as returned by Godot's Input.GetVector.
  /// </param>
  /// <param name="cameraBasis">The camera's global transform basis.</param>
  public Vector3 GetMoveDirection(Vector2 input, Basis cameraBasis)
  {
    var corrected = new Vector3
    {
      X = input.X * Mathf.Sqrt(1.0f - (input.Y * input.Y / 2.0f)),
      Z = input.Y * Mathf.Sqrt(1.0f - (input.X * input.X / 2.0f))
    };
    return (cameraBasis * corrected) with { Y = 0f };
  }

  /// <summary>
  ///   Computes the player's velocity for one physics tick: ramps the
  ///   horizontal velocity toward the requested move direction at the walk
  ///   or run speed (clamped), preserves vertical velocity, and accumulates
  ///   gravity.
  /// </summary>
  /// <param name="currentVelocity">The velocity at the start of the tick.</param>
  /// <param name="input">The 2D input vector (see <see cref="GetMoveDirection"/>).</param>
  /// <param name="cameraBasis">The camera's global transform basis.</param>
  /// <param name="delta">Physics tick duration (seconds).</param>
  /// <param name="running">Whether the player is running (vs walking).</param>
  public Vector3 ComputeVelocity(
    Vector3 currentVelocity,
    Vector2 input,
    Basis cameraBasis,
    float delta,
    bool running
  )
  {
    var moveDirection = GetMoveDirection(input, cameraBasis);
    var maxSpeed = running ? RunSpeed : WalkSpeed;

    var velocity = currentVelocity with { Y = 0f };
    var lerpWeight = Mathf.Clamp(Acceleration * delta, 0f, 1f);
    velocity = velocity.Lerp(moveDirection * maxSpeed, lerpWeight);

    // Snap to a stop when input is released and we're moving slowly.
    if (input.LengthSquared() <= 0f && velocity.Length() < StoppingSpeed)
    {
      velocity = Vector3.Zero;
    }

    // Hard clamp to the current max speed (guards against frame-rate spikes).
    if (velocity.Length() > maxSpeed)
    {
      velocity = velocity.Normalized() * maxSpeed;
    }

    // Don't clear the vertical velocity (we may be falling/jumping) —
    // accumulate gravity on top of it instead.
    velocity.Y = currentVelocity.Y + (Gravity * delta);

    return velocity;
  }

  /// <summary>Applies the jump impulse to a velocity.</summary>
  /// <param name="currentVelocity">The velocity to jump from.</param>
  public Vector3 Jump(Vector3 currentVelocity) =>
    currentVelocity with { Y = JumpImpulseForce };

  /// <summary>
  ///   Updates the grounded state from the latest floor check and reports
  ///   whether the player just landed or just left the ground.
  /// </summary>
  /// <param name="isOnFloor">The latest floor check result.</param>
  public GroundTransition UpdateGrounded(bool isOnFloor)
  {
    var transition = GroundTransition.None;
    if (isOnFloor && !IsGrounded)
    {
      transition = GroundTransition.Landed;
    }
    else if (!isOnFloor && IsGrounded)
    {
      transition = GroundTransition.LeftGround;
    }

    IsGrounded = isOnFloor;
    return transition;
  }
}
