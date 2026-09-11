// Original — swim overlay; does not live in PlayerMotion (contract)
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Pure swim / dive math applied by <see cref="PlayerController"/> after
///   <see cref="PlayerMotion.ComputeVelocity"/>. Space = up, Ctrl = dive.
/// </summary>
public static class PlayerSwim
{
  public const string DiveAction = "swim_dive";

  /// <summary>
  ///   Ctrl is a modifier: Windows often never matches an InputMap event
  ///   that only sets <c>physical_keycode</c>. Poll the key as well.
  /// </summary>
  public static bool IsDiveHeld() =>
    Input.IsKeyPressed(Key.Ctrl)
    || Input.IsPhysicalKeyPressed(Key.Ctrl)
    || Input.IsActionPressed(DiveAction);

  /// <summary>In water when the capsule center is this far below the surface.</summary>
  public const float EnterOffset = 0.85f;

  public const float HorizontalScale = 0.55f;
  public const float VerticalSpeed = 4.5f;
  public const float MaxDiveDepth = 12f;
  public const float FloatDepth = 0.45f;

  public static bool IsInWater(float bodyY, float surfaceY) =>
    bodyY < surfaceY + EnterOffset;

  /// <summary>
  ///   Replaces gravity with swim vertical. Horizontal speed is scaled;
  ///   XZ direction still comes from <see cref="PlayerMotion"/>.
  /// </summary>
  public static Vector3 Apply(
    Vector3 motionVelocity,
    bool swimUp,
    bool swimDown,
    float bodyY,
    float surfaceY)
  {
    var horizontal = (motionVelocity with { Y = 0f }) * HorizontalScale;
    float vy;
    if (swimUp)
      vy = VerticalSpeed;
    else if (swimDown)
      vy = -VerticalSpeed;
    else
    {
      var targetY = surfaceY - FloatDepth;
      vy = Mathf.Clamp((targetY - bodyY) * 2.5f, -VerticalSpeed, VerticalSpeed);
    }

    var minY = surfaceY - MaxDiveDepth;
    if (bodyY <= minY && vy < 0f)
      vy = 0f;

    return new Vector3(horizontal.X, vy, horizontal.Z);
  }
}
