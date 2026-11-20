# GameEvents 收敛实施计划（方案 B）

> **For agentic workers:** REQUIRED SUB-SKILL: 用它自己的方式逐任务执行本计划。步骤用 `- [ ]` 勾选跟踪。

**Goal:** 让 `GameEvents` 每个事件只有一个语义、都有真实发送方与接收方，并用自动化测试守住订阅/解绑配对。

**Architecture:** 三个提交：① 拆出投射物/远程语义（新增 `ProjectileHit`/`RangedAttack`，抽出纯静态 `CombatFeedback` 分派）；② 删 5 个死事件 + 给 `CraftingProgress`/`GameSaved`/`GameLoaded` 接上 UI；③ 新增 `GameEventsHygieneTest` 做静态扫描守卫。

**Tech Stack:** Godot 4.7.1 mono / C# net8.0 / Chickensoft.GoDotTest + Shouldly。

**Spec:** `game/.godot-dev/specs/2026-11-17-gameevents-convergence-design.md`

## Global Constraints

- Godot **不会**自动重编译 C#：每次改完代码先 `dotnet build SeaAnomaly.csproj -v minimal`，再跑测试。
- 测试命令固定：`godot --headless --path . --run-tests --quit-on-finish`（缺 `--quit-on-finish` 会挂住）。
- 节点访问一律 `GetNodeOrNull<T>` + `IsInstanceValid`，禁止 `GetNode<T>` 断言。
- 静态事件订阅必须在 `_ExitTree` 成对解绑（契约「事件总线 `_ExitTree` 手动解绑」）。
- `GameOver` 只由真结局 raise，死亡路径禁止 raise。
- 测试类放 `test/src/*.cs` 即可被自动发现（无注册表；`test/**/*.cs` 仅在 Debug/RUN_TESTS 下编译）。
- 基线：`dotnet build` 0 error（79 warning），测试 484 passed / 0 failed。
- 提交在 `game/` 自己的 git 仓库进行。

## Review Focus

1. **投矛命中后教程不得再推进**（`TutorialUI` 第 6 步只认 `MeleeHit`）——Task 2 用「投射物命中只发 `ProjectileHit`」的断言钉住。
2. **弓与投掷的音效不得互串**：弓→`bow_shoot`，投掷→`melee_swing`（保持现状）——Task 2 用 `SfxHook.RangedSfxId` 纯函数断言钉住。
3. **HUD 未接线 Player 时（`Initialize` 从未调用）保存提示不能静默失效或崩**——Task 4 把订阅放在 `_Ready` 而非 `Initialize`，测试直接事件驱动。
4. **取消制作 / 完成后重开面板不得残留满格进度条**——Task 4 断言 `CraftingProgress(0)` 与 `CraftingCompleted` 都隐藏进度条。
5. **场景切换后不得遗留静态订阅导致重复回调**——Task 5 的文件级 `+=`/`-=` 计数守卫钉住。

---

### Task 1: `CombatFeedback` 纯分派 seam

**Files:**
- Create: `src/combat/CombatFeedback.cs`
- Test: `test/src/CombatFeedbackTest.cs`

**Interfaces:**
- Consumes: `AttackType`（`src/combat/CombatLogic.cs:10`，值 `Melee`/`Throw`/`Shoot`）。
- Produces: `public static void CombatFeedback.RaiseAttack(AttackType attack, string itemId)`；`Melee` → `GameEvents.RaiseAttackSwing(itemId)`，其余 → `GameEvents.RaiseRangedAttack(itemId)`。

- [ ] **Step 1: 先加 `RangedAttack` 事件与 helper** `src/core/GameEvents.cs`

在 `public static event Action<string>? AttackSwing;` 之后：

```csharp
/// <summary>
///   A ranged attack was launched (Throw or Shoot). Sibling of
///   <see cref="AttackSwing"/>, which is now melee-only.
/// </summary>
public static event Action<string>? RangedAttack;
```

