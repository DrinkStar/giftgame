// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System.Linq;
using Godot;

/// <summary>
///   T8.4 minimal crafting UI: a full-screen modal panel listing every
///   currently craftable recipe as a button (pure C# construction, no scene
///   dependencies). Pressing a button starts the craft through
///   <see cref="CraftingSystem.StartCrafting"/>.
///
///   Behavior contract (iter8-plan T8.4):
///   - C ("crafting") toggles the panel; Esc ("ui_cancel") closes it. The
///     C-key path only reacts after the forced tutorial finished
///     (<see cref="GameEvents.TutorialCompleted"/>); before that it shows a
///     guide hint once. The gameplay-input lock raised while open belongs to
///     this panel, so a locked state held by ANOTHER panel blocks opening
///     but never blocks closing with C. FIX(code-review): the tutorial does
///     NOT hold this lock — it gates interaction through its own full-screen
///     dim + the TutorialCompleted gate on C, and relies on event-driven
///     steps, so "e.g. the tutorial" in earlier docs was wrong.
///   - Opening switches the mouse to Visible and raises
///     <see cref="GameEvents.GameplayInputLockChanged"/>(true) so polled
///     consumers (move/attack/build/use) ignore input; closing reverses both.
///     The full-screen backdrop (Control + dimmer, both MouseFilter.Stop)
///     swallows mouse clicks before _UnhandledInput consumers (WeaponSystem
///     attacks) see them.
///   - The button list is rebuilt from
///     <see cref="CraftingSystem.GetAvailableRecipes"/> whenever the panel
///     opens, and while it is open whenever InventoryChanged /
///     CraftingStarted / CraftingCompleted / CraftingFailed fire.
///
///   Fail-closed: a null or unwired <see cref="Crafting"/> never crashes —
///   the panel opens with the empty-list hint. SUBSCRIBES five GameEvents;
///   unsubscribes all of them in _ExitTree (Decision 13).
/// </summary>
public partial class CraftUI : CanvasLayer
{
  private const string GateHintText = "完成新手教程后解锁合成";
  private const string EmptyHintText = "没有可合成的配方（靠近篝火/工作台可解锁更多）";

  /// <summary>
  ///   Recipe source. Wired from Game.tscn by the main orchestrator (the
  ///   scene file carries only the root node; everything else is built in
  ///   code). Null = fail-closed (panel opens but shows the empty hint).
  /// </summary>
  [Export] public CraftingSystem? Crafting { get; set; }

  /// <summary>Whether the crafting panel is currently open.</summary>
  public bool IsOpen { get; private set; }

  private VBoxContainer? _recipeList;
  private Label? _hintLabel;
  private bool _tutorialDone;
  private bool _gateHintShown;

  public override void _Ready()
  {
    BuildUi();
    Visible = false;
    Subscribe();
  }

  public override void _ExitTree() => Unsubscribe();

  public override void _Input(InputEvent @event)
  {
    if (@event.IsActionPressed("crafting"))
    {
      // The gameplay-input lock is held by another modal (e.g. the forced
      // tutorial). CraftUI's own lock (set while open) must not block
      // closing again with C, so a held lock only counts when this panel
      // is not the one holding it.
      if (GameEvents.GameplayInputLocked && !IsOpen)
        return;

      if (!_tutorialDone)
      {
        if (!_gateHintShown)
        {
          _gateHintShown = true;
          GameEvents.RaiseGuideLine(GateHintText);
        }

        return;
      }

      Toggle();
    }
    else if (IsOpen && @event.IsActionPressed("ui_cancel"))
    {
      Close();
      GetViewport().SetInputAsHandled();
    }
  }

  /// <summary>Opens the panel when closed, closes it when open (tests / external callers).</summary>
  public void Toggle()
  {
    if (IsOpen)
      Close();
    else
      Open();
  }

  /// <summary>
  ///   Rebuilds the recipe button list from
  ///   <see cref="CraftingSystem.GetAvailableRecipes"/>. Idempotent: previous
  ///   buttons are detached and queued for deletion before new ones are added.
  /// </summary>
  public void Refresh()
  {
    // FIX(iter8): the static event bus can deliver a call after this node was
    // freed (cross-test/teardown edge) — never touch a disposed child.
    if (_recipeList == null || !IsInstanceValid(_recipeList))
      return;

    // Detach immediately so GetChildCount is accurate within the same frame
    // (tests assert on it); QueueFree reclaims the nodes at frame end.
    foreach (var child in _recipeList.GetChildren())
    {
      _recipeList.RemoveChild(child);
      child.QueueFree();
    }

    var crafting = Crafting;
    if (crafting == null || crafting.GetAvailableRecipes().Count == 0)
    {
      if (_hintLabel != null)
        _hintLabel.Visible = true;
      return;
    }

    if (_hintLabel != null)
      _hintLabel.Visible = false;

    foreach (var recipe in crafting.GetAvailableRecipes())
    {
      var button = new Button
      {
        Text = BuildButtonText(recipe),
        Disabled = !crafting.CanCraft(recipe)
      };
      button.Pressed += () => crafting.StartCrafting(recipe);
      _recipeList.AddChild(button);
    }
  }

