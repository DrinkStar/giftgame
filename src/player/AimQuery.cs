// Original — third-person aim ray (skip follow-camera gap, then first hit)
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   Shared third-person aim math. The follow camera sits several meters
///   behind the body, so a 3 m ray from the lens never reaches a tree or
///   crab in front of the player.
///   The camera-to-body gap is skipped in one jump (no RID exclude — the
///   island is a single StaticBody3D; excluding it would also punch through
///   hills in front of the player). Past the body, soft Area misses are
///   skipped so a decorative volume does not block a tree/crab behind it;
///   a World/body miss still stops the ray (no through-mountain picks).
/// </summary>
public static class AimQuery
{
  /// <summary>Max soft Area skips before giving up (prevents infinite loops).</summary>
  public const int MaxPierceSteps = 8;

  /// <summary>
  ///   Ray length from the camera: the follow gap plus
  ///   <paramref name="range"/> meters past the body.
  /// </summary>
  public static float MaxDistance(Vector3 cameraOrigin, Vector3 bodyOrigin, float range) =>
    cameraOrigin.DistanceTo(bodyOrigin) + range;

  /// <summary>
  ///   Point on the look ray that sits one follow-gap away from the camera
  ///   (approximately the body). Used as the gameplay ray origin so terrain
  ///   behind the player is never queried.
  /// </summary>
  public static Vector3 OriginAtBody(Vector3 cameraOrigin, Vector3 bodyOrigin, Vector3 lookDirection)
  {
    var len = lookDirection.Length();
    if (len < 0.0001f)
      return bodyOrigin;
    return cameraOrigin + (lookDirection / len) * cameraOrigin.DistanceTo(bodyOrigin);
  }

  /// <summary>
  ///   Skips <paramref name="pierceUntilDistance"/> meters from
  ///   <paramref name="from"/> without excluding colliders, then returns the
  ///   first hit that <paramref name="accept"/> allows. Non-accepted
  ///   <see cref="Area3D"/> hits are skipped (soft); any other collider stops
  ///   the ray (hard — terrain / bodies).
  /// </summary>
  public static Godot.Collections.Dictionary IntersectPiercing(
    PhysicsDirectSpaceState3D space,
    Vector3 from,
    Vector3 to,
    uint mask,
    Godot.Collections.Array<Rid> exclude,
    Func<GodotObject, bool> accept,
    float pierceUntilDistance
  )
  {
    if (space == null)
      return new Godot.Collections.Dictionary();

    var dir = to - from;
    var dirLen = dir.Length();
    if (dirLen < 0.0001f)
      return new Godot.Collections.Dictionary();

    var unit = dir / dirLen;
    var skip = Mathf.Clamp(pierceUntilDistance, 0f, dirLen);
    var origin = from + unit * skip;
    if ((to - origin).LengthSquared() < 0.0001f)
      return new Godot.Collections.Dictionary();

    var excludeList = new Godot.Collections.Array<Rid>();
    if (exclude != null)
    {
      foreach (var rid in exclude)
        excludeList.Add(rid);
    }

    for (var step = 0; step < MaxPierceSteps; step++)
    {
      if ((to - origin).LengthSquared() < 0.0001f)
        return new Godot.Collections.Dictionary();

      var query = PhysicsRayQueryParameters3D.Create(origin, to);
      query.CollisionMask = mask;
      query.CollideWithAreas = true;
      query.CollideWithBodies = true;
      if (excludeList.Count > 0)
        query.Exclude = excludeList;

      var hit = space.IntersectRay(query);
      if (hit.Count == 0)
        return new Godot.Collections.Dictionary();

      var collider = hit["collider"].AsGodotObject();
      if (collider != null && accept(collider))
        return hit;

      // Soft miss: skip Areas that failed accept so a dormant interactable
      // volume does not block the target behind it.
      if (collider is Area3D area)
      {
        excludeList.Add(area.GetRid());
        var hitPos = hit["position"].AsVector3();
        origin = hitPos + unit * 0.05f;
        continue;
      }

      // Hard miss: terrain / character body — stop (no mountain punch-through).
      return new Godot.Collections.Dictionary();
    }

    return new Godot.Collections.Dictionary();
  }
}
