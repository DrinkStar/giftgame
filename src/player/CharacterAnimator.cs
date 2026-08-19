// Original — visual layer over imported Kenney/Quaternius clips
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Logical clip kinds resolved against whatever names a GLB/GLTF actually
///   shipped. Matching is by animation stem (the part after <c>|</c> or
///   <c>/</c>), so <c>CharacterArmature|Idle</c> and <c>Idle</c> both count.
/// </summary>
public enum CharacterAnimKind
{
  Idle,
  Walk,
  Run,
  Attack,
  Jump,
  Hit,
  Death
}

/// <summary>
///   Visual-only driver. Prefers an <see cref="AnimationTree"/> (locomotion
///   blend + attack/hit/death one-shots) when Godot can build one; falls
///   back to hard-cut <see cref="AnimationPlayer.Play"/>. Never writes
///   velocity or AI state. Fail-closed: a missing player is a silent no-op.
/// </summary>
public partial class CharacterAnimator : Node
{
  public const string TreeNodeName = "CharacterAnimTree";

  [Export] public NodePath ModelPath = new();
  [Export] public float VisualYawOffsetDegrees = 180f;
  [Export] public float TurnSpeed = 10f;
  [Export] public float IdleSpeedThreshold = 0.45f;
  [Export] public float RunSpeedThreshold = 6f;
  [Export] public float BlendSpeed = 6f;

  /// <summary>The clip currently targeted, or empty when nothing is bound.</summary>
  public string CurrentClip { get; private set; } = "";

  /// <summary>True when an AnimationTree is driving playback.</summary>
  public bool UsesAnimationTree { get; private set; }

  private AnimationPlayer? _player;
  private AnimationTree? _tree;
  private Node3D? _visual;
  private bool _oneShot;
  private bool _dying;
  private string? _attackStyle;
  private bool _bound;
  private bool _hasJumpLayer;
  private bool _hasAttackShot;
  private bool _hasHitShot;
  private bool _hasDeathShot;
  private float _blendPos;
  private float _lastHealth = -1f;
  private float _deathLength;

  public override void _Ready()
  {
    BindFromTree();
    SetProcess(true);
  }

  public void BindFromTree()
  {
    Node? root = !ModelPath.IsEmpty
      ? GetNodeOrNull(ModelPath)
      : GetParent();
    Bind(root);
  }

  public void Bind(Node? root)
  {
    _player = FindAnimationPlayer(root);
    _visual = ResolveVisual(root);
    _bound = _player != null;
    _dying = false;
    _oneShot = false;
    UsesAnimationTree = false;
    if (_player == null)
      return;

    _player.Active = true;
    TryBuildTree();
    ApplyLocomotion(0f, running: false);
  }

  /// <summary>Play an attack one-shot; locomotion keeps blending underneath.</summary>
  public void NotifyAttack(string? style = null)
  {
    if (_dying)
      return;
    _attackStyle = style;
    FireShot(CharacterAnimKind.Attack, style, restart: true);
  }

  /// <summary>Play a hit react when the clip exists.</summary>
  public void NotifyHit()
  {
    if (_dying)
      return;
    FireShot(CharacterAnimKind.Hit, restart: true);
  }

  /// <summary>
  ///   Play death and freeze locomotion. Returns clip length in seconds
  ///   (0 when there is no player / clip) so callers can delay QueueFree.
  /// </summary>
  public float NotifyDeath()
  {
    _dying = true;
    FireShot(CharacterAnimKind.Death, restart: true);
    if (_player == null || string.IsNullOrEmpty(CurrentClip))
      return 0f;
    var anim = _player.GetAnimation(CurrentClip);
    _deathLength = (float)(anim?.Length ?? 0.0);
    return Mathf.Max(_deathLength, 0f);
  }

  public override void _Process(double delta)
  {
    var dt = (float)delta;
    if (GetParent() is PlayerController player)
      TickPlayer(player, dt);
    else if (GetParent() is EnemyBase enemy)
      TickEnemy(enemy, dt);
  }

