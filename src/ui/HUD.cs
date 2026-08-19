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
///   - node paths are resolved from scenes/hud.tscn (hand-authored);
///   - top-left survival chrome uses Chinese labels (生命/饥饿/口渴/体力)
///     plus current/max numbers, and a Chinese clock (时间/第N天/时段).
///
///   SUBSCRIBES fourteen GameEvents (nine survival/HUD, three quest tracker
///   events, plus TutorialStepChanged / TutorialCompleted so 当前目标 hides
///   only during the chapter-1 overlay); unsubscribes all of them in
///   _ExitTree (Decision 13). The tracker is fail-closed when QuestService
///   is missing, never pauses the tree, and is distinct from the right-side
///   keybind caption. Craft/inventory lock does not hide 当前目标.
/// </summary>
public partial class HUD : CanvasLayer
{
  /// <summary>Player whose subsystems the HUD displays.</summary>
  [Export] public PlayerController? Player { get; set; }

  /// <summary>Day/night service (clock display).</summary>
  [Export] public DayNightService? DayNightService { get; set; }

  /// <summary>Weather service (kept for upstream parity / future UI).</summary>
  [Export] public WeatherService? WeatherService { get; set; }

  private const string StatsRoot =
    "MarginContainer/VBoxContainer/TopBar/StatusPanel/StatsContainer";

  private static readonly Color HealthFill = new(0.95f, 0.16f, 0.2f);
  private static readonly Color HungerFill = new(1f, 0.55f, 0.08f);
  private static readonly Color ThirstFill = new(0.08f, 0.78f, 0.95f);
  private static readonly Color StaminaFill = new(0.18f, 0.82f, 0.32f);

  private ProgressBar? _healthBar;
  private ProgressBar? _hungerBar;
  private ProgressBar? _thirstBar;
  private ProgressBar? _staminaBar;
  private Label? _healthValue;
  private Label? _hungerValue;
  private Label? _thirstValue;
  private Label? _staminaValue;
  private Label? _timeLabel;
  private Label? _interactionPrompt;
  private Label? _guideSubtitle;
  private Label? _questObjective;
  private bool _sawAQuest;
  private bool _questTrackerSubscribed;
  /// <summary>True while the 6-step chapter-1 tutorial card is on screen.</summary>
  private bool _chapter1OverlayActive;
  private ProgressBar? _interactionProgress;
  private HBoxContainer? _hotbarContainer;
  private Control? _crosshairNormal;
  private Control? _crosshairInteract;
  private Tween? _guideTween;
  private int _displayedClockMinutes = int.MinValue;

  private PlayerStats? _stats;
  private PlayerInteraction? _interaction;
  private InventorySystem? _inventory;

  private Panel? _inventoryPanel;
  private bool _inventoryOpen;
  private bool _initialized;
  private ulong _releaseLockAtFrame = ulong.MaxValue;

  /// <summary>True while the Tab inventory panel is open.</summary>
  public bool IsInventoryOpen => _inventoryOpen;

  public override void _Ready()
  {
    ResolveNodes();
    SetInteractionPrompt("");
    CreateInventoryPanel();
    EnsureQuestObjectiveLabel();
    RefreshQuestObjective();

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

    // Guide narration subtitle (Iter7): no initial value to pull — lines
    // only arrive via GuideLine events after this subscription.
    GameEvents.GuideLine += OnGuideLine;

    EnsureQuestObjectiveLabel();
    RefreshQuestObjective();

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
    GameEvents.GuideLine -= OnGuideLine;
    GameEvents.QuestStarted -= OnQuestTrackerEvent;
    GameEvents.QuestProgress -= OnQuestProgress;
    GameEvents.QuestCompleted -= OnQuestTrackerEvent;
    GameEvents.TutorialStepChanged -= OnChapter1OverlayStep;
    GameEvents.TutorialCompleted -= OnChapter1OverlayDone;
    _questTrackerSubscribed = false;
    _chapter1OverlayActive = false;
    // A panel torn down mid-game must not leave the static lock held
    // (mirrors CraftUI / StorageUI). Drop a pending deferred release too.
    var pendingRelease = _releaseLockAtFrame != ulong.MaxValue;
    _releaseLockAtFrame = ulong.MaxValue;
    if (_inventoryOpen || pendingRelease)
      GameEvents.RaiseGameplayInputLockChanged(false);
  }