在 `RaiseAttackSwing` 之后加：

```csharp
public static void RaiseRangedAttack(string itemId) => RangedAttack?.Invoke(itemId);
```

（`ProjectileHit` 属 Task 2，不在此加——本任务的测试不引用它。）

- [ ] **Step 2: 写失败测试** `test/src/CombatFeedbackTest.cs`

```csharp
public class CombatFeedbackTest : TestClass
{
  public CombatFeedbackTest(Node testScene) : base(testScene) { }

  [Test]
  public void RaiseAttack_Melee_RaisesOnlyAttackSwing()
  {
    var swings = new List<string>();
    var ranged = new List<string>();
    Action<string> swing = id => swings.Add(id);
    Action<string> shoot = id => ranged.Add(id);
    GameEvents.AttackSwing += swing;
    GameEvents.RangedAttack += shoot;
    try { CombatFeedback.RaiseAttack(AttackType.Melee, "wooden_axe"); }
    finally { GameEvents.AttackSwing -= swing; GameEvents.RangedAttack -= shoot; }

    swings.ShouldBe(new[] { "wooden_axe" });
    ranged.ShouldBeEmpty();
  }

  [Test]
  public void RaiseAttack_ThrowAndShoot_RaiseOnlyRangedAttack()
  {
    var swings = 0;
    var ranged = new List<string>();
    Action<string> swing = _ => swings++;
    Action<string> shoot = id => ranged.Add(id);
    GameEvents.AttackSwing += swing;
    GameEvents.RangedAttack += shoot;
    try
    {
      CombatFeedback.RaiseAttack(AttackType.Throw, "wooden_spear");
      CombatFeedback.RaiseAttack(AttackType.Shoot, "wooden_bow");
    }
    finally { GameEvents.AttackSwing -= swing; GameEvents.RangedAttack -= shoot; }

    swings.ShouldBe(0);
    ranged.ShouldBe(new[] { "wooden_spear", "wooden_bow" });
  }
}
```

- [ ] **Step 3: 跑测试确认失败**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 预期编译失败：`CombatFeedback` 不存在。

- [ ] **Step 4: 实现** `src/combat/CombatFeedback.cs`（含 `namespace SeaAnomaly;`）

```csharp
public static class CombatFeedback
{
  public static void RaiseAttack(AttackType attack, string itemId)
  {
    if (attack == AttackType.Melee)
      GameEvents.RaiseAttackSwing(itemId);
    else
      GameEvents.RaiseRangedAttack(itemId);
  }
}
```

