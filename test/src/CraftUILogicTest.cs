// Original (Iter8) — no upstream port
namespace SeaAnomaly;

using System;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Chickensoft.GodotTestDriver.Util;
using Godot;
using Shouldly;

/// <summary>
///   T8.4 logic tests for the minimal crafting UI
///   (<see cref="CraftUI"/>). Recipe filtering itself is covered by
///   <see cref="CraftingSystemTest"/>; these tests focus on the UI's own
///   state machine: open/close + the gameplay-input lock, the C-key
///   tutorial gate, event-driven refresh, the station gate surfacing as
///   list membership/button enablement, and fail-closed behavior with a
///   null <see cref="CraftingSystem"/>. Nodes live in the tree root
///   (Fixture, like CraftingSystemTest); recipes are the real .tres assets
///   and no wall-clock timing is involved.
/// </summary>
public class CraftUILogicTest : TestClass, IDisposable
{
  private Fixture _fixture = default!;
  private InventorySystem _inventory = default!;
  private CraftingSystem _crafting = default!;
  private CraftUI _craftUI = default!;

  public CraftUILogicTest(Node testScene) : base(testScene) { }

  [Setup]
  public async Task Setup()
  {
    _fixture = new Fixture(TestScene.GetTree());

    _inventory = new InventorySystem();
    _crafting = new CraftingSystem();
    await _fixture.AddToRoot(_inventory, autoRemoveFromRoot: true);
    await _fixture.AddToRoot(_crafting, autoRemoveFromRoot: true);
    _crafting.Initialize(_inventory);

    _craftUI = new CraftUI { Crafting = _crafting };
    await _fixture.AddToRoot(_craftUI, autoRemoveFromRoot: true);
  }

  [Cleanup]
  public void Cleanup()
  {
    // Static lock hygiene (plan Decision 13): never leak a held
    // gameplay-input lock into the next test case.
    GameEvents.RaiseGameplayInputLockChanged(false);
    _fixture.Cleanup();
    Dispose();
  }

  /// <summary>
  ///   GoDotTest drives <see cref="Cleanup"/> per test; Dispose mirrors it so
  ///   the disposable node fields satisfy CA1001.
  /// </summary>
  public void Dispose()
  {
    if (_craftUI == null)
      return;

    _craftUI.Dispose();
    _craftUI = null!;
    _crafting?.Dispose();
    _crafting = null!;
    _inventory?.Dispose();
    _inventory = null!;
    GC.SuppressFinalize(this);
  }

  private static CraftingRecipe LoadRecipe(string id) =>
    GD.Load<CraftingRecipe>($"res://assets/recipes/{id}.tres");

  private static ItemData LoadItem(string id) =>
    GD.Load<ItemData>($"res://assets/items/{id}.tres");

  private VBoxContainer RecipeList =>
    _craftUI.GetNodeOrNull<VBoxContainer>("Backdrop/Center/Panel/VBox/Scroll/RecipeList")!;

  private Label HintLabel =>
    _craftUI.GetNodeOrNull<Label>("Backdrop/Center/Panel/VBox/HintLabel")!;

  private void Press(string action) =>
    _craftUI._Input(new InputEventAction { Action = action, Pressed = true });

  /// <summary>① default state: closed, hidden, and no input lock held.</summary>
  [Test]
  public void DefaultStateIsClosedAndHidden()
  {
    _craftUI.IsOpen.ShouldBeFalse();
    _craftUI.Visible.ShouldBeFalse();
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }

  /// <summary>
  ///   ② Toggle acquires the gameplay-input lock when opening and releases
  ///   it when closing. The release is DEFERRED (Esc in the same input pass
  ///   must not let GameManager pause), so the test awaits a frame.
  /// </summary>
  [Test]
  public async Task ToggleAcquiresAndReleasesGameplayInputLock()
  {
    _craftUI.Toggle();

    _craftUI.IsOpen.ShouldBeTrue();
    _craftUI.Visible.ShouldBeTrue();
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    _craftUI.Toggle();

    _craftUI.IsOpen.ShouldBeFalse();
    _craftUI.Visible.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }

