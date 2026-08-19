// Original (Iter8.5) — no upstream port
namespace SeaAnomaly;

using Godot;

/// <summary>
///   Forced-tutorial overlay, extended in Iter8.5 (T8.5.10/T8.5.11) to run
///   THREE sequences on one CanvasLayer chrome:
///   <list type="bullet">
///     <item>
///       <b>chapter1</b> (default, 6 steps): the original T8.3 new-player
///       tutorial — move → gather → drink → campfire → bed → combat.
///       <see cref="StartTutorial()"/> keeps starting this sequence.
///     </item>
///     <item>
///       <b>chapter2</b> (2 steps, T8.5.10): plant a farm plot
///       (<see cref="GameEvents.CropPlanted"/>, any crop) then read the ruin
///       log (<see cref="GameEvents.StoryPointReached"/> == "ruin").
///       Triggered by <see cref="GameEvents.QuestStarted"/>("quest_harvest")
///       once chapter1 completed.
///     </item>
///     <item>
///       <b>chapter3</b> (1 step, T8.5.11): board the raft and sail away.
///       The step polls the player's distance from the world origin in
///       <see cref="_Process"/> (&gt; <see cref="SailDistanceMeters"/>),
///       deliberately requiring NO Raft node reference. Triggered by
///       <see cref="GameEvents.StoryPointReached"/>("shark_king") once
///       chapter1 completed.
///     </item>
///   </list>
///   A CanvasLayer whose children are built in C#: a full-screen dim (a
///   Stop-filtered Control carrying a semi-transparent ColorRect) and a
///   centered step card (title, body, step dots — one per step of the
///   active sequence). Sequencing lives in the pure <see cref="TutorialFlow"/>
///   state machine; this node translates GameEvents occurrences into
///   "current step done" and renders the card. The day/night cycle is
///   intentionally NOT paused during the tutorial (never sets
///   GetTree().Paused).
///
///   Completion raises <see cref="GameEvents.TutorialCompleted"/> for every
///   sequence — the gate CraftUI and other gated systems wait on (T8.3).
///
///   Step completion signals (all through the existing event bus):
///   1 move — the player moves &gt; 2 m from the step-start position (_Process);
///   2 gather — ItemAdded of wood/coconut;
///   3 drink — ItemRemoved of berries/coconut (the F key consumes one; a
///     thirst-gain check would never fire because the player starts at full
///     thirst, FIX(iter8));
///   4 campfire / 5 bed — BuildingPlaced with the matching buildable name;
///   6 combat — MeleeHit or EnemyDied.
///   chapter2 step 1 — CropPlanted (any crop);
///   chapter2 step 2 — StoryPointReached("ruin");
///   chapter3 step 1 — player distance from the origin &gt; 15 m (_Process).
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
///   but legitimate building input. Chapter 2/3 steps are all keyboard
///   driven (E to plant, E to read, WASD to sail) → Captured, dim Stop.
///
///   Wiring guards: <see cref="Player"/> is an optional direct node reference
///   (null-safe). When unwired, chapter1 step 1 completes immediately so a
///   missing wiring never soft-locks the forced tutorial; chapter3 step 1
///   (sail detection needs the player) does the same. Chapter2 is purely
///   event-driven and never depends on the player.
///
///   SUBSCRIBES GameStarted/QuestStarted/StoryPointReached ALWAYS (the
///   chapter triggers must be live while no tutorial is active — chapter2
///   fires on QuestStarted("quest_harvest"), chapter3 on
///   StoryPointReached("shark_king")), and ItemAdded/ItemRemoved/
///   BuildingPlaced/MeleeHit/EnemyDied/CropPlanted while a sequence is
///   active. Chapter-1 auto-start (GameStarted + the deferred fallback) is
///   gated by <see cref="AutoStartChapter1"/> — the product main menu sets
///   this false so the player chooses 新手教程 / 开始游戏. _ExitTree
///   unsubscribes everything (Decision 13); completion unsubscribes only
///   the active step events so later triggers stay live.
/// </summary>
public partial class TutorialUI : CanvasLayer
{
  #region Sequence ids

  /// <summary>Chapter 1: the original 6-step new-player tutorial.</summary>
  public const string Chapter1Sequence = "chapter1";

  /// <summary>Chapter 2: plant a farm plot, read the ruin log (T8.5.10).</summary>
  public const string Chapter2Sequence = "chapter2";

