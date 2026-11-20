# 2026-11-17 GameEvents 收敛设计（方案 B）

> 来源：审查收口任务 4 第 4 选（`GameEvents` 一事件一语义）。
> 决策记录：方案 B + 事件处置表已获确认（`CraftingProgress`/`GameSaved`/`GameLoaded` 接线，其余删除）。
> 基线：`last_stable_commit = b67a258`，`--run-tests` 484 passed / 0 failed。

## 1. 意图

`GameEvents` 是全项目的静态事件总线（54 个事件、42 个文件引用）。现在混了三类问题：

1. **一个事件被两种语义共用** —— 投矛/箭命中复用 `MeleeHit`，远程攻击复用 `AttackSwing`。后果是玩家可见的：投矛能推进教程「用工具砍」那一步，弓射出的是近战挥击音效。
2. **事件发了没人听，或者干脆没有发送方** —— 10 个事件属于此类，读代码时无法判断它是不是还有用。
3. **契约「事件总线 `_ExitTree` 手动解绑」目前靠人肉遵守** —— 今天已因类似的静态状态泄漏踩过一次 flake。

收敛目的是：**看到事件名就知道它唯一的意思，看到事件就能找到发送方和接收方，配对关系有自动守卫。**

## 2. 目标 / 非目标

目标：
- 每个事件只有一个语义，且生产代码里有 ≥1 个发送方、≥1 个接收方（`GameOver` 例外，见下）。
- 删除 5 个死事件，接线 3 个「本该有人听」的事件。
- 新增一条自动测试守住「订阅必配解绑」和「事件不得空转」。

非目标：
- 不改 `GameplayInputLockChanged` 的 bool 语义（那是方案 C，单独开票）。
- 不拆 `GameEvents.cs` 文件（54 个事件仍集中，按域分区注释已经够用）。
- 不动 `GuideLine`（16 个订阅者是有意的广播：字幕 + 引导 + 新手提示）。
- 不重命名任何现有保留事件（改动面会扩散到 20+ 文件，收益仅是命名风格）。

反目标：为了「干净」把 `GameOver` 删掉 —— 它的语义是「真结局」，`GameManager` 注释和 2 个测试都在守「死亡不是 GameOver」，删掉会丢失这条契约。

## 3. 约束

来自 `state.json > contracts[]`，本次必须继续成立：

- **事件总线 `_ExitTree` 手动解绑**：新增的订阅（HUD / CraftUI / SfxHook）必须在 `_ExitTree` 里成对解绑。
- **PlayerDied ⇢ GameOver**：`GameOver` 仍然只由真结局触发，死亡路径不得 raise。
- **Godot 4.7 命令纪律**：节点一律 `GetNodeOrNull` + `IsInstanceValid`，不用 `GetNode<T>` 断言；场景 export 走 `node_paths`。
- **AI 状态机 + PlayerMotion 保持纯**：`PlayerMotion` 不入场，本次不碰。

## 4. 方案对比（已选 B）

| 方案 | 内容 | 取舍 |
|---|---|---|
| A | 只修语义错用 | 最小改动，但 10 个死事件继续误导读者；「一事件一语义」只完成一半 |
| **B（选）** | A + 死事件清账 + 卫生测试 | 一次把「语义 + 空转 + 配对守卫」三件事做完；约 12 个文件，行为变化只有 2 处且都是修 bug |
| C | B + 输入锁改所有权栈 | 能根除 `GameplayInputLockChanged` 的跨场景泄漏，但要动 4 个 UI + GameManager + 3 个测试，与刚修好的输入锁测试互相掩盖风险 |

选 B，C 另开票。

## 5. 设计

### 5.1 事件处置表

**新增（2）**

| 事件 | 语义 | 发送方 | 接收方 |
|---|---|---|---|
| `ProjectileHit(string itemId)` | 投掷物/箭命中敌人（区别于近战命中） | `Projectile.OnHit` | `SfxHook`（`enemy_hit`，该音效已导入但一直没人播） |
| `RangedAttack(string itemId)` | 远程攻击发起（投掷/弓射） | `CombatFeedback.RaiseAttack` | `SfxHook`（弓→`bow_shoot`，已导入未使用；投掷→`melee_swing`，与现状一致） |

