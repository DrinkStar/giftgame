// Original (Iter5) — no upstream port
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   A farm plot: the object instance of the "farmland" buildable (Iter5 plan
///   Decisions 1/2/8). Plant by holding a seed and pressing E, wait the real
///   growth time, then harvest for produce plus one seed back (self-sustaining
///   loop). The plot is a StaticBody3D on collision layer 5 (Buildings) so the
///   player's interaction ray (mask 29) can hit it, and implements
///   <see cref="IInteractable"/> so <see cref="PlayerInteraction"/> drives it
///   through the standard E pipeline — including the Decision 8 reachability
///   fix that checks a BuildableInstance's ObjectInstance.
/// </summary>
public partial class FarmPlot : StaticBody3D, IInteractable
{
  /// <summary>Soil brown, matches the shipped farm_plot.tscn material (#8B6914).</summary>
  private static readonly Color EmptyColor = new(0.545098f, 0.411765f, 0.0784314f);

  /// <summary>Green tint while the crop is growing.</summary>
  private static readonly Color GrowingColor = new(0.24f, 0.55f, 0.24f);

  /// <summary>Golden tint when the crop is ready to harvest.</summary>
  private static readonly Color ReadyColor = new(0.854902f, 0.647059f, 0.12549f);

  /// <summary>
  ///   Optional crop override. When empty (default), the crop is inferred at
  ///   plant time from the seed the player is holding
  ///   (<see cref="FarmingData.GetBySeedId"/>).
  /// </summary>
  [Export]
  public string CropId = "";

  private CropData? _crop;
  private float _elapsed;
  private bool _ready;
  private readonly Random _rng = new();
  private MeshInstance3D? _visual;
  private StandardMaterial3D? _visualMaterial;

  /// <summary>Whether a crop is currently planted (any state).</summary>
  public bool HasCrop => _crop != null;

  /// <summary>Whether the planted crop is ready to harvest.</summary>
  public bool IsReady => _ready;

  public override void _Ready()
  {
    // FIX(iter8-plan): T8.2 — group registration lets SaveService collect the
    // (dynamically placed) plots for the unified save.
    AddToGroup("farm_plots");

    // Cache the "Visual" child and a per-instance copy of its material so
    // state tinting never mutates the shared sub-resource of the scene.
    _visual = GetNodeOrNull<MeshInstance3D>("Visual");
    if (_visual != null)
    {
      _visualMaterial =
        _visual.GetActiveMaterial(0) is StandardMaterial3D existing
          ? existing.Duplicate() is StandardMaterial3D copy ? copy : new StandardMaterial3D()
          : new StandardMaterial3D();
      _visual.MaterialOverride = _visualMaterial;
    }

    ApplyVisualState();
  }

  /// <summary>
  ///   FIX(iter8-plan): T8.2 — snapshots the plot state (crop id + growth
  ///   progress) for the unified save, keyed by world position.
  /// </summary>
  public FarmSaveData GetSaveState() =>
    new()
    {
      PositionX = GlobalPosition.X,
      PositionZ = GlobalPosition.Z,
      CropId = _crop?.Id ?? "",
      ElapsedSeconds = _elapsed,
      Ready = _ready
    };

  /// <summary>
  ///   FIX(iter8-plan): T8.2 — restores plot state after a load. Writes the
  ///   fields directly (no CropPlanted/CropHarvested events) and refreshes the
  ///   visual; an unknown crop id leaves the plot empty.
  /// </summary>
  public void ApplySaveState(FarmSaveData state)
  {
    _crop = string.IsNullOrEmpty(state.CropId) ? null : FarmingData.Get(state.CropId);
    _elapsed = state.ElapsedSeconds;
    _ready = state.Ready;
    ApplyVisualState();
  }

  public override void _Process(double delta)
  {
    if (_crop == null || _ready)
      return;

    // Decision 1: real wall-clock growth, no day/night scaling.
    _elapsed += (float)delta;
    if (CropLogic.IsReady(_elapsed, _crop.GrowthSeconds))
    {
      _ready = true;
      ApplyVisualState();
    }
  }

  public string GetInteractionPrompt()
  {
    if (_crop == null)
      return "[E] Plant (hold a seed)";

    if (!_ready)
    {
      var percent = (int)Math.Floor(
        CropLogic.Progress(_elapsed, _crop.GrowthSeconds) * 100f
      );
      return $"[E] Growing ({percent}%)";
    }

    return $"[E] Harvest {_crop.DisplayName}";
  }

  /// <summary>
  ///   Decision 2: no player context is available to this contract method, so
  ///   it is state-based and simply returns true — the prompt must stay
  ///   visible in every state (empty/growing/ready) and the real validation
  ///   happens inside <see cref="Interact"/> (held-item check, no-op when the
  ///   player is not holding a valid seed).
  /// </summary>
  public bool CanInteract() => true;

  public bool RequiresHold() => false;

  public void Interact(PlayerController player)
  {
    var inventory = player.GetNodeOrNull<InventorySystem>("InventorySystem");
    if (inventory == null)
      return;

    if (_crop == null)
    {
      Plant(inventory);
    }
    else if (_ready && _crop is { } crop)
    {
      Harvest(inventory, crop);
    }

    // Growing: no-op; the prompt already reports the percent.
  }

  /// <summary>
  ///   Decision 2: the crop is resolved from the held seed unless an explicit
  ///   <see cref="CropId"/> override is set. Consumes exactly one seed.
  /// </summary>
  private void Plant(InventorySystem inventory)
  {
    var selected = inventory.SelectedItem;
    if (selected == null)
      return;

    var crop = !string.IsNullOrEmpty(CropId)
      ? FarmingData.Get(CropId)
      : FarmingData.GetBySeedId(selected.Id);
    if (crop == null)
      return;

    if (!inventory.RemoveItem(crop.SeedItemId, 1))
      return;

    _crop = crop;
    _elapsed = 0f;
    _ready = false;
    ApplyVisualState();
    GameEvents.RaiseCropPlanted(crop.Id);
  }

  /// <summary>
  ///   Decision 1: grants a randomized yield plus one seed back, but only when
  ///   the FULL yield fits the inventory. AddItem places what it can and
  ///   returns the remainder; on any remainder we roll the placed portion
  ///   back out and refuse the harvest (crop stays ready, no seed granted) so
  ///   nothing is silently lost or half-granted.
  /// </summary>
  private void Harvest(InventorySystem inventory, CropData crop)
  {
    var produce = GD.Load<ItemData>($"res://assets/items/{crop.ProduceItemId}.tres");
    if (produce == null)
      return;

    var count = CropLogic.HarvestYield(crop.MinYield, crop.MaxYield, _rng);
    var remaining = inventory.AddItem(produce, count);
    if (remaining > 0)
    {
      // Roll back the portion AddItem actually placed so the refusal is
      // atomic: the plot keeps the crop and the inventory keeps its
      // pre-harvest contents.
      inventory.RemoveItem(crop.ProduceItemId, count - remaining);
      return;
    }

    var seed = GD.Load<ItemData>($"res://assets/items/{crop.SeedItemId}.tres");
    if (seed != null)
    {
      // FIX(iter5-plan): seed add atomicity — no silent loss
      var seedRemaining = inventory.AddItem(seed, CropLogic.SeedReturn);
      if (seedRemaining > 0)
      {
        // The seed did not fit: roll the produce back out and keep the crop
        // ready so the player can harvest again with space.
        inventory.RemoveItem(crop.ProduceItemId, count);
        return;
      }
    }

    GameEvents.RaiseCropHarvested(crop.Id);
    _crop = null;
    _ready = false;
    ApplyVisualState();
  }

  private void ApplyVisualState()
  {
    if (_visualMaterial == null)
      return;

    _visualMaterial.AlbedoColor = _crop == null
      ? EmptyColor
      : _ready
        ? ReadyColor
        : GrowingColor;
  }
}
