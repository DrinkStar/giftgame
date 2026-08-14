// Ported from 2Retr0/GodotOceanWaves (MIT) — godot-refs/2Retr0-GodotOceanWaves/LICENSE
// GetWaveHeight readback adapted from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   Handles updating the displacement/normal maps for the water material as
///   well as managing wave generation pipelines (port of water.gd; the foam
///   particle emitter logic at water.gd:7,110 is intentionally not ported —
///   that material is out of scope for this iteration).
/// </summary>
public partial class WaterMesh : MeshInstance3D
{
  private const string WaterMaterialPath = "res://assets/water/mat_water.tres";

  /// <summary>How many times the wave simulation should update per second.</summary>
  [Export(PropertyHint.Range, "0, 60")]
  public float UpdatesPerSecond { get; set; } = 50.0f;

  /// <summary>How many times per second the displacement map is read back to CPU.</summary>
  [Export(PropertyHint.Range, "1, 60")]
  public int DisplacementReadbackPerSecond { get; set; } = 10;

  [Export]
  public int MapSize { get; set; } = 1024;

  [Export]
  public Color WaterColor { get; set; } = new(0.1f, 0.15f, 0.18f);

  [Export]
  public Color FoamColor { get; set; } = new(0.73f, 0.67f, 0.62f);

  public WaveCascadeParameters[] Parameters { get; private set; } = System.Array.Empty<WaveCascadeParameters>();

  private WaveGenerator? _waveGenerator;
  private readonly RandomNumberGenerator _rng = new();
  private double _time;
  private double _nextUpdateTime;

  /// <summary>
  ///   FIX(iter8.5): false when the RenderingDevice is unavailable (headless
  ///   runs, CI, tests) and the ocean GPU pipeline could not start. The wave
  ///   simulation and readback are skipped; GetWaveHeight returns 0 so
  ///   buoyancy keeps working on a flat sea.
  /// </summary>
  private bool _gpuEnabled;

  private readonly Texture2DArrayRD _displacementMaps = new();
  private readonly Texture2DArrayRD _normalMaps = new();

  private Vector4[] _mapScales = System.Array.Empty<Vector4>();

  // CPU-side cache of the cascade-0 displacement image (updated at the
  // readback rate) used by GetWaveHeight.
  private Image? _cachedDisplacementImage;
  private int _imgWidth;
  private int _imgHeight;
  private double _readbackAccumulator;
  private double _readbackUpdateRate;

  private ShaderMaterial? _waterMaterial;

  public override void _Ready()
  {
    _rng.Seed = 1234; // This seed gives big waves!

    // Cascade parameters from main.tscn:43-83 (2Retr0 demo).
    var cascade0 = new WaveCascadeParameters
    {
      TileLength = new Vector2(88, 88),
      DisplacementScale = 1.0f,
      NormalScale = 1.0f,
      WindSpeed = 10.0f,
      WindDirectionDegrees = 20.0f,
      FetchLengthKm = 150.0f,
      Swell = 0.8f,
      Spread = 0.2f,
      Whitecap = 0.5f,
      FoamAmount = 8.0f,
      Detail = 1.0f,
    };
    var cascade1 = new WaveCascadeParameters
    {
      TileLength = new Vector2(57, 57),
      DisplacementScale = 0.75f,
      NormalScale = 1.0f,
      WindSpeed = 5.0f,
      WindDirectionDegrees = 15.0f,
      FetchLengthKm = 150.0f,
      Swell = 0.8f,
      Spread = 0.4f,
      Whitecap = 0.0f,
      FoamAmount = 0.0f,
      Detail = 1.0f,
    };
    var cascade2 = new WaveCascadeParameters
    {
      TileLength = new Vector2(16, 16),
      DisplacementScale = 0.0f,
      NormalScale = 0.25f,
      WindSpeed = 20.0f,
      WindDirectionDegrees = 20.0f,
      FetchLengthKm = 550.0f,
      Swell = 0.8f,
      Spread = 0.4f,
      Whitecap = 0.25f,
      FoamAmount = 3.0f,
      Detail = 1.0f,
    };
    Parameters = new[] { cascade0, cascade1, cascade2 };
    for (int i = 0; i < Parameters.Length; i++)
    {
      Parameters[i].SpectrumSeed = new Vector2I(
        _rng.RandiRange(-10000, 10000),
        _rng.RandiRange(-10000, 10000)
      );
      // We make sure to choose a time offset such that cascades don't interfere!
      Parameters[i].Time = 120.0f + Mathf.Pi * i;
    }

    _waterMaterial = GD.Load<ShaderMaterial>(WaterMaterialPath);

    RenderingServer.GlobalShaderParameterSet("water_color", WaterColor.SrgbToLinear());
    RenderingServer.GlobalShaderParameterSet("foam_color", FoamColor.SrgbToLinear());

    // FIX(iter8.5): headless runs have no RenderingDevice — the FFT pipeline
    // cannot start and must not take the whole scene down (GameTest loads
    // Game.tscn under GoDotTest). Degrade to a flat, no-simulation sea.
    _gpuEnabled = true;
    try
    {
      SetupWaveGenerator();
      UpdateScalesUniform();

      _readbackUpdateRate = 1.0 / DisplacementReadbackPerSecond;
      _cachedDisplacementImage = _waveGenerator!.RetrieveDisplacementImage(0);
      _imgWidth = _cachedDisplacementImage.GetWidth();
      _imgHeight = _cachedDisplacementImage.GetHeight();
    }
    catch (Exception e)
    {
      GD.PushWarning($"WaterMesh: ocean GPU init failed ({e.Message}); simulation disabled.");
      _gpuEnabled = false;
      if (_waveGenerator != null)
      {
        RemoveChild(_waveGenerator);
        _waveGenerator.QueueFree();
        _waveGenerator = null;
      }
    }
  }

