// Original — no upstream port
namespace SeaAnomaly;

using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   Visual-layer clip wiring: alias resolution against the Kenney/Quaternius
///   stems already in the imported models, plus AnimationPlayer playback on a
///   dummy library (no GLB required).
/// </summary>
public class CharacterAnimatorTest : TestClass
{
  private Fixture _fixture = default!;

  public CharacterAnimatorTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup() => _fixture = new Fixture(TestScene.GetTree());

  [Cleanup]
  public void Cleanup() => _fixture.Cleanup();

  private static readonly string[] WomanClips =
  {
    "CharacterArmature|Death",
    "CharacterArmature|Gun_Shoot",
    "CharacterArmature|HitRecieve",
    "CharacterArmature|Idle",
    "CharacterArmature|Idle_Neutral",
    "CharacterArmature|Punch_Right",
    "CharacterArmature|Roll",
    "CharacterArmature|Run",
    "CharacterArmature|Sword_Slash",
    "CharacterArmature|Walk"
  };

  private static readonly string[] WolfClips =
  {
    "Attack", "Death", "Gallop", "Idle", "Walk"
  };

  private static readonly string[] PigClips =
  {
    "Armature|Idle", "Armature|Jump"
  };

  private static readonly string[] CrabClips =
  {
    "MonsterArmature|Bite_Front",
    "MonsterArmature|Idle",
    "MonsterArmature|Walk"
  };

  private static readonly string[] SharkClips = { "Armature|Swim" };