  private void TickPlayer(PlayerController player, float dt)
  {
    if (!_bound)
      BindFromTree();

    PulsePlayerHit(player);

    if (player.Stats is { IsAlive: false })
    {
      if (!_dying)
        NotifyDeath();
      return;
    }

    if (_dying)
    {
      _dying = false;
      AbortShot("die");
    }

    if (!UsesAnimationTree && _oneShot && _player != null && _player.IsPlaying())
    {
      FaceFromPlayerInput(player, dt);
      return;
    }

    _oneShot = false;
    var input = Input.GetVector(
      PlayerController.MoveLeftAction,
      PlayerController.MoveRightAction,
      PlayerController.MoveForwardAction,
      PlayerController.MoveBackAction
    );
    var moving = input.LengthSquared() > 0.0001f;
    var grounded = player.IsOnFloor();
    ApplyLocomotion(
      moving ? (player.Running ? RunSpeedThreshold : IdleSpeedThreshold + 1f) : 0f,
      player.Running,
      grounded,
      snap: false,
      dt: dt
    );
    FaceFromPlayerInput(player, dt);
  }

  private void TickEnemy(EnemyBase enemy, float dt)
  {
    if (!_bound)
      BindFromTree();
    if (_dying)
      return;

    if (!UsesAnimationTree && _oneShot && _player != null && _player.IsPlaying())
    {
      FaceHorizontal(enemy.Velocity, dt, ExtraYawDegreesForEnemy(enemy.EnemyData?.Id));
      return;
    }

    _oneShot = false;
    var horizontal = new Vector3(enemy.Velocity.X, 0f, enemy.Velocity.Z);
    var speed = horizontal.Length();
    ApplyLocomotion(
      speed,
      running: enemy.IsCharging || speed >= RunSpeedThreshold,
      grounded: true,
      snap: false,
      dt: dt
    );
    FaceHorizontal(horizontal, dt, ExtraYawDegreesForEnemy(enemy.EnemyData?.Id));
  }

  /// <summary>
  ///   Real crabs scuttle sideways: keep the model's local forward 90° off
  ///   the travel direction so Walk reads as a sidestep. Other species face
  ///   along velocity (plus the Quaternius +Z vs Godot -Z 180° offset).
  /// </summary>
  public static float ExtraYawDegreesForEnemy(string? enemyId) =>
    string.Equals(enemyId, "crab", StringComparison.Ordinal) ? 90f : 0f;

  /// <summary>
  ///   Test seam: pick a locomotion clip without a PlayerController/EnemyBase
  ///   parent. Does not touch facing.
  /// </summary>
  public void ApplyLocomotion(
    float horizontalSpeed,
    bool running,
    bool grounded = true,
    bool snap = true,
    float dt = 0.016f
  )
  {
    if (_dying)
      return;
    if (!UsesAnimationTree && _oneShot && _player != null && _player.IsPlaying())
      return;

    _oneShot = false;
    CharacterAnimKind kind;
    if (!grounded)
      kind = CharacterAnimKind.Jump;
    else if (horizontalSpeed < IdleSpeedThreshold)
      kind = CharacterAnimKind.Idle;
    else if (running || horizontalSpeed >= RunSpeedThreshold)
      kind = CharacterAnimKind.Run;
    else
      kind = CharacterAnimKind.Walk;

    var targetBlend = kind switch
    {
      CharacterAnimKind.Idle => 0f,
      CharacterAnimKind.Walk => 0.5f,
      CharacterAnimKind.Run => 1f,
      _ => _blendPos
    };

    if (UsesAnimationTree && _tree != null)
    {
      _blendPos = snap
        ? targetBlend
        : Mathf.MoveToward(_blendPos, targetBlend, Mathf.Max(0.01f, BlendSpeed) * dt);
      _tree.Set("parameters/loc/blend_position", _blendPos);
      if (_hasJumpLayer)
        _tree.Set("parameters/air/blend_amount", grounded ? 0f : 1f);
      var clip = ResolveClip(
        _player!.GetAnimationList(),
        grounded ? kind : CharacterAnimKind.Jump,
        _attackStyle
      );
      if (!string.IsNullOrEmpty(clip) && (grounded || _hasJumpLayer))
        CurrentClip = clip;
      else if (!grounded && !_hasJumpLayer)
      {
        // Freeze the last locomotion pose rather than snapping to idle.
        _tree.Set("parameters/loc/blend_position", _blendPos);
      }
      return;
    }

    if (!grounded)
      PlayKind(CharacterAnimKind.Jump, loop: false);
    else if (kind == CharacterAnimKind.Idle)
      PlayKind(CharacterAnimKind.Idle, loop: true);
    else if (kind == CharacterAnimKind.Run)
      PlayKind(CharacterAnimKind.Run, loop: true);
    else
      PlayKind(CharacterAnimKind.Walk, loop: true);
  }

