// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using System;
using Godot;

/// <summary>
///   T8.5.8: the storage-box modal, 1:1 the CraftUI modal paradigm: a
///   full-screen dimmed backdrop (MouseFilter.Stop) swallows mouse clicks,
///   the centered panel shows the chest's 20 slots and the player's 40 grid
///   slots as Panel + Label cells; clicking a cell transfers the WHOLE stack
///   to the other side, rolling the remainder back when the destination
///   fills up (nothing is lost or split across a transfer).
///
///   Opened by <see cref="StorageBox.Interact"/> (singleton-style: exactly
///   one StorageUI is found under the current scene); Esc ("ui_cancel")
///   closes it. While open the mouse is Visible and the gameplay-input lock
///   is held, so polled consumers (move/attack/build/use) ignore input.
///   Mutual-exclusion gate (CraftUI contract): a lock held by ANOTHER modal
///   (e.g. the forced tutorial) blocks opening, never closing.
///
///   Fail-closed: a null box or player inventory renders empty cells without
///   crashing. SUBSCRIBES <see cref="GameEvents.InventoryChanged"/>;
///   unsubscribes in _ExitTree (Decision 13).
/// </summary>
public partial class StorageUI : CanvasLayer
{
  private static readonly Color EmptyCellColor = new(0.16f, 0.16f, 0.16f);
  private static readonly Color FilledCellColor = new(0.2f, 0.35f, 0.22f);

  /// <summary>Whether the storage panel is currently open.</summary>
  public bool IsOpen { get; private set; }

  private StorageBox? _box;
  private InventorySystem? _playerInventory;
  private GridContainer? _chestSlots;
  private GridContainer? _playerSlots;

  public override void _Ready()
  {
    BuildUi();
    Visible = false;
    GameEvents.InventoryChanged += OnInventoryChanged;
  }

  public override void _ExitTree() => GameEvents.InventoryChanged -= OnInventoryChanged;

  public override void _Input(InputEvent @event)
  {
    // Esc closes while this panel is open. The gameplay-input lock held by
    // ANOTHER modal never blocks closing (mirrors CraftUI's contract).
    if (IsOpen && @event.IsActionPressed("ui_cancel"))
    {
      Close();
      GetViewport().SetInputAsHandled();
    }
  }

  /// <summary>
  ///   Opens the panel targeting <paramref name="box"/> plus the player's
  ///   inventory. Refuses while ANOTHER modal holds the gameplay-input lock
  ///   (mutual-exclusion gate); re-opening while already open just re-targets
  ///   the panel (the lock is this panel's own then).
  /// </summary>
  public void Open(StorageBox box, InventorySystem? playerInventory)
  {
    if (box == null)
      return;

    if (GameEvents.GameplayInputLocked && !IsOpen)
      return;

    _box = box;
    _playerInventory = playerInventory;
    IsOpen = true;
    Visible = true;
    Input.MouseMode = Input.MouseModeEnum.Visible;
    GameEvents.RaiseGameplayInputLockChanged(true);
    Refresh();
  }

  /// <summary>Closes the panel and releases the gameplay-input lock.</summary>
  public void Close()
  {
    IsOpen = false;
    Visible = false;
    Input.MouseMode = Input.MouseModeEnum.Captured;
    GameEvents.RaiseGameplayInputLockChanged(false);
  }

  /// <summary>
  ///   Rebuilds both slot grids from the current box/inventory contents.
  ///   Idempotent: previous cells are detached and queued for deletion before
  ///   new ones are added.
  /// </summary>
  public void Refresh()
  {
    // The static event bus can deliver a call after this node was freed
    // (cross-test/teardown edge) — never touch a disposed child.
    if (!IsInstanceValid(_chestSlots) || !IsInstanceValid(_playerSlots))
      return;

    if (_box == null || _playerInventory == null)
      return;

    RebuildChestSlots();
    RebuildPlayerSlots();
  }

  /// <summary>
  ///   Moves the WHOLE stack in the player's grid slot
  ///   <paramref name="playerSlotIndex"/> (row-major: index = y * width + x)
  ///   into the chest. Atomic: if the chest cannot hold the whole stack, the
  ///   unplaced remainder is rolled back into the just-freed player slot.
  /// </summary>
  public void TransferPlayerToChest(int playerSlotIndex)
  {
    if (_box == null || _playerInventory == null)
      return;

    var width = _playerInventory.InventoryWidth;
    var slot = _playerInventory.GetInventorySlot(
      playerSlotIndex % width, playerSlotIndex / width
    );
    if (slot.IsEmpty || slot.Item == null)
      return;

    var item = slot.Item;
    var amount = slot.Amount;

    // Remove first (full stack), then place into the box; the removal only
    // succeeds when the full amount left, so the transfer is all-or-nothing
    // apart from the rollback below.
    if (!_playerInventory.RemoveItem(item.Id, amount))
      return;

    var remainder = _box.Inventory.AddItem(item, amount);
    if (remainder > 0)
      _playerInventory.AddItem(item, remainder);
  }

  /// <summary>
  ///   Moves the WHOLE stack in chest slot <paramref name="chestSlotIndex"/>
  ///   into the player's inventory. Atomic: if the player inventory cannot
  ///   hold the whole stack, the unplaced remainder is rolled back into the
  ///   just-freed chest slot.
  /// </summary>
  public void TransferChestToPlayer(int chestSlotIndex)
  {
    if (_box == null || _playerInventory == null)
      return;

    var slot = _box.Inventory.GetSlot(chestSlotIndex);
    if (slot.IsEmpty || slot.Item == null)
      return;

    var item = slot.Item;
    var amount = slot.Amount;

    if (!_box.Inventory.RemoveItem(item.Id, amount))
      return;

    var remainder = _playerInventory.AddItem(item, amount);
    if (remainder > 0)
      _box.Inventory.AddItem(item, remainder);
  }

