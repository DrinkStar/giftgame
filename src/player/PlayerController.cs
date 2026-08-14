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
  public const string SprintAction = "sprint";

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

  /// <summary>
  ///   Survival stats (stamina source/sink). Optional: when null, sprinting
  ///   is not stamina-gated and no stamina is drained, so scenes without the
  ///   survival stack keep working unchanged.
  /// </summary>
  [Export]
  public PlayerStats? Stats { get; set; }

  #endregion Exports

  /// <summary>
  ///   Whether the player is running. Bound to the sprint action and gated on
  ///   stamina (see <see cref="_PhysicsProcess"/>); the run speed itself is
  ///   applied by <see cref="PlayerMotion.ComputeVelocity"/>.
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
    // FIX(iter7-plan): T7.0 respawn contract — a dead player takes no input:
    // zero velocity, no sprint drain, no jump, no MoveAndSlide. Respawn
    // (GameManager.RespawnPlayer → PlayerStats.Revive) re-enables movement.
    if (Stats is { IsAlive: false })
    {
      Velocity = Vector3.Zero;
      return;
    }

    // Sprint: the action is held AND stamina allows it (plan Decision 2).
    // With no stats node wired (Stats == null) sprinting stays ungated.
    Running = Input.IsActionPressed(SprintAction)
      && (Stats is null || Stats.CanSprint());

    var camera = GetViewport().GetCamera3D();
    var cameraBasis = camera is null ? Basis.Identity : camera.GlobalBasis;

    var input = GetInputVector();

    // Sprint drains stamina only while actually running with horizontal
    // input (rate read from the stats export, not hardcoded — Decision 2).
    if (Running && input.LengthSquared() > 0f)
      Stats?.DrainStamina(Stats.SprintStaminaDrain * (float)delta);

    var velocity = _motion.ComputeVelocity(
      Velocity, input, cameraBasis, (float)delta, Running
    );

    // Only jump from the ground (tracked by the motion object).
    if (Input.IsActionJustPressed(JumpAction) && _motion.IsGrounded)
    {
      Stats?.DrainStamina(Stats.JumpStaminaCost);
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