  public static string AttackStyleFromItemId(string? itemId)
  {
    if (string.IsNullOrEmpty(itemId))
      return "punch";
    if (itemId.Contains("bow", StringComparison.OrdinalIgnoreCase))
      return "bow";
    if (itemId.Contains("spear", StringComparison.OrdinalIgnoreCase)
        || itemId.Contains("sword", StringComparison.OrdinalIgnoreCase)
        || itemId.Contains("axe", StringComparison.OrdinalIgnoreCase)
        || itemId.Contains("pickaxe", StringComparison.OrdinalIgnoreCase)
        || itemId.Contains("sickle", StringComparison.OrdinalIgnoreCase))
      return "melee";
    return "punch";
  }

  public static string? ResolveClip(
    IReadOnlyList<string> clips,
    CharacterAnimKind kind,
    string? attackStyle = null
  )
  {
    if (clips.Count == 0)
      return null;

    var match = ResolveAliases(clips, kind, attackStyle);
    if (match != null)
      return match;

    // Walk before Jump so crab (Walk+Jump) runs with Walk; pig (Idle+Jump)
    // still charges with Jump because it has no Walk clip.
    return kind switch
    {
      CharacterAnimKind.Run =>
        ResolveAliases(clips, CharacterAnimKind.Walk, attackStyle)
        ?? ResolveAliases(clips, CharacterAnimKind.Jump, attackStyle)
        ?? ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      CharacterAnimKind.Walk =>
        ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      CharacterAnimKind.Jump =>
        ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      CharacterAnimKind.Attack =>
        ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      CharacterAnimKind.Hit =>
        ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      CharacterAnimKind.Death =>
        ResolveAliases(clips, CharacterAnimKind.Idle, attackStyle),
      _ => null
    };
  }

  private static string? ResolveAliases(
    IReadOnlyList<string> clips,
    CharacterAnimKind kind,
    string? attackStyle
  )
  {
    foreach (var alias in AliasesFor(kind, attackStyle))
    {
      var exact = FindByStem(clips, alias, exact: true);
      if (exact != null)
        return exact;
    }

    foreach (var alias in AliasesFor(kind, attackStyle))
    {
      var fuzzy = FindByStem(clips, alias, exact: false);
      if (fuzzy != null)
        return fuzzy;
    }

    return null;
  }

  public static string Stem(string clipName)
  {
    var name = clipName;
    var slash = name.LastIndexOf('/');
    if (slash >= 0)
      name = name[(slash + 1)..];
    var bar = name.LastIndexOf('|');
    if (bar >= 0)
      name = name[(bar + 1)..];
    return name.ToLowerInvariant();
  }

  private void FireShot(CharacterAnimKind kind, string? style = null, bool restart = false)
  {
    if (_player == null)
      return;

    var clip = ResolveClip(_player.GetAnimationList(), kind, style ?? _attackStyle);
    if (string.IsNullOrEmpty(clip))
      return;

    CurrentClip = clip;
    _oneShot = kind != CharacterAnimKind.Death;

    if (UsesAnimationTree && _tree != null)
    {
      var node = kind switch
      {
        CharacterAnimKind.Attack when _hasAttackShot => "atk",
        CharacterAnimKind.Hit when _hasHitShot => "hit",
        CharacterAnimKind.Death when _hasDeathShot => "die",
        _ => null
      };
      if (node != null)
      {
        _tree.Set(
          $"parameters/{node}/request",
          (int)AnimationNodeOneShot.OneShotRequest.Fire
        );
        return;
      }
    }

    PlayKind(kind, loop: false, restart: restart, attackStyle: style);
  }

  private void AbortShot(string node)
  {
    if (!UsesAnimationTree || _tree == null)
      return;
    _tree.Set(
      $"parameters/{node}/request",
      (int)AnimationNodeOneShot.OneShotRequest.FadeOut
    );
  }

  private void PlayKind(
    CharacterAnimKind kind,
    bool loop,
    bool restart = false,
    string? attackStyle = null
  )
  {
    if (_player == null)
      return;

    var clip = ResolveClip(_player.GetAnimationList(), kind, attackStyle ?? _attackStyle);
    if (string.IsNullOrEmpty(clip))
      return;

    if (!restart && clip == CurrentClip)
      return;

    var anim = _player.GetAnimation(clip);
    if (anim != null)
    {
      anim.LoopMode = loop
        ? Animation.LoopModeEnum.Linear
        : Animation.LoopModeEnum.None;
    }

    _player.Play(clip);
    CurrentClip = clip;
    _oneShot = !loop;
  }

