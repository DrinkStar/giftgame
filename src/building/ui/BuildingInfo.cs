// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Globalization;
using Godot;

/// <summary>
///   Represents the user interface for displaying building information
///   (upstream InfoInterface). Subscribes to the static
///   <see cref="GameEvents"/> bus instead of an exported EventBus node, and
///   unsubscribes in _ExitTree (plan Decision 13).
/// </summary>
public partial class BuildingInfo : Control
{
  private Label _levelLabel = default!;
  private Label _buildModeLabel = default!;
  private Label _demolitionModeLabel = default!;

  /// <summary>Called when the node is ready to be used.</summary>
  public override void _Ready()
  {
    _levelLabel = GetNode<Label>(
      "PanelContainer/MarginContainer/GridContainer/LevelLabel"
    );
    _buildModeLabel = GetNode<Label>(
      "PanelContainer/MarginContainer/GridContainer/BuildModeLabel"
    );
    _demolitionModeLabel = GetNode<Label>(
      "PanelContainer/MarginContainer/GridContainer/DemolitionModeLabel"
    );

    GameEvents.BuildingLevelChanged += OnLevelChanged;
    GameEvents.BuildModeChanged += OnBuildModeChanged;
    GameEvents.DemolitionModeChanged += OnDemolitionModeChanged;
  }

  /// <inheritdoc/>
  public override void _ExitTree()
  {
    GameEvents.BuildingLevelChanged -= OnLevelChanged;
    GameEvents.BuildModeChanged -= OnBuildModeChanged;
    GameEvents.DemolitionModeChanged -= OnDemolitionModeChanged;
  }

  /// <summary>Called when the level changes.</summary>
  /// <param name="level">The new level.</param>
  public void OnLevelChanged(int level)
  {
    _levelLabel.Text = level.ToString(CultureInfo.InvariantCulture);
  }

  /// <summary>Called when the build mode changes.</summary>
  /// <param name="enabled">Whether build mode is enabled or disabled.</param>
  public void OnBuildModeChanged(bool enabled)
  {
    _buildModeLabel.Text = enabled ? "on" : "off";
  }

  /// <summary>Called when the demolition mode changes.</summary>
  /// <param name="enabled">Whether demolition mode is enabled or disabled.</param>
  public void OnDemolitionModeChanged(bool enabled)
  {
    _demolitionModeLabel.Text = enabled ? "on" : "off";
  }
}