- [ ] **Step 5: 跑测试确认通过**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；`godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（486）。

**不单独提交**：与 Task 2 合并为提交 A。

---

### Task 2: 拆开投射物 / 远程语义

**Files:**
- Modify: `src/core/GameEvents.cs`（`MeleeHit` 后加 `ProjectileHit`，`AttackSwing` 后加 `RangedAttack`；helper 加在 `RaiseMeleeHit` / `RaiseAttackSwing` 之后）
- Modify: `src/combat/WeaponSystem.cs:404,412-418,441,454,507-535`
- Modify: `src/combat/Projectile.cs:16,100-107`
- Modify: `src/core/SfxHook.cs`（订阅/解绑 + 两个 handler + 静态 `RangedSfxId`）
- Modify: `test/src/CombatHurtboxTest.cs`（新增 1 例）
- Modify: `test/src/AudioManagerTest.cs`（新增 1 例）

**Interfaces:**
- Consumes: `CombatFeedback.RaiseAttack(AttackType, string)`、`GameEvents.RangedAttack` + `RaiseRangedAttack(string)`（Task 1 已加）。
- Produces: `GameEvents.ProjectileHit`（`Action<string>`，参数为投射物 item id）、`Projectile.ItemId`（`[Export] string`，默认 `""`）、`SfxHook.RangedSfxId(string) -> string`。

- [ ] **Step 1: 加 `ProjectileHit` 事件与 helper** `src/core/GameEvents.cs`

在 `public static event Action<string>? MeleeHit;` 之后：

```csharp
/// <summary>
///   A player projectile (thrown spear / arrow) hit an enemy. Distinct from
///   <see cref="MeleeHit"/>: the tutorial's "chop with a tool" step must not
///   advance on a throw. Raise-only, consumed by SfxHook (enemy_hit).
/// </summary>
public static event Action<string>? ProjectileHit;
```

在 `RaiseMeleeHit` 之后加 `public static void RaiseProjectileHit(string itemId) => ProjectileHit?.Invoke(itemId);`。

- [ ] **Step 2: 投射物带 item id 并改发新事件** `src/combat/Projectile.cs`

字段区（`Lifetime` 之后）加 `[Export] public string ItemId = "";`。

`ApplyHit` 内把：

```csharp
// Same hit-feel bus as melee so SfxHook plays melee_hit on spear/arrow.
GameEvents.RaiseMeleeHit("");
```

换成 `GameEvents.RaiseProjectileHit(ItemId);`（注释改为「proj 命中走自己的事件，避免教程/近战音效误触发」）。

- [ ] **Step 3: 武器系统分派** `src/combat/WeaponSystem.cs`

- `PulseAttackVisual()` → `PulseAttackVisual(AttackType attack)`，方法体最后一行改为 `CombatFeedback.RaiseAttack(attack, ResolveEffectiveItem()?.Id ?? "");`（animator 那两行不动）。
- 三个调用点：`TryMelee` 里改 `PulseAttackVisual(AttackType.Melee)`；`case AttackType.Throw:` 里改 `PulseAttackVisual(AttackType.Throw)`；`case AttackType.Shoot:` 里改 `PulseAttackVisual(AttackType.Shoot)`。
- `SpawnProjectile` 里在 `projectile.Damage = damage;` 之前加一行 `projectile.ItemId = ResolveEffectiveItem()?.Id ?? "";`。

- [ ] **Step 4: 音效路由** `src/core/SfxHook.cs`

- `_Ready` 加 `GameEvents.ProjectileHit += OnProjectileHit; GameEvents.RangedAttack += OnRangedAttack;`；`_ExitTree` 加对应 `-=`。
- 新增：

```csharp
/// <summary>
///   Ranged sfx router: bows shoot arrows (bow_shoot); everything else in the
///   ranged path is a thrown spear, which keeps the pre-existing swing whoosh.
///   Same id convention as CombatLogic.ResolveAttack (Contains("bow")).
/// </summary>
public static string RangedSfxId(string itemId) =>
  itemId.Contains("bow") ? "bow_shoot" : "melee_swing";

private void OnProjectileHit(string itemId) =>
  ResolveAudioManager()?.PlaySfx("enemy_hit");

private void OnRangedAttack(string itemId) =>
  ResolveAudioManager()?.PlaySfx(RangedSfxId(itemId));
