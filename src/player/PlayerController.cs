// Ported/adapted from chickensoft-games/GameDemo (MIT License) —
// see godot-refs/chickensoft-GameDemo/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Third-person player controller. Reads the input actions that are defined
///   in project.godot (move_forward/move_back/move_left/move_right/jump),
///   delegates all movement math to <see cref="PlayerMotion"/>, and applies
///   the resulting velocity via MoveAndSlide each physics tick.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
  #region Input action names (added to project.godot by a later task)

  public const string MoveForwardAction = "move_forward";
  public const string MoveBackAction = "move_back";
  public const string MoveLeftAction = "move_left";
  public const string MoveRightAction = "move_right";
  public const string JumpAction = "jump";

  #endregion Input action names

  #region Exports (defaults adapted from GameDemo)

  /// <summary>Stopping velocity (meters/sec).</summary>
  [Export(PropertyHint.Range, "0, 100, 0.1")]
  public float StoppingSpeed { get; set; } = 1f;

  /// <summary>Player gravity (meters/sec²). Negative = down.</summary>
  [Export(PropertyHint.Range, "-100, 0, 0.1")]
  public float Gravity { get; set; } = -20f;

  /// <summary>Walking speed (meters/sec).</summary>
  [Export(PropertyHint.Range, "0, 100, 0.1")]
  public float WalkSpeed { get; set; } = 8f;

  /// <summary>Running speed multiplier applied to the walking speed.</summary>
  [Export(PropertyHint.Range, "0, 10, 0.1")]
  public float RunMultiplier { get; set; } = 1.5f;

  /// <summary>Horizontal acceleration (lerp weight per second).</summary>
  [Export(PropertyHint.Range, "0, 100, 0.1")]
  public float Acceleration { get; set; } = 4f;

  /// <summary>Initial vertical velocity (meters/sec) on jump.</summary>
  [Export(PropertyHint.Range, "0, 100, 0.1")]
  public float JumpImpulseForce { get; set; } = 8f;

  #endregion Exports

  /// <summary>
  ///   Whether the player is running. Defaults to walking; a future task can
  ///   bind this to an input action or a sprint system.
  /// </summary>
  public bool Running { get; set; }

  private PlayerMotion _motion = new();

  public override void _Ready()
  {
    // Copy the exported tunables into the pure-C# motion math object.
    _motion = new PlayerMotion
    {
      WalkSpeed = WalkSpeed,
      RunMultiplier = RunMultiplier,
      Acceleration = Acceleration,
      StoppingSpeed = StoppingSpeed,
      Gravity = Gravity,
      JumpImpulseForce = JumpImpulseForce
    };

    SetPhysicsProcess(true);
  }

  public override void _PhysicsProcess(double delta)
  {
    var camera = GetViewport().GetCamera3D();
    var cameraBasis = camera is null ? Basis.Identity : camera.GlobalBasis;

    var input = GetInputVector();

    var velocity = _motion.ComputeVelocity(
      Velocity, input, cameraBasis, (float)delta, Running
    );

    // Only jump from the ground (tracked by the motion object).
    if (Input.IsActionJustPressed(JumpAction) && _motion.IsGrounded)
    {
      velocity = _motion.Jump(velocity);
    }

    Velocity = velocity;
    MoveAndSlide();

    _motion.UpdateGrounded(IsOnFloor());
  }

  /// <summary>
  ///   Reads the movement input actions into a 2D vector where X is
  ///   right/left and Y is back/forward (forward = -Y), matching the
  ///   convention used by the GameDemo port.
  /// </summary>
  private static Vector2 GetInputVector() =>
    Input.GetVector(
      MoveLeftAction, MoveRightAction, MoveForwardAction, MoveBackAction
    );
}
