// Original (Iter8p) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8p.4 (iter8p-plan Decision 6): the bed buildable. Interacting sleeps
///   until morning:
///   1. the day/night clock jumps to 06:00 (SetTime) — no day roll, since
///      SetTime does not increment CurrentDay, intended;
///   2. stamina refills to full;
///   3. the respawn slot moves to this bed, so the next death resurrects
///      here instead of at the world spawn point.
///
///   The bed is a runtime buildable (scenes/building/buildables/bed.tscn):
///   BuildableInstance instantiates the scene as its ObjectInstance child
///   with the generated collider on the parent. PlayerInteraction's
///   FindInteractable reaches into BuildableInstance.ObjectInstance for
///   IInteractable (added in Iter 5 for FarmPlot, plan Decision 8), so this
///   script — the scene root — stays ray-reachable.
/// </summary>
public partial class BedInteract : Node3D, IInteractable
{
  public string GetInteractionPrompt() => "[E] Sleep until morning";

  public bool CanInteract() => true;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    var gm = GetTree().CurrentScene?.GetNodeOrNull<GameManager>("GameManager");
    if (gm == null)
      return;

    // Jump to dawn. SetTime moves the clock without rolling the day — no day
    // roll, since SetTime does not increment CurrentDay, intended.
    gm.DayNightService?.SetTime(6f);

    // Sleep refills stamina to full.
    var stats = player.GetNodeOrNull<PlayerStats>("PlayerStats");
    if (stats != null)
      stats.Stamina = stats.StaminaMax;

    // The bed becomes the respawn point. The +Y offset (1 m) lifts the
    // revived player above the bed's generated collider instead of spawning
    // inside it (avoids a collider push-out on revive).
    gm.SetRespawnPoint(GlobalPosition + Vector3.Up * 1.0f);
  }
}
