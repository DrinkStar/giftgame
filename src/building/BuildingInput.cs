// Ported from MarkoDM/GodotInGameBuildingSystem (MIT) —
// godot-refs/MarkoDM-GodotInGameBuildingSystem/LICENSE
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   Input registration for the grid building system, adapted from the
///   upstream BSUtils.RegisterInputActions (the rest of BSUtils is out of
///   port scope). Plan Decision 7: only the 7 building actions are registered
///   (upstream also re-registered movement/toggle_menu/rotation actions that
///   SeaAnomaly already owns). Each action is guarded by
///   <see cref="InputMap.HasAction"/> so repeated registration (scene reload,
///   tests) is a no-op instead of a duplicate-action crash.
/// </summary>
public static class BuildingInput
{
  /// <summary>
  ///   Registers the 7 building input actions if they are not present yet.
  ///   Called from <see cref="BuildingSystem._Ready"/>.
  /// </summary>
  public static void RegisterInputActions()
  {
    // TODO - Load from configuration.
    var inputActions = new Dictionary<string, Key>
    {
      { "rotate_object", Key.R },
      { "build_mode", Key.B },
      { "grid_level_up", Key.Pageup },
      { "grid_level_down", Key.Pagedown },
      { "demolish", Key.Delete },
      { "quick_save", Key.F5 },
      { "quick_load", Key.F9 }
    };

    foreach (var action in inputActions)
    {
      if (InputMap.HasAction(action.Key))
      {
        continue;
      }

      InputMap.AddAction(action.Key);
      var inputKey = new InputEventKey
      {
        // PhysicalKeycode (not Keycode, like upstream) so the bindings follow
        // the physical keyboard layout, matching project.godot's existing
        // movement actions.
        PhysicalKeycode = action.Value
      };
      InputMap.ActionAddEvent(action.Key, inputKey);
    }
  }
}
