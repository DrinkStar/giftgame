// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Interaction ray, ported from upstream SurvivalIsland's PlayerInteraction
///   with these adaptations (plan Decision 8):
///   - a MANUAL IntersectRay each _Process frame driven by the exported
///     <see cref="CameraPath"/> (no RayCast3D node — the upstream RayCast3D
///     belongs to the attack ray, which is a later iteration);
///   - prompts/progress are published through the static
///     <see cref="GameEvents"/> bus instead of [Signal] delegates;
///   - <see cref="CurrentPrompt"/>/<see cref="CurrentProgress"/> expose the
///     live state so late subscribers (HUD) can pull initial values.
///   This node only publishes; it subscribes to nothing, so no _ExitTree
///   unsubscribe is needed.
/// </summary>
public partial class PlayerInteraction : Node
{
  /// <summary>The input action that triggers interaction (E).</summary>
  public const string InteractAction = "interact";

  /// <summary>Maximum raycast reach (meters).</summary>
  [Export] public float InteractionDistance = 3f;

  /// <summary>Hold duration (seconds) required for hold-to-interact targets.</summary>
  [Export] public float HoldInteractionTime = 1f;

  /// <summary>Path to the camera driving the ray (e.g. "../CameraPivot/Camera3D").</summary>
  [Export] public NodePath? CameraPath { get; set; }

  private PlayerController? _player;
  private Camera3D? _camera;
  private IInteractable? _currentTarget;
  private float _holdProgress;
  private bool _isHolding;
  private readonly Godot.Collections.Array<Rid> _rayExclude = [];

  private string _lastPublishedPrompt = "";
  private float _lastPublishedProgress;

  /// <summary>The prompt the HUD should currently show (empty = nothing).</summary>
  public string CurrentPrompt =>
    _currentTarget != null && _currentTarget.CanInteract()
      ? _currentTarget.GetInteractionPrompt()
      : "";

  /// <summary>Hold progress in [0, 1], or 0 when not holding.</summary>
  public float CurrentProgress => _isHolding ? _holdProgress : 0f;

  public override void _Ready()
  {
    _player = GetParentOrNull<PlayerController>();
    _camera = CameraPath is null ? null : GetNodeOrNull<Camera3D>(CameraPath);
    if (_player != null)
    {
      _rayExclude.Clear();
      _rayExclude.Add(_player.GetRid());
    }
  }

  public override void _Process(double delta)
  {
    // FIX(code-review P2-07): no interaction while dead — the T7.0 respawn
    // contract stops ALL player input on death (WeaponSystem already does
    // this). Clear any held target so no prompt/progress leaks, and skip the
    // ray + input handling until revive.
    if (_player?.Stats is { IsAlive: false })
    {
      ClearTarget();
      return;
    }

    UpdateInteractionTarget();
    HandleInteractionInput(delta);
  }

  private void UpdateInteractionTarget()
  {
    if (_player == null || _camera == null)
      return;

    var spaceState = _player.GetWorld3D().DirectSpaceState;
    var from = _camera.GlobalPosition;
    var direction = -_camera.GlobalTransform.Basis.Z; // Camera forward.
    var to = from + direction * InteractionDistance;

    var query = PhysicsRayQueryParameters3D.Create(from, to);
    // Layers 1 (World), 3 (Interactables), 4 (Animals) and 5 (Buildings —
    // FIX(iter5-plan): added so farm plots placed through the building system
    // are ray-reachable, plan Decision 8); the player's own body is excluded
    // by RID so the ray can pass through it.
    query.CollisionMask = 0b11101;
    query.CollideWithAreas = true;
    query.CollideWithBodies = true;
    query.Exclude = _rayExclude;

    var result = spaceState.IntersectRay(query);

    if (result.Count == 0)
    {
      ClearTarget();
      return;
    }

    var collider = result["collider"].AsGodotObject();
    var interactable = FindInteractable(collider);

    if (interactable == null || !interactable.CanInteract())
    {
      ClearTarget();
      return;
    }

    if (_currentTarget != interactable)
    {
      _currentTarget = interactable;
      _holdProgress = 0f;
      _isHolding = false;
      PublishPrompt(interactable.GetInteractionPrompt());
    }
  }

  /// <summary>
  ///   Walks the collider's parent chain looking for an IInteractable: the
  ///   collider itself first, then every ancestor up to the scene root.
  ///   FIX(iter5-plan): when an ancestor is a BuildableInstance, its
  ///   ObjectInstance is checked too (Decision 8) — buildable models like
  ///   FarmPlot are instantiated as a CHILD of the BuildableInstance, so the
  ///   plain ancestor walk would never reach them and buildable interactables
  ///   would stay permanently unreachable.
  /// </summary>
  private static IInteractable? FindInteractable(GodotObject collider)
  {
    if (collider is IInteractable direct)
      return direct;

    if (collider is not Node node)
      return null;

    var current = node;
    while (current != null)
    {
      if (current is IInteractable interactable)
        return interactable;

      // FIX(iter5-plan): reach into buildable model for IInteractable (Decision 8)
      if (
        current is BuildableInstance buildable
        && buildable.ObjectInstance is IInteractable objectInteractable
      )
      {
        return objectInteractable;
      }

      current = current.GetParent();
    }

    return null;
  }

  private void ClearTarget()
  {
    if (_currentTarget == null)
      return;

    _currentTarget = null;
    _holdProgress = 0f;
    _isHolding = false;
    PublishPrompt("");
    PublishProgress(0f);
  }

  private void HandleInteractionInput(double delta)
  {
    if (_player == null || _currentTarget == null)
      return;

    if (Input.IsActionJustPressed(InteractAction))
    {
      if (!_currentTarget.RequiresHold())
      {
        _currentTarget.Interact(_player);
      }
      else
      {
        _isHolding = true;
        _holdProgress = 0f;
      }
    }

    if (Input.IsActionJustReleased(InteractAction))
    {
      _isHolding = false;
      _holdProgress = 0f;
      PublishProgress(0f);
    }

    if (_isHolding && _currentTarget.RequiresHold())
    {
      _holdProgress += (float)delta / HoldInteractionTime;
      PublishProgress(_holdProgress);

      if (_holdProgress >= 1f)
      {
        _currentTarget.Interact(_player);
        _holdProgress = 0f;
        _isHolding = false;
        PublishProgress(0f);

        // The interaction may have consumed the target (e.g. harvested).
        if (!_currentTarget.CanInteract())
          ClearTarget();
      }
    }
  }

  private void PublishPrompt(string prompt)
  {
    if (prompt == _lastPublishedPrompt)
      return;

    _lastPublishedPrompt = prompt;
    GameEvents.RaiseInteractionPromptChanged(prompt);
  }

  private void PublishProgress(float progress)
  {
    if (Mathf.IsEqualApprox(progress, _lastPublishedProgress))
      return;

    _lastPublishedProgress = progress;
    GameEvents.RaiseInteractionProgressChanged(progress);
  }
}

/// <summary>
///   Contract for anything the interaction ray can hit. Prompts are English
///   (plan Decision 8).
/// </summary>
public interface IInteractable
{
  /// <summary>Prompt text shown by the HUD while this is the ray target.</summary>
  string GetInteractionPrompt();

  /// <summary>Whether the interaction is currently available.</summary>
  bool CanInteract();

  /// <summary>Whether the player must hold E for HoldInteractionTime.</summary>
  bool RequiresHold();

  /// <summary>Performs the interaction.</summary>
  void Interact(PlayerController player);
}