  private void TryBuildTree()
  {
    UsesAnimationTree = false;
    _hasJumpLayer = false;
    _hasAttackShot = false;
    _hasHitShot = false;
    _hasDeathShot = false;
    if (_player == null)
      return;

    var existing = GetNodeOrNull<AnimationTree>(TreeNodeName);
    existing?.QueueFree();

    var clips = _player.GetAnimationList();
    var idle = ResolveClip(clips, CharacterAnimKind.Idle);
    if (string.IsNullOrEmpty(idle))
      return;

    var walk = ResolveClip(clips, CharacterAnimKind.Walk) ?? idle;
    var run = ResolveClip(clips, CharacterAnimKind.Run) ?? walk;
    var jumpExact = ResolveAliases(clips, CharacterAnimKind.Jump, null);
    var attack = ResolveAliases(clips, CharacterAnimKind.Attack, _attackStyle)
      ?? ResolveAliases(clips, CharacterAnimKind.Attack, "punch");
    var hit = ResolveAliases(clips, CharacterAnimKind.Hit, null);
    var death = ResolveAliases(clips, CharacterAnimKind.Death, null);

    try
    {
      var loc = new AnimationNodeBlendSpace1D
      {
        MinSpace = 0f,
        MaxSpace = 1f,
        BlendMode = AnimationNodeBlendSpace1D.BlendModeEnum.Interpolated
      };
      loc.AddBlendPoint(MakeAnim(idle), 0f, -1, "idle");
      if (walk != idle)
        loc.AddBlendPoint(MakeAnim(walk), 0.5f, -1, "walk");
      loc.AddBlendPoint(MakeAnim(run), 1f, -1, "run");

      var blend = new AnimationNodeBlendTree();
      blend.AddNode("loc", loc, new Vector2(0, 80));
      var feed = "loc";

      if (!string.IsNullOrEmpty(jumpExact))
      {
        var air = new AnimationNodeBlend2();
        blend.AddNode("jump_clip", MakeAnim(jumpExact), new Vector2(220, 0));
        blend.AddNode("air", air, new Vector2(220, 80));
        blend.ConnectNode("air", 0, feed);
        blend.ConnectNode("air", 1, "jump_clip");
        feed = "air";
        _hasJumpLayer = true;
      }

      feed = AddOneShot(blend, feed, "atk", "atk_clip", attack, new Vector2(440, 80), out _hasAttackShot);
      feed = AddOneShot(blend, feed, "hit", "hit_clip", hit, new Vector2(660, 80), out _hasHitShot);
      feed = AddOneShot(blend, feed, "die", "die_clip", death, new Vector2(880, 80), out _hasDeathShot);
      blend.ConnectNode("output", 0, feed);

      var tree = new AnimationTree
      {
        Name = TreeNodeName,
        AnimPlayer = new NodePath(),
        TreeRoot = blend,
        Active = false
      };
      AddChild(tree);
      tree.AnimPlayer = tree.GetPathTo(_player);
      tree.Active = true;
      _tree = tree;
      UsesAnimationTree = true;
      _blendPos = 0f;
    }
    catch (Exception)
    {
      UsesAnimationTree = false;
      _tree = null;
    }
  }

  private static string AddOneShot(
    AnimationNodeBlendTree blend,
    string feed,
    string shotName,
    string clipName,
    string? clip,
    Vector2 pos,
    out bool enabled
  )
  {
    enabled = false;
    if (string.IsNullOrEmpty(clip))
      return feed;

    var shot = new AnimationNodeOneShot
    {
      FadeInTime = 0.08f,
      FadeOutTime = 0.12f
    };
    blend.AddNode(clipName, MakeAnim(clip), pos + new Vector2(0, -80));
    blend.AddNode(shotName, shot, pos);
    blend.ConnectNode(shotName, 0, feed);
    blend.ConnectNode(shotName, 1, clipName);
    enabled = true;
    return shotName;
  }

  private static AnimationNodeAnimation MakeAnim(string clip) =>
    new() { Animation = clip, ResourceName = clip };

  private void PulsePlayerHit(PlayerController player)
  {
    var hp = player.Stats?.Health ?? -1f;
    if (_lastHealth > 0f && hp > 0f && _lastHealth - hp >= 1.5f)
      NotifyHit();
    if (hp >= 0f)
      _lastHealth = hp;
  }

