// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T9.x — attribution screen. Sources.md records license obligations
///   that must reach players: Eric Matyas BGM (soundimage.org), game-icons
///   CC BY (Delapouite / Lorc / Rihlsul), and CC0 model/texture credits
///   (Kenney, Quaternius, Gobkit, Poly Haven). Shown while paused via
///   GamePaused/GameResumed; fail-closed when the events are missing.
/// </summary>
public partial class CreditsUI : CanvasLayer
{
  public const string CreditsText =
    "Music by Eric Matyas / www.soundimage.org\n" +
    "Game icons by Delapouite, Lorc, and Rihlsul — https://game-icons.net (CC BY 3.0)\n" +
    "Models: Kenney (CC0), Quaternius (CC0) via Poly Pizza, Gobkit (CC0)\n" +
    "Terrain textures: Poly Haven sand_01, coast_sand_01, rocks_ground_01, rock_ground_02, snow_02 (CC0)\n" +
    "SurvivalIsland concept (personal non-commercial use)";

  public override void _Ready()
  {
    var label = new Label
    {
      Name = "CreditsLabel",
      Text = CreditsText,
      HorizontalAlignment = HorizontalAlignment.Center,
      AutowrapMode = TextServer.AutowrapMode.Word,
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    label.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
    label.OffsetTop = -110;
    label.OffsetBottom = -10;
    label.AddThemeFontSizeOverride("font_size", 12);
    AddChild(label);

    Visible = false;
    GameEvents.GamePaused += OnPaused;
    GameEvents.GameResumed += OnResumed;
  }

  public override void _ExitTree()
  {
    GameEvents.GamePaused -= OnPaused;
    GameEvents.GameResumed -= OnResumed;
  }

  private void OnPaused() => Visible = true;

  private void OnResumed() => Visible = false;
}
