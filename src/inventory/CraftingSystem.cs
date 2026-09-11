// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
///   Recipe-driven crafting, ported from upstream SurvivalIsland with the
///   completion refund REWRITTEN per plan Decision 3. All events go through
///   the static <see cref="GameEvents"/> bus; this node publishes only and
///   subscribes to nothing, so no _ExitTree unsubscribe is needed.
///
///   Upstream bug #2 (total loss): upstream CompleteCrafting called
///   AddItem(Result) and, on failure, only emitted "Inventory full" — the
///   consumed ingredients AND the crafted result were both silently lost.
///
  ///   Upstream hole #3 (double grant): the naive fix "refund every ingredient
  ///   whenever the result cannot be fully placed" would let a PARTIALLY
  ///   placed result (AddItem places what fits) coexist with a FULL ingredient
  ///   refund — the player keeps the placed results and gets all materials
  ///   back, duplicating value for free. The plan fix refunds ONLY the
  ///   unplaced fraction: remaining / ResultAmount of each ingredient's
  ///   per-craft consumption, rounded UP so ResultAmount &gt; 1 recipes (e.g.
  ///   arrow×5 from wood×1) never silently drop a fractional refund to zero.
  ///   For ResultAmount == 1 this still degrades to "full refund only when
  ///   nothing was placed, no refund otherwise".
  /// </summary>
public partial class CraftingSystem : Node
{
  [Export] public Array<CraftingRecipe> Recipes = new();

  private InventorySystem _inventory = null!;
  private bool _isCrafting;
  private CraftingRecipe? _currentRecipe;
  private float _craftingProgress;

  /// <summary>
  ///   Station proximity flags, set by the world/interaction layer. Kept as
  ///   plain settable booleans exactly like upstream so recipes can be
  ///   station-gated without a scene dependency.
  /// </summary>
  public bool IsNearCampfire { get; set; }

  public bool IsNearWorkbench { get; set; }

  // Iter8.5 (T8.5.6): furnace proximity flag, wired by StationLinker like the
  // campfire/workbench flags; gates recipes with CraftingRecipe.RequiresFurnace.
  public bool IsNearFurnace { get; set; }

  /// <summary>Called once after the player's InventorySystem is ready.</summary>
  public void Initialize(InventorySystem inventory) => _inventory = inventory;

  public override void _Process(double delta)
  {
    if (_isCrafting && _currentRecipe != null)
    {
      _craftingProgress += (float)delta / _currentRecipe.CraftingTime;
      GameEvents.RaiseCraftingProgress(_craftingProgress);

      if (_craftingProgress >= 1.0f)
        CompleteCrafting();
    }
  }

  public Array<CraftingRecipe> GetAvailableRecipes()
  {
    var available = new Array<CraftingRecipe>();

    foreach (var recipe in Recipes)
    {
      if (CanCraft(recipe))
        available.Add(recipe);
    }

    return available;
  }

  public bool CanCraft(CraftingRecipe recipe)
  {
    if (recipe.RequiresCampfire && !IsNearCampfire)
      return false;

    if (recipe.RequiresWorkbench && !IsNearWorkbench)
      return false;

    if (recipe.RequiresFurnace && !IsNearFurnace)
      return false;

    foreach (var ingredient in recipe.Ingredients)
    {
      if (ingredient.Item == null)
        continue;

      if (!_inventory.HasItem(ingredient.Item.Id, ingredient.Amount))
        return false;
    }

    return true;
  }

  /// <summary>
  ///   Starts crafting, consuming the ingredients up front (as upstream).
  ///   Returns false when already crafting or when CanCraft fails.
  /// </summary>
  public bool StartCrafting(CraftingRecipe recipe)
  {
    if (_isCrafting)
    {
      GameEvents.RaiseCraftingFailed("Already crafting");
      return false;
    }

    if (!CanCraft(recipe))
    {
      GameEvents.RaiseCraftingFailed("Missing ingredients or station");
      return false;
    }

    // Consume ingredients.
    foreach (var ingredient in recipe.Ingredients)
    {
      if (ingredient.Item != null)
        _inventory.RemoveItem(ingredient.Item.Id, ingredient.Amount);
    }

    _currentRecipe = recipe;
    _craftingProgress = 0;
    _isCrafting = true;

    GameEvents.RaiseCraftingStarted(recipe.Id);
    return true;
  }

  /// <summary>
  ///   Cancels the in-progress craft and refunds every ingredient in full.
  ///   This is the user-initiated cancel path: no result was produced, so a
  ///   complete refund is correct here (the double-grant hole only existed
  ///   on the COMPLETION failure path, see CompleteCrafting).
  /// </summary>
  public void CancelCrafting()
  {
    if (!_isCrafting || _currentRecipe == null)
      return;

    // Refund ingredients.
    foreach (var ingredient in _currentRecipe.Ingredients)
    {
      if (ingredient.Item != null)
        _inventory.AddItem(ingredient.Item, ingredient.Amount);
    }

    _isCrafting = false;
    _currentRecipe = null;
    _craftingProgress = 0;

    GameEvents.RaiseCraftingProgress(0f);
  }

  private void CompleteCrafting()
  {
    if (_currentRecipe?.Result == null)
    {
      _isCrafting = false;
      _currentRecipe = null;
      _craftingProgress = 0;
      return;
    }

    var recipe = _currentRecipe;

    // Fixed refund (plan Decision 3, see class docs): AddItem returns the
    // count of results that could NOT be placed. Refund the unplaced
    // fraction of each ingredient (ceil so Amount * remaining < ResultAmount
    // still returns at least one unit — avoids silent material loss on
    // multi-output recipes like arrow×5).
    var remaining = _inventory.AddItem(recipe.Result, recipe.ResultAmount);
    if (remaining > 0)
    {
      foreach (var ingredient in recipe.Ingredients)
      {
        if (ingredient.Item != null && recipe.ResultAmount > 0)
        {
          var refund = (ingredient.Amount * remaining + recipe.ResultAmount - 1)
            / recipe.ResultAmount;
          if (refund > 0)
            _inventory.AddItem(ingredient.Item, refund);
        }
      }

      GameEvents.RaiseCraftingFailed(
        remaining == recipe.ResultAmount
          ? "Inventory full"
          : "Inventory partially full"
      );
    }
    else
    {
      GameEvents.RaiseCraftingCompleted(recipe.Id);
    }

    _isCrafting = false;
    _currentRecipe = null;
    _craftingProgress = 0;
  }

  public CraftingRecipe? GetRecipeById(string id) =>
    Recipes.FirstOrDefault(r => r.Id == id);
}
