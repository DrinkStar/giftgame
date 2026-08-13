// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Represents a UI slot for displaying a single buildable object (upstream
///   Slot). Clicks raise <see cref="GameEvents.BuildingSlotClicked"/> with
///   the LIBRARY index carried by <see cref="SetSlotData"/> — upstream used
///   the child index of the slot, which silently breaks when null library
///   entries are skipped.
/// </summary>
public partial class BuildSlot : PanelContainer
{
  private TextureRect _textureRect = default!;
  private Label _label = default!;
  private int _libraryIndex;

  /// <summary>Called when the node is ready.</summary>
  public override void _Ready()
  {
    _textureRect = GetNode<TextureRect>("MarginContainer/TextureRect");
    _label = GetNode<Label>("Name");
  }

  /// <summary>Called when a GUI input event occurs.</summary>
  /// <param name="event">The input event.</param>
  public override void _GuiInput(InputEvent @event)
  {
    if (
      @event is InputEventMouseButton mouseButtonEvent
      && mouseButtonEvent.ButtonIndex == MouseButton.Left
      && mouseButtonEvent.Pressed
    )
    {
      AcceptEvent();
      GameEvents.RaiseBuildingSlotClicked(_libraryIndex, (int)MouseButton.Left);
    }
  }

  /// <summary>Sets the slot data for the slot.</summary>
  /// <param name="slotData">The buildable resource data for the slot.</param>
  /// <param name="libraryIndex">The index of the resource in the buildable library.</param>
  public void SetSlotData(BuildableResource slotData, int libraryIndex)
  {
    _libraryIndex = libraryIndex;

    var size = "";
    switch (slotData.SnapBehaviour)
    {
      case SnapBehaviour.Wall:
        size = $"{slotData.Size.X} x {slotData.Size.Y}";
        break;
      case SnapBehaviour.Ground:
        size = $"{slotData.Size.X} x {slotData.Size.Z}";
        break;
      default:
        break;
    }

    _textureRect.Texture = slotData.TextureAtlas ?? slotData.TextureImage;
    TooltipText = $"{slotData.Name}\n{slotData.Description}";
    _label.Text = $"{slotData.Name}\n{size}";
  }
}
