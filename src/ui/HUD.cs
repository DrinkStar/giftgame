// Ported from srperens/SurvivalIsland (user decision: personal non-commercial
// use) — see godot-refs/srperens-SurvivalIsland
namespace SeaAnomaly;

using System.Globalization;
using Godot;

/// <summary>
///   Heads-up display, ported from upstream SurvivalIsland's HUD with these
///   adaptations:
///   - all events come from the static <see cref="GameEvents"/> bus
///     (Decision 1) instead of per-node [Signal] delegates;
///   - upstream WarmthBar is replaced by StaminaBar (no warmth in this
///     iteration's design, Decision 9);
///   - initial-value race fix: after subscribing each event the HUD
///     immediately pulls the current value from the source node, so values
///     published before the subscription are never missed;
///   - node paths are resolved from scenes/hud.tscn exactly as upstream
///     (the scene file is hand-authored to match, Decision 9).
///
///   SUBSCRIBES eight GameEvents; unsubscribes all of them in _ExitTree
///   (Decision 13).
/// </summary>
public partial class HUD : CanvasLayer
{
  /// <summary>Player whose subsystems the HUD displays.</summary>
  [Export] public PlayerController? Player { get; set; }

  /// <summary>Day/night service (clock display).</summary>
  [Export] public DayNightService? DayNightService { get; set; }

  /// <summary>Weather service (kept for upstream parity / future UI).</summary>
  [Export] public WeatherService? WeatherService { get; set; }

  private ProgressBar? _healthBar;
  private ProgressBar? _hungerBar;
  private ProgressBar? _thirstBar;
  private ProgressBar? _staminaBar;
  private Label? _timeLabel;
  private Label? _interactionPrompt;
  private ProgressBar? _interactionProgress;
  private HBoxContainer? _hotbarContainer;
  private Control? _crosshairNormal;
  private Control? _crosshairInteract;

  private PlayerStats? _stats;
  private PlayerInteraction? _interaction;
  private InventorySystem? _inventory;

  private Panel? _inventoryPanel;
  private bool _inventoryOpen;
  private bool _initialized;

  public override void _Ready()
  {
    ResolveNodes();
    SetInteractionPrompt("");
    CreateInventoryPanel();

    // Self-wire when the scene exports are set. GameManager may have called
    // Initialize already (it precedes HUD in Game.tscn _Ready order); the
    // guard inside Initialize prevents double subscription.
    if (Player != null)
      Initialize(Player, DayNightService, WeatherService);
  }

  /// <summary>
  ///   Wires the HUD to a player and the ambient services, resolving the
  ///   player subsystems by node name (PlayerStats/PlayerInteraction/
  ///   InventorySystem). Called by GameManager; idempotent.
  /// </summary>
  public void Initialize(
    PlayerController player, DayNightService? dayNight, WeatherService? weather
  )
  {
    if (_initialized)
      return;

    _initialized = true;

    Player = player;
    DayNightService = dayNight;
    WeatherService = weather;

    // May run before this node's _Ready when called from GameManager, so
    // resolve the scene child references first (idempotent).
    ResolveNodes();

    _stats = player.GetNodeOrNull<PlayerStats>("PlayerStats");
    _interaction = player.GetNodeOrNull<PlayerInteraction>("PlayerInteraction");
    _inventory = player.GetNodeOrNull<InventorySystem>("InventorySystem");

    // Subscribe + immediately pull current values (initial-value race fix).

    GameEvents.HealthChanged += OnHealthChanged;
    if (_stats != null)
      OnHealthChanged(_stats.Health, _stats.MaxHealth);

    GameEvents.HungerChanged += OnHungerChanged;
    if (_stats != null)
      OnHungerChanged(_stats.Hunger, _stats.MaxHunger);

    GameEvents.ThirstChanged += OnThirstChanged;
    if (_stats != null)
      OnThirstChanged(_stats.Thirst, _stats.MaxThirst);

    GameEvents.StaminaChanged += OnStaminaChanged;
    if (_stats != null)
      OnStaminaChanged(_stats.Stamina, _stats.StaminaMax);

    GameEvents.InteractionPromptChanged += OnInteractionPromptChanged;
    OnInteractionPromptChanged(_interaction?.CurrentPrompt ?? "");

    GameEvents.InteractionProgressChanged += OnInteractionProgressChanged;
    OnInteractionProgressChanged(_interaction?.CurrentProgress ?? 0f);

    GameEvents.InventoryChanged += OnInventoryChanged;
    OnInventoryChanged();

    GameEvents.HotbarSelectionChanged += OnHotbarSelectionChanged;
    if (_inventory != null)
      OnHotbarSelectionChanged(_inventory.SelectedHotbarSlot);

    SetupHotbar();
  }