  public override void _Process(double delta)
  {
    // One-shot deferred lock release: Esc in the same input pass must not
    // let GameManager toggle pause (CraftUI / StorageUI contract).
    if (_releaseLockAtFrame != ulong.MaxValue && Engine.GetProcessFrames() >= _releaseLockAtFrame)
    {
      _releaseLockAtFrame = ulong.MaxValue;
      GameEvents.RaiseGameplayInputLockChanged(false);
    }

    if (_timeLabel == null || DayNightService == null)
      return;

    var hour = DayNightService.CurrentHour;
    var hours = (int)hour;
    var minutes = (int)((hour - hours) * 60);
    var packed = DayNightService.CurrentDay * 1440 + hours * 60 + minutes;
    if (packed == _displayedClockMinutes)
      return;

    _displayedClockMinutes = packed;
    _timeLabel.Text = FormatClock(
      DayNightService.CurrentDay, hours, minutes, hour, DayNightService.CurrentPeriod
    );
  }

  public override void _Input(InputEvent @event)
  {
    if (_inventoryOpen && (
        @event.IsActionPressed("pause") || @event.IsActionPressed("ui_cancel")))
    {
      ToggleInventory();
      GetViewport().SetInputAsHandled();
      return;
    }

    if (@event.IsActionPressed("inventory"))
      ToggleInventory();
  }

  /// <summary>
  ///   Resolves the scene child nodes from scenes/hud.tscn. Idempotent; safe
  ///   to call before this node's own _Ready.
  /// </summary>
  private void ResolveNodes()
  {
    _healthBar = GetNodeOrNull<ProgressBar>($"{StatsRoot}/HealthRow/HealthBar");
    _hungerBar = GetNodeOrNull<ProgressBar>($"{StatsRoot}/HungerRow/HungerBar");
    _thirstBar = GetNodeOrNull<ProgressBar>($"{StatsRoot}/ThirstRow/ThirstBar");
    _staminaBar = GetNodeOrNull<ProgressBar>($"{StatsRoot}/StaminaRow/StaminaBar");
    _healthValue = GetNodeOrNull<Label>($"{StatsRoot}/HealthRow/ValueLabel");
    _hungerValue = GetNodeOrNull<Label>($"{StatsRoot}/HungerRow/ValueLabel");
    _thirstValue = GetNodeOrNull<Label>($"{StatsRoot}/ThirstRow/ValueLabel");
    _staminaValue = GetNodeOrNull<Label>($"{StatsRoot}/StaminaRow/ValueLabel");
    _timeLabel = GetNodeOrNull<Label>($"{StatsRoot}/TimeLabel");
    ApplyStatusChrome();
    _interactionPrompt = GetNodeOrNull<Label>("BottomContainer/InteractionPrompt");
    _interactionProgress = GetNodeOrNull<ProgressBar>(
      "BottomContainer/InteractionProgress"
    );
    _guideSubtitle = GetNodeOrNull<Label>("BottomContainer/GuideSubtitle");
    _hotbarContainer = GetNodeOrNull<HBoxContainer>("BottomContainer/HotbarContainer");
    _crosshairNormal = GetNodeOrNull<Control>(
      "CenterContainer/Crosshair/CrosshairNormal"
    );
    _crosshairInteract = GetNodeOrNull<Control>(
      "CenterContainer/Crosshair/CrosshairInteract"
    );
  }

  private void OnHealthChanged(float value, float max) =>
    WriteBar(_healthBar, _healthValue, value, max);

  private void OnHungerChanged(float value, float max) =>
    WriteBar(_hungerBar, _hungerValue, value, max);

  private void OnThirstChanged(float value, float max) =>
    WriteBar(_thirstBar, _thirstValue, value, max);

  private void OnStaminaChanged(float value, float max) =>
    WriteBar(_staminaBar, _staminaValue, value, max);

