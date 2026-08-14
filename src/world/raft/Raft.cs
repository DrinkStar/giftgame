// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   T8.5.2 (iter8.5): player raft with three control modes — Paddle
///   (camera-relative WASD thrust + yaw), Sail (fixed +Z thrust with gentle
///   A/D steering) and Anchored (body frozen in place). Inherits
///   <see cref="FloatingBody"/> so all buoyancy math is reused verbatim; the
///   raft ships default buoyancy cells (4 corners + center for the 3×0.3×2
///   deck) that keep the raft about half-submerged at 500 kg/m³.
///   <para>
///     The wave height sampler is wired either from the scene
///     (<see cref="WaterMeshPath"/>, set by the main orchestrator in
///     Game.tscn) or at runtime via <see cref="RaftSetup"/>. Raw samples are
///     lerp-smoothed once per physics tick because WaterMesh readback steps
///     at DisplacementReadbackPerSecond (10 Hz) and raw stepped heights would
///     make the raft jitter; every buoyancy cell then samples the same
///     smoothed surface level per tick.
///   </para>
/// </summary>
public partial class Raft : FloatingBody
{
  /// <summary>Raft control modes (strategy T8.5.2: paddle → sail → anchor).</summary>
  public enum RaftMode
  {
    /// <summary>Camera-relative paddle thrust (WASD) plus yaw steering.</summary>
    Paddle,

    /// <summary>Fixed world +Z sail thrust with gentle A/D steering.</summary>
    Sail,

    /// <summary>Anchored: the body is frozen in place.</summary>
    Anchored,
  }

  /// <summary>Paddle thrust in newtons (world force).</summary>
  [Export(PropertyHint.Range, "0, 200, 1")]
  public float PaddleForce { get; set; } = 30f;

  /// <summary>
  ///   Sail thrust in newtons, always along world +Z (no wind system yet —
  ///   the direction is hardcoded per strategy T8.5.2).
  /// </summary>
  [Export(PropertyHint.Range, "0, 200, 1")]
  public float SailForce { get; set; } = 40f;

  /// <summary>Yaw torque (N·m) applied around Y from lateral input in Paddle mode.</summary>
  [Export(PropertyHint.Range, "0, 100, 0.5")]
  public float TurnTorque { get; set; } = 6f;

  /// <summary>Gentle yaw torque (N·m) from A/D while sailing.</summary>
  [Export(PropertyHint.Range, "0, 100, 0.5")]
  public float SailTurnTorque { get; set; } = 2f;

  /// <summary>
  ///   Optional NodePath to the scene's <see cref="WaterMesh"/>. When set, the
  ///   raft wires its own wave-height provider in <see cref="_Ready"/>
  ///   (Game.tscn's orchestrator points this at the Water node). Empty by
  ///   default so code-constructed rafts stay un-wired and inert.
  /// </summary>
  [Export]
  public NodePath WaterMeshPath { get; set; } = new();

  /// <summary>
  ///   Current control mode. Switching to <see cref="RaftMode.Anchored"/>
  ///   freezes the body and zeroes leftover velocity once; leaving it
  ///   unfreezes. The freeze is applied inside <see cref="_PhysicsProcess"/>
  ///   (after the base buoyancy pass) so scene-load order and direct property
  ///   assignment both behave consistently.
  /// </summary>
  [Export]
  public RaftMode Mode { get; set; } = RaftMode.Paddle;

  /// <summary>Original (un-smoothed) wave sampler; null while un-wired.</summary>
  private Func<Vector3, float>? _rawWaveHeightProvider;

  /// <summary>Lerp-smoothed wave height, refreshed once per physics tick.</summary>
  private float _smoothedHeight;

  /// <summary>Mode-key held states for self-tracked input edges (T8.5.2).</summary>
  private bool _anchorKeyHeld;
  private bool _modeKeyHeld;

  public override void _Ready()
  {
    // Scene-driven wiring: the main orchestrator sets WaterMeshPath to the
    // Water node; GetWaveHeight becomes the raw sampler behind the smoother.
    if (!WaterMeshPath.IsEmpty)
    {
      var waterMesh = GetNodeOrNull<WaterMesh>(WaterMeshPath);
      if (waterMesh is null)
      {
        GD.PushWarning(
          $"Raft: WaterMesh node '{WaterMeshPath}' not found; buoyancy wiring skipped."
        );
      }
      else
      {
        RaftSetup(waterMesh.GetWaveHeight);
      }
    }

    // Default buoyancy cells only when nothing was injected, so tests/scenes
    // may still override with custom cell layouts.
    if (Cells.Length == 0)
    {
      Cells = DefaultCells();
    }
    if (BodySize == Vector3.Zero)
    {
      BodySize = new Vector3(3f, 0.3f, 2f);
    }

    // Actions are registered in code (project.godot is untouched); guarded by
    // InputMap.HasAction so repeated registration is a no-op.
    RaftInput.RegisterInputActions();
  }

  public override void _PhysicsProcess(double delta)
  {
    // WaterMesh readback steps at DisplacementReadbackPerSecond (10 Hz).
    // Lerp toward the raw sample once per physics tick so the buoyancy cells
    // see a smooth surface instead of 10 Hz jumps (T8.5.2 wave-jitter fix).
    if (_rawWaveHeightProvider is not null)
    {
      _smoothedHeight = Mathf.Lerp(
        _smoothedHeight, _rawWaveHeightProvider(GlobalPosition), 0.2f
      );
    }

    // Base buoyancy first (a no-op when un-wired: the provider is null and
    // FloatingBody guards on it). The provider handed to the base returns the
    // smoothed height so every cell shares one stable surface level.
    base._PhysicsProcess(delta);

    ApplyMode();
  }

