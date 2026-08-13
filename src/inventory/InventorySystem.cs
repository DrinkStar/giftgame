// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System.Linq;
using Godot;

/// <summary>
///   Grid inventory (10x4) plus a 5-slot hotbar, ported from upstream
///   SurvivalIsland with one behavioral change and the bus swap:
///   - <see cref="AddItem"/> returns the int REMAINDER that could not be
///     placed (upstream returned a bool). The crafting system needs the
///     remainder to refund exactly the unplaced fraction on completion
///     (plan Decision 3 / B1 fix).
///   - All events are published through the static <see cref="GameEvents"/>
///     bus instead of [Signal] delegates. This node only publishes and
///     subscribes to nothing, so no _ExitTree unsubscribe is needed.
///
///   StartingItems follows the plan Decision 10 dictionary contract: each
///   entry is a Dictionary with keys "item" (ItemData) and "amount" (int).
/// </summary>
public partial class InventorySystem : Node
{
  [Export] public int InventoryWidth = 10;
  [Export] public int InventoryHeight = 4;
  [Export] public int HotbarSize = 5;
  [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> StartingItems = new();

  private InventorySlot[,] _inventory = null!;
  private InventorySlot[] _hotbar = null!;
  private int _selectedHotbarSlot;

  /// <summary>
  ///   Selecting a slot clamps to the hotbar range and publishes
  ///   <see cref="GameEvents.HotbarSelectionChanged"/> (Decision 1 bus).
  /// </summary>
  public int SelectedHotbarSlot
  {
    get => _selectedHotbarSlot;
    set
    {
      _selectedHotbarSlot = Mathf.Clamp(value, 0, HotbarSize - 1);
      GameEvents.RaiseHotbarSelectionChanged(_selectedHotbarSlot);
    }
  }

  public ItemData? SelectedItem => _hotbar[_selectedHotbarSlot].Item;

  public override void _Ready()
  {
    _inventory = new InventorySlot[InventoryWidth, InventoryHeight];
    _hotbar = new InventorySlot[HotbarSize];

    for (var x = 0; x < InventoryWidth; x++)
    {
      for (var y = 0; y < InventoryHeight; y++)
        _inventory[x, y] = new InventorySlot();
    }

    for (var i = 0; i < HotbarSize; i++)
      _hotbar[i] = new InventorySlot();

    // Add starting items (Decision 10: keys "item"/"amount").
    foreach (var entry in StartingItems)
    {
      if (
        entry.TryGetValue("item", out var itemVar)
        && entry.TryGetValue("amount", out var amountVar)
      )
      {
        var item = itemVar.As<ItemData>();
        if (item != null)
          AddItem(item, amountVar.AsInt32());
      }
    }
  }

  public override void _Input(InputEvent @event)
  {
    // Hotbar selection with number keys.
    for (var i = 0; i < HotbarSize; i++)
    {
      if (@event.IsActionPressed($"hotbar_{i + 1}"))
      {
        SelectedHotbarSlot = i;
        break;
      }
    }

    // Mouse wheel for hotbar.
    if (@event is InputEventMouseButton mouseButton)
    {
      if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        SelectedHotbarSlot = (SelectedHotbarSlot - 1 + HotbarSize) % HotbarSize;
      else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        SelectedHotbarSlot = (SelectedHotbarSlot + 1) % HotbarSize;
    }
  }

  /// <summary>
  ///   Adds an amount of an item, stacking into existing stacks first and
  ///   then into empty slots (hotbar before grid, as upstream). Returns the
  ///   number of items that could NOT be placed (0 = all placed). Partial
  ///   success still publishes ItemAdded/InventoryChanged for the placed
  ///   portion — crafters rely on the returned remainder for refunds.
  /// </summary>
  public int AddItem(ItemData item, int amount = 1)
  {
    var remaining = amount;

    // First try to stack with existing items.
    remaining = TryStackItem(item, remaining);

    // Then try to add to empty slots.
    if (remaining > 0)
      remaining = TryAddToEmptySlots(item, remaining);

    if (remaining < amount)
    {
      GameEvents.RaiseItemAdded(item.Id, amount - remaining);
      GameEvents.RaiseInventoryChanged();
    }

    return remaining;
  }

  private int TryStackItem(ItemData item, int amount)
  {
    // Try hotbar first.
    foreach (var slot in _hotbar)
    {
      if (slot.Item?.Id == item.Id && slot.Amount < item.MaxStack)
      {
        var canAdd = item.MaxStack - slot.Amount;
        var toAdd = Mathf.Min(canAdd, amount);
        slot.Amount += toAdd;
        amount -= toAdd;
        if (amount == 0)
          return 0;
      }
    }

    // Then inventory.
    for (var y = 0; y < InventoryHeight; y++)
    {
      for (var x = 0; x < InventoryWidth; x++)
      {
        var slot = _inventory[x, y];
        if (slot.Item?.Id == item.Id && slot.Amount < item.MaxStack)
        {
          var canAdd = item.MaxStack - slot.Amount;
          var toAdd = Mathf.Min(canAdd, amount);
          slot.Amount += toAdd;
          amount -= toAdd;
          if (amount == 0)
            return 0;
        }
      }
    }

    return amount;
  }

  private int TryAddToEmptySlots(ItemData item, int amount)
  {
    // Try hotbar first.
    foreach (var slot in _hotbar)
    {
      if (slot.Item == null)
      {
        var toAdd = Mathf.Min(item.MaxStack, amount);
        slot.Item = item;
        slot.Amount = toAdd;
        amount -= toAdd;
        if (amount == 0)
          return 0;
      }
    }

    // Then inventory.
    for (var y = 0; y < InventoryHeight; y++)
    {
      for (var x = 0; x < InventoryWidth; x++)
      {
        var slot = _inventory[x, y];
        if (slot.Item == null)
        {
          var toAdd = Mathf.Min(item.MaxStack, amount);
          slot.Item = item;
          slot.Amount = toAdd;
          amount -= toAdd;
          if (amount == 0)
            return 0;
        }
      }
    }

    return amount;
  }

  /// <summary>
  ///   Removes up to <paramref name="amount"/> of <paramref name="itemId"/>
  ///   from any slot. Returns true only when the full amount was removed.
  ///   Partial removal publishes ItemRemoved/InventoryChanged for the part
  ///   actually taken.
  /// </summary>
  public bool RemoveItem(string itemId, int amount = 1)
  {
    var remaining = amount;

    // Search all slots, hotbar first (matches upstream enumeration order).
    var allSlots = _hotbar.Concat(_inventory.Cast<InventorySlot>());

    foreach (var slot in allSlots)
    {
      if (slot.Item?.Id == itemId)
      {
        var toRemove = Mathf.Min(slot.Amount, remaining);
        slot.Amount -= toRemove;
        remaining -= toRemove;

        if (slot.Amount == 0)
          slot.Item = null;

        if (remaining == 0)
          break;
      }
    }

    if (remaining < amount)
    {
      GameEvents.RaiseItemRemoved(itemId, amount - remaining);
      GameEvents.RaiseInventoryChanged();
    }

    return remaining == 0;
  }

  public int GetItemCount(string itemId)
  {
    var count = 0;

    foreach (var slot in _hotbar)
    {
      if (slot.Item?.Id == itemId)
        count += slot.Amount;
    }

    for (var x = 0; x < InventoryWidth; x++)
    {
      for (var y = 0; y < InventoryHeight; y++)
      {
        if (_inventory[x, y].Item?.Id == itemId)
          count += _inventory[x, y].Amount;
      }
    }

    return count;
  }

  public bool HasItem(string itemId, int amount = 1) =>
    GetItemCount(itemId) >= amount;

  public InventorySlot GetHotbarSlot(int index) => _hotbar[index];

  public InventorySlot GetInventorySlot(int x, int y) => _inventory[x, y];

  public void SwapSlots(InventorySlot slot1, InventorySlot slot2)
  {
    (slot1.Item, slot2.Item) = (slot2.Item, slot1.Item);
    (slot1.Amount, slot2.Amount) = (slot2.Amount, slot1.Amount);
    GameEvents.RaiseInventoryChanged();
  }
}

public class InventorySlot
{
  public ItemData? Item { get; set; }
  public int Amount { get; set; }

  public bool IsEmpty => Item == null || Amount == 0;
}