  public override void _ExitTree()
  {
    GameEvents.HealthChanged -= OnHealthChanged;
    GameEvents.HungerChanged -= OnHungerChanged;
    GameEvents.ThirstChanged -= OnThirstChanged;
    GameEvents.StaminaChanged -= OnStaminaChanged;
    GameEvents.InteractionPromptChanged -= OnInteractionPromptChanged;
    GameEvents.InteractionProgressChanged -= OnInteractionProgressChanged;
    GameEvents.InventoryChanged -= OnInventoryChanged;
    GameEvents.HotbarSelectionChanged -= OnHotbarSelectionChanged;
  }

  public override void _Process(double delta)
  {
    if (_timeLabel != null && DayNightService != null)
      _timeLabel.Text = DayNightService.GetTimeString();
  }

  public override void _Input(InputEvent @event)
  {
    if (@event.IsActionPressed("inventory"))
      ToggleInventory();
  }

  /// <summary>
  ///   Resolves the scene child nodes from scenes/hud.tscn. Idempotent; safe
  ///   to call before this node's own _Ready.
  /// </summary>
  private void ResolveNodes()
  {
    _healthBar = GetNodeOrNull<ProgressBar>(
      "MarginContainer/VBoxContainer/TopBar/StatsContainer/HealthBar"
    );
    _hungerBar = GetNodeOrNull<ProgressBar>(
      "MarginContainer/VBoxContainer/TopBar/StatsContainer/HungerBar"
    );
    _thirstBar = GetNodeOrNull<ProgressBar>(
      "MarginContainer/VBoxContainer/TopBar/StatsContainer/ThirstBar"
    );
    _staminaBar = GetNodeOrNull<ProgressBar>(
      "MarginContainer/VBoxContainer/TopBar/StatsContainer/StaminaBar"
    );
    _timeLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/TopBar/TimeLabel");
    _interactionPrompt = GetNodeOrNull<Label>("BottomContainer/InteractionPrompt");
    _interactionProgress = GetNodeOrNull<ProgressBar>(
      "BottomContainer/InteractionProgress"
    );
    _hotbarContainer = GetNodeOrNull<HBoxContainer>("BottomContainer/HotbarContainer");
    _crosshairNormal = GetNodeOrNull<Control>(
      "CenterContainer/Crosshair/CrosshairNormal"
    );
    _crosshairInteract = GetNodeOrNull<Control>(
      "CenterContainer/Crosshair/CrosshairInteract"
    );
  }

  private void OnHealthChanged(float value, float max)
  {
    if (_healthBar != null)
    {
      _healthBar.MaxValue = max;
      _healthBar.Value = value;
    }
  }

  private void OnHungerChanged(float value, float max)
  {
    if (_hungerBar != null)
    {
      _hungerBar.MaxValue = max;
      _hungerBar.Value = value;
    }
  }

  private void OnThirstChanged(float value, float max)
  {
    if (_thirstBar != null)
    {
      _thirstBar.MaxValue = max;
      _thirstBar.Value = value;
    }
  }

  private void OnStaminaChanged(float value, float max)
  {
    if (_staminaBar != null)
    {
      _staminaBar.MaxValue = max;
      _staminaBar.Value = value;
    }
  }

  private void OnInteractionPromptChanged(string prompt) =>
    SetInteractionPrompt(prompt);

  private void OnInteractionProgressChanged(float progress) =>
    SetInteractionProgress(progress);

  private void OnInventoryChanged()
  {
    UpdateHotbar();
    if (_inventoryOpen)
      UpdateInventoryPanel();
  }

  private void OnHotbarSelectionChanged(int slot)
  {
    if (_hotbarContainer == null)
      return;

    for (var i = 0; i < 5; i++)
    {
      var slotPanel = _hotbarContainer.GetNodeOrNull<Panel>($"HotbarSlot{i}");
      if (slotPanel?.GetThemeStylebox("panel") is not StyleBoxFlat style)
        continue;

      style.BorderColor = i == slot
        ? new Color(1f, 0.8f, 0.2f)
        : new Color(0.5f, 0.5f, 0.5f);
    }
  }

