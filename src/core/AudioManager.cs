// Original (Iter6.1) — no upstream port
namespace SeaAnomaly;

using Godot;
using Godot.Collections;

/// <summary>
///   Global-ish audio playback node (Iter6.1 plan Decision 2 — "autoload 候选";
///   intentionally NOT registered as an autoload yet, so GameManager/Game.tscn
///   can instantiate it as a plain child node later).
///   - <see cref="PlayBgm"/>: switches the single BGM player to the stream and
///     loops it (immediate switch, no cross-fade — keep v1 simple).
///   - <see cref="PlaySfx"/>: one-shot playback through a small pool of
///     AudioStreamPlayer children, round-robin.
///   - Safe degradation: missing/unknown ids only push a warning, never crash.
///   All streams live in [Export] dictionaries so the editor (or a later
///   scene wiring task) can fill them in assets/audio/.
/// </summary>
public partial class AudioManager : Node
{
  /// <summary>BGM streams keyed by id (e.g. "island", "night", "boss").</summary>
  [Export]
  public Dictionary<string, AudioStream> Bgm { get; set; } = new();

  /// <summary>One-shot SFX streams keyed by id (e.g. "melee_swing", "ui_click").</summary>
  [Export]
  public Dictionary<string, AudioStream> Sfx { get; set; } = new();

  /// <summary>Number of pooled one-shot players for SFX (round-robin).</summary>
  [Export] public int SfxPoolSize = 8;

  /// <summary>
  ///   When true, <see cref="AutoBgmId"/> starts looping on <c>_Ready</c>
  ///   (Game.tscn leaves this true so the island ambient track plays on load,
  ///   Iter6.1 todo 6).
  /// </summary>
  [Export] public bool AutoPlayBgm = true;

  /// <summary>BGM id started by <see cref="AutoPlayBgm"/> (default "island").</summary>
  [Export] public string AutoBgmId = "island";

  private const string BgmPlayerName = "BgmPlayer";
  private const string SfxPlayerPrefix = "SfxPlayer";

  private int _nextSfxPlayer;

  public override void _Ready()
  {
    // BGM: one dedicated looping player.
    if (GetNodeOrNull<AudioStreamPlayer>(BgmPlayerName) is null)
    {
      AddChild(new AudioStreamPlayer { Name = BgmPlayerName });
    }

    // SFX: a pool of one-shot players so rapid hits don't cut each other off.
    // FIX(code-review P2-19): clamp the pool size so SfxPoolSize = 0 can
    // never divide by zero in PlaySfx — a zero/negative export behaves like 1.
    var poolSize = Mathf.Max(1, SfxPoolSize);
    SfxPoolSize = poolSize;
    for (var i = 0; i < poolSize; i++)
    {
      var name = $"{SfxPlayerPrefix}{i}";
      if (GetNodeOrNull<AudioStreamPlayer>(name) is null)
      {
        AddChild(new AudioStreamPlayer { Name = name });
      }
    }

    _nextSfxPlayer = 0;

    // T9.6: looping ocean ambience at low volume when the stream is
    // registered — the sea is always present once the ocean scene is merged.
    if (Sfx is { } sfx && sfx.TryGetValue("ocean_waves", out var ambient) && ambient != null)
    {
      var ambientPlayer = GetNodeOrNull<AudioStreamPlayer>("AmbientPlayer")
        ?? new AudioStreamPlayer { Name = "AmbientPlayer", VolumeDb = -14f };
      if (ambientPlayer.GetParent() == null)
      {
        AddChild(ambientPlayer);
      }

      SetLoop(ambient, loop: true);
      ambientPlayer.Stream = ambient;
      ambientPlayer.Play();
    }

    // Scene-level auto-start (Game.tscn: island BGM). Headless runs (CI
    // import/smoke/tests) have no audio device; playing there can leave the
    // playback chain referenced past ObjectDB cleanup in Godot 4.7.1
    // headless (flaky leaked-instance warnings at exit). Headless audio is
    // still exercised directly by AudioManagerTest via PlayBgm/PlaySfx.
    // Unknown ids degrade to a warning via PlayBgm, so an unwired scene
    // never crashes on ready.
    if (AutoPlayBgm && DisplayServer.GetName() != "headless")
      PlayBgm(AutoBgmId);
  }

