// Original (Iter7) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   One-shot story point trigger (plan Decision 7): an Area3D that raises
///   <see cref="GameEvents.StoryPointReached"/> with its exported
///   <see cref="StoryPointId"/> the first time the player body enters.
///   Monitoring is on and the collision mask is layer 1 (the player's layer),
///   so it only reacts to the player. The sphere CollisionShape3D (radius 1)
///   is created in code when the scene does not provide one.
/// </summary>
public partial class StoryPointTrigger : Area3D
{
  [Export] public string StoryPointId { get; set; } = "";

  private bool _fired;

  public override void _Ready()
  {
    Monitoring = true;
    CollisionMask = 1; // layer 1 — the player's default layer
    CollisionLayer = 0; // the trigger itself occupies no physics layer
    BodyEntered += OnBodyEntered;

    if (!HasNode("CollisionShape3D"))
    {
      AddChild(
        new CollisionShape3D
        {
          Name = "CollisionShape3D",
          Shape = new SphereShape3D { Radius = 1f }
        }
      );
    }
  }

  public override void _ExitTree()
  {
    BodyEntered -= OnBodyEntered;
  }

  private void OnBodyEntered(Node3D body)
  {
    if (_fired || body is not PlayerController)
      return;

    _fired = true;
    GameEvents.RaiseStoryPointReached(StoryPointId);
  }
}
