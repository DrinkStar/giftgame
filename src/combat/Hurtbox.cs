// Original — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Combat hurtbox. Axis-aligned to the owning body (not CharacterAnimator
///   ExtraYaw mesh hacks). Enemies use layer 9; the player uses layer 10.
///   Per-species sizes are rest-pose visual fits — not raw animation AABBs
///   (pig/wolf/bat/shark clips inflate Z by several meters). Physics body
///   capsules stay small so enemies do not clip islands.
/// </summary>
public partial class Hurtbox : Area3D
{
  public enum Kind
  {
    Enemy,
    Player
  }

  public const string NodeName = "Hurtbox";
  public const string ShapeNodeName = "CollisionShape3D";

  /// <summary>Fallback capsule when an enemy id has no profile (placeholder).</summary>
  public const float EnemyRadius = 0.45f;
  public const float EnemyHeight = 1.5f;

  /// <summary>
  ///   Woman.glb rest mesh Y[0, 1.84], feet at local 0. Radius 0.4 covers
  ///   torso (arm-span AABB 1.65 is T-pose, not used). Offset Y = height/2
  ///   so the capsule covers [0, 1.84] including the head.
  /// </summary>
  public const float PlayerRadius = 0.4f;
  public const float PlayerHeight = 1.84f;
  public const float PlayerOffsetY = 0.92f;

  /// <summary>Rest-pose hurtbox for one species (local, pre-EnemyData.Scale).</summary>
  public readonly struct Profile
  {
    public readonly bool UseBox;
    public readonly float Radius;
    public readonly float Height;
    public readonly Vector3 BoxSize;
    public readonly Vector3 Offset;

    public Profile(float radius, float height, Vector3 offset)
    {
      UseBox = false;
      Radius = radius;
      Height = height;
      BoxSize = Vector3.Zero;
      Offset = offset;
    }

    public Profile(Vector3 boxSize, Vector3 offset)
    {
      UseBox = true;
      Radius = 0f;
      Height = 0f;
      BoxSize = boxSize;
      Offset = offset;
    }

    public float TopY => UseBox
      ? Offset.Y + BoxSize.Y * 0.5f
      : Offset.Y + Height * 0.5f;
  }

  /// <summary>
  ///   Rest-pose sizes vs measured mesh AABB (Godot instantiate, not anim
  ///   extremes). Box Y is centered on Offset so the volume sits on the feet.
  ///   <list type="bullet">
  ///     <item>player Woman (1.65, 1.84, 0.37) Y[0, 1.84] → capsule 0.4×1.84 @ y=0.92</item>
  ///     <item>crab (2.54, 1.51, 1.39) Y[0, 1.50] → box 2.2×1.5×1.3 @ y=0.75</item>
  ///     <item>boar Pig AABB Z=9.8 is jump/idle extent → standing box 1.6×1.6×2.4</item>
  ///     <item>wolf AABB Z=5.55 gallop extent → standing box 0.9×1.4×2.2</item>
  ///     <item>mutant Alien AABB X=3.87 arms → torso box 1.4×2.6×1.0</item>
  ///     <item>storm_beast Squidle Y starts ~1.3 → box 3.2×2.0×1.8 @ y=2.3</item>
  ///     <item>bat flying AABB 5m tall → body box 1.4×1.2×1.6</item>
  ///     <item>shark (Pirate Kit swim) AABB Z≈9.9 → box 1.6×1.4×4.0</item>
  ///     <item>shark_king/shark_pup (Gobkit @ root_scale 0.002) rest ≈0.98×1.38×0.87 → box 0.9×1.2×1.0 @ y=0.6; Scale multiplies</item>
  ///     <item>spider AABB XZ~6m legs → body box 2.4×1.2×2.2</item>
  ///   </list>
  /// </summary>
  public static Profile ForSpecies(string? id) => id switch
  {
    "player" => new Profile(PlayerRadius, PlayerHeight, new Vector3(0f, PlayerOffsetY, 0f)),
    "crab" => new Profile(new Vector3(2.2f, 1.5f, 1.3f), new Vector3(0f, 0.75f, 0f)),
    "boar" => new Profile(new Vector3(1.6f, 1.6f, 2.4f), new Vector3(0f, 0.8f, 0f)),
    "wolf" => new Profile(new Vector3(0.9f, 1.4f, 2.2f), new Vector3(0f, 0.7f, 0f)),
    "mutant" => new Profile(new Vector3(1.4f, 2.6f, 1.0f), new Vector3(0f, 1.3f, 0f)),
    "storm_beast" => new Profile(new Vector3(3.2f, 2.0f, 1.8f), new Vector3(0f, 2.3f, 0f)),
    "bat" => new Profile(new Vector3(1.4f, 1.2f, 1.6f), new Vector3(0f, 0.4f, 0f)),
    "shark" => new Profile(
      new Vector3(1.6f, 1.4f, 4.0f), new Vector3(0f, 0.3f, 0f)),
    "shark_king" or "shark_pup" => new Profile(
      new Vector3(0.9f, 1.2f, 1.0f), new Vector3(0f, 0.6f, 0f)),
    "spider" => new Profile(new Vector3(2.4f, 1.2f, 2.2f), new Vector3(0f, 0.6f, 0f)),
    _ => new Profile(EnemyRadius, EnemyHeight, Vector3.Zero)
  };
  /// <summary>
  ///   Walks the collider (Hurtbox or any ancestor) to an <see cref="EnemyBase"/>.
  ///   Direct <c>EnemyBase</c> colliders are a documented body-fallback for
  ///   thin misses; production melee/projectiles mask Hurtbox only.
  /// </summary>
  public static EnemyBase? ResolveEnemy(GodotObject? collider)
  {
    var node = collider as Node;
    while (node != null)
    {
      if (node is EnemyBase enemy)
        return enemy;
      node = node.GetParent();
    }

    return null;
  }