```

- 类注释的事件映射表补两行（ProjectileHit → enemy_hit，RangedAttack → bow_shoot / melee_swing）。

- [ ] **Step 5: 测试** `test/src/CombatHurtboxTest.cs` 新增

```csharp
[Test]
public async Task ProjectileHit_RaisesProjectileHitNotMeleeHit()
{
  var (_, enemy) = await SpawnMeleeRig(new Vector3(0f, 0f, -2f));
  var packed = ResourceLoader.Load<PackedScene>("res://scenes/combat/projectile.tscn");
  var projectile = packed!.Instantiate<Projectile>();
  projectile.Damage = 10f; projectile.Gravity = 0f; projectile.ItemId = "wooden_spear";
  projectile.SetVelocity(new Vector3(0f, 0f, -12f));
  enemy.GetParent().AddChild(projectile);
  projectile.GlobalPosition = new Vector3(0f, 0.75f, -1.1f);
  projectile.SetPhysicsProcess(false);
  await TestScene.ProcessFrame(3);

  var hits = new List<string>(); var melee = 0;
  Action<string> onHit = id => hits.Add(id);
  Action<string> onMelee = _ => melee++;
  GameEvents.ProjectileHit += onHit; GameEvents.MeleeHit += onMelee;
  try { for (var i = 0; i < 8; i++) projectile._PhysicsProcess(0.02); }
  finally { GameEvents.ProjectileHit -= onHit; GameEvents.MeleeHit -= onMelee; }

  hits.ShouldBe(new[] { "wooden_spear" });
  melee.ShouldBe(0);
}
```

`test/src/AudioManagerTest.cs` 新增：

```csharp
[Test]
public void RangedSfxId_BowShootsBow_ThrowKeepsSwing()
{
  SfxHook.RangedSfxId("wooden_bow").ShouldBe("bow_shoot");
  SfxHook.RangedSfxId("iron_bow").ShouldBe("bow_shoot");
  SfxHook.RangedSfxId("wooden_spear").ShouldBe("melee_swing");
}
```

- [ ] **Step 6: 验证并提交**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；`godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（488）。

```bash
git add src/combat/CombatFeedback.cs src/combat/WeaponSystem.cs src/combat/Projectile.cs src/core/GameEvents.cs src/core/SfxHook.cs test/src/CombatFeedbackTest.cs test/src/CombatHurtboxTest.cs test/src/AudioManagerTest.cs
git commit -m "feat(events): split projectile/ranged semantics"
```

---

### Task 3: 退役 5 个死事件

**Files:**
- Modify: `src/core/GameEvents.cs:146,153,180,221,228`（声明）与 `:401-405,412-413,426-430`（helper）
- Modify: `src/combat/BossPhaseController.cs`（删 raise + 类注释那一行）
- Modify: `src/farming/Livestock.cs`（删 2 处 raise）
- Modify: `test/src/BossPhaseTest.cs:145-197`（改 2 例）

**Interfaces:**
- Consumes: `BossPhaseController.CurrentPhase`（公开属性，无需新接口）。
- Produces: 无新接口；删掉 `LivestockTamed`/`LivestockProduced`/`BossPhaseChanged`/`TalentUnlocked`/`SkillActivated` 及其 `Raise*` helper。

- [ ] **Step 1: 改测试，不依赖被删事件** `test/src/BossPhaseTest.cs`

把 `BossPhaseChanged_RaisedExactlyOncePerTransition` 换名为 `PhaseTransitions_MoveCurrentPhase2Then3AndStay`：去掉全部事件订阅，改为每次 `_PhysicsProcess(0.016)` 后采样 `_controller.CurrentPhase`，断言序列 `[2, 3, 3]`（第三次同阶段不回退）。

把 `BossPhaseChanged_NoEventWhileStayingInPhaseOne` 换名为 `PhaseOne_StaysOneAboveSeventyPercent`：去掉订阅与计数器，`TakeDamage(50f)` 后连跑两次 `_PhysicsProcess`，断言 `_controller.CurrentPhase.ShouldBe(1)`。

- [ ] **Step 2: 跑测试确认当前仍通过（改测试不改实现）**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；测试 `Failed: 0`（488，两例改名）。

- [ ] **Step 3: 删事件与 helper** `src/core/GameEvents.cs`

删除这 5 个声明，及其 `RaiseLiveLivestockTamed` 等成对的 raise helper：`LivestockTamed`、`LivestockProduced`、`BossPhaseChanged`、`TalentUnlocked`、`SkillActivated`。同步删掉各自上方的 `<summary>` 注释块。

- [ ] **Step 4: 删 raise 调用点**

