// Ported from 2Retr0/GodotOceanWaves (MIT) — godot-refs/2Retr0-GodotOceanWaves/LICENSE
namespace SeaAnomaly;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   A wrapper around <see cref="RenderingDevice"/> that handles basic memory
///   management/allocation for compute shader work (1:1 port of
///   render_context.gd). All created resources are tracked in a deletion queue
///   and freed in reverse allocation order when <see cref="Free"/> is called.
/// </summary>
public sealed class RenderingContext
{
  /// <summary>A tracked rendering resource with its uniform type.</summary>
  public sealed class Descriptor
  {
    public Rid Rid { get; }
    public RenderingDevice.UniformType Type { get; }

    internal Descriptor(Rid rid, RenderingDevice.UniformType type)
    {
      Rid = rid;
      Type = type;
    }
  }

  /// <summary>
  ///   A compute pipeline bound to a set of descriptor sets and block
  ///   dimensions. Calling <see cref="Dispatch"/> records the compute work
  ///   into an active compute list.
  /// </summary>
  public sealed class ComputePipeline
  {
    private readonly Rid _pipelineRid;
    private readonly uint[] _blockDimensions;
    private readonly Rid[] _descriptorSets;

    internal ComputePipeline(Rid pipelineRid, uint[] blockDimensions, Rid[] descriptorSets)
    {
      _pipelineRid = pipelineRid;
      _blockDimensions = blockDimensions;
      _descriptorSets = descriptorSets;
    }

    /// <summary>
    ///   Dispatches the pipeline within the given compute list.
    /// </summary>
    /// <param name="context">The owning rendering context.</param>
    /// <param name="computeList">The active compute list id.</param>
    /// <param name="pushConstant">Optional push constant bytes (16-byte padded).</param>
    /// <param name="descriptorSetOverwrites">Optional descriptor set replacement.</param>
    public void Dispatch(
      RenderingContext context,
      long computeList,
      byte[]? pushConstant = null,
      Rid[]? descriptorSetOverwrites = null
    )
    {
      var device = context.Device;
      var sets = descriptorSetOverwrites ?? _descriptorSets;
      pushConstant ??= Array.Empty<byte>();

      device.ComputeListBindComputePipeline(computeList, _pipelineRid);
      device.ComputeListSetPushConstant(computeList, pushConstant, (uint)pushConstant.Length);
      for (uint i = 0; i < sets.Length; i++)
      {
        device.ComputeListBindUniformSet(computeList, sets[i], i);
      }
      device.ComputeListDispatch(
        computeList,
        _blockDimensions[0],
        _blockDimensions[1],
        _blockDimensions[2]
      );
    }
  }

  /// <summary>Default texture usage (sampling + color attachment + storage + copy).</summary>
  public const uint DefaultTextureUsage = (uint)(
    RenderingDevice.TextureUsageBits.SamplingBit |
    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
    RenderingDevice.TextureUsageBits.StorageBit |
    RenderingDevice.TextureUsageBits.CanCopyToBit |
    RenderingDevice.TextureUsageBits.CanCopyFromBit
  );

  public RenderingDevice Device { get; private set; } = null!;

  private readonly List<Rid> _deletionQueue = new();
  private readonly Dictionary<string, Rid> _shaderCache = new();
  private readonly bool _ownsDevice;

  public bool NeedsSync { get; private set; }

  private RenderingContext(RenderingDevice device, bool ownsDevice)
  {
    Device = device;
    _ownsDevice = ownsDevice;
  }

  /// <summary>
  ///   Creates a rendering context. If no device is given, a local rendering
  ///   device is created (and owned/freed by this context).
  /// </summary>
  public static RenderingContext Create(RenderingDevice? device = null)
  {
    if (device == null)
    {
      device = RenderingServer.CreateLocalRenderingDevice();
      return new RenderingContext(device, ownsDevice: true);
    }
    return new RenderingContext(device, ownsDevice: false);
  }

  // --- WRAPPER FUNCTIONS ---

  public void Submit()
  {
    Device.Submit();
    NeedsSync = true;
  }

  public void Sync()
  {
    Device.Sync();
    NeedsSync = false;
  }

  public long ComputeListBegin() => Device.ComputeListBegin();

  public void ComputeListEnd() => Device.ComputeListEnd();

  public void ComputeListAddBarrier(long computeList) => Device.ComputeListAddBarrier(computeList);

  // --- HELPER FUNCTIONS ---

  /// <summary>Loads (and caches) an RD shader file from a res:// path.</summary>
  public Rid LoadShader(string path)
  {
    if (!_shaderCache.TryGetValue(path, out var rid))
    {
      var shaderFile = GD.Load<RDShaderFile>(path);
      var shaderSpirv = shaderFile.GetSpirV();
      rid = Device.ShaderCreateFromSpirV(shaderSpirv);
      _deletionQueue.Add(rid);
      _shaderCache[path] = rid;
    }
    return rid;
  }

  public Descriptor CreateStorageBuffer(int size, byte[]? data = null)
  {
    data ??= Array.Empty<byte>();
    if (size > data.Length)
    {
      var padded = new byte[size];
      Array.Copy(data, padded, data.Length);
      data = padded;
    }
    var rid = Device.StorageBufferCreate(
      (uint)Math.Max(size, data.Length),
      data,
      (RenderingDevice.StorageBufferUsage)0
    );
    _deletionQueue.Add(rid);
    return new Descriptor(rid, RenderingDevice.UniformType.StorageBuffer);
  }

