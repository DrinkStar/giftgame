// Ported from 2Retr0/GodotOceanWaves (MIT) — godot-refs/2Retr0-GodotOceanWaves/LICENSE
// Readback semantics adapted from ManickYoj/godot-ocean-waves-buoyancy (MIT) —
// godot-refs/ManickYoj-godot-ocean-waves-buoyancy/LICENSE
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Handles the compute pipeline for wave spectra generation/FFT
///   (1:1 port of wave_generator.gd).
/// </summary>
public partial class WaveGenerator : Node
{
  public const float G = 9.81f;
  public const float Depth = 20.0f;

  public int MapSize { get; set; }

  public RenderingContext? Context { get; private set; }

  /// <summary>Keyed by pipeline name (spectrum_compute, fft_compute, ...).</summary>
  public Dictionary<string, RenderingContext.ComputePipeline> Pipelines { get; } = new();

  /// <summary>
  ///   Keyed by descriptor name (spectrum, butterfly_factors, fft_buffer,
  ///   displacement_map, normal_map).
  /// </summary>
  public Dictionary<string, RenderingContext.Descriptor> Descriptors { get; } = new();

  // Generator state per invocation of `Update()`.
  private WaveCascadeParameters[] _passParameters = Array.Empty<WaveCascadeParameters>();
  private int _passNumCascadesRemaining;

  public void InitGpu(int numCascades)
  {
    // --- DEVICE/SHADER CREATION ---
    Context ??= RenderingContext.Create(RenderingServer.GetRenderingDevice());
    var context = Context!;
    var spectrumComputeShader = context.LoadShader("res://assets/shaders/compute/spectrum_compute.glsl");
    var fftButterflyShader = context.LoadShader("res://assets/shaders/compute/fft_butterfly.glsl");
    var spectrumModulateShader = context.LoadShader("res://assets/shaders/compute/spectrum_modulate.glsl");
    var fftComputeShader = context.LoadShader("res://assets/shaders/compute/fft_compute.glsl");
    var transposeShader = context.LoadShader("res://assets/shaders/compute/transpose.glsl");
    var fftUnpackShader = context.LoadShader("res://assets/shaders/compute/fft_unpack.glsl");

    // --- DESCRIPTOR PREPARATION ---
    var dims = new Vector2I(MapSize, MapSize);
    int numFftStages = (int)(Math.Log(MapSize) / Math.Log(2));

    Descriptors["spectrum"] = context.CreateTexture(
      dims,
      RenderingDevice.DataFormat.R32G32B32A32Sfloat,
      (uint)(RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanCopyFromBit),
      (uint)numCascades
    );
    // Size: (#FFT stages * map size * sizeof(vec4))
    Descriptors["butterfly_factors"] = context.CreateStorageBuffer(numFftStages * MapSize * 4 * 4);
    // Size: (map size^2 * 4 FFTs * 2 temp buffers (for Stockham FFT) * sizeof(vec2))
    Descriptors["fft_buffer"] = context.CreateStorageBuffer(numCascades * MapSize * MapSize * 4 * 2 * 2 * 4);
    // Note: CAN_COPY_FROM is required for the CPU readback in
    // RetrieveDisplacementImage (per the ManickYoj fork).
    Descriptors["displacement_map"] = context.CreateTexture(
      dims,
      RenderingDevice.DataFormat.R16G16B16A16Sfloat,
      (uint)(
        RenderingDevice.TextureUsageBits.StorageBit |
        RenderingDevice.TextureUsageBits.SamplingBit |
        RenderingDevice.TextureUsageBits.CanUpdateBit |
        RenderingDevice.TextureUsageBits.CanCopyFromBit
      ),
      (uint)numCascades
    );
    Descriptors["normal_map"] = context.CreateTexture(
      dims,
      RenderingDevice.DataFormat.R16G16B16A16Sfloat,
      (uint)(
        RenderingDevice.TextureUsageBits.StorageBit |
        RenderingDevice.TextureUsageBits.SamplingBit |
        RenderingDevice.TextureUsageBits.CanUpdateBit
      ),
      (uint)numCascades
    );

    var spectrumSet = context.CreateDescriptorSet(
      new[] { Descriptors["spectrum"] }, spectrumComputeShader, 0);
    var fftButterflySet = context.CreateDescriptorSet(
      new[] { Descriptors["butterfly_factors"] }, fftButterflyShader, 0);
    var fftComputeSet = context.CreateDescriptorSet(
      new[] { Descriptors["butterfly_factors"], Descriptors["fft_buffer"] }, fftComputeShader, 0);
    var fftBufferSet = context.CreateDescriptorSet(
      new[] { Descriptors["fft_buffer"] }, spectrumModulateShader, 1);
    var unpackSet = context.CreateDescriptorSet(
      new[] { Descriptors["displacement_map"], Descriptors["normal_map"] }, fftUnpackShader, 0);

    // --- COMPUTE PIPELINE CREATION ---
    Pipelines["spectrum_compute"] = context.CreatePipeline(
      new uint[] { (uint)(MapSize / 16), (uint)(MapSize / 16), 1 },
      new[] { spectrumSet },
      spectrumComputeShader
    );
    Pipelines["spectrum_modulate"] = context.CreatePipeline(
      new uint[] { (uint)(MapSize / 16), (uint)(MapSize / 16), 1 },
      new[] { spectrumSet, fftBufferSet },
      spectrumModulateShader
    );
    Pipelines["fft_butterfly"] = context.CreatePipeline(
      new uint[] { (uint)(MapSize / 2 / 64), (uint)numFftStages, 1 },
      new[] { fftButterflySet },
      fftButterflyShader
    );
    Pipelines["fft_compute"] = context.CreatePipeline(
      new uint[] { 1, (uint)MapSize, 4 },
      new[] { fftComputeSet },
      fftComputeShader
    );
    Pipelines["transpose"] = context.CreatePipeline(
      new uint[] { (uint)(MapSize / 32), (uint)(MapSize / 32), 4 },
      new[] { fftComputeSet },
      transposeShader
    );
    Pipelines["fft_unpack"] = context.CreatePipeline(
      new uint[] { (uint)(MapSize / 16), (uint)(MapSize / 16), 1 },
      new[] { unpackSet, fftBufferSet },
      fftUnpackShader
    );

    // We only need to generate butterfly factors once for each map_size.
    long computeList = context.ComputeListBegin();
    Pipelines["fft_butterfly"].Dispatch(context, computeList);
    context.ComputeListEnd();
  }