  private void OnInventoryChanged()
  {
    if (IsOpen)
      Refresh();
  }

  private void RebuildChestSlots()
  {
    DetachAndFreeChildren(_chestSlots!);

    for (var i = 0; i < StorageBox.SlotCount; i++)
    {
      var slotIndex = i;
      _chestSlots!.AddChild(
        BuildSlotCell(_box!.Inventory.GetSlot(i), () => TransferChestToPlayer(slotIndex))
      );
    }
  }

  private void RebuildPlayerSlots()
  {
    DetachAndFreeChildren(_playerSlots!);

    var width = _playerInventory!.InventoryWidth;
    for (var y = 0; y < _playerInventory.InventoryHeight; y++)
    {
      for (var x = 0; x < width; x++)
      {
        var slotIndex = y * width + x;
        _playerSlots!.AddChild(
          BuildSlotCell(
            _playerInventory.GetInventorySlot(x, y),
            () => TransferPlayerToChest(slotIndex)
          )
        );
      }
    }
  }

  /// <summary>Builds one Panel + Label cell; left-click runs <paramref name="onClick"/>.</summary>
  private static Control BuildSlotCell(InventorySlot slot, Action onClick)
  {
    var cell = new PanelContainer
    {
      CustomMinimumSize = new Vector2(96, 36),
      MouseFilter = Control.MouseFilterEnum.Stop
    };

    var style = new StyleBoxFlat
    {
      BgColor = slot.IsEmpty ? EmptyCellColor : FilledCellColor,
      BorderColor = new Color(0.35f, 0.35f, 0.35f)
    };
    style.SetBorderWidthAll(1);
    style.SetCornerRadiusAll(4);
    cell.AddThemeStyleboxOverride("panel", style);

    var label = new Label
    {
      Text = BuildSlotText(slot),
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };
    cell.AddChild(label);

    cell.GuiInput += @event =>
    {
      if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        onClick();
    };

    return cell;
  }

  private static string BuildSlotText(InventorySlot slot)
  {
    if (slot.IsEmpty || slot.Item == null)
      return "";

    var name = string.IsNullOrEmpty(slot.Item.DisplayName) ? slot.Item.Id : slot.Item.DisplayName;
    return $"{name} ×{slot.Amount}";
  }

  private static void DetachAndFreeChildren(GridContainer grid)
  {
    // Detach immediately so GetChildCount is accurate within the same frame
    // (tests assert on it); QueueFree reclaims the nodes at frame end.
    foreach (var child in grid.GetChildren())
    {
      grid.RemoveChild(child);
      child.QueueFree();
    }
  }

  /// <summary>
  ///   Builds the whole UI tree in C# (the scene only carries the root node,
  ///   exactly like CraftUI): full-screen dimmed backdrop -> centered panel
  ///   with the title, the chest grid (5 columns) and the player grid
  ///   (10 columns).
  /// </summary>
  private void BuildUi()
  {
    var backdrop = new Control
    {
      Name = "Backdrop",
      MouseFilter = Control.MouseFilterEnum.Stop
    };
    backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    AddChild(backdrop);

    // Semi-transparent dimmer; the whole Control chain uses
    // MouseFilter.Stop so mouse clicks are swallowed before _UnhandledInput
    // consumers (WeaponSystem attack/use) see them.
    var dim = new ColorRect
    {
      Name = "Dim",
      Color = new Color(0f, 0f, 0f, 0.55f),
      MouseFilter = Control.MouseFilterEnum.Stop
    };
    dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    backdrop.AddChild(dim);

    var center = new CenterContainer { Name = "Center" };
    center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    backdrop.AddChild(center);

    var panel = new PanelContainer { Name = "Panel" };
    var style = new StyleBoxFlat
    {
      BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f),
      BorderColor = new Color(0.4f, 0.4f, 0.4f)
    };
    style.SetBorderWidthAll(2);
    style.SetCornerRadiusAll(8);
    panel.AddThemeStyleboxOverride("panel", style);
    center.AddChild(panel);

    var vbox = new VBoxContainer { Name = "VBox" };
    vbox.AddThemeConstantOverride("separation", 10);
    panel.AddChild(vbox);

    var title = new Label
    {
      Name = "Title",
      Text = "STORAGE (Esc to close)",
      HorizontalAlignment = HorizontalAlignment.Center
    };
    title.AddThemeFontSizeOverride("font_size", 22);
    vbox.AddChild(title);

    var chestTitle = new Label { Name = "ChestTitle", Text = "储物箱" };
    vbox.AddChild(chestTitle);

    var chestSlots = new GridContainer
    {
      Name = "ChestSlots",
      Columns = 5,
      CustomMinimumSize = new Vector2(500, 0)
    };
    vbox.AddChild(chestSlots);

    var playerTitle = new Label { Name = "PlayerTitle", Text = "背包" };
    vbox.AddChild(playerTitle);

    var playerSlots = new GridContainer
    {
      Name = "PlayerSlots",
      Columns = 10,
      CustomMinimumSize = new Vector2(1000, 0)
    };
    vbox.AddChild(playerSlots);

    _chestSlots = chestSlots;
    _playerSlots = playerSlots;
  }
}
