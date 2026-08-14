// Original (Iter8p) — interface reservation
namespace SeaAnomaly;

using Godot;

/// <summary>
///   R1 (Iter8p): everything a skill needs to decide and act. Plain data
///   bag (no scene-tree access) so skill implementations stay unit-testable;
///   the shipped NullSkillHost never constructs one.
/// </summary>
public class SkillContext
{
  /// <summary>The activating player node (null when not applicable).</summary>
  public Node? Player { get; init; }

  /// <summary>The target node of the skill (null when not applicable).</summary>
  public Node? Target { get; init; }

  /// <summary>The item the player currently holds (null when empty-handed).</summary>
  public ItemData? SelectedItem { get; init; }
}