  public override void _Process(double delta)
  {
    // Update one cascade each frame for load balancing.
    if (_passNumCascadesRemaining == 0 || Context == null)
    {
      return;
    }
    _passNumCascadesRemaining -= 1;

    var context = Context!;
    long computeList = context.ComputeListBegin();
    UpdateOne(computeList, _passNumCascadesRemaining, _passParameters);
    context.ComputeListEnd();
  }

  private void UpdateOne(
    long computeList,
    int cascadeIndex,
    WaveCascadeParameters[] parameters
  )
  {
    var parametersForCascade = parameters[cascadeIndex];
    var context = Context!;

    // --- WAVE SPECTRA UPDATE ---
    if (parametersForCascade.ShouldGenerateSpectrum)
    {
      float alpha = JonswapAlpha(
        parametersForCascade.WindSpeed,
        parametersForCascade.FetchLengthKm * 1e3f
      );
      float omega = JonswapPeakAngularFrequency(
        parametersForCascade.WindSpeed,
        parametersForCascade.FetchLengthKm * 1e3f
      );
      Pipelines["spectrum_compute"].Dispatch(
        context,
        computeList,
        RenderingContext.CreatePushConstant(
          parametersForCascade.SpectrumSeed.X,
          parametersForCascade.SpectrumSeed.Y,
          parametersForCascade.TileLength.X,
          parametersForCascade.TileLength.Y,
          alpha,
          omega,
          parametersForCascade.WindSpeed,
          Mathf.DegToRad(parametersForCascade.WindDirectionDegrees),
          Depth,
          parametersForCascade.Swell,
          parametersForCascade.Detail,
          parametersForCascade.Spread,
          cascadeIndex
        )
      );
      parametersForCascade.ShouldGenerateSpectrum = false;
    }

    Pipelines["spectrum_modulate"].Dispatch(
      context,
      computeList,
      RenderingContext.CreatePushConstant(
        parametersForCascade.TileLength.X,
        parametersForCascade.TileLength.Y,
        Depth,
        parametersForCascade.Time,
        cascadeIndex
      )
    );

    // --- WAVE SPECTRA INVERSE FOURIER TRANSFORM ---
    var fftPushConstant = RenderingContext.CreatePushConstant(cascadeIndex);
    // Note: We need not do a second transpose after computing FFT on rows since
    //       rotating the wave by PI/2 doesn't affect it visually.
    Pipelines["fft_compute"].Dispatch(context, computeList, fftPushConstant);
    Pipelines["transpose"].Dispatch(context, computeList, fftPushConstant);
    context.ComputeListAddBarrier(computeList); // FIXME: Why is a barrier only needed here?!
    Pipelines["fft_compute"].Dispatch(context, computeList, fftPushConstant);

    // --- DISPLACEMENT/NORMAL MAP UPDATE ---
    Pipelines["fft_unpack"].Dispatch(
      context,
      computeList,
      RenderingContext.CreatePushConstant(
        cascadeIndex,
        parametersForCascade.Whitecap,
        parametersForCascade.FoamGrowRate,
        parametersForCascade.FoamDecayRate
      )
    );
  }