- `src/combat/BossPhaseController.cs`：删 `GameEvents.RaiseBossPhaseChanged(BossId, phase);` 一行；类注释里「`RaiseBossPhaseChanged` fires exactly once per transition」那段删掉（`CurrentPhase` 的说明保留）。
- `src/farming/Livestock.cs`：删 `GameEvents.RaiseLivestockTamed(LivestockType);` 与 `GameEvents.RaiseLivestockProduced(LivestockType);` 各一行（状态赋值保留）。

- [ ] **Step 5: 标注 `GameOver` 语义** `src/core/GameEvents.cs`

在 `public static event Action? GameOver;` 的 `<summary>` 里补一句：

```
  ///   TRUE-ENDING ONLY. Death goes PlayerDied -> respawn and must never raise
  ///   this; the hygiene test whitelists it because no producer exists yet.
```

- [ ] **Step 6: 验证**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；`godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（488）。
Run: `Select-String -Path src\**\*.cs -Pattern 'SkillActivated|TalentUnlocked|LivestockProduced|LivestockTamed|BossPhaseChanged'` → 0 命中。

**不单独提交**：与 Task 4 合并为提交 B。

---

### Task 4: 接线 `CraftingProgress` / `GameSaved` / `GameLoaded`

**Files:**
- Modify: `src/ui/CraftUI.cs`（字段 + BuildUi + 订阅 + 3 个 handler）
- Modify: `src/ui/HUD.cs`（`_Ready`/`_ExitTree` + `_Process` + `ShowToast`/`EnsureToastLabel`）
- Modify: `test/src/CraftUILogicTest.cs`（新增 1 例）
- Modify: `test/src/HUDTest.cs`（新增 1 例）

**Interfaces:**
- Consumes: `GameEvents.CraftingProgress(float)`、`GameEvents.GameSaved(bool)`、`GameEvents.GameLoaded(string)`（均已存在）。
- Produces: CraftUI 内 `ProgressBar` 名为 `"CraftProgress"`；HUD 内 `Label` 名为 `"ToastLabel"`；`public void HUD.ShowToast(string text)`。

- [ ] **Step 1: 进度条（CraftUI）**

字段区加 `private ProgressBar? _progressBar;`。

`BuildUi` 在 `var hint = ...; vbox.AddChild(hint);` 之前插入：

```csharp
var progress = new ProgressBar
{
  Name = "CraftProgress",
  MinValue = 0, MaxValue = 1, Step = 0.001,
  Visible = false,
  CustomMinimumSize = new Vector2(0, 10)
};
vbox.AddChild(progress);
_progressBar = progress;
```

订阅区加 `GameEvents.CraftingProgress += OnCraftingProgress;`；解绑区加 `-=`。

`Subscribe` 里把 `CraftingCompleted`/`CraftingFailed` 的 handler 从 `OnCraftingStateChanged` 换成新的 `OnCraftingFinished`（`Unsubscribe` 同步换），并新增：

```csharp
private void OnCraftingProgress(float progress)
{
  if (_progressBar == null || !IsInstanceValid(_progressBar))
    return;
  // 0 is the cancel terminal state (CancelCrafting raises progress 0, not a
  // Failed event) — treat it as "hide", same as Completed/Failed.
  _progressBar.Visible = progress > 0f;
  if (progress > 0f)
    _progressBar.Value = progress;
}