  private void SetupHotbar()
  {
    if (_hotbarContainer == null)
      return;

    // Clear existing children (in case of reload).
    foreach (var child in _hotbarContainer.GetChildren())
      child.QueueFree();

    for (var i = 0; i < 5; i++)
    {
      var slot = new Panel();
      slot.Name = $"HotbarSlot{i}";
      slot.CustomMinimumSize = new Vector2(64, 64);

      var styleBox = new StyleBoxFlat();
      styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
      styleBox.BorderColor = new Color(0.5f, 0.5f, 0.5f);
      styleBox.SetBorderWidthAll(2);
      slot.AddThemeStyleboxOverride("panel", styleBox);

      var icon = new TextureRect();
      icon.Name = "Icon";
      icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
      icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
      icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
      icon.OffsetLeft = 4;
      icon.OffsetTop = 4;
      icon.OffsetRight = -4;
      icon.OffsetBottom = -16;
      slot.AddChild(icon);

      var amountLabel = new Label();
      amountLabel.Name = "Amount";
      amountLabel.HorizontalAlignment = HorizontalAlignment.Right;
      amountLabel.VerticalAlignment = VerticalAlignment.Bottom;
      amountLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
      amountLabel.OffsetRight = -4;
      amountLabel.OffsetBottom = -2;
      slot.AddChild(amountLabel);

      // Item names show only when the item has no icon (Decision 15: the
      // shipped .tres files omit icons, so the name is the usual display).
      var nameLabel = new Label();
      nameLabel.Name = "ItemName";
      nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
      nameLabel.VerticalAlignment = VerticalAlignment.Center;
      nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
      nameLabel.AddThemeFontSizeOverride("font_size", 9);
      nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
      nameLabel.Visible = false;
      slot.AddChild(nameLabel);

      var keyLabel = new Label();
      keyLabel.Name = "Key";
      keyLabel.Text = (i + 1).ToString(CultureInfo.InvariantCulture);
      keyLabel.HorizontalAlignment = HorizontalAlignment.Left;
      keyLabel.VerticalAlignment = VerticalAlignment.Top;
      keyLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
      keyLabel.OffsetLeft = 4;
      keyLabel.OffsetTop = 2;
      keyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
      slot.AddChild(keyLabel);

      _hotbarContainer.AddChild(slot);
    }

    UpdateHotbar();
  }

  private void UpdateHotbar()
  {
    if (_inventory == null || _hotbarContainer == null)
      return;

    for (var i = 0; i < 5; i++)
    {
      var slot = _inventory.GetHotbarSlot(i);
      var slotPanel = _hotbarContainer.GetNodeOrNull<Panel>($"HotbarSlot{i}");
      var icon = slotPanel?.GetNodeOrNull<TextureRect>("Icon");
      var amountLabel = slotPanel?.GetNodeOrNull<Label>("Amount");
      var nameLabel = slotPanel?.GetNodeOrNull<Label>("ItemName");

      if (icon != null)
        icon.Texture = slot.Item?.Icon;

      // Show item name if no icon (Decision 15 fallback).
      if (nameLabel != null)
      {
        if (slot.Item != null && slot.Item.Icon == null)
        {
          nameLabel.Text = slot.Item.DisplayName;
          nameLabel.Visible = true;
        }
        else
        {
          nameLabel.Visible = false;
        }
      }

      if (amountLabel != null)
        amountLabel.Text = slot.Amount > 1
          ? slot.Amount.ToString(CultureInfo.InvariantCulture)
          : "";
    }
  }

  private void SetInteractionPrompt(string prompt)
  {
    if (_interactionPrompt != null)
    {
      _interactionPrompt.Text = prompt;
      _interactionPrompt.Visible = !string.IsNullOrEmpty(prompt);
    }

    // Crosshair: normal while nothing is targetable, highlighted otherwise.
    var canInteract = !string.IsNullOrEmpty(prompt);
    if (_crosshairNormal != null)
      _crosshairNormal.Visible = !canInteract;
    if (_crosshairInteract != null)
      _crosshairInteract.Visible = canInteract;
  }

  private void SetInteractionProgress(float progress)
  {
    if (_interactionProgress != null)
    {
      _interactionProgress.Value = progress * 100;
      _interactionProgress.Visible = progress > 0;
    }
  }