  /// <summary>
  ///   Runtime wiring point for the wave sampler, mirroring OceanTest's
  ///   floatBox.WaveHeightProvider assignment. Call with null to leave the
  ///   raft un-wired (no buoyancy, stays put).
  /// </summary>
  public void RaftSetup(Func<Vector3, float>? waveHeightProvider)
  {
    _rawWaveHeightProvider = waveHeightProvider;
    WaveHeightProvider = waveHeightProvider is null
      ? null
      : _ => _smoothedHeight;
  }

  /// <summary>
  ///   Per-mode force application (T8.5.2). Reads mode-switch input first so
  ///   a single press takes effect immediately, then drives the raft.
  /// </summary>
  private void ApplyMode()
  {
    HandleModeInput();

    if (Mode == RaftMode.Anchored)
    {
      if (!Freeze)
      {
        // Entering anchor: freeze once and kill leftover motion so raising
        // the anchor later leaves a still raft (it does not drift on).
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
      }
      return;
    }

    if (Freeze)
    {
      Freeze = false;
    }

    var input = Input.GetVector(
      PlayerController.MoveLeftAction,
      PlayerController.MoveRightAction,
      PlayerController.MoveForwardAction,
      PlayerController.MoveBackAction
    );

    switch (Mode)
    {
      case RaftMode.Paddle:
        ApplyPaddle(input);
        break;
      case RaftMode.Sail:
        ApplySail(input);
        break;
      case RaftMode.Anchored:
        break; // Unreachable — handled above.
    }
  }

  /// <summary>
  ///   Mode-switch input: G toggles the anchor (Anchored ↔ Paddle), M cycles
  ///   Paddle ↔ Sail (ignored while anchored — raise the anchor first).
  ///
  ///   Edge detection is self-tracked (held-state + own previous flags) instead
  ///   of Input.IsActionJustPressed: the just-pressed edge is frame-global and
  ///   unreliable under manual FlushBufferedEvents (GoDotTest), while the held
  ///   state is deterministic in both real gameplay and tests.
  /// </summary>
  private void HandleModeInput()
  {
    var anchorHeld = Input.IsActionPressed(RaftInput.AnchorAction);
    if (anchorHeld && !_anchorKeyHeld)
    {
      _anchorKeyHeld = true;
      Mode = Mode == RaftMode.Anchored ? RaftMode.Paddle : RaftMode.Anchored;
    }
    else if (!anchorHeld)
    {
      _anchorKeyHeld = false;
    }

    var modeHeld = Input.IsActionPressed(RaftInput.ModeAction);
    if (modeHeld && !_modeKeyHeld && Mode != RaftMode.Anchored)
    {
      _modeKeyHeld = true;
      Mode = Mode == RaftMode.Paddle ? RaftMode.Sail : RaftMode.Paddle;
    }
    else if (!modeHeld)
    {
      _modeKeyHeld = false;
    }
  }

  /// <summary>
  ///   Camera-relative paddle: WASD maps onto the camera basis (W = away from
  ///   the camera) and lateral input also yaws the raft around Y.
  /// </summary>
  private void ApplyPaddle(Vector2 input)
  {
    var camera = GetViewport().GetCamera3D();
    var basis = camera is null ? Basis.Identity : camera.GlobalBasis;

    // Input.Y follows PlayerController.GetInputVector's convention (forward
    // = -Y), so direction = basis.X * x + basis.Z * y points forward on W.
    var direction = basis.X * input.X + basis.Z * input.Y;
    direction.Y = 0f;
    if (direction.LengthSquared() > 1e-6f)
    {
      ApplyCentralForce(direction.Normalized() * PaddleForce);
    }
    if (input.X != 0f)
    {
      ApplyTorque(new Vector3(0f, input.X * TurnTorque, 0f));
    }
  }

  /// <summary>
  ///   Sail: fixed world +Z thrust (no wind system — hardcoded per strategy)
  ///   with gentle A/D yaw steering.
  /// </summary>
  private void ApplySail(Vector2 input)
  {
    ApplyCentralForce(new Vector3(0f, 0f, 1f) * SailForce);
    if (input.X != 0f)
    {
      ApplyTorque(new Vector3(0f, input.X * SailTurnTorque, 0f));
    }
  }

  /// <summary>
  ///   Default cell layout for the 3×0.3×2 deck: four corners plus a center
  ///   cell, all at 500 kg/m³ (≈ half-submersion, matching OceanTest's box).
  /// </summary>
  private static BuoyantCellData[] DefaultCells() => new[]
  {
    new BuoyantCellData(new Vector3(-1.1f, 0f, -0.7f), new Vector3(0.8f, 0.3f, 0.8f), 500f),
    new BuoyantCellData(new Vector3(1.1f, 0f, -0.7f), new Vector3(0.8f, 0.3f, 0.8f), 500f),
    new BuoyantCellData(new Vector3(-1.1f, 0f, 0.7f), new Vector3(0.8f, 0.3f, 0.8f), 500f),
    new BuoyantCellData(new Vector3(1.1f, 0f, 0.7f), new Vector3(0.8f, 0.3f, 0.8f), 500f),
    new BuoyantCellData(new Vector3(0f, 0f, 0f), new Vector3(0.8f, 0.3f, 0.8f), 500f),
  };
}