**语义收窄（2，不改签名）**

| 事件 | 改法 |
|---|---|
| `MeleeHit` | 只有 `WeaponSystem.PerformMelee` 命中时 raise；`Projectile` 不再复用 |
| `AttackSwing` | 只有近战挥击 raise；投掷/弓射改发 `RangedAttack` |

**删除（5）**

| 事件 | 依据 | 连带改动 |
|---|---|---|
| `SkillActivated` | 全库 0 发送 0 订阅 | 删 helper |
| `TalentUnlocked` | 全库 0 发送 0 订阅 | 删 helper |
| `LivestockProduced` | 无接收方，且无对应 UI 计划 | `Livestock.cs:191` 删 raise |
| `LivestockTamed` | 同上 | `Livestock.cs:153` 删 raise |
| `BossPhaseChanged` | 无生产接收方 | `BossPhaseController` 删 raise；`BossPhaseTest` 两例改断 `CurrentPhase` |

**接线（3）**

| 事件 | 接收方 | 实现 |
|---|---|---|
| `CraftingProgress(float)` | `CraftUI` | 代码建一个 `ProgressBar`（`EnsureProgressBar()`，与 `HUD.EnsureQuestObjectiveLabel()` 同风格，不改 `.tscn`），`CraftingStarted/Completed/Failed` 时显隐 |
| `GameSaved(bool)` | `HUD` | `EnsureToastLabel()` + `ShowToast(string)`，`_Process` 里 1.5s 倒计时淡出；文案「已保存」 |
| `GameLoaded(string)` | `HUD` | 同上，文案「已读取」 |

**保留但标注（1）**

`GameOver`：注释写清「真结局专用；死亡走 PlayerDied → 重生，禁止从死亡路径 raise」，并进卫生测试的白名单。

### 5.2 代码改动点

- `src/core/GameEvents.cs`：+2 event +2 raise helper；-5 event -5 helper；`GameOver` 补注释。
- `src/combat/CombatFeedback.cs`（新）：`static void RaiseAttack(AttackType attack, string itemId)`，`Melee → AttackSwing`，`Throw/Shoot → RangedAttack`。抽成静态纯分派是为了能在没有 rig 的情况下单测（与 `WeatherFog` 同一思路）。
- `src/combat/WeaponSystem.cs`：`PulseAttackVisual(AttackType)` 改为调 `CombatFeedback.RaiseAttack`；`SpawnProjectile` 把 `ItemId` 传给投射物。
- `src/combat/Projectile.cs`：新增 `[Export] public string ItemId`，命中时 `RaiseProjectileHit(ItemId)`。
- `src/core/SfxHook.cs`：`+ProjectileHit → enemy_hit`；`+RangedAttack` 按 `itemId.Contains("bow")`（与 `CombatLogic.ResolveAttack` 同一约定）分流到 `bow_shoot` / `melee_swing`，`_ExitTree` 成对解绑。
- `src/ui/HUD.cs`、`src/ui/CraftUI.cs`：上面两个接收方，均代码建节点 + `_ExitTree` 解绑。
- `src/combat/BossPhaseController.cs`、`src/farming/Livestock.cs`：删 raise。

### 5.3 测试

| 测试 | 断言 |
|---|---|
| `CombatFeedbackTest`（新，2 例） | `Melee → AttackSwing` 且不发 `RangedAttack`；`Throw/Shoot → RangedAttack` 且不发 `AttackSwing` |
| `CombatHurtboxTest` 扩 1 例 | 投射物命中敌人：`ProjectileHit == 1`、`MeleeHit == 0`（复用现有 projectile rig） |
| `BossPhaseTest` 改 2 例 | 断 `CurrentPhase` 跃迁，不再依赖被删事件 |
| `CraftUILogicTest` 扩 1 例 | `CraftingProgress(0.5)` 后进度条可见且值是 0.5 |
| `HUDTest` 扩 1 例 | `RaiseGameSaved` 后 toast 可见，文案含「保存」 |
| `GameEventsHygieneTest`（新，3 例） | ① `src/` 内每个文件每个事件 `+=` 数 == `-=` 数；② 每个声明事件在生产代码里有 ≥1 发送 + ≥1 订阅（白名单 `GameOver`），且发送方文件里确实出现 `Raise<Name>(`（扫描排除 `GameEvents.cs` 本身与 `test/`，否则断言恒真）；③ 声明名唯一 |

