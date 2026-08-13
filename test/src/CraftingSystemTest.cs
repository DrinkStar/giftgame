// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Behavioral tests for <see cref="CraftingSystem"/> plus .tres load
///   assertions that lock the recipe/item serialization format at unit-test
///   level (plan B1 fix). Inventory and crafting nodes live in the tree root;
///   crafting completes through an explicit _Process call with a delta >=
///   CraftingTime, so no wall-clock timing is involved. The near-full and
///   completely-full refund tests pin the plan Decision 3 fix: upstream lost
///   everything on a failed completion, and the naive "full refund on
///   failure" fix would double-grant on partial placement — we refund only
///   the unplaced fraction.
/// </summary>
public class CraftingSystemTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private InventorySystem _inventory = default!;
  private CraftingSystem _crafting = default!;

  public CraftingSystemTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());
    _inventory = new InventorySystem();
    _crafting = new CraftingSystem();
    _fixture.AddToRoot(_inventory, autoRemoveFromRoot: true);
    _fixture.AddToRoot(_crafting, autoRemoveFromRoot: true);
    _crafting.Initialize(_inventory);
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
    if (_crafting == null)
      return;

    _crafting.Dispose();
    _crafting = null!;
    _inventory?.Dispose();
    _inventory = null!;
    GC.SuppressFinalize(this);
  }

  private static ItemData MakeItem(string id, int maxStack = 64) =>
    new() { Id = id, DisplayName = id, MaxStack = maxStack };

  private static CraftingRecipe MakeRecipe(
    string id,
    ItemData result,
    int resultAmount,
    params (ItemData Item, int Amount)[] ingredients
  )
  {
    var recipe = new CraftingRecipe
    {
      Id = id,
      DisplayName = id,
      Result = result,
      ResultAmount = resultAmount,
      CraftingTime = 1.0f
    };

    foreach (var (item, amount) in ingredients)
      recipe.Ingredients.Add(new CraftingIngredient { Item = item, Amount = amount });

    return recipe;
  }

  [Test]
  public void CanCraftFalseWhenIngredientsMissing()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    var axe = MakeItem("test_axe", maxStack: 1);
    var recipe = MakeRecipe("axe", axe, 1, (wood, 3));

    _inventory.AddItem(wood, 2);
    _crafting.CanCraft(recipe).ShouldBeFalse();

    _inventory.AddItem(wood, 1);
    _crafting.CanCraft(recipe).ShouldBeTrue();
  }

  [Test]
  public void CanCraftRespectsCampfireGate()
  {
    var raw = MakeItem("test_raw", maxStack: 20);
    var cooked = MakeItem("test_cooked", maxStack: 20);
    var recipe = MakeRecipe("cook", cooked, 1, (raw, 1));
    recipe.RequiresCampfire = true;

    _inventory.AddItem(raw, 1);

    _crafting.IsNearCampfire.ShouldBeFalse();
    _crafting.CanCraft(recipe).ShouldBeFalse();

    _crafting.IsNearCampfire = true;
    _crafting.CanCraft(recipe).ShouldBeTrue();
  }

  [Test]
  public void CanCraftRespectsWorkbenchGate()
  {
    var ore = MakeItem("test_ore", maxStack: 20);
    var ingot = MakeItem("test_ingot", maxStack: 20);
    var recipe = MakeRecipe("smelt", ingot, 1, (ore, 2));
    recipe.RequiresWorkbench = true;

    _inventory.AddItem(ore, 2);

    _crafting.IsNearWorkbench.ShouldBeFalse();
    _crafting.CanCraft(recipe).ShouldBeFalse();

    _crafting.IsNearWorkbench = true;
    _crafting.CanCraft(recipe).ShouldBeTrue();
  }

  [Test]
  public void StartCraftingConsumesIngredientsAndBlocksSecondStart()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    var axe = MakeItem("test_axe", maxStack: 1);
    var recipe = MakeRecipe("axe", axe, 1, (wood, 3));

    _inventory.AddItem(wood, 5);

    _crafting.StartCrafting(recipe).ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(2);

    // Already crafting: a second start must be rejected.
    _crafting.StartCrafting(recipe).ShouldBeFalse();
  }

  [Test]
  public void CancelCraftingRefundsAllIngredients()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    var axe = MakeItem("test_axe", maxStack: 1);
    var recipe = MakeRecipe("axe", axe, 1, (wood, 3));

    _inventory.AddItem(wood, 5);

    _crafting.StartCrafting(recipe).ShouldBeTrue();
    _inventory.GetItemCount("test_wood").ShouldBe(2);

    _crafting.CancelCrafting();
    _inventory.GetItemCount("test_wood").ShouldBe(5);
    _crafting.CanCraft(recipe).ShouldBeTrue();
  }

  [Test]
  public void CompleteCraftingGrantsResultAndEmitsCompleted()
  {
    var wood = MakeItem("test_wood", maxStack: 10);
    var axe = MakeItem("test_axe", maxStack: 1);
    var recipe = MakeRecipe("axe", axe, 1, (wood, 3));

    _inventory.AddItem(wood, 5);

    string? completed = null;
    Action<string> onCompleted = id => completed = id;

    GameEvents.CraftingCompleted += onCompleted;
    try
    {
      _crafting.StartCrafting(recipe).ShouldBeTrue();

      // Cross the finish line deterministically.
      _crafting._Process(recipe.CraftingTime);

      _inventory.GetItemCount("test_axe").ShouldBe(1);
      _inventory.GetItemCount("test_wood").ShouldBe(2);
      completed.ShouldBe("axe");
    }
    finally
    {
      GameEvents.CraftingCompleted -= onCompleted;
    }
  }

  [Test]
  public void CompleteCraftingRefundsOnlyUnplacedPortionWhenNearFull()
  {
    var junk = MakeItem("test_junk", maxStack: 10);
    var wood = MakeItem("test_wood", maxStack: 10);
    var stone = MakeItem("test_stone", maxStack: 10);
    var ingot = MakeItem("test_ingot", maxStack: 1);
    var recipe = MakeRecipe("ingot", ingot, 3, (wood, 3), (stone, 3));

    // 43 of 45 slots filled (41 junk stacks + wood 7 + stone 7): exactly 2
    // empty slots remain, so only 2 of the 3 ingots can be placed.
    _inventory.AddItem(junk, 410).ShouldBe(0);
    _inventory.AddItem(wood, 7).ShouldBe(0);
    _inventory.AddItem(stone, 7).ShouldBe(0);

    string? failed = null;
    Action<string> onFailed = reason => failed = reason;

    GameEvents.CraftingFailed += onFailed;
    try
    {
      _crafting.StartCrafting(recipe).ShouldBeTrue();
      _inventory.GetItemCount("test_wood").ShouldBe(4);
      _inventory.GetItemCount("test_stone").ShouldBe(4);

      _crafting._Process(recipe.CraftingTime);

      // Fixed refund (plan Decision 3): 1 of 3 results unplaced -> 1/3 of the
      // ingredients (1 wood + 1 stone) comes back. A full refund here would
      // be the upstream double-grant hole (2 ingots + all materials back).
      _inventory.GetItemCount("test_ingot").ShouldBe(2);
      _inventory.GetItemCount("test_wood").ShouldBe(5);
      _inventory.GetItemCount("test_stone").ShouldBe(5);
      failed.ShouldBe("Inventory partially full");
    }
    finally
    {
      GameEvents.CraftingFailed -= onFailed;
    }
  }

  [Test]
  public void CompleteCraftingRefundsEverythingWhenCompletelyFull()
  {
    var junk = MakeItem("test_junk", maxStack: 10);
    var wood = MakeItem("test_wood", maxStack: 10);
    var stone = MakeItem("test_stone", maxStack: 10);
    var ingot = MakeItem("test_ingot", maxStack: 1);
    var recipe = MakeRecipe("ingot", ingot, 1, (wood, 3), (stone, 3));

    // All 45 slots filled: 43 junk stacks + wood 7 + stone 7, zero free.
    _inventory.AddItem(junk, 410).ShouldBe(0);
    _inventory.AddItem(wood, 7).ShouldBe(0);
    _inventory.AddItem(stone, 7).ShouldBe(0);
    _inventory.AddItem(junk, 20).ShouldBe(0);

    string? failed = null;
    Action<string> onFailed = reason => failed = reason;

    GameEvents.CraftingFailed += onFailed;
    try
    {
      _crafting.StartCrafting(recipe).ShouldBeTrue();
      _inventory.GetItemCount("test_wood").ShouldBe(4);
      _inventory.GetItemCount("test_stone").ShouldBe(4);

      _crafting._Process(recipe.CraftingTime);

      // Nothing placed -> everything refunded (upstream bug #2 locked here:
      // no total loss of result AND ingredients).
      _inventory.GetItemCount("test_ingot").ShouldBe(0);
      _inventory.GetItemCount("test_wood").ShouldBe(7);
      _inventory.GetItemCount("test_stone").ShouldBe(7);
      failed.ShouldBe("Inventory full");
    }
    finally
    {
      GameEvents.CraftingFailed -= onFailed;
    }
  }

  [Test]
  public void StoneAxeRecipeTresLoadsWithTwoIngredients()
  {
    // Locks the B1 .tres recipe format at unit-test level: sub_resources
    // typed by the standalone CraftingIngredient script class.
    var recipe = GD.Load<CraftingRecipe>("res://assets/recipes/stone_axe.tres");

    recipe.ShouldNotBeNull();
    recipe.Id.ShouldBe("stone_axe");
    recipe.DisplayName.ShouldBe("Stone Axe");
    recipe.Result.ShouldNotBeNull();
    recipe.Result!.Id.ShouldBe("stone_axe");
    recipe.ResultAmount.ShouldBe(1);
    recipe.CraftingTime.ShouldBe(3f);
    recipe.RequiresCampfire.ShouldBeFalse();
    recipe.RequiresWorkbench.ShouldBeFalse();

    recipe.Ingredients.Count.ShouldBe(2);
    recipe.Ingredients[0].Item.ShouldNotBeNull();
    recipe.Ingredients[0].Item!.Id.ShouldBe("wood");
    recipe.Ingredients[0].Amount.ShouldBe(2);
    recipe.Ingredients[1].Item!.Id.ShouldBe("stone");
    recipe.Ingredients[1].Amount.ShouldBe(1);
  }

  [Test]
  public void AllRecipeAndItemTresFilesLoad()
  {
    var recipeIds = new[] { "stone_axe", "cooked_meat", "iron_ingot" };
    foreach (var id in recipeIds)
    {
      var recipe = GD.Load<CraftingRecipe>($"res://assets/recipes/{id}.tres");
      recipe.ShouldNotBeNull();
      recipe.Id.ShouldBe(id);
      recipe.Ingredients.Count.ShouldBeGreaterThan(0);
    }

    var itemIds = new[]
    {
      "wood", "stone", "iron_ore", "iron_ingot", "berries", "coconut",
      "raw_meat", "cooked_meat", "stone_axe", "torch"
    };

    foreach (var id in itemIds)
    {
      var item = GD.Load<ItemData>($"res://assets/items/{id}.tres");
      item.ShouldNotBeNull();
      item.Id.ShouldBe(id);
      item.DisplayName.ShouldNotBeNullOrEmpty();
    }
  }
}
