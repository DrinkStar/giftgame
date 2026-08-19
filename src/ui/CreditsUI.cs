// Original (Iter9) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T9.x — attribution screen. Sources.md records two license obligations
///   that must reach players: "Music by Eric Matyas / www.soundimage.org"
///   (BGM, soundimage.org custom license) and "Game icons by Delapouite and
///   Lorc — game-icons.net (CC BY 3.0)". Shown as a bottom-of-screen overlay
///   while the game is paused (GamePaused/GameResumed), fail-closed when the
///   events are missing.
/// </summary>
public partial class CreditsUI : CanvasLayer
{
  public const string CreditsText =
    "Music by Eric Matyas / www.soundimage.org\n" +
    "Game icons by Delapouite and Lorc — https://game-icons.net (CC BY 3.0)\n" +
    "Models: Kenney (CC0), Quaternius (CC0) via Poly Pizza\n" +
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