  [Test]
  public void WomanClips_ResolveIdleWalkRunAttack()
  {
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Idle)
      .ShouldBe("CharacterArmature|Idle_Neutral");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Walk)
      .ShouldBe("CharacterArmature|Walk");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Run)
      .ShouldBe("CharacterArmature|Run");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Attack, "punch")
      .ShouldBe("CharacterArmature|Punch_Right");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Attack, "melee")
      .ShouldBe("CharacterArmature|Sword_Slash");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Attack, "bow")
      .ShouldBe("CharacterArmature|Gun_Shoot");
  }

  [Test]
  public void WomanClips_JumpUsesRoll_WhenNoJumpClip()
  {
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Jump)
      .ShouldBe("CharacterArmature|Roll");
  }

  [Test]
  public void WomanClips_HitAndDeath_Resolve()
  {
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Hit)
      .ShouldBe("CharacterArmature|HitRecieve");
    CharacterAnimator.ResolveClip(WomanClips, CharacterAnimKind.Death)
      .ShouldBe("CharacterArmature|Death");
  }

  [Test]
  public void CrabRun_PrefersWalk_OverJump()
  {
    var crabWithJump = new[]
    {
      "MonsterArmature|Bite_Front",
      "MonsterArmature|Idle",
      "MonsterArmature|Jump",
      "MonsterArmature|Walk"
    };
    CharacterAnimator.ResolveClip(crabWithJump, CharacterAnimKind.Run)
      .ShouldBe("MonsterArmature|Walk");
    CharacterAnimator.ResolveClip(crabWithJump, CharacterAnimKind.Jump)
      .ShouldBe("MonsterArmature|Jump");
  }

  [Test]
  public void WomanClips_JumpFallsBackToIdle_WhenRollAndJumpMissing()
  {
    var noAir = new[]
    {
      "CharacterArmature|Idle_Neutral",
      "CharacterArmature|Walk",
      "CharacterArmature|Run"
    };
    CharacterAnimator.ResolveClip(noAir, CharacterAnimKind.Jump)
      .ShouldBe("CharacterArmature|Idle_Neutral");
  }

  [Test]
  public void WolfClips_ResolveIdleWalkGallopAttack()
  {
    CharacterAnimator.ResolveClip(WolfClips, CharacterAnimKind.Idle)
      .ShouldBe("Idle");
    CharacterAnimator.ResolveClip(WolfClips, CharacterAnimKind.Walk)
      .ShouldBe("Walk");
    CharacterAnimator.ResolveClip(WolfClips, CharacterAnimKind.Run)
      .ShouldBe("Gallop");
    CharacterAnimator.ResolveClip(WolfClips, CharacterAnimKind.Attack)
      .ShouldBe("Attack");
  }

  [Test]
  public void PigClips_WalkFallsBackToIdle_RunUsesJump()
  {
    CharacterAnimator.ResolveClip(PigClips, CharacterAnimKind.Idle)
      .ShouldBe("Armature|Idle");
    CharacterAnimator.ResolveClip(PigClips, CharacterAnimKind.Walk)
      .ShouldBe("Armature|Idle");
    CharacterAnimator.ResolveClip(PigClips, CharacterAnimKind.Run)
      .ShouldBe("Armature|Jump");
    CharacterAnimator.ResolveClip(PigClips, CharacterAnimKind.Attack)
      .ShouldBe("Armature|Idle");
  }

  [Test]
  public void CrabAndShark_ResolveAvailableClips()
  {
    CharacterAnimator.ResolveClip(CrabClips, CharacterAnimKind.Idle)
      .ShouldBe("MonsterArmature|Idle");
    CharacterAnimator.ResolveClip(CrabClips, CharacterAnimKind.Walk)
      .ShouldBe("MonsterArmature|Walk");
    CharacterAnimator.ResolveClip(CrabClips, CharacterAnimKind.Attack)
      .ShouldBe("MonsterArmature|Bite_Front");
  }

  [Test]
  public void Crab_FacesPerpendicularToTravel_UnlikeWolf()
  {
    CharacterAnimator.ExtraYawDegreesForEnemy("crab").ShouldBe(90f);
    CharacterAnimator.ExtraYawDegreesForEnemy("wolf").ShouldBe(0f);
    CharacterAnimator.ExtraYawDegreesForEnemy(null).ShouldBe(0f);
    CharacterAnimator.ResolveClip(SharkClips, CharacterAnimKind.Idle)
      .ShouldBe("Armature|Swim");
    CharacterAnimator.ResolveClip(SharkClips, CharacterAnimKind.Walk)
      .ShouldBe("Armature|Swim");
  }

  [Test]
  public void AttackStyleFromItemId_MapsWeapons()
  {
    CharacterAnimator.AttackStyleFromItemId("wooden_bow").ShouldBe("bow");
    CharacterAnimator.AttackStyleFromItemId("wooden_spear").ShouldBe("melee");
    CharacterAnimator.AttackStyleFromItemId("stone_axe").ShouldBe("melee");
    CharacterAnimator.AttackStyleFromItemId("torch").ShouldBe("punch");
    CharacterAnimator.AttackStyleFromItemId(null).ShouldBe("punch");
  }

  [Test]
  public void EmptyClipList_ReturnsNull()
  {
    CharacterAnimator.ResolveClip(System.Array.Empty<string>(), CharacterAnimKind.Idle)
      .ShouldBeNull();
  }

  [Test]
  public async Task DummyPlayer_PlaysIdleThenWalkThenAttack()
  {
    var host = new Node3D { Name = "AnimHost" };
    var ap = new AnimationPlayer { Name = "AnimationPlayer" };
    var lib = new AnimationLibrary();
    lib.AddAnimation("Idle", new Animation { Length = 1f });
    lib.AddAnimation("Walk", new Animation { Length = 1f });
    lib.AddAnimation("Attack", new Animation { Length = 0.4f });
    ap.AddAnimationLibrary("", lib);
    host.AddChild(ap);

    var animator = new CharacterAnimator { Name = "CharacterAnimator" };
    host.AddChild(animator);
    await _fixture.AddToRoot(host, autoRemoveFromRoot: true);

    animator.CurrentClip.ShouldBe("Idle");
    animator.UsesAnimationTree.ShouldBeTrue();

    animator.ApplyLocomotion(3f, running: false);
    animator.CurrentClip.ShouldBe("Walk");

    animator.NotifyAttack();
    animator.CurrentClip.ShouldBe("Attack");

    animator.NotifyHit();
    animator.CurrentClip.ShouldBe("Idle");
  }

  [Test]
  public async Task MissingAnimationPlayer_IsSilentNoOp()
  {
    var host = new Node3D { Name = "EmptyHost" };
    var animator = new CharacterAnimator { Name = "CharacterAnimator" };
    host.AddChild(animator);
    await _fixture.AddToRoot(host, autoRemoveFromRoot: true);

    animator.CurrentClip.ShouldBe("");
    animator.ApplyLocomotion(4f, running: true);
    animator.NotifyAttack();
    animator.CurrentClip.ShouldBe("");
  }
}