  private void CreateInventoryPanel()
  {
    _inventoryPanel = new Panel();
    _inventoryPanel.Name = "InventoryPanel";
    _inventoryPanel.Visible = false;
    _inventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
    _inventoryPanel.Size = new Vector2(660, 340);
    _inventoryPanel.Position = new Vector2(-330, -170);

    var style = new StyleBoxFlat();
    style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
    style.BorderColor = new Color(0.4f, 0.4f, 0.4f);
    style.SetBorderWidthAll(2);
    style.SetCornerRadiusAll(8);
    _inventoryPanel.AddThemeStyleboxOverride("panel", style);

    var vbox = new VBoxContainer();
    vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    vbox.AddThemeConstantOverride("separation", 10);
    vbox.OffsetLeft = 15;
    vbox.OffsetTop = 15;
    vbox.OffsetRight = -15;
    vbox.OffsetBottom = -15;
    _inventoryPanel.AddChild(vbox);

    var title = new Label();
    title.Text = "INVENTORY (Tab to close)";
    title.HorizontalAlignment = HorizontalAlignment.Center;
    vbox.AddChild(title);

    var grid = new GridContainer();
    grid.Name = "InventoryGrid";
    grid.Columns = 10;
    grid.AddThemeConstantOverride("h_separation", 4);
    grid.AddThemeConstantOverride("v_separation", 4);
    vbox.AddChild(grid);

    // Create 40 display-only slots (10x4).
    for (var i = 0; i < 40; i++)
      grid.AddChild(CreateSlotPanel($"InvSlot{i}"));

    AddChild(_inventoryPanel);
  }

  private static Panel CreateSlotPanel(string name)
  {
    var slot = new Panel();
    slot.Name = name;
    slot.CustomMinimumSize = new Vector2(56, 56);

    var slotStyle = new StyleBoxFlat();
    slotStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
    slotStyle.SetBorderWidthAll(1);
    slot.AddThemeStyleboxOverride("panel", slotStyle);

    var icon = new TextureRect();
    icon.Name = "Icon";
    icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
    icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    icon.OffsetLeft = 4;
    icon.OffsetTop = 4;
    icon.OffsetRight = -4;
    icon.OffsetBottom = -16;
    slot.AddChild(icon);

    var nameLabel = new Label();
    nameLabel.Name = "ItemName";
    nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
    nameLabel.VerticalAlignment = VerticalAlignment.Center;
    nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    nameLabel.AddThemeFontSizeOverride("font_size", 8);
    nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
    nameLabel.Visible = false;
    slot.AddChild(nameLabel);

    var amount = new Label();
    amount.Name = "Amount";
    amount.HorizontalAlignment = HorizontalAlignment.Right;
    amount.VerticalAlignment = VerticalAlignment.Bottom;
    amount.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
    amount.OffsetRight = -4;
    amount.OffsetBottom = -2;
    slot.AddChild(amount);

    return slot;
  }

  private void ToggleInventory()
  {
    _inventoryOpen = !_inventoryOpen;

    if (_inventoryPanel == null)
      return;

    _inventoryPanel.Visible = _inventoryOpen;
    if (_inventoryOpen)
    {
      Input.MouseMode = Input.MouseModeEnum.Visible;
      UpdateInventoryPanel();
    }
    else
    {
      Input.MouseMode = Input.MouseModeEnum.Captured;
    }
  }

  private void UpdateInventoryPanel()
  {
    if (_inventory == null || _inventoryPanel == null)
      return;

    var grid = _inventoryPanel.GetNodeOrNull<GridContainer>(
      "VBoxContainer/InventoryGrid"
    );
    if (grid == null)
      return;

    for (var i = 0; i < 40; i++)
    {
      var x = i % 10;
      var y = i / 10;
      var slot = _inventory.GetInventorySlot(x, y);

      var slotPanel = grid.GetNodeOrNull<Panel>($"InvSlot{i}");
      var icon = slotPanel?.GetNodeOrNull<TextureRect>("Icon");
      var amount = slotPanel?.GetNodeOrNull<Label>("Amount");
      var nameLabel = slotPanel?.GetNodeOrNull<Label>("ItemName");

      if (icon != null)
        icon.Texture = slot.Item?.Icon;

      // Show item name if no icon (Decision 15 fallback).
      if (nameLabel != null)
      {
        if (slot.Item != null && slot.Item.Icon == null)
        {
          nameLabel.Text = slot.Item.DisplayName;
          nameLabel.Visible = true;
        }
        else
        {
          nameLabel.Visible = false;
        }
      }

      if (amount != null)
        amount.Text = slot.Amount > 1
          ? slot.Amount.ToString(CultureInfo.InvariantCulture)
          : "";
    }
  }
}
