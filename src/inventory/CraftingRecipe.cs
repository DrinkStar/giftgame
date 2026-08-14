// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;
using Godot.Collections;

/// <summary>
///   Data-only crafting definition, serialized in assets/recipes/*.tres.
///   <see cref="Ingredients"/> is a typed array of <see cref="CraftingIngredient"/>
///   (its own [GlobalClass] file — see that class for why the split exists).
/// </summary>
[GlobalClass]
public partial class CraftingRecipe : Resource
{
  [Export] public string Id = "";
  [Export] public string DisplayName = "";
  [Export] public ItemData? Result;
  [Export] public int ResultAmount = 1;
  [Export] public Array<CraftingIngredient> Ingredients = new();
  [Export] public bool RequiresCampfire;
  [Export] public bool RequiresWorkbench;

  // Iter8.5 (T8.5.6): furnace gate — true gates the recipe behind
  // CraftingSystem.IsNearFurnace (wired by StationLinker "furnace" case).
  [Export] public bool RequiresFurnace;
  [Export] public float CraftingTime = 1.0f;
}