  /// <summary>
  ///   Test hook: unlocks the C-key tutorial gate without raising
  ///   <see cref="GameEvents.TutorialCompleted"/>.
  /// </summary>
  public void UnlockForTests() => _tutorialDone = true;

  private void Open()
  {
    // FIX(code-review P2-30): mutual-exclusion guard on the open path itself
    // (mirrors StorageUI.Open) — the _Input path already refuses under a held
    // lock, but Toggle() is a public entry point (tests/external callers) and
    // must fail closed too: never stack a second modal on a held lock.
    if (GameEvents.GameplayInputLocked)
      return;

    IsOpen = true;
    Visible = true;
    Input.MouseMode = Input.MouseModeEnum.Visible;
    GameEvents.RaiseGameplayInputLockChanged(true);
    Refresh();
  }

  private void Close()
  {
    IsOpen = false;
    Visible = false;
    Input.MouseMode = Input.MouseModeEnum.Captured;
    // FIX(code-review): release the gameplay-input lock DEFERRED — _Input
    // dispatches in reverse tree order, so GameManager sees this node's Esc
    // handling first; if the lock dropped synchronously, GameManager would
    // then toggle pause in the SAME input pass (Esc = close panel + pause).
    CallDeferred(nameof(ReleaseInputLock));
  }

  private void ReleaseInputLock() => GameEvents.RaiseGameplayInputLockChanged(false);

  private void Subscribe()
  {
    GameEvents.InventoryChanged += OnInventoryChanged;
    GameEvents.CraftingStarted += OnCraftingStateChanged;
    GameEvents.CraftingCompleted += OnCraftingStateChanged;
    GameEvents.CraftingFailed += OnCraftingStateChanged;
    GameEvents.TutorialCompleted += OnTutorialCompleted;
  }

  private void Unsubscribe()
  {
    GameEvents.InventoryChanged -= OnInventoryChanged;
    GameEvents.CraftingStarted -= OnCraftingStateChanged;
    GameEvents.CraftingCompleted -= OnCraftingStateChanged;
    GameEvents.CraftingFailed -= OnCraftingStateChanged;
    GameEvents.TutorialCompleted -= OnTutorialCompleted;
  }

  private void OnInventoryChanged()
  {
    if (IsOpen)
      Refresh();
  }

  private void OnCraftingStateChanged(string recipeId)
  {
    if (IsOpen)
      Refresh();
  }

  private void OnTutorialCompleted() => _tutorialDone = true;

  /// <summary>
  ///   Button label: "DisplayName (IngredientNamexAmount, ...)" with the
  ///   ingredient name falling back to the item id, then "?".
  /// </summary>
  private static string BuildButtonText(CraftingRecipe recipe)
  {
    var ingredients = string.Join(
      ", ",
      recipe.Ingredients.Select(ingredient =>
      {
        var name = ingredient.Item?.DisplayName ?? ingredient.Item?.Id ?? "?";
        return $"{name}x{ingredient.Amount}";
      })
    );

    return $"{recipe.DisplayName} ({ingredients})";
  }

  /// <summary>
  ///   Builds the whole UI tree in C# (the .tscn only carries the root
  ///   node): full-screen dimmed backdrop -> centered panel with the title,
  ///   a scrollable recipe list and the empty-list hint.
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

    // Semi-transparent dimmer. The whole Control chain uses
    // MouseFilter.Stop so mouse clicks are swallowed before _UnhandledInput
    // consumers (WeaponSystem attack/use) see them; keyboard input keeps
    // flowing through _Input.
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
      Text = "CRAFTING (C to close)",
      HorizontalAlignment = HorizontalAlignment.Center
    };
    title.AddThemeFontSizeOverride("font_size", 22);
    vbox.AddChild(title);

    var scroll = new ScrollContainer
    {
      Name = "Scroll",
      CustomMinimumSize = new Vector2(420, 320)
    };
    vbox.AddChild(scroll);

    var recipeList = new VBoxContainer
    {
      Name = "RecipeList",
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
    };
    scroll.AddChild(recipeList);

    var hint = new Label
    {
      Name = "HintLabel",
      Text = EmptyHintText,
      HorizontalAlignment = HorizontalAlignment.Center,
      Visible = false
    };
    hint.AutowrapMode = TextServer.AutowrapMode.Word;
    vbox.AddChild(hint);

    _recipeList = recipeList;
    _hintLabel = hint;
  }
}