  private void FaceFromPlayerInput(PlayerController player, float dt)
  {
    var input = Input.GetVector(
      PlayerController.MoveLeftAction,
      PlayerController.MoveRightAction,
      PlayerController.MoveForwardAction,
      PlayerController.MoveBackAction
    );
    if (input.LengthSquared() <= 0.0001f)
      return;

    var camera = player.GetViewport()?.GetCamera3D();
    var basis = camera is null ? Basis.Identity : camera.GlobalBasis;
    var world = (basis * new Vector3(input.X, 0f, input.Y)) with { Y = 0f };
    FaceHorizontal(world, dt);
  }

  private void FaceHorizontal(Vector3 worldDir, float dt, float extraYawDegrees = 0f)
  {
    if (_visual == null)
      return;

    var flat = worldDir with { Y = 0f };
    if (flat.LengthSquared() < 0.0001f)
      return;

    flat = flat.Normalized();
    var target = Mathf.Atan2(flat.X, flat.Z)
      + Mathf.DegToRad(VisualYawOffsetDegrees + extraYawDegrees);
    var yaw = Mathf.LerpAngle(_visual.Rotation.Y, target, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
    _visual.Rotation = _visual.Rotation with { Y = yaw };
  }

  private Node3D? ResolveVisual(Node? root)
  {
    if (GetParent() is PlayerController player)
    {
      var model = player.GetNodeOrNull<Node3D>("PlayerModel");
      if (model != null)
        return model;
    }

    if (GetParent() is EnemyBase enemy && !enemy.ModelPath.IsEmpty)
    {
      var model = enemy.GetNodeOrNull<Node3D>(enemy.ModelPath);
      if (model != null)
        return model;
    }

    return root as Node3D;
  }

  private static AnimationPlayer? FindAnimationPlayer(Node? root)
  {
    if (root == null)
      return null;

    AnimationPlayer? best = null;
    var bestCount = -1;
    var queue = new Queue<Node>();
    queue.Enqueue(root);
    while (queue.Count > 0)
    {
      var node = queue.Dequeue();
      if (node is AnimationPlayer candidate)
      {
        var count = candidate.GetAnimationList().Length;
        if (count > bestCount)
        {
          best = candidate;
          bestCount = count;
        }
      }

      foreach (var child in node.GetChildren())
        queue.Enqueue(child);
    }

    return best;
  }

  private static string? FindByStem(IReadOnlyList<string> clips, string alias, bool exact)
  {
    foreach (var clip in clips)
    {
      if (Stem(clip) == "reset")
        continue;
      var stem = Stem(clip);
      if (exact)
      {
        if (stem == alias)
          return clip;
      }
      else if (stem.Contains(alias, StringComparison.Ordinal))
      {
        return clip;
      }
    }

    return null;
  }

  private static IReadOnlyList<string> AliasesFor(CharacterAnimKind kind, string? attackStyle)
  {
    if (kind == CharacterAnimKind.Attack)
    {
      return attackStyle switch
      {
        "bow" => new[]
        {
          "gun_shoot", "idle_gun_shoot", "run_shoot", "attack", "punch_right"
        },
        "melee" => new[]
        {
          "sword_slash", "attack", "punch_right", "bite_front", "bite_inplace",
          "headbutt", "spider_attack", "bat_attack", "punch", "kick_right"
        },
        _ => new[]
        {
          "punch_right", "punch_left", "sword_slash", "attack", "bite_front",
          "bite_inplace", "headbutt", "punch", "spider_attack", "bat_attack",
          "kick_right"
        }
      };
    }

    return kind switch
    {
      CharacterAnimKind.Idle => new[]
      {
        "idle_neutral", "idle", "idle_2", "flying_idle", "spider_idle",
        "swim", "bat_flying", "flying"
      },
      CharacterAnimKind.Walk => new[]
      {
        "walk", "spider_walk", "swim", "bat_flying", "flying"
      },
      CharacterAnimKind.Run => new[]
      {
        "run", "gallop", "fast_flying", "bat_flying", "swim"
      },
      CharacterAnimKind.Jump => new[]
      {
        "jump", "gallop_jump", "jump_toidle", "roll"
      },
      CharacterAnimKind.Hit => new[]
      {
        "hitrecieve", "hitrecieve_2", "hit_react", "hitreact",
        "idle_hitreact", "bat_hit", "hit"
      },
      CharacterAnimKind.Death => new[] { "death", "bat_death", "spider_death" },
      _ => Array.Empty<string>()
    };
  }
}
