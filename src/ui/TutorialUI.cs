// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   T8.3 forced new-player tutorial overlay. A CanvasLayer whose children
///   are built in C#: a full-screen dim (a Stop-filtered Control carrying a
///   semi-transparent ColorRect) and a centered step card (title, body, 6
///   step dots). Sequencing lives in the pure <see cref="TutorialFlow"/>
///   state machine; this node translates GameEvents occurrences into
///   "current step done" and renders the card. The day/night cycle is
///   intentionally NOT paused during the tutorial (never sets
///   GetTree().Paused).
///
///   Completion raises <see cref="GameEvents.TutorialCompleted"/> — the gate
///   CraftUI and other gated systems wait on (T8.3).
///
///   Step completion signals (all through the existing event bus):
///   1 move — the player moves &gt; 2 m from the step-start position (_Process);
///   2 gather — ItemAdded of wood/coconut;
///   3 drink — ItemRemoved of berries/coconut (the F key consumes one; a
///     thirst-gain check would never fire because the player starts at full
///     thirst, FIX(iter8));
///   4 campfire / 5 bed — BuildingPlaced with the matching buildable name;
///   6 combat — MeleeHit or EnemyDied.
///
///   Mouse policy (T8.3): steps 1-3 are keyboard-driven → Captured; steps
///   4-5 use the mouse-driven building menu → Visible; step 6 (combat) →
///   Captured. The dim blocks the mouse (MouseFilter.Stop) only while no
///   mouse interaction is needed (steps 1-3 — blocking it also blocks the
///   attack input). From step 4 on the dim must let real clicks through
///   (MouseFilter.Ignore): the build menu / 3D placement (steps 4-5) and
///   the attack (step 6) would otherwise be unclickable behind the
///   full-screen dim. Attack during build mode is already disabled by
///   WeaponSystem (Decision 11), so Ignore on steps 4-5 unblocks nothing
///   but legitimate building input.
///
///   Wiring guards: <see cref="Player"/> is an optional direct node reference
///   (null-safe). When unwired, step 1 completes immediately so a missing
///   wiring never soft-locks the forced tutorial.
///
///   SUBSCRIBES GameStarted (always) and ItemAdded/ItemRemoved/
///   BuildingPlaced/MeleeHit/EnemyDied (while active); unsubscribes all in
///   _ExitTree and on completion (Decision 13).
/// </summary>
public partial class TutorialUI : CanvasLayer
{
  #region Exports

  /// <summary>
  ///   Player whose movement completes step 1. Direct node reference (the
  ///   same wiring style as GameManager/SaveService) — NodePath-based
  ///   GetNodeOrNull type resolution proved unreliable under GoDotTest.
  ///   Null-safe: with no player wired, step 1 completes immediately.
  /// </summary>
  [Export] public PlayerController? Player;

  /// <summary>
  ///   Player stats whose thirst step 3 observes (kept for parity; the step
  ///   now completes on item consumption). Null-safe: step 3 still works via
  ///   <see cref="GameEvents.ItemRemoved"/>.
  /// </summary>
  [Export] public PlayerStats? Stats;

  #endregion Exports

  #region Step copy (Chinese, T8.3)

  private static readonly string[] StepTitles =
  {
    "移动", "采集", "喝水", "造篝火", "造床", "基础战斗"
  };

  private static readonly string[] StepBodies =
  {
    "按 WASD 移动",
    "走到树旁按 E 采集木头/椰子",
    "按 F 使用浆果/椰子解渴",
    "按 B 进入建造模式，放置篝火",
    "放置床",
    "对海蟹按左键攻击"
  };

  #endregion Step copy

  #region Tuning

  /// <summary>
  ///   Step 3 (drink): the items the step body tells the player to use. The
  ///   F key ("use_item") consumes exactly one, which raises ItemRemoved —
  ///   detected instead of thirst-gain because the tutorial player starts at
  ///   FULL thirst (100), so a "baseline + threshold" thirst check could
  ///   never fire (FIX(iter8): real soft-lock discovered in review).
  /// </summary>
  private static readonly string[] DrinkStepItemIds = { "berries", "coconut" };

