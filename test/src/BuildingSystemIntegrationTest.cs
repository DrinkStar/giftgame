namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   End-to-end building core integration test (plan Decision 16): loads the
///   real scenes/building/building_system.tscn next to a test ground, then
///   drives placement through the grid API directly (no camera rays — those
///   are unreliable headless). Locks the Decision 2 contract: CanPlace
///   validates without placing, TryToPlaceObject returns bool and only
///   occupies on true, and ClearObject frees the cells again.
/// </summary>
public class BuildingSystemIntegrationTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private BuildingSystem _buildingSystem = default!;
  private StaticBody3D _ground = default!;

  public BuildingSystemIntegrationTest(Node testScene)
    : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    // Test ground on layer 1 (World), mirroring the real Ground in
    // Game.tscn that the build ray targets (plan Decision 3).
    _ground = new StaticBody3D
    {
      Name = "TestGround",
      CollisionLayer = 1u,
      CollisionMask = 0u
    };
    var groundShape = new CollisionShape3D
    {
      Shape = new BoxShape3D { Size = new Vector3(80, 1, 80) }
    };
    _ground.AddChild(groundShape);
    _fixture.AddToRoot(_ground, autoRemoveFromRoot: true);

    var packed = GD.Load<PackedScene>("res://scenes/building/building_system.tscn");
    packed.ShouldNotBeNull();
    _buildingSystem = packed!.Instantiate<BuildingSystem>();
    _fixture.AddToRoot(_buildingSystem, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_buildingSystem == null)
    {
      return;
    }

    _buildingSystem.Dispose();
    _buildingSystem = null!;
    _ground?.Dispose();
    _ground = null!;
    GC.SuppressFinalize(this);
  }

  [Test]
  public void CanPlaceThenTryToPlaceOccupiesGridCell()
  {
    var grid = _buildingSystem.Grids[0];
    var resource = GD.Load<BuildableResource>("res://assets/buildables/campfire.tres");

    // Lock the asset format too: name matches the file stem, cost per
    // Decision 11.
    resource.ShouldNotBeNull();
    resource!.Name.ShouldBe("campfire");
    resource.CostItemId.ShouldBe("wood");
    resource.CostAmount.ShouldBe(5);

    var position = grid.GetCellGlobalPosition(5, 7);

    // Decision 2: validation happens BEFORE the cost is paid and must not
    // place anything.
    grid.CanPlace(resource, 0f, position).ShouldBeTrue();
    grid.TryToPlaceObject(resource, 0f, position).ShouldBeTrue();

    var index = grid.GetGridPosition(position);
    var cell = grid.GetGridCell(index.X, index.Y);
    cell.ShouldNotBeNull();
    cell!.HasGroundObject().ShouldBeTrue();
    cell.GroundObject.ShouldNotBeNull();
    cell.GroundObject!.BuildableResource.Name.ShouldBe("campfire");

    // The cell is occupied now: both validation and placement must refuse.
    grid.CanPlace(resource, 0f, position).ShouldBeFalse();
    grid.TryToPlaceObject(resource, 0f, position).ShouldBeFalse();
  }

  [Test]
  public void ClearObjectFreesOccupiedGridCell()
  {
    var grid = _buildingSystem.Grids[0];
    var resource = GD.Load<BuildableResource>("res://assets/buildables/campfire.tres");
    var position = grid.GetCellGlobalPosition(12, 4);

    grid.CanPlace(resource, 0f, position).ShouldBeTrue();
    grid.TryToPlaceObject(resource, 0f, position).ShouldBeTrue();

    var index = grid.GetGridPosition(position);
    var cell = grid.GetGridCell(index.X, index.Y);
    cell.ShouldNotBeNull();
    cell!.HasGroundObject().ShouldBeTrue();

    // Demolition path (BuildableInstance.ClearObject): the cell must be
    // freed immediately so the same spot can be validated again.
    cell.GroundObject!.ClearObject();

    cell.HasGroundObject().ShouldBeFalse();
    grid.CanPlace(resource, 0f, position).ShouldBeTrue();
  }

  [Test]
  public void CanPlaceIsFalseOutsideOfGrid()
  {
    var grid = _buildingSystem.Grids[0];
    var resource = GD.Load<BuildableResource>("res://assets/buildables/campfire.tres");
    var position = grid.GetCellGlobalPosition(39, 39);

    grid.CanPlace(resource, 0f, position).ShouldBeTrue();
    grid.TryToPlaceObject(resource, 0f, position).ShouldBeTrue();

    // A second campfire on the neighboring in-bounds cell is fine, but the
    // cell far beyond the last row must refuse placement.
    var farOut = position + new Vector3(0, 0, 500);
    grid.CanPlace(resource, 0f, farOut).ShouldBeFalse();
    grid.TryToPlaceObject(resource, 0f, farOut).ShouldBeFalse();
  }
}
