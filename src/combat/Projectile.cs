// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Player projectile (Iter6 plan Decision 4). An Area3D that monitors the
///   World (1) and EnemyHurtbox (9) layers, integrates velocity with gravity
///   in _PhysicsProcess, and frees itself on hurtbox hit, ground/wall hit, or
///   lifetime expiry. Player bodies are ignored so a fresh projectile can
///   never hurt the shooter. <see cref="Hurtbox.ResolveEnemy"/> still accepts
///   an EnemyBase body collider as a thin-miss fallback.
/// </summary>
public partial class Projectile : Area3D
{
  [Export] public float Damage = 15f;
  [Export] public float Speed = 20f;

  /// <summary>Projectile gravity (hides Area3D.Gravity, Decision 4).</summary>
  [Export] public new float Gravity = 2f;

  [Export] public float Lifetime = 5f;

  private Vector3 _velocity;
  private float _elapsed;
  private Godot.Collections.Array<Rid>? _sweepExclude;

  public override void _Ready()
  {
    CollisionMask = CombatLayers.ProjectileMask;
    Monitoring = true;
    BodyEntered += OnBodyEntered;
    AreaEntered += OnAreaEntered;
  }

  public override void _ExitTree()
  {
    BodyEntered -= OnBodyEntered;
    AreaEntered -= OnAreaEntered;
  }

  /// <summary>Sets the initial velocity (called by WeaponSystem on spawn).</summary>
  public void SetVelocity(Vector3 velocity) => _velocity = velocity;

  public override void _PhysicsProcess(double delta)
  {
    var dt = (float)delta;

    _elapsed += dt;
    if (_elapsed >= Lifetime)
    {
      QueueFree();
      return;
    }

    var previous = GlobalPosition;
    _velocity.Y += Gravity * dt;
    GlobalPosition += _velocity * dt;

    // FIX(code-review P2-06): continuous collision — an Area3D only reports
    // body overlaps at physics-frame boundaries, so a fast projectile
    // (20-25 m/s ≈ 0.4 m per frame) can tunnel through thin walls, the raft
    // or a small enemy between frames. Sweep the step with a ray from the
    // previous to the new position; hit handling mirrors OnBodyEntered.
    var spaceState = GetWorld3D().DirectSpaceState;
    if (spaceState == null)
      return;

    var query = PhysicsRayQueryParameters3D.Create(previous, GlobalPosition);
    query.CollisionMask = CollisionMask;
    query.CollideWithAreas = true;
    query.CollideWithBodies = true;
    _sweepExclude ??= [];
    if (_sweepExclude.Count == 0)
    {
      _sweepExclude.Add(GetRid());
    }

    query.Exclude = _sweepExclude;
    var hit = spaceState.IntersectRay(query);
    if (hit.Count == 0)
      return;

    ApplyHit(hit["collider"].As<Node>());
  }

  private void OnAreaEntered(Area3D area) => ApplyHit(area);

  private void OnBodyEntered(Node body) => ApplyHit(body);

  private void ApplyHit(Node? collider)
  {
    // Guard against double-handling: the sweep ray above may have already
    // queued this projectile for deletion in the same physics step.
    if (IsQueuedForDeletion())
      return;

    // Ignore the player (Decision 4) — projectiles only hurt enemies.
    if (collider is PlayerController)
      return;

    if (Hurtbox.ResolveEnemy(collider) is { } enemy)
    {
      enemy.TakeDamage(Damage);
      QueueFree();
      return;
    }

    // Ground or wall (StaticBody3D) stops the projectile; the raft is a
    // RigidBody3D (FloatingBody) and must stop it too (P2-06).
    if (collider is StaticBody3D or RigidBody3D)
      QueueFree();
  }
}