  /// <summary>
  ///   Fail-closed bar write: missing nodes are skipped so a partial HUD
  ///   scene still boots.
  /// </summary>
  private static void WriteBar(
    ProgressBar? bar, Label? valueLabel, float value, float max
  )
  {
    if (bar != null)
    {
      bar.MaxValue = max;
      bar.Value = value;
    }

    if (valueLabel != null)
    {
      valueLabel.Text =
        $"{Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)}/" +
        Mathf.RoundToInt(max).ToString(CultureInfo.InvariantCulture);
    }
  }

  private void ApplyStatusChrome()
  {
    var panel = GetNodeOrNull<PanelContainer>(
      "MarginContainer/VBoxContainer/TopBar/StatusPanel"
    );
    if (panel != null)
      panel.AddThemeStyleboxOverride("panel", MakeStatusPanelStyle());

    ApplyBarStyle(_healthBar, HealthFill);
    ApplyBarStyle(_hungerBar, HungerFill);
    ApplyBarStyle(_thirstBar, ThirstFill);
    ApplyBarStyle(_staminaBar, StaminaFill);
  }

  private static void ApplyBarStyle(ProgressBar? bar, Color fill)
  {
    if (bar == null)
      return;

    bar.ShowPercentage = false;
    bar.AddThemeStyleboxOverride("background", MakeBarBackground());
    bar.AddThemeStyleboxOverride("fill", MakeBarFill(fill));
  }

  private static StyleBoxFlat MakeStatusPanelStyle()
  {
    var style = new StyleBoxFlat
    {
      BgColor = new Color(0.04f, 0.07f, 0.12f, 0.82f),
      BorderColor = new Color(0.35f, 0.78f, 0.95f, 0.55f)
    };
    style.SetBorderWidthAll(2);
    style.SetCornerRadiusAll(8);
    style.ContentMarginLeft = 12;
    style.ContentMarginTop = 10;
    style.ContentMarginRight = 12;
    style.ContentMarginBottom = 10;
    return style;
  }

  private static StyleBoxFlat MakeBarBackground()
  {
    var style = new StyleBoxFlat
    {
      BgColor = new Color(0.06f, 0.08f, 0.12f, 0.9f)
    };
    style.SetCornerRadiusAll(4);
    style.SetContentMarginAll(2);
    return style;
  }

  private static StyleBoxFlat MakeBarFill(Color fill)
  {
    var style = new StyleBoxFlat { BgColor = fill };
    style.SetCornerRadiusAll(3);
    return style;
  }

  private static string FormatClock(
    int day, int hours, int minutes, float hour, DayPeriod period
  )
  {
    var dayText = day.ToString(CultureInfo.InvariantCulture);
    return $"时间 第{dayText}天 {hours:D2}:{minutes:D2} {PeriodName(hour, period)}";
  }

  private static string PeriodName(float hour, DayPeriod period) =>
    period switch
    {
      DayPeriod.Dawn => "黎明",
      DayPeriod.Day => hour < 12f ? "上午" : "下午",
      DayPeriod.Dusk => "黄昏",
      _ => "夜晚"
    };

  private void OnInteractionPromptChanged(string prompt) =>
    SetInteractionPrompt(prompt);

  private void OnInteractionProgressChanged(float progress) =>
    SetInteractionProgress(progress);

  private void OnQuestTrackerEvent(string questId) => RefreshQuestObjective();

  private void OnQuestProgress(string questId, int done, int need) =>
    RefreshQuestObjective();