  public override void _Process(double delta)
  {
    // FIX(iter8.5): without the GPU pipeline there is nothing to simulate.
    if (!_gpuEnabled)
      return;

    // Update waves once every 1.0/updates_per_second.
    if (UpdatesPerSecond == 0 || _time >= _nextUpdateTime)
    {
      double targetUpdateDelta = 1.0 / (UpdatesPerSecond + 1e-10);
      double updateDelta = UpdatesPerSecond == 0
        ? delta
        : targetUpdateDelta + (_time - _nextUpdateTime);
      _nextUpdateTime = _time + targetUpdateDelta;
      UpdateWater(updateDelta);
    }
    _time += delta;

    // Resample the displacement image for CPU-side wave height queries.
    _readbackAccumulator += delta;
    if (_readbackAccumulator >= _readbackUpdateRate)
    {
      _readbackAccumulator -= _readbackUpdateRate;
      // TODO: Switch to asynchronous readback — TextureGetData is synchronous
      // and stalls the render thread for one frame per readback.
      _cachedDisplacementImage = _waveGenerator!.RetrieveDisplacementImage(0);
      _imgWidth = _cachedDisplacementImage.GetWidth();
      _imgHeight = _cachedDisplacementImage.GetHeight();
    }
  }

  /// <summary>
  ///   Returns the water surface height (Y) at the given world position, by
  ///   bilinearly sampling the cached cascade-0 displacement image
  ///   (ManickYoj fork water.gd:87-97, 168-196). Returns 0 before the first
  ///   readback.
  /// </summary>
  public float GetWaveHeight(Vector3 worldPosition)
  {
    if (_cachedDisplacementImage == null)
    {
      return 0f;
    }

    // TODO: Sample every cascade for best accuracy (fork water.gd:91).
    var cascade0 = Parameters[0];
    var sampleUv = new Vector2(
      worldPosition.X / cascade0.TileLength.X,
      worldPosition.Z / cascade0.TileLength.Y
    );
    Color sample = SampleDisplacement(sampleUv);
    // The G channel is the vertical displacement; scale by the cascade's
    // displacement scale.
    return sample.G * cascade0.DisplacementScale;
  }

  private Color SampleDisplacement(Vector2 uv)
  {
    // Wrap UVs.
    uv.X = Mathf.Wrap(uv.X, 0.0f, 1.0f);
    uv.Y = Mathf.Wrap(uv.Y, 0.0f, 1.0f);

    // Calculate coordinates.
    float x = uv.X * (_imgWidth - 1);
    float y = uv.Y * (_imgHeight - 1);

    int x0 = Mathf.FloorToInt(x);
    int y0 = Mathf.FloorToInt(y);
    int x1 = Mathf.Min(x0 + 1, _imgWidth - 1);
    int y1 = Mathf.Min(y0 + 1, _imgHeight - 1);

    float fx = x - x0;
    float fy = y - y0;

    // Get cached pixel data.
    Color c00 = _cachedDisplacementImage!.GetPixel(x0, y0);
    Color c10 = _cachedDisplacementImage.GetPixel(x1, y0);
    Color c01 = _cachedDisplacementImage.GetPixel(x0, y1);
    Color c11 = _cachedDisplacementImage.GetPixel(x1, y1);

    // Bilinear interpolation.
    Color colX0 = c00.Lerp(c10, fx);
    Color colX1 = c01.Lerp(c11, fx);
    return colX0.Lerp(colX1, fy);
  }

  private void SetupWaveGenerator()
  {
    if (Parameters.Length <= 0)
    {
      return;
    }
    foreach (var parametersForCascade in Parameters)
    {
      parametersForCascade.ShouldGenerateSpectrum = true;
    }

    _waveGenerator = new WaveGenerator { MapSize = MapSize };
    AddChild(_waveGenerator);
    _waveGenerator.InitGpu(Mathf.Max(2, Parameters.Length));

    // NOTE: These RID assignments mirror water.gd:93-96 — without wrapping the
    // RD textures in Texture2DArrayRD and setting the global shader parameters,
    // the water renders black.
    _displacementMaps.TextureRdRid = default;
    _normalMaps.TextureRdRid = default;
    _displacementMaps.TextureRdRid = _waveGenerator.Descriptors["displacement_map"].Rid;
    _normalMaps.TextureRdRid = _waveGenerator.Descriptors["normal_map"].Rid;

    RenderingServer.GlobalShaderParameterSet("num_cascades", Parameters.Length);
    RenderingServer.GlobalShaderParameterSet("displacements", _displacementMaps);
    RenderingServer.GlobalShaderParameterSet("normals", _normalMaps);
  }

  private void UpdateScalesUniform()
  {
    _mapScales = new Vector4[Parameters.Length];
    for (int i = 0; i < Parameters.Length; i++)
    {
      var parametersForCascade = Parameters[i];
      var uvScale = Vector2.One / parametersForCascade.TileLength;
      _mapScales[i] = new Vector4(
        uvScale.X,
        uvScale.Y,
        parametersForCascade.DisplacementScale,
        parametersForCascade.NormalScale
      );
    }
    // No global shader parameter for arrays :(
    _waterMaterial!.SetShaderParameter("map_scales", _mapScales);
  }

  private void UpdateWater(double delta)
  {
    if (_waveGenerator == null)
    {
      SetupWaveGenerator();
    }
    _waveGenerator!.Update(delta, Parameters);
  }

  public override void _Notification(int what)
  {
    if (what == NotificationPredelete)
    {
      _displacementMaps.TextureRdRid = default;
      _normalMaps.TextureRdRid = default;
    }
  }
}
