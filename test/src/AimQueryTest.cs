// Original — third-person aim reach + piercing ray contract
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

public class AimQueryTest : TestClass
{
  private Fixture _fixture = default!;

  public AimQueryTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup() => _fixture = new Fixture(TestScene.GetTree());

  [Cleanup]
  public void Cleanup() => _fixture.Cleanup();

  private const double Tolerance = 0.0001;

  /// <summary>
  ///   Rig is parked far above the origin so a leftover ground / island body
  ///   from another suite cannot intercept the test rays.
  /// </summary>
  private static readonly Vector3 RigOrigin = new(0f, 200f, 0f);

  [Test]
  public void MaxDistance_AddsFollowGapToGameplayRange()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var range = 3f;

    var reach = AimQuery.MaxDistance(camera, body, range);

    reach.ShouldBe(camera.DistanceTo(body) + range, Tolerance);
    reach.ShouldBeGreaterThan(range);
  }

  [Test]
  public void MaxDistance_WhenCameraOnBody_EqualsRange()
  {
    var origin = new Vector3(1f, 2f, 3f);
    AimQuery.MaxDistance(origin, origin, 3f).ShouldBe(3f, Tolerance);
  }

  [Test]
  public void OriginAtBody_WhenLookAtBody_LandsOnBody()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var origin = AimQuery.OriginAtBody(camera, body, body - camera);
    origin.DistanceTo(body).ShouldBe(0f, Tolerance);
  }

  [Test]
  public void OriginAtBody_AlongLook_EqualsFollowGap()
  {
    var camera = new Vector3(0f, 2f, 5f);
    var body = Vector3.Zero;
    var look = new Vector3(0f, 0f, -1f);
    var origin = AimQuery.OriginAtBody(camera, body, look);
    origin.DistanceTo(camera).ShouldBe(camera.DistanceTo(body), Tolerance);
  }

  /// <summary>
  ///   A decorative (non-accepted) Area must not eat the interaction ray: the
  ///   pierce loop skips it and reports the accepted target behind it.
  /// </summary>
  [Test]
  public async Task IntersectPiercing_SkipsRejectedArea_AndHitsTargetBehind()
  {
    var root = new Node3D { Name = "PierceSoftRig" };
    var blocker = MakeArea("Blocker", RigOrigin + new Vector3(0f, 0f, -2f));
    var target = MakeArea("Target", RigOrigin + new Vector3(0f, 0f, -4f));
    root.AddChild(blocker);
    root.AddChild(target);
    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    await TestScene.ProcessFrame(2);

    var hit = AimQuery.IntersectPiercing(
      root.GetWorld3D().DirectSpaceState,
      from: RigOrigin,
      to: RigOrigin + new Vector3(0f, 0f, -6f),
      mask: uint.MaxValue,
      exclude: null!,
      accept: collider => collider == target,
      pierceUntilDistance: 0f
    );

    hit.Count.ShouldBeGreaterThan(0);
    hit["collider"].AsGodotObject().ShouldBe(target);
  }

  /// <summary>
  ///   Terrain / character bodies are hard stops: a look at a hillside must
  ///   never pick the tree behind it.
  /// </summary>
  [Test]
  public async Task IntersectPiercing_WorldBodyStopsRay()
  {
    var root = new Node3D { Name = "PierceHardRig" };
    var wall = MakeBody("Wall", RigOrigin + new Vector3(0f, 0f, -2f));
    var target = MakeArea("Target", RigOrigin + new Vector3(0f, 0f, -4f));
    root.AddChild(wall);
    root.AddChild(target);
    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    await TestScene.ProcessFrame(2);

    var hit = AimQuery.IntersectPiercing(
      root.GetWorld3D().DirectSpaceState,
      from: RigOrigin,
      to: RigOrigin + new Vector3(0f, 0f, -6f),
      mask: uint.MaxValue,
      exclude: null!,
      accept: collider => collider == target,
      pierceUntilDistance: 0f
    );

    hit.Count.ShouldBe(0);
  }

  /// <summary>
  ///   The loop is bounded: a stack of rejected Areas deeper than
  ///   <see cref="AimQuery.MaxPierceSteps"/> gives up instead of scanning the
  ///   whole level.
  /// </summary>
  [Test]
  public async Task IntersectPiercing_GivesUpAfterMaxPierceSteps()
  {
    var root = new Node3D { Name = "PierceLimitRig" };
    for (var i = 0; i < AimQuery.MaxPierceSteps + 2; i++)
    {
      root.AddChild(
        MakeArea($"Blocker{i}", RigOrigin + new Vector3(0f, 0f, -1f * (i + 1)))
      );
    }
    var target = MakeArea(
      "Target", RigOrigin + new Vector3(0f, 0f, -1f * (AimQuery.MaxPierceSteps + 4))
    );
    root.AddChild(target);
    await _fixture.AddToRoot(root, autoRemoveFromRoot: true);
    await TestScene.ProcessFrame(2);

    var hit = AimQuery.IntersectPiercing(
      root.GetWorld3D().DirectSpaceState,
      from: RigOrigin,
      to: RigOrigin + new Vector3(0f, 0f, -60f),
      mask: uint.MaxValue,
      exclude: null!,
      accept: collider => collider == target,
      pierceUntilDistance: 0f
    );

    hit.Count.ShouldBe(0);
  }

  private static Area3D MakeArea(string name, Vector3 globalPosition)
  {
    var area = new Area3D
    {
      Name = name,
      CollisionLayer = 4u,
      CollisionMask = 0u,
      GlobalPosition = globalPosition
    };
    area.AddChild(
      new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.25f } }
    );
    return area;
  }

  private static StaticBody3D MakeBody(string name, Vector3 globalPosition)
  {
    var body = new StaticBody3D
    {
      Name = name,
      CollisionLayer = 1u,
      CollisionMask = 0u,
      GlobalPosition = globalPosition
    };
    body.AddChild(
      new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4f, 4f, 0.4f) } }
    );
    return body;
  }
}