  /// <summary>Step 1: meters the player must move from the step start.</summary>
  private const float MoveThresholdMeters = 2f;

  /// <summary>Dim overlay color (semi-transparent black).</summary>
  private static readonly Color DimColor = new(0f, 0f, 0f, 0.55f);

  /// <summary>Step card size (centered on screen).</summary>
  private static readonly Vector2 CardSize = new(420f, 200f);

  #endregion Tuning

  private readonly TutorialFlow _flow = CreateFlow();

  private Control? _dim;
  private PanelContainer? _stepCard;
  private Label? _stepTitle;
  private Label? _stepBody;
  private HBoxContainer? _dots;

  private PlayerController? _player;

  private Vector3 _stepStartPosition;

  /// <summary>True from tutorial start until all six steps are done.</summary>
  public bool IsTutorialActive { get; private set; }

  /// <summary>Current 1-based step (0 = not started, 7 = complete).</summary>
  public int CurrentStep => _flow.CurrentStep;

  /// <summary>The T8.3 step order (all externally driven, see class docs).</summary>
  private static TutorialFlow CreateFlow() => new(
    new TutorialFlow.Step("move", null),
    new TutorialFlow.Step("gather", null),
    new TutorialFlow.Step("drink", null),
    new TutorialFlow.Step("campfire", null),
    new TutorialFlow.Step("bed", null),
    new TutorialFlow.Step("combat", null)
  );

  public override void _Ready()
  {
    ProcessMode = ProcessModeEnum.Always;
    GameEvents.GameStarted += OnGameStarted;

    // Deferred fallback: GameManager raises GameStarted in its own _Ready,
    // which may run before ours — the deferred start covers both orders and
    // lets the tree settle so relative NodePaths resolve.
    CallDeferred(nameof(StartTutorial));
  }

  public override void _ExitTree()
  {
    UnsubscribeEvents();
  }

  public override void _Process(double delta)
  {
    if (!IsTutorialActive)
      return;

    // Step 1 (move): displacement from the step-start position.
    if (_flow.CurrentStep == 1 && _player != null)
    {
      if (_player.GlobalPosition.DistanceTo(_stepStartPosition) > MoveThresholdMeters)
        AdvanceFromEvent();
    }
  }

  /// <summary>
  ///   Starts the forced tutorial: resolves the exported references, builds
  ///   the UI, subscribes the GameEvents listeners and begins step 1.
  ///   Idempotent — safe to call from the GameStarted handler, the deferred
  ///   fallback and tests. Expects the node to be inside the tree so the
  ///   direct node references are usable.
  /// </summary>
  public void StartTutorial()
  {
    if (IsTutorialActive || _flow.IsComplete)
      return;

    _player = Player;

    IsTutorialActive = true;
    BuildUi();
    SubscribeEvents();
    _flow.Start();
    OnStepAdvanced();
  }

  /// <summary>
  ///   Debug/test shortcut: force-completes every remaining step and runs
  ///   the normal completion path (raises TutorialCompleted, hides the UI).
  /// </summary>
  public void SkipTutorial()
  {
    if (!IsTutorialActive)
      return;

    while (!_flow.IsComplete)
      _flow.CompleteCurrentStep();

    FinalizeTutorial();
  }

  #region Event translation

  private void OnGameStarted()
  {
    if (IsTutorialActive || _flow.IsComplete)
      return;

    CallDeferred(nameof(StartTutorial));
  }

  private void OnItemAdded(string itemId, int amount)
  {
    if (!IsTutorialActive || _flow.CurrentStep != 2)
      return;

    if (itemId is "wood" or "coconut")
      AdvanceFromEvent();
  }