卫生测试实现说明：`ProjectSettings.GlobalizePath("res://src")` 遍历 `*.cs`，逐行跳过 `//` 注释行后做正则匹配；已知局限（字符串字面量里出现 `GameEvents.X +=` 会误判）写进类注释。今天已验证该规则在当前 `src/` 上是 0 违例，所以能直接落地。

### 5.4 实现前已核实的边界（builder-check 五问）

| 问题 | 代码事实 | 定论 |
|---|---|---|
| 启动会不会弹「已读取」 | `SaveService.AutoLoadOnReady` 默认 `false`，`Game.tscn` 未覆盖；`GameLoaded` 只由 F9（`TryLoadMostRecent`）与主菜单选档（`MainMenuUI.OnSaveSlotPressed → TryLoadPath`）触发 | 不做「抑制启动期」逻辑；主菜单读档后弹一次正是期望效果。`HUDTest` 直接用 `RaiseGameLoaded` 构造，不模拟 F9 |
| 卫生测试②的发送方判定 | `GameEvents.cs` 内含全部 `RaiseX(` 定义 | 扫描排除 `GameEvents.cs` 与 `test/` |
| 投掷播 `bow_shoot` 是否可接受 | 现状 `PulseAttackVisual` 对所有攻击无差别 raise `AttackSwing` → 投掷**已经**在播 `melee_swing`；`CombatLogic.ResolveAttack` 用 `selectedId.Contains("bow")` 判弓 | 弓→`bow_shoot`（修错音效），投掷→`melee_swing`（与今日行为一致，零回归）。分流同用 `Contains("bow")` |
| 取消制作后进度条谁收 | `CancelCrafting()` 只发 `CraftingProgress(0f)`，无 `CraftingFailed/Completed` | CraftUI 把 `progress <= 0` 视为终态并隐藏；不改 `CraftingSystem` 与 `CraftingSystemTest` |
| 面板关闭期间制作继续，重开如何补显 | `CraftingSystem` 无公开进度访问器，`_Process` 每帧发 `CraftingProgress` | 依赖下一帧 tick 自然补显，不新增公开 API；测试断言点放在「收到 progress 后可见」而非「打开瞬间可见」 |

## 6. 验收

1. `dotnet build SeaAnomaly.csproj -v minimal` → 0 error。
2. `godot --headless --path . --run-tests --quit-on-finish` → `Failed: 0`（基线 484 + 新增约 8 例）。
3. `grep -c "event Action" src/core/GameEvents.cs` == 54 - 5 + 2 = **51**。
4. 全库搜索 `SkillActivated|TalentUnlocked|LivestockProduced|LivestockTamed|BossPhaseChanged` → 0 命中。
5. `Projectile` 不再出现 `RaiseMeleeHit`；`TutorialUI` 只订阅 `MeleeHit`。
6. 提交拆 3 个：`feat(events): split projectile/ranged semantics`、`refactor(events): retire dead events + wire craft/save feedback`、`test(events): guard subscribe/unsubscribe symmetry`。

## 7. 风险与回滚

- **风险 1**：`GameplayInputLocked` 相关的 HUD/CraftUI 改动可能再次影响输入锁时序 → 缓解：两个新订阅都在 `_ExitTree` 解绑，且 `PlayerInputSimulationTest` 已改成显式 `Dismiss()`，不依赖时序。
- **风险 2**：`BossPhaseTest` 改断 `CurrentPhase` 后覆盖度下降（少了「恰好一次」计数）→ 缓解：用「阶段值序列」断言等价于计数（每 tick 采样必须单调不回退）。
- **风险 3**：卫生测试文本扫描误伤 → 缓解：只扫 `src/`，跳过注释行，失败信息打印 `文件:事件:次数`。
- **回滚**：三个提交各自独立，`git revert` 任一即可；事件删除是编译期错误，回滚后不会留悬空引用。
