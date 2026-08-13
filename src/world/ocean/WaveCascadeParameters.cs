// Ported from 2Retr0/GodotOceanWaves (MIT) — godot-refs/2Retr0-GodotOceanWaves/LICENSE
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Parameters describing a single wave cascade. One parameter set is passed
///   to the spectrum compute shader per cascade; defaults match cascade 0 of
///   the 2Retr0 demo (main.tscn:43-56).
/// </summary>
public partial class WaveCascadeParameters : Resource
{
  /// <summary>Denotes the distance the cascade's tile should cover (in meters).</summary>
  [Export]
  public Vector2 TileLength { get; set; } = new(88, 88);

  /// <summary>
  ///   Note: Should be reduced as the number of cascades increases to avoid
  ///   *too* much detail!
  /// </summary>
  [Export(PropertyHint.Range, "0, 2")]
  public float DisplacementScale { get; set; } = 1.0f;

  [Export(PropertyHint.Range, "0, 2")]
  public float NormalScale { get; set; } = 1.0f;

  /// <summary>
  ///   Average wind speed above the water (m/s). Increasing makes waves
  ///   steeper and more 'chaotic'.
  /// </summary>
  [Export]
  public float WindSpeed { get; set; } = 10.0f;

  [Export(PropertyHint.Range, "-360, 360")]
  public float WindDirectionDegrees { get; set; } = 20.0f;

  /// <summary>
  ///   Distance from shoreline (km). Increasing makes waves steeper, but
  ///   reduces their 'choppiness'.
  /// </summary>
  [Export]
  public float FetchLengthKm { get; set; } = 150.0f;

  [Export(PropertyHint.Range, "0, 2")]
  public float Swell { get; set; } = 0.8f;

  /// <summary>Modifies how much wind and swell affect the direction of the waves.</summary>
  [Export(PropertyHint.Range, "0, 1")]
  public float Spread { get; set; } = 0.2f;

  /// <summary>Modifies how steep a wave needs to be before foam can accumulate.</summary>
  [Export(PropertyHint.Range, "0, 2")]
  public float Whitecap { get; set; } = 0.5f;

  /// <summary>Foam amount; larger values give 'wispier' foam.</summary>
  [Export(PropertyHint.Range, "0, 10")]
  public float FoamAmount { get; set; } = 8.0f;

  /// <summary>
  ///   Modifies the attenuation of high frequency waves. REQUIRED — this is
  ///   the 7th float of the spectrum compute push constant
  ///   (spectrum_compute.glsl:18-30); omitting it silently corrupts the layout.
  /// </summary>
  [Export(PropertyHint.Range, "0, 1")]
  public float Detail { get; set; } = 1.0f;

  // --- Runtime state (not exported; mutated by WaveGenerator) ---

  public Vector2I SpectrumSeed { get; set; }

  public bool ShouldGenerateSpectrum { get; set; } = true;

  public float Time { get; set; }

  public float FoamGrowRate { get; set; }

  public float FoamDecayRate { get; set; }
}