  private void OnItemRemoved(string itemId, int amount)
  {
    // Step 3 (drink): pressing F on a thirst item consumes one unit. With
    // full thirst the stats never move, so item consumption is the reliable
    // completion signal.
    if (!IsTutorialActive || _flow.CurrentStep != 3)
      return;

    if (System.Array.IndexOf(DrinkStepItemIds, itemId) >= 0)
      AdvanceFromEvent();
  }

  private void OnBuildingPlaced(string buildableName)
  {
    if (!IsTutorialActive)
      return;

    if (_flow.CurrentStep == 4 && buildableName == "campfire")
      AdvanceFromEvent();
    else if (_flow.CurrentStep == 5 && buildableName == "bed")
      AdvanceFromEvent();
  }

  private void OnMeleeHit(string weaponId)
  {
    if (!IsTutorialActive || _flow.CurrentStep != 6)
      return;

    AdvanceFromEvent();
  }

  private void OnEnemyDied(string enemyId)
  {
    if (!IsTutorialActive || _flow.CurrentStep != 6)
      return;

    AdvanceFromEvent();
  }

  #endregion Event translation

  #region Sequencing

  private void SubscribeEvents()
  {
    GameEvents.ItemAdded += OnItemAdded;
    GameEvents.ItemRemoved += OnItemRemoved;
    GameEvents.BuildingPlaced += OnBuildingPlaced;
    GameEvents.MeleeHit += OnMeleeHit;
    GameEvents.EnemyDied += OnEnemyDied;
  }

  private void UnsubscribeEvents()
  {
    GameEvents.GameStarted -= OnGameStarted;
    GameEvents.ItemAdded -= OnItemAdded;
    GameEvents.ItemRemoved -= OnItemRemoved;
    GameEvents.BuildingPlaced -= OnBuildingPlaced;
    GameEvents.MeleeHit -= OnMeleeHit;
    GameEvents.EnemyDied -= OnEnemyDied;
  }

  private void AdvanceFromEvent()
  {
    _flow.CompleteCurrentStep();
    OnStepAdvanced();
  }

  private void OnStepAdvanced()
  {
    if (_flow.IsComplete)
    {
      FinalizeTutorial();
      return;
    }

    GameEvents.RaiseTutorialStepChanged(_flow.CurrentStep, TutorialFlow.TotalSteps);
    UpdateCard();
    ApplyStepAppearance();
    RecordStepBaselines();
  }

  private void RecordStepBaselines()
  {
    // Defensive: with an unwired player export the movement condition can
    // never fire — complete step 1 immediately instead of soft-locking the
    // forced tutorial.
    if (_flow.CurrentStep == 1)
    {
      _stepStartPosition = _player?.GlobalPosition ?? Vector3.Zero;
      if (_player == null)
        AdvanceFromEvent();
    }
  }

  private void FinalizeTutorial()
  {
    IsTutorialActive = false;
    UnsubscribeEvents();

    if (_dim != null)
      _dim.Visible = false;
    if (_stepCard != null)
      _stepCard.Visible = false;

    Input.MouseMode = Input.MouseModeEnum.Captured;
    GameEvents.RaiseTutorialCompleted();
  }

  private void ApplyStepAppearance()
  {
    // Combat (step 6) and the build steps (4-5) need real mouse clicks: the
    // dim must not swallow them. Steps 1-3 block the mouse (no interaction
    // is needed — blocking it also blocks the attack input). See the class
    // docs for why steps 4-5 use Ignore too.
    if (_dim != null)
    {
      _dim.MouseFilter = _flow.CurrentStep >= 4
        ? Control.MouseFilterEnum.Ignore
        : Control.MouseFilterEnum.Stop;
    }

    switch (_flow.CurrentStep)
    {
      case 4:
      case 5:
        // The building menu is mouse-driven (T8.3).
        Input.MouseMode = Input.MouseModeEnum.Visible;
        break;

      default:
        Input.MouseMode = Input.MouseModeEnum.Captured;
        break;
    }
  }

  #endregion Sequencing

  #region UI construction

