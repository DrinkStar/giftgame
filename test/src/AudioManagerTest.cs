// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

/// <summary>
///   AudioManager tests (Iter6.1 plan Decision 2): every landed audio asset
///   must load as an AudioStream, one-shot playback must not throw and must
///   use the GetNode-based pool players created in _Ready, and unknown ids
///   must degrade to a warning without crashing.
///   Robust to partial downloads: assertions on loaded assets are skipped for
///   any .ogg that did not land (checked via FileAccess.FileExists); the
///   degradation path is asserted unconditionally.
/// </summary>
public class AudioManagerTest : TestClass, IDisposable
{
  private static readonly string[] BgmPaths =
  {
    "res://assets/audio/bgm/island.ogg",
    "res://assets/audio/bgm/night.ogg",
    "res://assets/audio/bgm/boss.ogg"
  };

  private static readonly string[] SfxPaths =
  {
    "res://assets/audio/sfx/ocean_waves.ogg",
    "res://assets/audio/sfx/storm.ogg",
    "res://assets/audio/sfx/melee_swing.ogg",
    "res://assets/audio/sfx/bow_shoot.ogg",
    "res://assets/audio/sfx/enemy_hit.ogg",
    "res://assets/audio/sfx/enemy_die.ogg",
    "res://assets/audio/sfx/chop.ogg",
    "res://assets/audio/sfx/ui_click.ogg"
  };

  private Fixture _fixture = default!;
  private AudioManager _audio = default!;

  public AudioManagerTest(Node testScene) : base(testScene) { }

  [Setup]
  public void Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _audio = new AudioManager { Name = "AudioManager" };

    // Fill the exported dictionaries only with assets that actually landed,
    // so the tests stay green under partial downloads.
    var landedBgm = new Godot.Collections.Dictionary<string, AudioStream>();
    var landedSfx = new Godot.Collections.Dictionary<string, AudioStream>();

    AddLanded(landedBgm, "island", BgmPaths[0]);
    AddLanded(landedBgm, "night", BgmPaths[1]);
    AddLanded(landedBgm, "boss", BgmPaths[2]);

    AddLanded(landedSfx, "ocean_waves", SfxPaths[0]);
    AddLanded(landedSfx, "storm", SfxPaths[1]);
    AddLanded(landedSfx, "melee_swing", SfxPaths[2]);
    AddLanded(landedSfx, "bow_shoot", SfxPaths[3]);
    AddLanded(landedSfx, "enemy_hit", SfxPaths[4]);
    AddLanded(landedSfx, "enemy_die", SfxPaths[5]);
    AddLanded(landedSfx, "chop", SfxPaths[6]);
    AddLanded(landedSfx, "ui_click", SfxPaths[7]);

    _audio.Bgm = landedBgm;
    _audio.Sfx = landedSfx;

    _fixture.AddToRoot(_audio, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_audio == null)
      return;

    _audio.Dispose();
    _audio = null!;
    GC.SuppressFinalize(this);
  }

  private static void AddLanded(
    Godot.Collections.Dictionary<string, AudioStream> dict,
    string id,
    string path)
  {
    if (!FileAccess.FileExists(path))
      return;

    var stream = GD.Load<AudioStream>(path);
    if (stream is not null)
      dict[id] = stream;
  }

  private static bool AnyLanded(IEnumerable<string> paths)
  {
    foreach (var path in paths)
    {
      if (FileAccess.FileExists(path))
        return true;
    }

    return false;
  }

  [Test]
  public void LandedBgmAssets_LoadAsAudioStreams()
  {
    // Only assert on assets that actually landed (partial-download safe).
    // The degradation path is covered separately and unconditionally.
    foreach (var path in BgmPaths)
    {
      if (FileAccess.FileExists(path))
        GD.Load<AudioStream>(path).ShouldNotBeNull(path);
    }
  }

  [Test]
  public void LandedSfxAssets_LoadAsAudioStreams()
  {
    foreach (var path in SfxPaths)
    {
      if (FileAccess.FileExists(path))
        GD.Load<AudioStream>(path).ShouldNotBeNull(path);
    }
  }

  [Test]
  public void PlaySfx_LandedId_DoesNotThrowAndPoolPlayerExists()
  {
    // Pool players are created in _Ready regardless of which assets landed.
    _audio.GetNodeOrNull<AudioStreamPlayer>("SfxPlayer0").ShouldNotBeNull();

    // AnyLanded guarantees one real id to play; ui_click is the canonical one.
    if (!AnyLanded(SfxPaths))
      return;

    var id = FileAccess.FileExists(SfxPaths[7]) ? "ui_click" : FirstLandedSfxId();
    Should.NotThrow(() => _audio.PlaySfx(id));

    // One-shot playback cycles the pool — the player node must exist.
    _audio.GetNodeOrNull<AudioStreamPlayer>("SfxPlayer0").ShouldNotBeNull();
  }

  [Test]
  public void PlayBgm_LandedId_AssignsLoopingStream()
  {
    if (!FileAccess.FileExists(BgmPaths[0]))
      return;

    var expected = GD.Load<AudioStream>(BgmPaths[0]);
    _audio.Bgm["island"] = expected;

    Should.NotThrow(() => _audio.PlayBgm("island"));

    var player = _audio.GetNodeOrNull<AudioStreamPlayer>("BgmPlayer");
    player.ShouldNotBeNull();
    player!.Stream.ShouldBe(expected);

    // Godot 4 keeps the loop flag on the stream; our BGM is Ogg Vorbis.
    var ogg = player.Stream.ShouldBeOfType<AudioStreamOggVorbis>();
    ogg.Loop.ShouldBeTrue();
  }

  [Test]
  public void PlayBgm_MissingId_DoesNotCrash()
  {
    Should.NotThrow(() => _audio.PlayBgm("no_such_bgm"));
    Should.NotThrow(() => _audio.PlayBgm(""));
  }

  [Test]
  public void PlaySfx_MissingId_DoesNotCrash()
  {
    Should.NotThrow(() => _audio.PlaySfx("no_such_sfx"));
    Should.NotThrow(() => _audio.PlaySfx(""));
  }

  private string FirstLandedSfxId()
  {
    if (FileAccess.FileExists(SfxPaths[0]))
      return "ocean_waves";
    if (FileAccess.FileExists(SfxPaths[1]))
      return "storm";
    if (FileAccess.FileExists(SfxPaths[2]))
      return "melee_swing";
    if (FileAccess.FileExists(SfxPaths[3]))
      return "bow_shoot";
    if (FileAccess.FileExists(SfxPaths[4]))
      return "enemy_hit";
    if (FileAccess.FileExists(SfxPaths[5]))
      return "enemy_die";
    if (FileAccess.FileExists(SfxPaths[6]))
      return "chop";

    return "ui_click";
  }
}
