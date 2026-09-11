// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8.5.9: a readable story object (book / stone tablet / log) on the
///   Interactables physics layer (layer 3, value 4). Interacting raises
///   <see cref="GameEvents.GuideLine"/> with <see cref="Text"/> and — on the
///   first read only — <see cref="GameEvents.StoryPointReached"/> with
///   <see cref="StoryPointId"/>. By default the interaction is one-shot (a
///   second interact is a no-op and the prompt disappears), mirroring
///   <see cref="StoryPointTrigger"/>'s _fired guard; setting
///   <see cref="Reusable"/> keeps the prompt so scattered lore logs can be
///   re-read (GuideLine re-fires every read, StoryPointReached still only
///   fires once). Geometry (collision + visual) is built in code like
///   <see cref="WoodTree"/>, so scenes only carry the script node with the
///   exports set.
/// </summary>
public partial class StoryInteractable : StaticBody3D, IInteractable
{
  /// <summary>Story point id delivered to QuestService (e.g. "radio", "shark_king").</summary>
  [Export] public string StoryPointId = "";

  /// <summary>Guide narration shown when the log is read.</summary>
  [Export] public string Text = "";

  /// <summary>
  ///   When true the log stays interactable after the first read and every
  ///   read re-raises GuideLine; StoryPointReached still fires only on the
  ///   first read. Mainline quest logs keep the default (false).
  /// </summary>
  [Export] public bool Reusable;

  private bool _fired;

  public override void _Ready()
  {
    // Layer 3 = Interactables (value 4). The log collides for the
    // interaction ray only; it blocks nothing else.
    CollisionLayer = 4;
    CollisionMask = 0;

    // Built in code only when the scene lacks them (direct construction in
    // tests), so scene-instanced and code-constructed logs look the same.
    if (GetNodeOrNull<CollisionShape3D>("CollisionShape3D") == null)
    {
      AddChild(
        new CollisionShape3D
        {
          Name = "CollisionShape3D",
          Position = new Vector3(0, 0.5f, 0),
          Shape = new BoxShape3D { Size = new Vector3(0.7f, 0.3f, 0.9f) }
        }
      );
    }

    if (GetNodeOrNull<MeshInstance3D>("Visual") == null)
    {
      // A flattened stone-tablet box, tilted slightly for readability.
      AddChild(
        new MeshInstance3D
        {
          Name = "Visual",
          Position = new Vector3(0, 0.5f, 0),
          RotationDegrees = new Vector3(0, 0, 8f),
          Mesh = new BoxMesh
          {
            Size = new Vector3(0.6f, 0.12f, 0.8f),
            Material = new StandardMaterial3D
            {
              AlbedoColor = new Color("8A7F6D")
            }
          }
        }
      );
    }
  }

  public string GetInteractionPrompt() => "[E] Read log";

  public bool CanInteract() => Reusable || !_fired;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    if (_fired && !Reusable)
      return;

    bool firstTime = !_fired;
    _fired = true;
    GameEvents.RaiseGuideLine(Text);

    // An empty StoryPointId reads as a pure narration object (no quest
    // hook); a non-empty one advances the quest chain only on the first
    // read, so a re-read never re-triggers quest progress.
    if (firstTime && !string.IsNullOrEmpty(StoryPointId))
      GameEvents.RaiseStoryPointReached(StoryPointId);
  }
}
