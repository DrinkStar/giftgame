// Ported/adapted from chickensoft-games/GameDemo (MIT License) —
// see godot-refs/chickensoft-GameDemo/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Third-person orbit/follow camera. The camera is expected to be attached
///   to a pivot node that is a child of the player, so it follows the player
///   automatically through parenting. Mouse motion orbits the pivot:
///   horizontal motion yaws around the player, vertical motion pitches up and
///   down (clamped to a configurable range). Zero mouse input is a graceful
///   no-op. A World-layer ray pulls the boom in so the lens does not sit inside
///   terrain / trees (does not touch <see cref="PlayerMotion"/>).
/// </summary>
public partial class PlayerCamera : Camera3D
{
  /// <summary>Mouse sensitivity (degrees per pixel of relative motion).</summary>
  [Export(PropertyHint.Range, "0, 10, 0.01")]
  public float MouseSensitivity { get; set; } = 0.12f;

  /// <summary>Minimum pitch angle (degrees). Negative = looking up.</summary>
  [Export(PropertyHint.Range, "-89.9, -0.01, 0.01")]
  public float VerticalMin { get; set; } = -55f;

  /// <summary>Maximum pitch angle (degrees). Positive = looking up.</summary>
  [Export(PropertyHint.Range, "0.01, 89.9, 0.01")]
  public float VerticalMax { get; set; } = 55f;

  /// <summary>Keep this many meters clear of the hit surface when pulling in.</summary>
  [Export(PropertyHint.Range, "0.05, 1, 0.01")]
  public float CollisionMargin { get; set; } = 0.25f;

  /// <summary>Never pull closer than this local boom length (meters).</summary>
  [Export(PropertyHint.Range, "0.2, 3, 0.05")]
  public float MinBoomLength { get; set; } = 0.6f;

  /// <summary>
  ///   How fast the boom length eases toward the collision target (m/s).
  ///   Pull-in uses this rate; release back to rest uses half so the lens
  ///   does not pop out of cover abruptly.
  /// </summary>
  [Export(PropertyHint.Range, "1, 40, 0.5")]
  public float BoomSmoothSpeed { get; set; } = 18f;

  /// <summary>Base FOV while walking / idle.</summary>
  [Export(PropertyHint.Range, "50, 100, 0.5")]
  public float WalkFov { get; set; } = 75f;

  /// <summary>FOV while the player is sprinting (subtle speed read).</summary>
  [Export(PropertyHint.Range, "50, 110, 0.5")]
  public float SprintFov { get; set; } = 82f;

  /// <summary>FOV ease rate (degrees per second).</summary>
  [Export(PropertyHint.Range, "1, 60, 0.5")]
  public float FovSmoothSpeed { get; set; } = 12f;

  /// <summary>Physics layers the boom ray collides with (default: World).</summary>
  [Export(PropertyHint.Layers3DPhysics)]
  public uint BoomCollisionMask { get; set; } = 1u;

  private Node3D _pivot = default!;
  private PlayerController? _player;
  private float _yawRad;
  private float _pitchRad;
  private Vector3 _restLocal;
  private float _currentBoom = -1f;
  private readonly Godot.Collections.Array<Rid> _boomExclude = [];

  public override void _Ready()
  {
    // The pivot (parent) carries the orbit angles; the camera itself keeps
    // its static local offset so it always looks at the player's origin.
    if (GetParent() is not Node3D pivot)
    {
      GD.PushWarning("PlayerCamera expects a Node3D pivot as its parent.");
      SetProcessUnhandledInput(false);
      SetPhysicsProcess(false);
      return;
    }

    _pivot = pivot;
    _player = _pivot.GetParentOrNull<PlayerController>();
    _restLocal = Position;
    _currentBoom = _restLocal.Length();
    Fov = WalkFov;
    _yawRad = _pivot.Rotation.Y;
    _pitchRad = _pivot.Rotation.X;
    ApplyPivotRotation();

    _boomExclude.Clear();
    if (_pivot.GetParent() is CollisionObject3D body)
      _boomExclude.Add(body.GetRid());

    GameEvents.GameplayInputLockChanged += OnGameplayInputLockChanged;

    // Third-person mouse look needs a captured cursor — but the main menu /
    // inventory may already hold the mouse Visible. Only capture when
    // gameplay input is unlocked.
    if (!GameEvents.GameplayInputLocked)
      Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  public override void _ExitTree()
  {
    GameEvents.GameplayInputLockChanged -= OnGameplayInputLockChanged;
    if (!GameEvents.GameplayInputLocked)
      Input.MouseMode = Input.MouseModeEnum.Visible;
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_pivot == null)
      return;

    var dt = (float)delta;
    ApplyBoomCollision(dt);
    ApplySprintFov(dt);
  }

