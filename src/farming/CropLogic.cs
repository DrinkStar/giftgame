// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using System;

/// <summary>
///   Pure static crop math (Iter5 plan Decision 1). No Godot types, no state:
///   growth progress, readiness and harvest yield live here so they are
///   unit-testable without a scene tree. FarmPlot drives it with real frame
///   time; the seed returned on harvest is a fixed constant so crops are
///   self-sustaining.
/// </summary>
public static class CropLogic
{
  /// <summary>Seeds returned to the player on every successful harvest.</summary>
  public const int SeedReturn = 1;

  /// <summary>
  ///   Growth progress in [0, 1]: elapsed time divided by total growth time,
  ///   clamped. A non-positive growth time counts as instantly grown.
  /// </summary>
  public static float Progress(float elapsedSeconds, float growthSeconds)
  {
    if (growthSeconds <= 0f)
      return 1f;

    return Math.Clamp(elapsedSeconds / growthSeconds, 0f, 1f);
  }

  /// <summary>True when the crop has reached (or passed) full growth.</summary>
  public static bool IsReady(float elapsedSeconds, float growthSeconds) =>
    Progress(elapsedSeconds, growthSeconds) >= 1f;

  /// <summary>
  ///   Rolls a harvest yield in the inclusive range [min, max] with the given
  ///   <see cref="Random"/> instance. An inverted range is normalized so the
  ///   caller can never pass a broken pair.
  /// </summary>
  public static int HarvestYield(int min, int max, Random rng)
  {
    if (max < min)
      (min, max) = (max, min);

    return rng.Next(min, max + 1);
  }
}