  /// <summary>Chapter 3: board the raft and sail away (T8.5.11).</summary>
  public const string Chapter3Sequence = "chapter3";

  #endregion Sequence ids

  #region Exports

  /// <summary>
  ///   Player whose movement completes step 1. Direct node reference (the
  ///   same wiring style as GameManager/SaveService) — NodePath-based
  ///   GetNodeOrNull type resolution proved unreliable under GoDotTest.
  ///   Null-safe: with no player wired, chapter1 step 1 (and chapter3
  ///   step 1) completes immediately.
  /// </summary>
  [Export] public PlayerController? Player;

  /// <summary>
  ///   Player stats whose thirst step 3 observes (kept for parity; the step
  ///   now completes on item consumption). Null-safe: step 3 still works via
  ///   <see cref="GameEvents.ItemRemoved"/>.
  /// </summary>
  [Export] public PlayerStats? Stats;

  /// <summary>
  ///   When true (the default, so existing tests keep working), chapter 1
  ///   starts from <see cref="_Ready"/> / <see cref="GameEvents.GameStarted"/>.
  ///   The product <c>Game.tscn</c> sets this false; the main menu then
  ///   calls <see cref="StartTutorial()"/> or
  ///   <see cref="CompleteChapter1WithoutPlaying"/>.
  /// </summary>
  [Export] public bool AutoStartChapter1 { get; set; } = true;

  /// <summary>
  ///   Optional island builder. Chapter-1 complete teleports the player from
  ///   the tutorial island back to the product spawn. Null-safe for tests.
  /// </summary>
  [Export] public IslandBuilder? IslandBuilder;

  #endregion Exports

  #region Step copy (Chinese, T8.3 + Iter8.5)

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

  /// <summary>Chapter 2 step titles (T8.5.10).</summary>
  private static readonly string[] Chapter2Titles =
  {
    "种一格农田", "阅读遗迹日志"
  };

  /// <summary>Chapter 2 step bodies (T8.5.10).</summary>
  private static readonly string[] Chapter2Bodies =
  {
    "手持种子，在农田格上按 E 种下一格作物",
    "前往遗迹，阅读石台上的日志"
  };

  /// <summary>Chapter 3 step title (T8.5.11).</summary>
  private static readonly string[] Chapter3Titles =
  {
    "登上木筏驶向远海"
  };