  private void OnGameplayInputLockChanged(bool locked)
  {
    // Re-assert capture when overlays unlock so look resumes without a
    // leftover Visible cursor (HUD / CraftUI usually set this too).
    if (!locked)
      Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    if (!ShouldAcceptLook())
      return;

    if (@event is not InputEventMouseMotion motion)
      return;

    // Graceful with zero input: no relative motion means nothing to change.
    if (motion.Relative.LengthSquared() <= 0.0001f)
      return;

    var radiansPerPixel = MouseSensitivity * Mathf.DegToRad(1f);
    _yawRad += -motion.Relative.X * radiansPerPixel;
    _pitchRad = Mathf.Clamp(
      _pitchRad + (-motion.Relative.Y * radiansPerPixel),
      Mathf.DegToRad(VerticalMin),
      Mathf.DegToRad(VerticalMax)
    );

    ApplyPivotRotation();
  }

  /// <summary>
  ///   Look is ignored while menus lock gameplay input. Cursor Visible is a
  ///   secondary guard (inventory / craft); headless may not honor Captured.
  /// </summary>
  public static bool ShouldAcceptLook() =>
    !GameEvents.GameplayInputLocked
    && Input.MouseMode != Input.MouseModeEnum.Visible;

  /// <summary>
  ///   Effective boom length along the rest offset when a World hit sits
  ///   <paramref name="hitDistance"/> meters from the pivot.
  /// </summary>
  public static float ResolveBoomLength(
    float restLength,
    float hitDistance,
    float margin,
    float minBoom
  )
  {
    if (restLength <= 0.0001f)
      return 0f;

    if (hitDistance <= 0f || float.IsNaN(hitDistance))
      return restLength;

    return Mathf.Clamp(hitDistance - margin, minBoom, restLength);
  }

  /// <summary>
  ///   Eases <paramref name="current"/> toward <paramref name="target"/>.
  ///   Pull-in (target &lt; current) uses full speed; release uses half so the
  ///   boom does not spring out of cover.
  /// </summary>
  public static float SmoothBoomLength(
    float current,
    float target,
    float delta,
    float speed
  )
  {
    if (speed <= 0f || delta <= 0f)
      return target;

    var rate = target < current - 0.0001f ? speed : speed * 0.5f;
    return Mathf.MoveToward(current, target, rate * delta);
  }

  private void ApplyBoomCollision(float delta)
  {
    var from = _pivot.GlobalPosition;
    var to = _pivot.ToGlobal(_restLocal);
    var restLen = from.DistanceTo(to);
    if (restLen <= 0.0001f)
    {
      Position = _restLocal;
      _currentBoom = 0f;
      return;
    }

    var space = GetWorld3D()?.DirectSpaceState;
    float targetBoom;
    if (space == null)
    {
      targetBoom = restLen;
    }
    else
    {
      var query = PhysicsRayQueryParameters3D.Create(from, to);
      query.CollisionMask = BoomCollisionMask;
      query.CollideWithAreas = false;
      query.CollideWithBodies = true;
      if (_boomExclude.Count > 0)
        query.Exclude = _boomExclude;

      var hit = space.IntersectRay(query);
      if (hit.Count == 0)
      {
        targetBoom = restLen;
      }
      else
      {
        var hitPos = hit["position"].AsVector3();
        targetBoom = ResolveBoomLength(
          restLen,
          from.DistanceTo(hitPos),
          CollisionMargin,
          MinBoomLength
        );
      }
    }

    if (_currentBoom < 0f)
      _currentBoom = restLen;

    _currentBoom = SmoothBoomLength(_currentBoom, targetBoom, delta, BoomSmoothSpeed);
    var dir = (to - from) / restLen;
    Position = _pivot.ToLocal(from + dir * _currentBoom);
  }

  private void ApplySprintFov(float delta)
  {
    var target = _player is { Running: true } ? SprintFov : WalkFov;
    Fov = Mathf.MoveToward(Fov, target, FovSmoothSpeed * delta);
  }

  private void ApplyPivotRotation() =>
    _pivot.Rotation = new Vector3(_pitchRad, _yawRad, 0f);
}