  public static bool IsPlayerHurtbox(GodotObject? collider) =>
    collider is Hurtbox hurtbox && hurtbox.CollisionLayer == CombatLayers.PlayerHurtboxMask;

  /// <summary>
  ///   Returns the existing child Hurtbox, or creates one. Applies the
  ///   species profile from a player host or <see cref="EnemyBase.EnemyData"/>.
  /// </summary>
  public static Hurtbox EnsureOn(Node3D host, Kind kind)
  {
    var hurtbox = host.GetNodeOrNull<Hurtbox>(NodeName);
    if (hurtbox == null)
    {
      hurtbox = new Hurtbox { Name = NodeName };
      hurtbox.AddChild(new CollisionShape3D { Name = ShapeNodeName });
      host.AddChild(hurtbox);
    }

    hurtbox.ApplyKind(kind);
    if (host is PlayerController)
      hurtbox.ApplySpecies("player");
    else if (host is EnemyBase enemy)
      hurtbox.ApplySpecies(enemy.EnemyData?.Id);
    else
      hurtbox.ApplySpecies(kind == Kind.Player ? "player" : null);

    return hurtbox;
  }

  public override void _Ready()
  {
    ApplyKind(DetectKind());
    if (GetParent() is PlayerController)
      ApplySpecies("player");
    else if (GetParent() is EnemyBase enemy)
      ApplySpecies(enemy.EnemyData?.Id);
  }

  public void ApplySpecies(string? speciesId)
  {
    var profile = ForSpecies(speciesId);
    var shapeNode = GetNodeOrNull<CollisionShape3D>(ShapeNodeName);
    if (shapeNode == null)
    {
      shapeNode = new CollisionShape3D { Name = ShapeNodeName };
      AddChild(shapeNode);
    }

    shapeNode.Shape = profile.UseBox
      ? new BoxShape3D { Size = profile.BoxSize }
      : new CapsuleShape3D { Radius = profile.Radius, Height = profile.Height };
    Position = profile.Offset;
  }

  public void Disable()
  {
    CollisionLayer = 0;
    CollisionMask = 0;
    Monitoring = false;
    Monitorable = false;
  }

  private Kind DetectKind() => GetParent() is EnemyBase ? Kind.Enemy : Kind.Player;

  private void ApplyKind(Kind kind)
  {
    Monitoring = false;
    Monitorable = true;
    CollisionMask = 0;
    CollisionLayer = kind == Kind.Enemy
      ? CombatLayers.EnemyHurtboxMask
      : CombatLayers.PlayerHurtboxMask;
  }
}
