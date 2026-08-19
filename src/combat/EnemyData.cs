// Original (Iter6) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Enemy movement archetypes (Iter6 plan Decision 5 + Iter6.1 todo 3):
///   MeleeChase walks straight at the player, Charge bursts when close and
///   the cooldown is ready, Swimmer tracks the player in all three axes
///   without gravity, Flyer (bat) seeks the player in 3 axes ignoring
///   gravity, Webbing (spider) chases like MeleeChase but slows the player
///   on hit, SeaBeast (storm beast) only pursues while the player is on a
///   floating body, Mutant (mutant) enrages below half health and slams a
///   short-range AoE. Serialized as the int value in .tres files (upstream
///   ItemType layout, explicit values so file/editor numbers stay stable).
/// </summary>
public enum EnemyBehavior
{
  MeleeChase = 0,
  Charge = 1,
  Swimmer = 2,
  Flyer = 3,
  Webbing = 4,
  SeaBeast = 5,
  Mutant = 6
}

/// <summary>
///   Species home biome used as DATA around the existing AI (contract 3:
///   the behavior switch is not rewritten). None = no leash / wander.
///   Explicit ints so .tres values stay stable.
/// </summary>
public enum EnemyHabitatKind
{
  None = 0,
  ForestRiver = 1,
  BeachShore = 2
}

/// <summary>
///   Data-only enemy definition serialized in assets/enemies/*.tres (Iter6
///   plan Decision 5/6). One resource per enemy; the file name equals
///   <see cref="Id"/>. Behavior specializations are applied by
///   <see cref="EnemyBase"/>; spider (Webbing), bat (Flyer),
///   storm_beast (SeaBeast) and mutant (Mutant) got their dedicated
///   behaviors in Iter6.1.
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
  /// <summary>Unique enemy id, equals the .tres file stem (e.g. "crab").</summary>
  [Export] public string Id = "";

  [Export] public float MaxHealth = 1f;

  /// <summary>Damage per hit applied to the player's PlayerStats.</summary>
  [Export] public float Damage = 1f;

  /// <summary>Horizontal chase/swim speed in meters/sec.</summary>
  [Export] public float MoveSpeed = 1f;

  /// <summary>Attack trigger distance in meters.</summary>
  [Export] public float AttackRange = 1.5f;

  /// <summary>Seconds between attacks.</summary>
  [Export] public float AttackCooldown = 1f;

  [Export] public EnemyBehavior Behavior = EnemyBehavior.MeleeChase;

  /// <summary>Boss enemies additionally raise BossDefeated on death.</summary>
  [Export] public bool Boss;

  /// <summary>Uniform visual/collision scale applied on _Ready.</summary>
  [Export] public float Scale = 1f;

  /// <summary>
  ///   Item id dropped as ground loot (GroundLoot) on death — T8.5.7 switched
  ///   enemy drops to the world-drop component instead of the inventory; empty
  ///   = no drop. The item resource is loaded from assets/items/&lt;id&gt;.tres.
  /// </summary>
  [Export] public string DropItemId = "";

  [Export] public int DropAmount = 1;

  /// <summary>
  ///   Home biome for spawn placement and wander/leash clamps. None keeps
  ///   the original always-chase-within-SleepDistance movement.
  /// </summary>
  [Export] public EnemyHabitatKind Habitat = EnemyHabitatKind.None;

  /// <summary>
  ///   Horizontal meters from the den (spawn) the enemy may roam. 0 falls
  ///   back to <see cref="EnemyHabitat.DefaultWanderRadius"/>.
  /// </summary>
  [Export] public float WanderRadius;
}
