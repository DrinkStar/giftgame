// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System.Collections.Generic;
using Godot;

/// <summary>
///   T8.5.8: the storage box buildable — a real 20-slot inventory on the
///   object instance of scenes/building/buildables/storage_box.tscn. The
///   scene root is a plain visual Node3D; BuildableInstance instantiates it
///   as its ObjectInstance child with the generated collider on the parent,
///   so PlayerInteraction's FindInteractable reaches this script through the
///   BuildableInstance.ObjectInstance check (the same path BedInteract and
///   FarmPlot use).
///
///   Contents live in a lightweight <see cref="StorageInventory"/> —
///   deliberately NOT a full <see cref="InventorySystem"/>, which binds to a
///   scene node and the hotbar/grid UI. A successful mutation only raises
///   <see cref="GameEvents.RaiseInventoryChanged"/> so the open StorageUI
///   refreshes.
///
///   Interact opens the singleton StorageUI (found under the current scene)
///   targeting this box plus the player's InventorySystem; save/load state
///   is keyed by world position via <see cref="StorageBoxSaveData"/>, the
///   FarmSaveData pattern. Group registration ("storage_boxes") lets
///   SaveService collect dynamically placed boxes.
/// </summary>
public partial class StorageBox : Node3D, IInteractable
{
  /// <summary>Fixed number of chest slots (mirrors the UI grid).</summary>
  public const int SlotCount = 20;

  /// <summary>Owns the actual item state; exposed so the UI and save read it.</summary>
  public StorageInventory Inventory { get; } = new();

  public override void _Ready()
  {
    // T8.5.8: group registration lets SaveService collect the (dynamically
    // placed) boxes for the unified save — same pattern as FarmPlot.
    AddToGroup("storage_boxes");
  }

  public string GetInteractionPrompt() => "[E] 打开储物箱";

  public bool CanInteract() => true;

  public bool RequiresHold() => false;

  /// <summary>
  ///   Opens the singleton <see cref="StorageUI"/> under the current scene,
  ///   targeting this box and the player's inventory. Fail-closed: without a
  ///   StorageUI (not yet wired into the scene) nothing happens — the prompt
  ///   stays and the box keeps working once the UI exists.
  /// </summary>
  public virtual void Interact(PlayerController player)
  {
    var ui = FindStorageUi();
    if (ui == null)
    {
      GD.PushWarning("StorageBox: no StorageUI under the current scene; cannot open.");
      return;
    }

    ui.Open(this, player.GetNodeOrNull<InventorySystem>("InventorySystem"));
  }

  /// <summary>
  ///   Called by <see cref="StorageUI.Close"/> after the panel hides. Default
  ///   is a no-op so player-built boxes without a lid stay quiet.
  /// </summary>
  public virtual void OnStorageClosed()
  {
  }

  /// <summary>Snapshots the box contents, keyed by world position (T8.5.8).</summary>
  public StorageBoxSaveData GetSaveState()
  {
    var state = new StorageBoxSaveData
    {
      PositionX = GlobalPosition.X,
      PositionZ = GlobalPosition.Z
    };

    for (var i = 0; i < SlotCount; i++)
    {
      var slot = Inventory.GetSlot(i);
      state.Slots.Add(
        new SlotSaveData { ItemId = slot.Item?.Id, Amount = slot.Item == null ? 0 : slot.Amount }
      );
    }

    return state;
  }

  /// <summary>
  ///   Restores box contents after a load. Slot positions are restored
  ///   verbatim (no per-slot events); an unknown item id leaves the slot
  ///   empty.
  /// </summary>
  public void ApplySaveState(StorageBoxSaveData state)
  {
    Inventory.Clear();

    for (var i = 0; i < state.Slots.Count && i < SlotCount; i++)
    {
      var saved = state.Slots[i];
      if (string.IsNullOrEmpty(saved.ItemId) || saved.Amount <= 0)
        continue;

      var item = GD.Load<ItemData>($"res://assets/items/{saved.ItemId}.tres");
      if (item == null)
        continue;

      var slot = Inventory.GetSlot(i);
      slot.Item = item;
      slot.Amount = saved.Amount;
    }
  }