  /// <summary>Chapter 3 step body (T8.5.11).</summary>
  private static readonly string[] Chapter3Bodies =
  {
    "登上木筏，驶离海岸 15 米以上"
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

  /// <summary>
  ///   Chapter 3 step 1 (T8.5.11): meters the player must be from the world
  ///   origin — the raft departs from the spawn coast, so "sailed away"
  ///   needs no Raft node reference (the ship is a plain RigidBody3D in the
  ///   storm zone scene).
  /// </summary>
  private const float SailDistanceMeters = 15f;

  /// <summary>Dim overlay color (semi-transparent black).</summary>
  private static readonly Color DimColor = new(0f, 0f, 0f, 0.55f);

  /// <summary>Step card size (centered on screen).</summary>
  private static readonly Vector2 CardSize = new(420f, 200f);

  #endregion Tuning

  private TutorialFlow _flow = CreateFlow(Chapter1Sequence);

  /// <summary>Id of the sequence currently running ("" = none).</summary>
  private string _activeSequence = "";

  private bool _chapter1Done;
  private bool _chapter2Done;
  private bool _chapter3Done;

  private Control? _dim;
  private PanelContainer? _stepCard;
  private Label? _stepTitle;
  private Label? _stepBody;
  private HBoxContainer? _dots;

  private PlayerController? _player;

  private Vector3 _stepStartPosition;

  /// <summary>True from tutorial start until all steps of the active sequence are done.</summary>
  public bool IsTutorialActive { get; private set; }

  /// <summary>Current 1-based step of the active sequence (0 = not started, N+1 = complete).</summary>
  public int CurrentStep => _flow.CurrentStep;

  /// <summary>True once the given sequence finished (never runs twice).</summary>
  private bool IsSequenceDone(string sequenceId) => sequenceId switch
  {
    Chapter1Sequence => _chapter1Done,
    Chapter2Sequence => _chapter2Done,
    Chapter3Sequence => _chapter3Done,
    _ => false
  };

  /// <summary>
  ///   Builds the flow for a sequence: chapter1 = the T8.3 6-step order
  ///   (all externally driven), chapter2 = plant/read_log, chapter3 = sail.
  /// </summary>
  private static TutorialFlow CreateFlow(string sequenceId) => sequenceId switch
  {
    Chapter2Sequence => new TutorialFlow(
      2,
      new TutorialFlow.Step("plant", null),
      new TutorialFlow.Step("read_log", null)
    ),
    Chapter3Sequence => new TutorialFlow(
      1,
      new TutorialFlow.Step("sail", null)
    ),
    _ => new TutorialFlow(
      new TutorialFlow.Step("move", null),
      new TutorialFlow.Step("gather", null),
      new TutorialFlow.Step("drink", null),
      new TutorialFlow.Step("campfire", null),
      new TutorialFlow.Step("bed", null),
      new TutorialFlow.Step("combat", null)
    )
  };

  public override void _Ready()
  {
    ProcessMode = ProcessModeEnum.Always;
    // Always-on listeners: GameStarted (chapter1 auto-start) and the
    // chapter triggers, which fire while NO tutorial is active.
    GameEvents.GameStarted += OnGameStarted;
    GameEvents.QuestStarted += OnQuestStarted;
    GameEvents.StoryPointReached += OnStoryPointReached;

    // Deferred fallback: GameManager raises GameStarted in its own _Ready,
    // which may run before ours — the deferred start covers both orders and
    // lets the tree settle so relative NodePaths resolve. Gated so the
    // product main menu can own the first-run choice.
    if (AutoStartChapter1)
      CallDeferred(nameof(StartTutorial));
  }

  public override void _ExitTree()
  {
    GameEvents.GameStarted -= OnGameStarted;
    GameEvents.QuestStarted -= OnQuestStarted;
    GameEvents.StoryPointReached -= OnStoryPointReached;
    UnsubscribeEvents();
  }

  public override void _Process(double delta)
  {
    if (!IsTutorialActive)
      return;

    // Chapter 1 step 1 (move): displacement from the step-start position.
    if (_activeSequence == Chapter1Sequence && _flow.CurrentStep == 1 && _player != null)
    {
      if (_player.GlobalPosition.DistanceTo(_stepStartPosition) > MoveThresholdMeters)
        AdvanceFromEvent();
    }

    // Chapter 3 step 1 (sail): distance from the world origin — no Raft
    // reference needed (T8.5.11).
    if (_activeSequence == Chapter3Sequence && _flow.CurrentStep == 1 && _player != null)
    {
      if (_player.GlobalPosition.DistanceTo(Vector3.Zero) > SailDistanceMeters)
        AdvanceFromEvent();
    }
  }

  /// <summary>
  ///   Starts the default chapter-1 forced tutorial. Idempotent — safe to
  ///   call from the GameStarted handler, the deferred fallback and tests.
  /// </summary>
  public void StartTutorial() => StartTutorial(Chapter1Sequence);

  /// <summary>
  ///   Starts a forced-tutorial sequence by id (<see cref="Chapter1Sequence"/>,
  ///   <see cref="Chapter2Sequence"/> or <see cref="Chapter3Sequence"/>):
  ///   resolves the exported references, (re)builds the UI, subscribes the
  ///   active step events and begins step 1. Idempotent per sequence — a
  ///   completed sequence never restarts. Expects the node to be inside the
  ///   tree so the direct node references are usable.
  /// </summary>
  public void StartTutorial(string sequenceId)
  {
    if (IsTutorialActive || IsSequenceDone(sequenceId))
      return;

    _player = Player;
    _activeSequence = sequenceId;
    _flow = CreateFlow(sequenceId);

    IsTutorialActive = true;
    BuildUi();
    SubscribeEvents();
    _flow.Start();
    OnStepAdvanced();
  }

  /// <summary>
  ///   Debug/test shortcut: force-completes every remaining step of the
  ///   active sequence and runs the normal completion path (raises
  ///   TutorialCompleted, hides the UI).
  /// </summary>
  public void SkipTutorial()
  {
    if (!IsTutorialActive)
      return;

    while (!_flow.IsComplete)
      _flow.CompleteCurrentStep();

    FinalizeTutorial();
  }

  /// <summary>
  ///   Marks chapter 1 complete without showing the overlay (main-menu
  ///   "开始游戏" / load-save). Raises <see cref="GameEvents.TutorialCompleted"/>
  ///   so CraftUI unlocks, and opens the chapter 2/3 gates. Idempotent —
  ///   a second call is a no-op. If chapter 1 is already on-screen this
  ///   falls through to <see cref="SkipTutorial"/>.
  /// </summary>
  public void CompleteChapter1WithoutPlaying()
  {
    if (_chapter1Done)
      return;

    if (IsTutorialActive && _activeSequence == Chapter1Sequence)
    {
      SkipTutorial();
      return;
    }

    _chapter1Done = true;
    GameEvents.RaiseTutorialCompleted();
  }

  #region Event translation

  private void OnGameStarted()
  {
    if (!AutoStartChapter1 || IsTutorialActive || _chapter1Done)
      return;

    CallDeferred(nameof(StartTutorial));
  }

  private void OnItemAdded(string itemId, int amount)
  {
    if (!IsTutorialActive || _activeSequence != Chapter1Sequence || _flow.CurrentStep != 2)
      return;

    if (itemId is "wood" or "coconut")
      AdvanceFromEvent();
  }

  private void OnItemRemoved(string itemId, int amount)
  {
    // Step 3 (drink): pressing F on a thirst item consumes one unit. With
    // full thirst the stats never move, so item consumption is the reliable
    // completion signal.
    if (!IsTutorialActive || _activeSequence != Chapter1Sequence || _flow.CurrentStep != 3)
      return;

    if (System.Array.IndexOf(DrinkStepItemIds, itemId) >= 0)
      AdvanceFromEvent();
  }

  private void OnBuildingPlaced(string buildableName)
  {
    if (!IsTutorialActive || _activeSequence != Chapter1Sequence)
      return;

    if (_flow.CurrentStep == 4 && buildableName == "campfire")
      AdvanceFromEvent();
    else if (_flow.CurrentStep == 5 && buildableName == "bed")
      AdvanceFromEvent();
  }

  private void OnMeleeHit(string weaponId)
  {
    if (!IsTutorialActive || _activeSequence != Chapter1Sequence || _flow.CurrentStep != 6)
      return;

    AdvanceFromEvent();
  }

  private void OnEnemyDied(string enemyId)
  {
    if (!IsTutorialActive || _activeSequence != Chapter1Sequence || _flow.CurrentStep != 6)
      return;

    AdvanceFromEvent();
  }

  /// <summary>
  ///   Iter8.5 chapter 2 step 1 (plant): ANY crop completes it.
  /// </summary>
  private void OnCropPlanted(string cropId)
  {
    if (!IsTutorialActive || _activeSequence != Chapter2Sequence || _flow.CurrentStep != 1)
      return;

    AdvanceFromEvent();
  }

  /// <summary>
  ///   Iter8.5: completes chapter 2 step 2 on the ruin log
  ///   (StoryPointReached("ruin")) and — when no tutorial is active and
  ///   chapter1 finished — starts chapter 3 on the shark-king story point.
  ///   Subscribed always (from _Ready) so the chapter3 trigger can never be
  ///   missed: it fires while no tutorial is running.
  /// </summary>
  private void OnStoryPointReached(string storyPointId)
  {
    // Chapter 2 step 2: reading the ruin log.
    if (
      IsTutorialActive
      && _activeSequence == Chapter2Sequence
      && _flow.CurrentStep == 2
      && storyPointId == "ruin"
    )
    {
      AdvanceFromEvent();
      return;
    }

    // Chapter 3 trigger (T8.5.11).
    if (!IsTutorialActive && _chapter1Done && !_chapter3Done && storyPointId == "shark_king")
      StartTutorial(Chapter3Sequence);
  }

  /// <summary>
  ///   Iter8.5 chapter 2 trigger: the harvest quest starts the farming
  ///   tutorial, but only after the chapter-1 tutorial completed (the forced
  ///   new-player tutorial must finish first).
  /// </summary>
  private void OnQuestStarted(string questId)
  {
    if (!IsTutorialActive && _chapter1Done && !_chapter2Done && questId == "quest_harvest")
      StartTutorial(Chapter2Sequence);
  }

  #endregion Event translation

  #region Sequencing

  /// <summary>Active-sequence step events (unsubscribed on completion).</summary>
  private void SubscribeEvents()
  {
    GameEvents.ItemAdded += OnItemAdded;
    GameEvents.ItemRemoved += OnItemRemoved;
    GameEvents.BuildingPlaced += OnBuildingPlaced;
    GameEvents.MeleeHit += OnMeleeHit;
    GameEvents.EnemyDied += OnEnemyDied;
    GameEvents.CropPlanted += OnCropPlanted;
  }

  private void UnsubscribeEvents()
  {
    GameEvents.ItemAdded -= OnItemAdded;
    GameEvents.ItemRemoved -= OnItemRemoved;
    GameEvents.BuildingPlaced -= OnBuildingPlaced;
    GameEvents.MeleeHit -= OnMeleeHit;
    GameEvents.EnemyDied -= OnEnemyDied;
    GameEvents.CropPlanted -= OnCropPlanted;
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

    GameEvents.RaiseTutorialStepChanged(_flow.CurrentStep, _flow.StepCount);
    UpdateCard();
    ApplyStepAppearance();
    RecordStepBaselines();
  }

  private void RecordStepBaselines()
  {
    if (_flow.CurrentStep != 1)
      return;

    if (_activeSequence == Chapter1Sequence)
    {
      // Defensive: with an unwired player export the movement condition can
      // never fire — complete step 1 immediately instead of soft-locking the
      // forced tutorial.
      _stepStartPosition = _player?.GlobalPosition ?? Vector3.Zero;
      if (_player == null)
        AdvanceFromEvent();
    }
    else if (_activeSequence == Chapter3Sequence && _player == null)
    {
      // Sail detection needs the player; never soft-lock on a missing wiring.
      AdvanceFromEvent();
    }
    // Chapter2 step 1 is purely event-driven — no baseline, no player.
  }

  private void FinalizeTutorial()
  {
    IsTutorialActive = false;

    var finished = _activeSequence;
    if (finished == Chapter1Sequence)
      _chapter1Done = true;
    else if (finished == Chapter2Sequence)
      _chapter2Done = true;
    else if (finished == Chapter3Sequence)
      _chapter3Done = true;
    _activeSequence = "";

    UnsubscribeEvents();

    if (_dim != null)
      _dim.Visible = false;
    if (_stepCard != null)
      _stepCard.Visible = false;

    Input.MouseMode = Input.MouseModeEnum.Captured;

    // Chapter 1 lived on the dedicated tutorial island; hand the player to
    // the product spawn (Main / PlayerSpawnPoint) before unlocking CraftUI.
    if (finished == Chapter1Sequence)
      IslandBuilder?.EndTutorialSession();

    GameEvents.RaiseTutorialCompleted();
  }

  private void ApplyStepAppearance()
  {
    // Combat (step 6) and the build steps (4-5) need real mouse clicks: the
    // dim must not swallow them. Steps 1-3 block the mouse (no interaction
    // is needed — blocking it also blocks the attack input). See the class
    // docs for why steps 4-5 use Ignore too. Chapter 2/3 steps are 1-2:
    // keyboard driven → dim Stop, mouse Captured.
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
    if (_dim == null)
    {
      // Full-screen dim: a Stop-filtered Control (blocks the mouse during
      // the keyboard steps) carrying a semi-transparent ColorRect.
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
      vbox.AddChild(_dots);

      _stepCard.AddChild(vbox);
      AddChild(_stepCard);
    }

    // Re-entry (a later chapter's sequence): re-show the chrome and rebuild
    // the step dots for the new sequence's step count.
    _dim.Visible = true;
    if (_stepCard != null)
      _stepCard.Visible = true;
    RebuildDots();
  }

  /// <summary>
  ///   Recreates the step dots so their count matches the active sequence
  ///   (chapter1: 6, chapter2: 2, chapter3: 1).
  /// </summary>
  private void RebuildDots()
  {
    if (_dots == null)
      return;

    foreach (var child in _dots.GetChildren())
    {
      _dots.RemoveChild(child);
      child.Free();
    }

    for (var i = 0; i < _flow.StepCount; i++)
      _dots.AddChild(CreateDot(i));
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

    var titles = _activeSequence switch
    {
      Chapter2Sequence => Chapter2Titles,
      Chapter3Sequence => Chapter3Titles,
      _ => StepTitles
    };
    var bodies = _activeSequence switch
    {
      Chapter2Sequence => Chapter2Bodies,
      Chapter3Sequence => Chapter3Bodies,
      _ => StepBodies
    };

    if (_stepTitle != null)
      _stepTitle.Text = titles[index];
    if (_stepBody != null)
      _stepBody.Text = bodies[index];

    UpdateDots();
  }

  private void UpdateDots()
  {
    if (_dots == null)
      return;

    for (var i = 0; i < _flow.StepCount; i++)
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