private void OnCraftingFinished(string recipeId)
{
  if (_progressBar != null && IsInstanceValid(_progressBar))
    _progressBar.Visible = false;
  if (IsOpen)
    Refresh();
}
```

- [ ] **Step 2: 保存/读取提示（HUD）**

字段区加 `private Label? _toastLabel;` 与 `private float _toastRemaining;`，并加常量 `private const float ToastSeconds = 1.5f;`。

`_Ready` 里 `EnsureQuestObjectiveLabel();` 之后加：

```csharp
EnsureToastLabel();
GameEvents.GameSaved += OnGameSaved;
GameEvents.GameLoaded += OnGameLoaded;
```

`_ExitTree` 里加 `GameEvents.GameSaved -= OnGameSaved; GameEvents.GameLoaded -= OnGameLoaded;`。

`_Process` **最开头**（在 `_releaseLockAtFrame` 判断之前）加：

```csharp
if (_toastRemaining > 0f)
{
  _toastRemaining -= (float)delta;
  if (_toastRemaining <= 0f && _toastLabel != null && IsInstanceValid(_toastLabel))
  {
    _toastRemaining = 0f;
    _toastLabel.Visible = false;
  }
}
```

新增（放在 `EnsureQuestObjectiveLabel` 附近，风格照抄它）：

```csharp
/// <summary>Shows a 1.5 s bottom-center toast; a second call resets the timer.</summary>
public void ShowToast(string text)
{
  EnsureToastLabel();
  if (_toastLabel == null || !IsInstanceValid(_toastLabel))
    return;
  _toastLabel.Text = text;
  _toastLabel.Visible = true;
  _toastRemaining = ToastSeconds;
}

private void EnsureToastLabel()
{
  if (_toastLabel != null && IsInstanceValid(_toastLabel))
    return;

  var label = new Label
  {
    Name = "ToastLabel",
    Visible = false,
    MouseFilter = Control.MouseFilterEnum.Ignore,
    HorizontalAlignment = HorizontalAlignment.Center,
    Text = ""
  };
  label.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
  label.OffsetLeft = -240f; label.OffsetRight = 240f;
  label.OffsetTop = -140f; label.OffsetBottom = -100f;
  label.AddThemeFontSizeOverride("font_size", 20);
  label.AddThemeColorOverride("font_color", new Color(0.85f, 1f, 0.85f));
  label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
  label.AddThemeConstantOverride("outline_size", 5);
  AddChild(label);
  _toastLabel = label;
}

private void OnGameSaved(bool overwrite) => ShowToast("已保存");
private void OnGameLoaded(string fileName) => ShowToast("已读取");
```

理由（写进注释即可）：订阅放在 `_Ready` 而不是 `Initialize`，因为测试与部分场景里 HUD 的 `Player` 导出为空、`Initialize` 永远不跑，而提示不需要玩家接线。

- [ ] **Step 3: 测试** `test/src/CraftUILogicTest.cs` 新增

```csharp
[Test]
public void CraftingProgress_DrivesBarAndZeroHidesIt()
{
  var bar = _craftUI.FindChild("CraftProgress", true, false) as ProgressBar;
  bar.ShouldNotBeNull();
  bar!.Visible.ShouldBeFalse();

  GameEvents.RaiseCraftingProgress(0.5f);
  bar.Visible.ShouldBeTrue();
  bar.Value.ShouldBe(0.5, 0.001);

  GameEvents.RaiseCraftingCompleted("wooden_spear");
  bar.Visible.ShouldBeFalse();

  GameEvents.RaiseCraftingProgress(0.7f);
  bar.Visible.ShouldBeTrue();
  GameEvents.RaiseCraftingProgress(0f);   // CancelCrafting's terminal state
  bar.Visible.ShouldBeFalse();
}
```

`test/src/HUDTest.cs` 新增：

```csharp
[Test]
public void GameSavedAndLoaded_ShowToastThatExpires()
{
  GameEvents.RaiseGameSaved(true);
  var toast = _hud.FindChild("ToastLabel", true, false) as Label;
  toast.ShouldNotBeNull();
  toast!.Visible.ShouldBeTrue();
  toast.Text.ShouldContain("保存");

  _hud._Process(2.0);
  toast.Visible.ShouldBeFalse();

  GameEvents.RaiseGameLoaded("user://saves/savegame_1.json");
  toast.Text.ShouldContain("读取");
  toast.Visible.ShouldBeTrue();
}
```

- [ ] **Step 4: 验证并提交**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；`godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（490）。

