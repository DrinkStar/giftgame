// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   3-phase boss state machine (Iter6.1 todo 4), attached as a child of the
///   boss <see cref="EnemyBase"/>. Reads the boss's <see cref="EnemyHealth"/>
///   each physics tick and drives the phase transitions:
///
///   - Phase 1 (>60% hp): nothing to do — the boss fights normally.
///   - Phase 2 (30–60%): summons one minion every
///     <see cref="MinionSpawnInterval"/> seconds, capped at
///     <see cref="MaxAliveMinions"/> alive (spawn decision delegated to
///     <see cref="CombatLogic.ShouldSpawnMinion"/>).
///   - Phase 3 (<30%): rage — speed ×1.8, damage ×1.5, shorter attack
///     interval, applied by <see cref="EnemyBase"/> reading
///     <see cref="CurrentPhase"/>, plus a dark-red visual tint applied here.
///
///   <see cref="GameEvents.RaiseBossPhaseChanged(string, int)"/> fires exactly
///   once per transition. This node is pure wiring — all thresholds and
///   decisions live in <see cref="CombatLogic"/>.
/// </summary>
public partial class BossPhaseController : Node
{
  /// <summary>Id reported with BossPhaseChanged events (defaults to the boss EnemyData id).</summary>
  [Export] public string BossId = "";

  /// <summary>Path to the boss EnemyBase; empty = this node's parent.</summary>
  [Export] public NodePath BossPath = new();

  /// <summary>Node under which minions spawn; empty = this node's parent.</summary>
  [Export] public NodePath MinionSpawnParentPath = new();

  /// <summary>Packed scene instantiated for each minion (enemy.tscn).</summary>
  [Export] public PackedScene? MinionScene;

  /// <summary>EnemyData assigned to spawned minions (e.g. shark.tres).</summary>
  [Export] public EnemyData? MinionData;

  /// <summary>Player NodePath assigned to spawned minions.</summary>
  [Export] public NodePath PlayerPath = new();

  /// <summary>
  ///   Optional DayNightService path (mirrors EnemyBase's own export). When
  ///   resolved, spawned minions inherit the night damage/speed boost via an
  ///   ABSOLUTE path (like IslandBuilder) so it works regardless of where the
  ///   minion is parented; empty = minions get no night boost (fail-closed,
  ///   same contract as a boss without a DayNightService).
  /// </summary>
  [Export] public NodePath DayNightServicePath = new();

  [Export] public float MinionSpawnInterval = 10f;
  [Export] public int MaxAliveMinions = 3;

  /// <summary>Dark-red tint applied to the boss in phase 3.</summary>
  [Export] public Color RageTint = new(0.35f, 0.04f, 0.04f);

  private EnemyBase? _boss;
  private DayNightService? _dayNight;
  private readonly List<EnemyBase> _minions = new();
  private int _phase = 1;
  private float _spawnTimer;

  /// <summary>Current boss phase (1/2/3); read by EnemyBase for rage math.</summary>
  public int CurrentPhase => _phase;

  public override void _Ready()
  {
    _boss = BossPath.IsEmpty
      ? GetParent() as EnemyBase
      : GetNodeOrNull<EnemyBase>(BossPath);

    if (_boss == null)
      GD.PushWarning("BossPhaseController: no boss EnemyBase found; disabled.");

    if (!DayNightServicePath.IsEmpty)
      _dayNight = GetNodeOrNull<DayNightService>(DayNightServicePath);

    if (string.IsNullOrEmpty(BossId))
      BossId = _boss?.EnemyData?.Id ?? "";
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_boss == null || _boss.EnemyData == null)
      return;

    var dt = (float)delta;

    var health = _boss.HealthTracker;
    var hpRatio = health == null ? 1f : health.Health / health.MaxHealth;

    var phase = CombatLogic.BossPhase(hpRatio);
    if (phase != _phase)
    {
      _phase = phase;
      GameEvents.RaiseBossPhaseChanged(BossId, phase);

      if (phase == 3)
        _boss.OverrideTint(RageTint);
    }

    if (_phase != 2)
      return;

    // Phase 2: periodic minion summons (decision delegated to CombatLogic).
    _spawnTimer += dt;
    PruneMinions();

    if (
      CombatLogic.ShouldSpawnMinion(
        _spawnTimer, MinionSpawnInterval, _minions.Count, MaxAliveMinions
      )
    )
    {
      _spawnTimer = 0f;
      SpawnMinion();
    }
  }

  /// <summary>Drops freed minions (death calls QueueFree) from the roster.</summary>
  private void PruneMinions()
  {
    _minions.RemoveAll(m => !IsInstanceValid(m) || m.IsQueuedForDeletion());
  }

  private void SpawnMinion()
  {
    if (MinionScene == null || MinionData == null)
      return;

    var parent = MinionSpawnParentPath.IsEmpty
      ? GetParent()
      : GetNodeOrNull<Node3D>(MinionSpawnParentPath);
    parent ??= GetParent();
    if (parent == null)
      return;

    // FIX(code-review P2-05): type guard — a minion scene whose root is not
    // an EnemyBase must fail closed (warn + skip) instead of throwing from
    // the generic Instantiate cast.
    var minion = MinionScene.Instantiate() as EnemyBase;
    if (minion == null)
    {
      GD.PushWarning(
        "BossPhaseController: minion scene root is not an EnemyBase; spawn skipped."
      );
      return;
    }

    minion.EnemyData = MinionData;
    if (!PlayerPath.IsEmpty)
      minion.Player = PlayerPath;

    // FIX(code-review P2-05): minions inherit the night boost. The boss's
    // DayNightServicePath is relative to the boss, so the minion (parented
    // elsewhere) gets an ABSOLUTE path — same pattern as IslandBuilder.
    if (_dayNight != null)
      minion.DayNightServicePath = new NodePath(_dayNight.GetPath());

    parent.AddChild(minion);

    // FIX(code-review P2-05): spawn slightly above the boss so the minion
    // never starts embedded in terrain/slopes; gravity settles it (swimmers
    // ignore gravity, so the offset is harmless there).
    minion.GlobalPosition = _boss!.GlobalPosition + new Vector3(2f, 1.5f, 2f);

    _minions.Add(minion);
  }
}