  /// <summary>
  ///   ③ Refresh builds exactly one button per available recipe and is
  ///   idempotent (a second Refresh must not duplicate buttons).
  /// </summary>
  [Test]
  public void RefreshBuildsOneButtonPerAvailableRecipe()
  {
    _crafting.Recipes.Add(LoadRecipe("stone_axe"));
    _inventory.AddItem(LoadItem("wood"), 2);
    _inventory.AddItem(LoadItem("stone"), 1);

    _crafting.GetAvailableRecipes().Count.ShouldBe(1);

    _craftUI.Refresh();
    RecipeList.GetChildCount().ShouldBe(1);
    HintLabel.Visible.ShouldBeFalse();

    _craftUI.Refresh();
    RecipeList.GetChildCount().ShouldBe(1);

    var button = RecipeList.GetChild<Button>(0);
    button.Text.ShouldContain("Stone Axe");
    button.Text.ShouldContain("Woodx2");
    button.Text.ShouldContain("Stonex1");
    button.Disabled.ShouldBeFalse();
  }

  /// <summary>
  ///   ④ the C key is ignored until the tutorial unlocks it
  ///   (<see cref="CraftUI.UnlockForTests"/>), then toggles open and closed.
  /// </summary>
  [Test]
  public void CraftingKeyIgnoredUntilTutorialUnlocked()
  {
    Press("crafting");
    _craftUI.IsOpen.ShouldBeFalse();

    _craftUI.UnlockForTests();
    Press("crafting");
    _craftUI.IsOpen.ShouldBeTrue();

    // C toggles closed again.
    Press("crafting");
    _craftUI.IsOpen.ShouldBeFalse();
  }

  /// <summary>
  ///   ④ the tutorial-gate guide hint is raised at most once, even when the
  ///   locked key is pressed repeatedly.
  /// </summary>
  [Test]
  public void CraftingKeyShowsGateHintOnlyOnce()
  {
    var guideLines = 0;
    Action<string> onGuideLine = _ => guideLines++;
    GameEvents.GuideLine += onGuideLine;
    try
    {
      Press("crafting");
      Press("crafting");
      Press("crafting");

      guideLines.ShouldBe(1);
      _craftUI.IsOpen.ShouldBeFalse();
    }
    finally
    {
      GameEvents.GuideLine -= onGuideLine;
    }
  }

  /// <summary>The real TutorialCompleted event unlocks the C key.</summary>
  [Test]
  public void TutorialCompletedEventUnlocksCraftingKey()
  {
    GameEvents.RaiseTutorialCompleted();

    Press("crafting");

    _craftUI.IsOpen.ShouldBeTrue();
  }

