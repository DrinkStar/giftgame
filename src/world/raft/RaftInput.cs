// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   T8.5.2 (iter8.5): input registration for the raft, mirroring
///   <see cref="BuildingInput.RegisterInputActions"/>. Actions are created in
///   code (project.godot is untouched) and each registration is guarded by
///   <see cref="InputMap.HasAction"/> so repeated calls (scene reload,
///   multiple rafts, tests) are a no-op instead of a duplicate-action crash.
/// </summary>
public static class RaftInput
{
  /// <summary>Anchor toggle (G): Anchored ↔ Paddle.</summary>
  public const string AnchorAction = "raft_anchor";

  /// <summary>Mode cycle (M): Paddle ↔ Sail (ignored while anchored).</summary>
  public const string ModeAction = "raft_mode";

  /// <summary>
  ///   Registers the raft input actions if they are not present yet. Called
  ///   from <see cref="Raft._Ready"/>; idempotent.
  /// </summary>
  public static void RegisterInputActions()
  {
    var actions = new Dictionary<string, Key>
    {
      { AnchorAction, Key.G },
      { ModeAction, Key.M },
    };

    foreach (var action in actions)
    {
      if (InputMap.HasAction(action.Key))
      {
        continue;
      }

      InputMap.AddAction(action.Key);
      // PhysicalKeycode (not Keycode, like BuildingInput) so the bindings
      // follow the physical keyboard layout.
      InputMap.ActionAddEvent(
        action.Key,
        new InputEventKey { PhysicalKeycode = action.Value }
      );
    }
  }
}