  private void BuildUi()
  {
    // Full-screen dim: a Stop-filtered Control (blocks the mouse during the
    // keyboard steps) carrying a semi-transparent ColorRect.
    _dim = new Control { Name = "Dim" };
    _dim.MouseFilter = Control.MouseFilterEnum.Stop;
    _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);

    var dimRect = new ColorRect
    {
      Color = DimColor,
      MouseFilter = Control.MouseFilterEnum.Ignore
    };
    dimRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _dim.AddChild(dimRect);
    AddChild(_dim);

    // Centered step card. Both the card and the dim are Ignore from step 4
    // on so real clicks reach the building menu / 3D placement / attack.
    _stepCard = new PanelContainer { Name = "StepCard" };
    _stepCard.MouseFilter = Control.MouseFilterEnum.Ignore;
    _stepCard.CustomMinimumSize = CardSize;
    _stepCard.Size = CardSize;
    _stepCard.SetAnchorsPreset(Control.LayoutPreset.Center);
    _stepCard.Position = -CardSize / 2f;

    var cardStyle = new StyleBoxFlat
    {
      BgColor = new Color(0.08f, 0.08f, 0.1f, 0.92f),
      BorderColor = new Color(0.6f, 0.5f, 0.2f)
    };
    cardStyle.SetBorderWidthAll(2);
    cardStyle.SetCornerRadiusAll(10);
    _stepCard.AddThemeStyleboxOverride("panel", cardStyle);

    var vbox = new VBoxContainer { Name = "VBox" };
    vbox.AddThemeConstantOverride("separation", 12);

    _stepTitle = new Label { Name = "Title" };
    _stepTitle.HorizontalAlignment = HorizontalAlignment.Center;
    _stepTitle.AddThemeFontSizeOverride("font_size", 26);
    vbox.AddChild(_stepTitle);

    _stepBody = new Label { Name = "Body" };
    _stepBody.HorizontalAlignment = HorizontalAlignment.Center;
    _stepBody.AutowrapMode = TextServer.AutowrapMode.Word;
    _stepBody.AddThemeFontSizeOverride("font_size", 18);
    vbox.AddChild(_stepBody);

    _dots = new HBoxContainer { Name = "Dots" };
    _dots.Alignment = BoxContainer.AlignmentMode.Center;
    _dots.AddThemeConstantOverride("separation", 10);
    for (var i = 0; i < TutorialFlow.TotalSteps; i++)
      _dots.AddChild(CreateDot(i));
    vbox.AddChild(_dots);

    _stepCard.AddChild(vbox);
    AddChild(_stepCard);
  }

  private static Panel CreateDot(int index)
  {
    var dot = new Panel { Name = $"Dot{index}" };
    dot.CustomMinimumSize = new Vector2(16, 16);

    var style = new StyleBoxFlat { BgColor = new Color(0.25f, 0.25f, 0.3f) };
    style.SetCornerRadiusAll(8);
    dot.AddThemeStyleboxOverride("panel", style);

    return dot;
  }

  private void UpdateCard()
  {
    var index = _flow.CurrentStep - 1;
    if (_stepTitle != null)
      _stepTitle.Text = StepTitles[index];
    if (_stepBody != null)
      _stepBody.Text = StepBodies[index];

    UpdateDots();
  }

  private void UpdateDots()
  {
    if (_dots == null)
      return;

    for (var i = 0; i < TutorialFlow.TotalSteps; i++)
    {
      var dot = _dots.GetNodeOrNull<Panel>($"Dot{i}");
      if (dot?.GetThemeStylebox("panel") is not StyleBoxFlat style)
        continue;

      var stepNumber = i + 1;
      style.BgColor = stepNumber == _flow.CurrentStep
        ? new Color(0.95f, 0.75f, 0.25f) // active
        : stepNumber < _flow.CurrentStep
          ? new Color(0.45f, 0.6f, 0.45f) // done
          : new Color(0.25f, 0.25f, 0.3f); // pending
    }
  }

  #endregion UI construction
}
