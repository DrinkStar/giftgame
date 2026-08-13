namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Locks the material cost contract (plan Decision 2): the pure
///   <see cref="BuildCost.TryConsume"/> helper pays in one atomic RemoveItem,
///   refuses when the inventory is short without deducting anything, and
///   treats an empty CostItemId as free. The InventorySystem node lives in
///   the tree root like in CraftingSystemTest.
/// </summary>
public class BuildingCostTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private InventorySystem _inventory = default!;

  public BuildingCostTest(Node testScene)
    : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _inventory = new InventorySystem();
    _fixture.AddToRoot(_inventory, autoRemoveFromRoot: true);
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
    if (_inventory == null)
    {
      return;
    }

    _inventory.Dispose();
    _inventory = null!;
    GC.SuppressFinalize(this);
  }

  private static ItemData MakeItem(string id, int maxStack = 20) =>
    new() { Id = id, DisplayName = id, MaxStack = maxStack };

  private static BuildableResource MakeBuildable(string costItemId, int costAmount)
  {
    var resource = new BuildableResource
    {
      Name = "test_buildable",
      CostItemId = costItemId,
      CostAmount = costAmount
    };

    return resource;
  }

  [Test]
  public void TryConsumeDeductsCostAndReturnsTrue()
  {
    _inventory.AddItem(MakeItem("test_wood"), 10);
    var buildable = MakeBuildable("test_wood", 5);

    var consumed = BuildCost.TryConsume(_inventory, buildable);

    consumed.ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(5);
  }

  [Test]
  public void TryConsumeReturnsFalseAndDeductsNothingWhenShort()
  {
    _inventory.AddItem(MakeItem("test_wood"), 3);
    var buildable = MakeBuildable("test_wood", 5);

    var consumed = BuildCost.TryConsume(_inventory, buildable);

    consumed.ShouldBeFalse();
    _inventory.GetItemCount("test_wood").ShouldBe(3);
  }

  [Test]
  public void TryConsumeReturnsFalseWhenItemMissingEntirely()
  {
    _inventory.AddItem(MakeItem("test_stone"), 10);
    var buildable = MakeBuildable("test_wood", 1);

    var consumed = BuildCost.TryConsume(_inventory, buildable);

    consumed.ShouldBeFalse();
    _inventory.GetItemCount("test_stone").ShouldBe(10);
  }

  [Test]
  public void TryConsumeReturnsTrueWithoutDeductionForFreeBuildables()
  {
    _inventory.AddItem(MakeItem("test_wood"), 10);
    var freeBuildable = MakeBuildable("", 0);

    var consumed = BuildCost.TryConsume(_inventory, freeBuildable);

    consumed.ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(10);
  }

  [Test]
  public void BuildableResourceDefaultsToFree()
  {
    // Decision 2 defaults: a freshly created resource must be free.
    var resource = new BuildableResource();

    resource.CostItemId.ShouldBe("");
    resource.CostAmount.ShouldBe(0);
    resource.SnapBehaviour.ShouldBe(SnapBehaviour.Ground);
  }
}
