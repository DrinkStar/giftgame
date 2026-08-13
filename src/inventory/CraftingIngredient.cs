// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   One ingredient line in a <see cref="CraftingRecipe"/>.
///
///   IMPORTANT (plan Decision 7): this type lives in its OWN file and is
///   [GlobalClass] on purpose. Recipe .tres files create ingredient
///   sub_resources through `script = ExtResource("...CraftingIngredient.cs")`,
///   and a .tres can only reference a script by its file path — a class
///   nested inside CraftingRecipe.cs has no file name to point at and can
///   never be instantiated from a .tres. Upstream declared both classes in
///   one file, which is why upstream recipe serialization was impossible.
/// </summary>
[GlobalClass]
public partial class CraftingIngredient : Resource
{
  [Export] public ItemData? Item;
  [Export] public int Amount = 1;
}