  public Descriptor CreateUniformBuffer(int size, byte[]? data = null)
  {
    size = Math.Max(16, size);
    data ??= Array.Empty<byte>();
    if (size > data.Length)
    {
      var padded = new byte[size];
      Array.Copy(data, padded, data.Length);
      data = padded;
    }
    var rid = Device.UniformBufferCreate((uint)Math.Max(size, data.Length), data);
    _deletionQueue.Add(rid);
    return new Descriptor(rid, RenderingDevice.UniformType.UniformBuffer);
  }

  /// <summary>
  ///   Creates a 2D (or 2D array, when <paramref name="numLayers"/> &gt; 1) texture.
  /// </summary>
  public Descriptor CreateTexture(
    Vector2I dimensions,
    RenderingDevice.DataFormat format,
    uint usage,
    uint numLayers = 1,
    RDTextureView? view = null,
    byte[]? data = null
  )
  {
    System.Diagnostics.Debug.Assert(numLayers >= 1);
    var textureFormat = new RDTextureFormat
    {
      ArrayLayers = Math.Max(1, numLayers),
      Format = format,
      Width = (uint)dimensions.X,
      Height = (uint)dimensions.Y,
      TextureType = numLayers <= 1
        ? RenderingDevice.TextureType.Type2D
        : RenderingDevice.TextureType.Type2DArray,
      UsageBits = (RenderingDevice.TextureUsageBits)usage,
    };
    // In Godot 4.7 C#, TextureCreate takes one byte[] per array layer; empty
    // initialization is achieved by omitting the data argument entirely.
    var rid = data == null
      ? Device.TextureCreate(textureFormat, view ?? new RDTextureView())
      : Device.TextureCreate(textureFormat, view ?? new RDTextureView(), new Godot.Collections.Array<byte[]>(new[] { data }));
    _deletionQueue.Add(rid);
    return new Descriptor(rid, RenderingDevice.UniformType.Image);
  }

  /// <summary>
  ///   Creates a descriptor set. The ordering of the provided descriptors
  ///   matches the binding ordering within the shader.
  /// </summary>
  public Rid CreateDescriptorSet(Descriptor[] descriptors, Rid shader, uint descriptorSetIndex = 0)
  {
    var uniforms = new Godot.Collections.Array<RDUniform>();
    for (int i = 0; i < descriptors.Length; i++)
    {
      var uniform = new RDUniform
      {
        UniformType = descriptors[i].Type,
        Binding = i, // This matches the binding in the shader.
      };
      uniform.AddId(descriptors[i].Rid);
      uniforms.Add(uniform);
    }
    var rid = Device.UniformSetCreate(uniforms, shader, descriptorSetIndex);
    _deletionQueue.Add(rid);
    return rid;
  }

  /// <summary>
  ///   Creates a compute pipeline which will dispatch (within a compute list)
  ///   based on the provided block dimensions.
  /// </summary>
  public ComputePipeline CreatePipeline(uint[] blockDimensions, Rid[] descriptorSets, Rid shader)
  {
    var pipelineRid = Device.ComputePipelineCreate(shader);
    _deletionQueue.Add(pipelineRid);
    return new ComputePipeline(pipelineRid, blockDimensions, descriptorSets);
  }

  /// <summary>
  ///   Returns a <see cref="byte[]"/> from the provided int/float values.
  ///   Matches encode_s32/encode_float semantics of the GDScript original.
  ///   NOTE: The byte count must EXACTLY match the shader's declared
  ///   push_constant size — Godot 4.7 debug builds strictly validate this
  ///   ("required=N supplied=M") and reject 16-byte-padded buffers, so no
  ///   padding is applied here (spectrum_compute=52, spectrum_modulate=20,
  ///   fft_compute=4, transpose=4, fft_unpack=16).
  /// </summary>
  public static byte[] CreatePushConstant(params object[] values)
  {
    int packedSize = values.Length * 4;
    System.Diagnostics.Debug.Assert(packedSize <= 128, "Push constant size must be at most 128 bytes!");

    var packedData = new byte[packedSize];

    for (int i = 0; i < values.Length; i++)
    {
      switch (values[i])
      {
        case int intValue:
          BitConverter.GetBytes(intValue).CopyTo(packedData, i * 4);
          break;
        case float floatValue:
          BitConverter.GetBytes(floatValue).CopyTo(packedData, i * 4);
          break;
        case double doubleValue:
          BitConverter.GetBytes((float)doubleValue).CopyTo(packedData, i * 4);
          break;
        default:
          throw new ArgumentException($"Unsupported push constant type: {values[i].GetType()}");
      }
    }
    return packedData;
  }

  /// <summary>
  ///   Frees all tracked resources (in reverse allocation order) and, if this
  ///   context owns a local rendering device, frees the device itself.
  /// </summary>
  public void Free()
  {
    for (int i = _deletionQueue.Count - 1; i >= 0; i--)
    {
      var rid = _deletionQueue[i];
      if (rid.IsValid)
      {
        Device.FreeRid(rid);
      }
    }
    _deletionQueue.Clear();
    _shaderCache.Clear();
    if (_ownsDevice)
    {
      Device.Free();
    }
    Device = null!;
  }
}
