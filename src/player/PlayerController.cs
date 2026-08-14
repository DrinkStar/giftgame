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

  /// <summary>
  ///   R1 (Iter8p): optional progression service. Null (the shipped default)
  ///   keeps the move_speed multiplier at ×1; a real talent tree lands later.
  /// </summary>
  [Export]
  public ProgressionService? Progression { get; set; }

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

    // R1 (Iter8p): move_speed multiplier, applied here at the copy point so
    // the pure PlayerMotion class stays untouched (null service → ×1).
    // Copied every tick so runtime WalkSpeed changes and progression
    // multipliers both take effect.
    _motion.WalkSpeed = WalkSpeed * (Progression?.GetMultiplier("move_speed") ?? 1f);

    var velocity = _motion.ComputeVelocity(
      Velocity, input, cameraBasis, (float)delta, Running
    );

    // Only jump from the ground (tracked by the motion object).
    if (Input.IsActionJustPressed(JumpAction) && _motion.IsGrounded)
    {
      Stats?.DrainStamina(Stats.JumpStaminaCost);
      velocity = _motion.Jump(velocity);
    }

    // T8.5.2 (raft carry): Godot's built-in platform-velocity inheritance
    // proved unreliable for RigidBody3D floors in this project's tests, so the
    // carry is done explicitly: a short downward probe from the feet finds a
    // Raft and its horizontal velocity is ADDED to the movement velocity every
    // tick (replaced, not accumulated — the player tracks the raft and can
    // still walk relative to it; walking off clears the carry; vertical
    // velocity — gravity/jump — is untouched). Fail-closed: no raft below (or
    // no physics space) → no carry.
    var carry = ResolveRaftCarry();
    velocity = new Vector3(velocity.X + carry.X, velocity.Y, velocity.Z + carry.Z);

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

  /// <summary>
  ///   T8.5.2 (raft carry): the horizontal velocity of the Raft under the
  ///   player's feet (zero when none). A short downward probe from the feet
  ///   (0.5 m, player body excluded) finds the platform; the raft's
  ///   LinearVelocity horizontal component is returned so _PhysicsProcess can
  ///   add it to the movement velocity. Public test seam (like the WeaponSystem
  ///   Resolve* methods) — the detection logic is unit-tested directly.
  ///   Fail-closed: no physics space or no raft → Vector3.Zero.
  /// </summary>
  public Vector3 ResolveRaftCarry()
  {
    var spaceState = GetWorld3D().DirectSpaceState;
    if (spaceState == null)
    {
      return Vector3.Zero;
    }

    var feet = GlobalPosition - new Vector3(0f, 1f, 0f);
    var probe = PhysicsRayQueryParameters3D.Create(
      feet + new Vector3(0f, 0.05f, 0f),
      feet - new Vector3(0f, 0.45f, 0f)
    );
    probe.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
    var hit = spaceState.IntersectRay(probe);
    if (hit.Count == 0)
    {
      return Vector3.Zero;
    }

    var collider = hit["collider"].As<Node>();
    for (var node = collider; node != null; node = node.GetParent())
    {
      if (node is Raft raft)
      {
        return new Vector3(raft.LinearVelocity.X, 0f, raft.LinearVelocity.Z);
      }
    }

    return Vector3.Zero;
  }
}
