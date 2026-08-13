namespace SeaAnomaly;

using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

/// <summary>
///   Locks the pure <see cref="GridMath"/> port at unit level, including the
///   two upstream bug fixes: ① IsValidGridIndex boundaries and ② the
///   cell-size divide/round/multiply in SnappedPosition. All math is static
///   and needs no scene tree.
/// </summary>
public class GridMathTest : TestClass
{
  private const int GridSize = 40;

  public GridMathTest(Node testScene)
    : base(testScene) { }

  [Test]
  public void GridPositionAndCellGlobalPositionRoundtrip()
  {
    // Grid origin away from world origin (like a grid node placed in a
    // scene), cellSize 1.
    var gridGlobalPosition = new Vector3(10, 0.5f, 10);
    var cell = new Vector2I(5, 7);

    var world = GridMath.CellGlobalPosition(
      gridGlobalPosition,
      GridSize,
      GridSize,
      cell.X,
      cell.Y,
      1f
    );

    var index = GridMath.GridPosition(world, gridGlobalPosition, GridSize, GridSize, 1f);

    index.ShouldBe(cell);
  }

  [Test]
  public void GridPositionHandlesNegativeWorldCoordinates()
  {
    // The first cell of the grid reaches into negative world space; floor
    // rounding must land there instead of at cell 1 (upstream behavior).
    var gridGlobalPosition = Vector3.Zero;
    var world = new Vector3(-0.5f, 0, -0.5f);

    var index = GridMath.GridPosition(world, gridGlobalPosition, GridSize, GridSize, 1f);

    index.ShouldBe(new Vector2I(19, 19));
  }

  [Test]
  public void IsValidGridIndexAcceptsOnlyInBounds()
  {
    GridMath.IsValidGridIndex(0, 0, GridSize, GridSize).ShouldBeTrue();
    GridMath.IsValidGridIndex(GridSize - 1, GridSize - 1, GridSize, GridSize)
      .ShouldBeTrue();

    // Fix ①: upstream accepted x == _xSize (one past the edge).
    GridMath.IsValidGridIndex(GridSize, 0, GridSize, GridSize).ShouldBeFalse();
    GridMath.IsValidGridIndex(0, GridSize, GridSize, GridSize).ShouldBeFalse();

    // Fix ①: upstream rejected the first row/column via x > 0 / y > 0.
    GridMath.IsValidGridIndex(-1, 0, GridSize, GridSize).ShouldBeFalse();
    GridMath.IsValidGridIndex(0, -1, GridSize, GridSize).ShouldBeFalse();
  }

  [Test]
  public void SnappedPositionRoundsToCellWithCellSizeOne()
  {
    var gridGlobalPosition = Vector3.Zero;
    var snapped = GridMath.SnappedPosition(
      new Vector3(1.4f, 0, 2.6f),
      gridGlobalPosition,
      1f,
      hasXOffset: false,
      hasZOffset: false,
      yRotationInDegrees: 0f
    );

    snapped.ShouldBe(new Vector3(1, 0, 3));
  }

  [Test]
  public void SnappedPositionDividesByCellSizeBeforeRounding()
  {
    // Fix ②: upstream rounded the raw distance, which only worked when
    // cellSize == 1. With cellSize 2 the snap points must be even multiples
    // of 2.
    var gridGlobalPosition = Vector3.Zero;
    var snapped = GridMath.SnappedPosition(
      new Vector3(3.1f, 0, 5.4f),
      gridGlobalPosition,
      2f,
      hasXOffset: false,
      hasZOffset: false,
      yRotationInDegrees: 0f
    );

    snapped.ShouldBe(new Vector3(4, 0, 6));
  }

  [Test]
  public void SnappedPositionAddsHalfCellOffsetForOddSizes()
  {
    // Odd footprint: the object snaps to cell centers (upstream lines
    // 117-137). Mouse at x=1.2: the cell midpoint 1.5 is nearer than 0.5.
    var gridGlobalPosition = Vector3.Zero;
    var snapped = GridMath.SnappedPosition(
      new Vector3(1.2f, 0, 0.2f),
      gridGlobalPosition,
      1f,
      hasXOffset: true,
      hasZOffset: false,
      yRotationInDegrees: 0f
    );

    snapped.X.ShouldBe(1.5f);
    snapped.Z.ShouldBe(0f);
  }

  [Test]
  public void SnappedPositionFlipsOddOffsetInsideCurrentCell()
  {
    // Same odd object, mouse on the OTHER side of the snapped cell line:
    // the half-cell offset must flip sign so the snap point stays inside the
    // cell the mouse is over (upstream lines 133-134).
    var gridGlobalPosition = Vector3.Zero;
    var snapped = GridMath.SnappedPosition(
      new Vector3(0.8f, 0, 0f),
      gridGlobalPosition,
      1f,
      hasXOffset: true,
      hasZOffset: false,
      yRotationInDegrees: 0f
    );

    snapped.X.ShouldBe(0.5f);
  }
}