```bash
git add src/ui/CraftUI.cs src/ui/HUD.cs src/core/GameEvents.cs src/combat/BossPhaseController.cs src/farming/Livestock.cs test/src/BossPhaseTest.cs test/src/CraftUILogicTest.cs test/src/HUDTest.cs
git commit -m "refactor(events): retire dead events + wire craft/save feedback"
```

---

### Task 5: 卫生守卫测试

**Files:**
- Create: `test/src/GameEventsHygieneTest.cs`

**Interfaces:**
- Consumes: `ProjectSettings.GlobalizePath("res://src")`、`System.IO`、`System.Text.RegularExpressions`。
- Produces: 3 个守卫用例（无生产 API）。

- [ ] **Step 1: 写测试（先跑，确认当前 0 违例、能通过）** `test/src/GameEventsHygieneTest.cs`

要点（实现细节固定如下，避免误判）：

- 遍历 `GlobalizePath("res://src")` 下全部 `*.cs`（递归）。
- 逐行扫描；`line.TrimStart()` 以 `//` 或 `*` 开头的行跳过（注释）。
- 订阅计数正则：`GameEvents\.(\w+)\s*\+=`；解绑：`GameEvents\.(\w+)\s*\-=`。按 `(文件, 事件)` 汇总 `+=` 与 `-=` 数量并断言相等，失败信息输出 `文件:事件 +=N -=M`。
- 声明集合：读 `src/core/GameEvents.cs`，正则 `public static event Action(?:<[^>]+>)?\??\s+(\w+);`。
- 发送方判定：**排除 `GameEvents.cs` 自身**，在其余 `src/*.cs` 中出现 `Raise<事件名>\s*\(`。
- 接收方判定：**排除 `GameEvents.cs`**，在 `src/*.cs` 中出现 `GameEvents\.<事件名>\s*[\+\-]=`。
- 白名单：`GameOver`（真结局专用，暂无生产发送方/接收方）——发送方与接收方两条断言都跳过它。

三个用例名与断言：

1. `SubscriptionsArePairedPerFile`：上面第 3 条，违例列表应为空。
2. `EveryDeclaredEventHasProducerAndConsumer`：声明数 `ShouldBe(51)`；对每个非白名单事件，发送方文件集与接收方文件集都非空（失败信息列事件名 + 缺哪一侧）。
3. `DeclaredEventNamesAreUnique`：声明集合的 `Count` 等于去重后 `Count`（`GroupBy` 后取 `Count > 1` 应空）。

- [ ] **Step 2: 跑测试**

Run: `dotnet build SeaAnomaly.csproj -v minimal` → 0 error；`godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（493）。

- [ ] **Step 3: 变异自检（证明守卫真的会失败）**

临时在 `src/ui/HUD.cs` 的 `_ExitTree` 里注释掉 `GameEvents.GameSaved -= OnGameSaved;` → 重跑测试，确认 `SubscriptionsArePairedPerFile` **失败**且信息里出现 `HUD.cs:GameSaved +=1 -=0`；然后还原，再次确认全绿。

- [ ] **Step 4: 提交**

```bash
git add test/src/GameEventsHygieneTest.cs
git commit -m "test(events): guard subscribe/unsubscribe symmetry"
```

---

## 验收（全部三个提交完成后）

1. `dotnet build SeaAnomaly.csproj -v minimal` → 0 error。
2. `godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`，493 passed。
3. `(Select-String -Path src\core\GameEvents.cs -Pattern 'public static event Action').Count` → 51。
4. 全库搜索 5 个死事件名 → 0 命中。
5. `Select-String -Path src\combat\Projectile.cs -Pattern 'RaiseMeleeHit'` → 0 命中。
6. 更新 `.godot-dev/state.json`：`last_stable_commit` 改为第三个提交的 hash；`verification.tests` 记 493/0。
7. 在 `.godot-dev/plans/2026-11-17-review-closeout.md` 补任务 4 结果小结。
