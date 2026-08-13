// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Represents the object menu in the grid building system (upstream
///   ObjectMenu). Slot clicks flow through the static
///   <see cref="GameEvents.BuildingSlotClicked"/> bus; the BuildingSystem
///   subscribes.
/// </summary>
public partial class BuildingMenu : Control
{
  private PackedScene _slot = default!;
  private HBoxContainer _objectContainer = default!;

  /// <summary>Called when the node is ready.</summary>
  public override void _Ready()
  {
    _slot = GD.Load<PackedScene>("res://scenes/building/ui/build_slot.tscn");
    _objectContainer = GetNode<HBoxContainer>(
      "PanelContainer/MarginContainer/ScrollContainer/HBoxContainer"
    );
  }

  /// <summary>
  ///   Populates the object grid with buildable objects from the library.
  /// </summary>
  /// <param name="buildableObjectLibrary">The buildable object library.</param>
  public void PopulateObjectGrid(BuildableResourceLibrary buildableObjectLibrary)
  {
    foreach (var child in _objectContainer.GetChildren())
    {
      child.QueueFree();
    }

    for (var i = 0; i < buildableObjectLibrary.BuildableObjects.Length; i++)
    {
      var resource = buildableObjectLibrary.BuildableObjects[i];
      if (resource == null)
      {
        // Skip empty library entries (upstream created an empty slot for
        // every null entry; with the 99-slot default array that leaves 96
        // blank buttons).
        continue;
      }

      var slotInstance = _slot.Instantiate<BuildSlot>();
      _objectContainer.AddChild(slotInstance);
      slotInstance.SetSlotData(resource, i);
    }
  }
}