  /// <summary>
  ///   Begins updating wave cascades based on the provided parameters. To
  ///   balance stutter, the generator will schedule one cascade update per
  ///   frame. All cascades from the previous invocation that have not been
  ///   processed yet will be updated.
  /// </summary>
  public void Update(double delta, WaveCascadeParameters[] parameters)
  {
    System.Diagnostics.Debug.Assert(parameters.Length != 0);

    if (Context == null)
    {
      InitGpu(Math.Max(2, parameters.Length));
    }
    else if (_passNumCascadesRemaining != 0)
    {
      // Update cascades from previous invocation that have yet to be processed...
      var context = Context!;
      long computeList = context.ComputeListBegin();
      for (int i = 0; i < _passNumCascadesRemaining; i++)
      {
        UpdateOne(computeList, i, _passParameters);
      }
      context.ComputeListEnd();
    }

    // Update each cascade's parameters that rely on time delta.
    for (int i = 0; i < parameters.Length; i++)
    {
      var parametersForCascade = parameters[i];
      parametersForCascade.Time += (float)delta;
      // Note: The constants are used to normalize parameters between 0 and 10.
      parametersForCascade.FoamGrowRate = (float)(delta * parametersForCascade.FoamAmount * 7.5);
      parametersForCascade.FoamDecayRate = (float)(
        delta * Mathf.Max(0.5f, 10.0f - parametersForCascade.FoamAmount) * 1.15
      );
    }

    _passParameters = parameters;
    _passNumCascadesRemaining = parameters.Length;
  }

  /// <summary>
  ///   Returns the CPU-side displacement image for a specific cascade. In
  ///   Godot 4.7, <c>TextureGetData(rid, layer)</c> returns ONLY the requested
  ///   layer of a 2D array texture, so no whole-array slicing is needed (the
  ///   ManickYoj fork's slicing code must NOT be ported).
  /// </summary>
  public Image RetrieveDisplacementImage(int cascade)
  {
    var displacement = Descriptors["displacement_map"];
    byte[] data = Context!.Device.TextureGetData(displacement.Rid, (uint)cascade);
    var image = Image.CreateFromData(MapSize, MapSize, false, Image.Format.Rgbah, data);
    image.Convert(Image.Format.Rgbaf); // Convert to a workable format.
    return image;
  }

  public override void _Notification(int what)
  {
    if (what == NotificationPredelete && Context != null)
    {
      Context.Free();
      Context = null;
    }
  }

  // Source: https://wikiwaves.org/Ocean-Wave_Spectra#JONSWAP_Spectrum
  public static float JonswapAlpha(float windSpeed = 20.0f, float fetchLength = 550e3f)
    => 0.076f * Mathf.Pow(windSpeed * windSpeed / (fetchLength * G), 0.22f);

  // Source: https://wikiwaves.org/Ocean-Wave_Spectra#JONSWAP_Spectrum
  public static float JonswapPeakAngularFrequency(
    float windSpeed = 20.0f,
    float fetchLength = 550e3f
  )
    => 22.0f * Mathf.Pow(G * G / (windSpeed * fetchLength), 1.0f / 3.0f);
}
