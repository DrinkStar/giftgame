// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Item category, serialized as its int value in .tres files (upstream
///   layout, explicit values so file/editor numbers stay stable).
/// </summary>
public enum ItemType
{
  Resource = 0,
  Tool = 1,
  Food = 2,
  Drink = 3,
  Buildable = 4
}

/// <summary>
///   Data-only item definition (plan Decisions 7/15). The upstream Icon
///   texture is kept as a field but the shipped .tres files omit the Icon
///   line because no icon assets exist in this project; UI falls back to
///   <see cref="DisplayName"/>.
/// </summary>
[GlobalClass]
public partial class ItemData : Resource
{
  [Export] public string Id = "";
  [Export] public string DisplayName = "";
  [Export] public string Description = "";
  [Export] public Texture2D? Icon;
  [Export] public ItemType Type = ItemType.Resource;
  [Export] public int MaxStack = 64;

  // For food/drink
  [Export] public float HungerRestore;
  [Export] public float ThirstRestore;
  [Export] public float HealthRestore;
  [Export] public bool RequiresCooking;

  // For tools
  [Export] public float ToolPower = 1.0f;
  [Export] public float ToolDurability = 100f;

  // For armor (T8.5.4): flat damage reduction applied by the player's armor.
  // Armor items (Type == Tool) with a value &gt; 0 are equippable with F.
  [Export] public float ArmorReduction;

  // For buildables
  [Export] public PackedScene? BuildableScene;
}
