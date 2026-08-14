// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Player projectile (Iter6 plan Decision 4). An Area3D that monitors the
///   World (1) and Enemies (8) layers, integrates velocity with gravity in
///   _PhysicsProcess, and frees itself on enemy hit, ground/wall hit, or
///   lifetime expiry. Player bodies are ignored so a fresh projectile can
///   never hurt the shooter.
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

  public override void _Ready()
  {
    // Decision 4: World (1) | Enemies (8) = 257.
    CollisionMask = 257u;
    Monitoring = true;
    BodyEntered += OnBodyEntered;
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

    _velocity.Y += Gravity * dt;
    GlobalPosition += _velocity * dt;
  }

  private void OnBodyEntered(Node body)
  {
    // Ignore the player (Decision 4) — projectiles only hurt enemies.
    if (body is PlayerController)
      return;

    if (body is EnemyBase enemy)
    {
      enemy.TakeDamage(Damage);
      QueueFree();
      return;
    }

    // Ground or wall (StaticBody3D) stops the projectile.
    if (body is StaticBody3D)
      QueueFree();
  }
}