  /// <summary>
  ///   Persistent top-center "当前目标：…" — authored in hud.tscn, not a
  ///   modal, MouseFilter.Ignore so it never steals WASD/combat. Fail-closed
  ///   if QuestService is missing. Hidden only while the chapter-1 tutorial
  ///   card is showing; craft/inventory lock leaves it visible.
  /// </summary>
  private void EnsureQuestObjectiveLabel()
  {
    if (_questObjective == null || !IsInstanceValid(_questObjective))
    {
      _questObjective = GetNodeOrNull<Label>("QuestObjectiveLabel");
      if (_questObjective == null)
      {
        var label = new Label
        {
          Name = "QuestObjectiveLabel",
          Visible = false,
          MouseFilter = Control.MouseFilterEnum.Ignore,
          HorizontalAlignment = HorizontalAlignment.Center,
          AutowrapMode = TextServer.AutowrapMode.Word,
          Text = ""
        };
        label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        // Clear StatusPanel (~392px) on the left and ControlHint on the right.
        label.OffsetLeft = 392f;
        label.OffsetTop = 16f;
        label.OffsetRight = -240f;
        label.OffsetBottom = 56f;
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.72f));
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 5);
        AddChild(label);
        _questObjective = label;
      }
    }

    if (_questTrackerSubscribed)
      return;

    _questTrackerSubscribed = true;
    GameEvents.QuestStarted += OnQuestTrackerEvent;
    GameEvents.QuestProgress += OnQuestProgress;
    GameEvents.QuestCompleted += OnQuestTrackerEvent;
    GameEvents.TutorialStepChanged += OnChapter1OverlayStep;
    GameEvents.TutorialCompleted += OnChapter1OverlayDone;
  }

  private void OnChapter1OverlayStep(int _currentStep, int totalSteps)
  {
    _chapter1OverlayActive = totalSteps == 6;
    RefreshQuestObjective();
  }

  private void OnChapter1OverlayDone()
  {
    _chapter1OverlayActive = false;
    RefreshQuestObjective();
  }

  private void RefreshQuestObjective()
  {
    if (_questObjective == null || !IsInstanceValid(_questObjective))
      return;

    var quests = GetParent()?.GetNodeOrNull<QuestService>("QuestService");
    if (quests == null)
    {
      _questObjective.Visible = false;
      return;
    }

    var hasContent = false;
    if (quests.TryGetCurrentTracker(out _, out var objective, out var done, out var need))
    {
      _sawAQuest = true;
      _questObjective.Text = StoryGuideCopy.FormatObjective(objective, done, need);
      hasContent = true;
    }
    else if (_sawAQuest)
    {
      _questObjective.Text =
        StoryGuideCopy.FormatObjective(StoryGuideCopy.SandboxObjective, 0, 1);
      hasContent = true;
    }

    _questObjective.Visible = hasContent && !_chapter1OverlayActive;
  }

  /// <summary>
  ///   Guide narration subtitle (Iter7 plan Decision 5): shows the line for
  ///   5 seconds, then fades it out over 1 second. A new line restarts the
  ///   fade.
  /// </summary>
  private void OnGuideLine(string text)
  {
    if (_guideSubtitle == null)
      return;

    _guideSubtitle.Text = text;
    _guideSubtitle.Visible = true;
    _guideSubtitle.Modulate = Colors.White;

    _guideTween?.Kill();
    _guideTween = CreateTween();
    _guideTween.TweenInterval(5.0);
    _guideTween.TweenProperty(_guideSubtitle, "modulate:a", 0f, 1.0);
    _guideTween.TweenCallback(
      Callable.From(() =>
      {
        if (IsInstanceValid(_guideSubtitle))
          _guideSubtitle.Visible = false;
      })
    );
  }

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
    // FIX(code-review P2-30): respect the gameplay-input lock — while a modal
    // (crafting/storage) holds it, Tab must neither open the inventory panel
    // nor steal the mouse mode; the modal's own Esc closes it first. Closing
    // the panel is always allowed (the lock, if any, belongs to this panel).
    if (_inventoryOpen)
    {
      _inventoryOpen = false;
      if (_inventoryPanel != null)
        _inventoryPanel.Visible = false;
      Input.MouseMode = Input.MouseModeEnum.Captured;
      // Deferred so GameManager's pause action in the same Esc pass still
      // sees GameplayInputLocked (does not pause the tree / day-night).
      _releaseLockAtFrame = Engine.GetProcessFrames() + 1;
      return;
    }

    if (GameEvents.GameplayInputLocked)
      return;

    _inventoryOpen = true;
    GameEvents.RaiseGameplayInputLockChanged(true);

    if (_inventoryPanel == null)
      return;

    _inventoryPanel.Visible = true;
    Input.MouseMode = Input.MouseModeEnum.Visible;
    UpdateInventoryPanel();
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