  /// <summary>
  ///   Singleton-style lookup: the first StorageUI anywhere under the current
  ///   scene (breadth-first walk), so Game.tscn wiring may place it at the
  ///   top level like CraftUI without this node knowing the exact path.
  /// </summary>
  protected StorageUI? FindStorageUi()
  {
    var tree = GetTree();
    if (tree?.CurrentScene == null)
      return null;

    var queue = new Queue<Node>();
    queue.Enqueue(tree.CurrentScene);

    while (queue.Count > 0)
    {
      var node = queue.Dequeue();
      if (node is StorageUI ui)
        return ui;

      foreach (var child in node.GetChildren())
        queue.Enqueue(child);
    }

    return null;
  }
}

/// <summary>
///   T8.5.8: lightweight fixed-size item container for a storage box.
///   Standalone on purpose (not a Node, no scene wiring): stacking and
///   removal mirror <see cref="InventorySystem.AddItem"/> /
///   <see cref="InventorySystem.RemoveItem"/> so transfers behave identically
///   on both sides of the UI, but a successful mutation only raises
///   <see cref="GameEvents.RaiseInventoryChanged"/> — the open StorageUI
///   refreshes on it.
/// </summary>
public sealed class StorageInventory
{
  private readonly InventorySlot[] _slots;

  /// <summary>Creates the container with <paramref name="slotCount"/> empty slots.</summary>
  public StorageInventory(int slotCount = StorageBox.SlotCount)
  {
    _slots = new InventorySlot[slotCount];
    for (var i = 0; i < _slots.Length; i++)
      _slots[i] = new InventorySlot();
  }

  /// <summary>Number of slots in the container.</summary>
  public int SlotCount => _slots.Length;

  /// <summary>True when every slot is empty.</summary>
  public bool IsEmpty
  {
    get
    {
      foreach (var slot in _slots)
      {
        if (!slot.IsEmpty)
          return false;
      }

      return true;
    }
  }

  /// <summary>Direct slot view (index in [0, SlotCount)).</summary>
  public InventorySlot GetSlot(int index) => _slots[index];

  /// <summary>
  ///   Adds an amount of an item, stacking into existing stacks first and
  ///   then into empty slots (InventorySystem order). Returns the number of
  ///   items that could NOT be placed (0 = all placed). Raises
  ///   <see cref="GameEvents.RaiseInventoryChanged"/> only when something was
  ///   actually placed.
  /// </summary>
  public int AddItem(ItemData item, int amount = 1)
  {
    if (item == null || amount <= 0)
      return amount;

    var remaining = TryStack(item, amount);
    if (remaining > 0)
      remaining = TryAddToEmpty(item, remaining);

    if (remaining < amount)
      GameEvents.RaiseInventoryChanged();

    return remaining;
  }

  /// <summary>
  ///   Removes up to <paramref name="amount"/> of <paramref name="itemId"/>
  ///   from any slots. Returns true only when the full amount was removed.
  ///   Raises <see cref="GameEvents.RaiseInventoryChanged"/> only when
  ///   something was actually removed.
  /// </summary>
  public bool RemoveItem(string itemId, int amount = 1)
  {
    if (string.IsNullOrEmpty(itemId) || amount <= 0)
      return false;

    var remaining = amount;
    foreach (var slot in _slots)
    {
      if (slot.Item?.Id != itemId)
        continue;

      var toRemove = Mathf.Min(slot.Amount, remaining);
      slot.Amount -= toRemove;
      remaining -= toRemove;

      if (slot.Amount == 0)
        slot.Item = null;

      if (remaining == 0)
        break;
    }

    if (remaining < amount)
      GameEvents.RaiseInventoryChanged();

    return remaining == 0;
  }

  /// <summary>Empties every slot silently (used by save restore).</summary>
  public void Clear()
  {
    foreach (var slot in _slots)
    {
      slot.Item = null;
      slot.Amount = 0;
    }
  }

  private int TryStack(ItemData item, int amount)
  {
    foreach (var slot in _slots)
    {
      if (slot.Item?.Id != item.Id || slot.Amount >= item.MaxStack)
        continue;

      var toAdd = Mathf.Min(item.MaxStack - slot.Amount, amount);
      slot.Amount += toAdd;
      amount -= toAdd;
      if (amount == 0)
        return 0;
    }

    return amount;
  }

  private int TryAddToEmpty(ItemData item, int amount)
  {
    foreach (var slot in _slots)
    {
      if (!slot.IsEmpty)
        continue;

      var toAdd = Mathf.Min(item.MaxStack, amount);
      slot.Item = item;
      slot.Amount = toAdd;
      amount -= toAdd;
      if (amount == 0)
        return 0;
    }

    return amount;
  }
}
