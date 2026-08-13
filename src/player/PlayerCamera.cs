// Ported/adapted from chickensoft-games/GameDemo (MIT License) —
// see godot-refs/chickensoft-GameDemo/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Third-person orbit/follow camera. The camera is expected to be attached
///   to a pivot node that is a child of the player, so it follows the player
///   automatically through parenting. Mouse motion orbits the pivot:
///   horizontal motion yaws around the player, vertical motion pitches up and
///   down (clamped to a configurable range). Zero mouse input is a graceful
///   no-op.
/// </summary>
public partial class PlayerCamera : Camera3D
{
  /// <summary>Mouse sensitivity (degrees per pixel of relative motion).</summary>
  [Export(PropertyHint.Range, "0, 10, 0.01")]
  public float MouseSensitivity { get; set; } = 0.2f;

  /// <summary>Minimum pitch angle (degrees). Negative = looking up.</summary>
  [Export(PropertyHint.Range, "-89.9, -0.01, 0.01")]
  public float VerticalMin { get; set; } = -45f;

  /// <summary>Maximum pitch angle (degrees). Positive = looking up.</summary>
  [Export(PropertyHint.Range, "0.01, 89.9, 0.01")]
  public float VerticalMax { get; set; } = 45f;

  private Node3D _pivot = default!;
  private float _yawRad;
  private float _pitchRad;

  public override void _Ready()
  {
    // The pivot (parent) carries the orbit angles; the camera itself keeps
    // its static local offset so it always looks at the player's origin.
    if (GetParent() is not Node3D pivot)
    {
      GD.PushWarning("PlayerCamera expects a Node3D pivot as its parent.");
      SetProcessUnhandledInput(false);
      return;
    }

    _pivot = pivot;
    _yawRad = _pivot.Rotation.Y;
    _pitchRad = _pivot.Rotation.X;
    ApplyPivotRotation();

    // Third-person mouse look needs a captured cursor.
    Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  public override void _ExitTree() =>
    Input.MouseMode = Input.MouseModeEnum.Visible;

  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event is not InputEventMouseMotion motion)
    {
      return;
    }

    // Graceful with zero input: no relative motion means nothing to change.
    if (motion.Relative.LengthSquared() <= 0.0001f)
    {
      return;
    }

    var radiansPerPixel = MouseSensitivity * Mathf.DegToRad(1f);
    _yawRad += -motion.Relative.X * radiansPerPixel;
    _pitchRad = Mathf.Clamp(
      _pitchRad + (-motion.Relative.Y * radiansPerPixel),
      Mathf.DegToRad(VerticalMin),
      Mathf.DegToRad(VerticalMax)
    );

    ApplyPivotRotation();
  }

  private void ApplyPivotRotation() =>
    _pivot.Rotation = new Vector3(_pitchRad, _yawRad, 0f);
}