  /// <summary>
  ///   The C key is ignored while ANOTHER modal holds the gameplay-input
  ///   lock (e.g. the forced tutorial), and works again once it is released.
  /// </summary>
  [Test]
  public void CraftingKeyIgnoredWhileAnotherModalHoldsLock()
  {
    _craftUI.UnlockForTests();

    GameEvents.RaiseGameplayInputLockChanged(true);
    try
    {
      Press("crafting");
      _craftUI.IsOpen.ShouldBeFalse();

      GameEvents.RaiseGameplayInputLockChanged(false);
      Press("crafting");
      _craftUI.IsOpen.ShouldBeTrue();
    }
    finally
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
      _craftUI.Toggle();
    }
  }

  /// <summary>
  ///   An open panel refreshes when InventoryChanged fires: adding the
  ///   missing materials turns the empty hint into a button without an
  ///   explicit Refresh call.
  /// </summary>
  [Test]
  public void InventoryChangedRefreshesOpenPanel()
  {
    _crafting.Recipes.Add(LoadRecipe("stone_axe"));
    _craftUI.UnlockForTests();
    Press("crafting");

    RecipeList.GetChildCount().ShouldBe(0);
    HintLabel.Visible.ShouldBeTrue();

    _inventory.AddItem(LoadItem("wood"), 2);
    _inventory.AddItem(LoadItem("stone"), 1);

    RecipeList.GetChildCount().ShouldBe(1);
    HintLabel.Visible.ShouldBeFalse();
  }

  /// <summary>
  ///   Pressing a recipe button starts the craft (materials consumed);
  ///   the CraftingStarted refresh then drops the button because the
  ///   ingredients are gone.
  /// </summary>
  [Test]
  public void PressingButtonStartsCraftingAndRefreshesList()
  {
    _crafting.Recipes.Add(LoadRecipe("stone_axe"));
    _inventory.AddItem(LoadItem("wood"), 2);
    _inventory.AddItem(LoadItem("stone"), 1);
    _craftUI.UnlockForTests();
    Press("crafting");

    var button = RecipeList.GetChild<Button>(0);
    button.EmitSignal(Button.SignalName.Pressed);

    _inventory.GetItemCount("wood").ShouldBe(0);
    RecipeList.GetChildCount().ShouldBe(0);
    HintLabel.Visible.ShouldBeTrue();
  }

  /// <summary>
  ///   ⑤ station gating surfaces in the list: away from the campfire the
  ///   cooked_meat recipe is filtered out entirely (hint shown); near it the
  ///   button is listed and enabled.
  /// </summary>
  [Test]
  public void CampfireGateFiltersRecipesAndEnablesButtonWhenNear()
  {
    _crafting.Recipes.Add(LoadRecipe("cooked_meat"));
    _inventory.AddItem(LoadItem("raw_meat"), 1);

    _crafting.IsNearCampfire = false;
    _craftUI.Refresh();
    RecipeList.GetChildCount().ShouldBe(0);
    HintLabel.Visible.ShouldBeTrue();

    _crafting.IsNearCampfire = true;
    _craftUI.Refresh();
    RecipeList.GetChildCount().ShouldBe(1);
    HintLabel.Visible.ShouldBeFalse();

    var button = RecipeList.GetChild<Button>(0);
    button.Text.ShouldContain("Cooked Meat");
    button.Text.ShouldContain("Raw Meatx1");
    button.Disabled.ShouldBeFalse();
  }

  /// <summary>
  ///   ui_cancel closes an open panel (and must not throw from
  ///   SetInputAsHandled); pressing it while closed changes nothing. The lock
  ///   release is deferred to the next frame (Esc must not pause in the same
  ///   pass), so the assertion awaits one.
  /// </summary>
  [Test]
  public async Task UiCancelClosesOpenPanel()
  {
    _craftUI.Toggle();
    _craftUI.IsOpen.ShouldBeTrue();

    Press("ui_cancel");
    _craftUI.IsOpen.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();

    Press("ui_cancel");
    _craftUI.IsOpen.ShouldBeFalse();
  }

  /// <summary>
  ///   Fail-closed: with a null/unwired CraftingSystem the panel still opens
  ///   and shows the empty hint instead of crashing.
  /// </summary>
  [Test]
  public async Task NullCraftingIsFailClosed()
  {
    _craftUI.Crafting = null;

    _craftUI.Toggle();

    _craftUI.IsOpen.ShouldBeTrue();
    RecipeList.GetChildCount().ShouldBe(0);
    HintLabel.Visible.ShouldBeTrue();

    _craftUI.Toggle();
    _craftUI.IsOpen.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }

  /// <summary>
  ///   FIX(code-review P2-30): mutual-exclusion guard on the OPEN path — while
  ///   another modal holds the gameplay-input lock, Toggle (public entry)
  ///   must fail closed instead of stacking a second modal. Closing is always
  ///   allowed (the lock belongs to this panel then).
  /// </summary>
  [Test]
  public async Task ToggleRefusesToOpenWhileAnotherModalHoldsLock()
  {
    GameEvents.RaiseGameplayInputLockChanged(true);
    try
    {
      _craftUI.Toggle();

      _craftUI.IsOpen.ShouldBeFalse();
      _craftUI.Visible.ShouldBeFalse();
    }
    finally
    {
      GameEvents.RaiseGameplayInputLockChanged(false);
    }

    // Once the lock is released, Toggle opens normally.
    _craftUI.Toggle();
    _craftUI.IsOpen.ShouldBeTrue();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeTrue();

    // Closing with C while holding our own lock still works.
    _craftUI.Toggle();
    _craftUI.IsOpen.ShouldBeFalse();
    await TestScene.ProcessFrame(2);
    GameEvents.GameplayInputLocked.ShouldBeFalse();
  }
}
