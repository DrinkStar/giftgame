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
///   hills in front of the player). Past the body, the first hit wins.
/// </summary>
public static class AimQuery
{
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
  ///   first hit that <paramref name="accept"/> allows. A world hit past the
  ///   body stops the ray (no through-mountain picks).
  /// </summary>
  public static Godot.Collections.Dictionary IntersectPiercing(
    PhysicsDirectSpaceState3D space,
    Vector3 from,
    Vector3 to,
    uint mask,
    Godot.Collections.Array<Rid> exclude,
    Func<GodotObject, bool> accept,
    float pierceUntilDistance)
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

    var query = PhysicsRayQueryParameters3D.Create(origin, to);
    query.CollisionMask = mask;
    query.CollideWithAreas = true;
    query.CollideWithBodies = true;
    if (exclude != null && exclude.Count > 0)
      query.Exclude = exclude;

    var hit = space.IntersectRay(query);
    if (hit.Count == 0)
      return new Godot.Collections.Dictionary();

    var collider = hit["collider"].AsGodotObject();
    if (collider != null && accept(collider))
      return hit;

    return new Godot.Collections.Dictionary();
  }
}