  /// <summary>
  ///   Stops every player when the node leaves the tree or is freed so a
  ///   playing BGM cannot outlive its node: covers a mid-game scene unload
  ///   (EXIT_TREE) and the `--quit-after` force-quit path, which only
  ///   delivers Predelete.
  /// </summary>
  public override void _Notification(int what)
  {
    if (what == NotificationPredelete || what == NotificationExitTree || what == NotificationWMCloseRequest)
      StopAll();
  }

  /// <summary>Stops the BGM player, the ambient player and the whole SFX pool.</summary>
  public void StopAll()
  {
    GetNodeOrNull<AudioStreamPlayer>(BgmPlayerName)?.Stop();
    GetNodeOrNull<AudioStreamPlayer>("AmbientPlayer")?.Stop();
    for (var i = 0; i < SfxPoolSize; i++)
      GetNodeOrNull<AudioStreamPlayer>($"{SfxPlayerPrefix}{i}")?.Stop();
  }

  /// <summary>
  ///   Switches the BGM player to <paramref name="id"/> and starts looping.
  ///   Unknown ids (or a null stream) only push a warning. FIX(code-review
  ///   P2-20): re-requesting the ALREADY-PLAYING track is a no-op — the
  ///   island/day/night BGM chain re-arms the same id on every scene reload,
  ///   and unconditionally restarting cut the track (and its position) for
  ///   nothing.
  /// </summary>
  public void PlayBgm(string id)
  {
    if (Bgm is null || !Bgm.TryGetValue(id, out var stream) || stream is null)
    {
      GD.PushWarning($"AudioManager.PlayBgm: unknown or unset BGM id '{id}'.");
      return;
    }

    var player = GetNodeOrNull<AudioStreamPlayer>(BgmPlayerName);
    if (player == null)
    {
      GD.PushWarning("AudioManager.PlayBgm: BGM player missing; skipped.");
      return;
    }

    if (player.Stream == stream && player.Playing)
      return;

    SetLoop(stream, loop: true);
    player.Stream = stream;
    player.Play();
  }

  /// <summary>
  ///   Godot 4 keeps the loop flag on the stream (not the player), so set it
  ///   per concrete stream type. Unknown stream types simply stay non-looping.
  /// </summary>
  private static void SetLoop(AudioStream stream, bool loop)
  {
    switch (stream)
    {
      case AudioStreamOggVorbis ogg:
        ogg.Loop = loop;
        break;
      case AudioStreamMP3 mp3:
        mp3.Loop = loop;
        break;
      case AudioStreamWav wav:
        wav.LoopMode = loop
          ? AudioStreamWav.LoopModeEnum.Forward
          : AudioStreamWav.LoopModeEnum.Disabled;
        break;
    }
  }

  /// <summary>
  ///   Plays a one-shot SFX through the next free pool player.
  ///   Unknown ids (or a null stream) only push a warning.
  /// </summary>
  public void PlaySfx(string id)
  {
    if (Sfx is null || !Sfx.TryGetValue(id, out var stream) || stream is null)
    {
      GD.PushWarning($"AudioManager.PlaySfx: unknown or unset SFX id '{id}'.");
      return;
    }

    var player = GetNodeOrNull<AudioStreamPlayer>($"{SfxPlayerPrefix}{_nextSfxPlayer}");
    _nextSfxPlayer = (_nextSfxPlayer + 1) % SfxPoolSize;
    if (player == null)
    {
      GD.PushWarning("AudioManager.PlaySfx: pool player missing; skipped.");
      return;
    }

    player.Stream = stream;
    player.Play();
  }
}
