# 全项目纵深代码审查报告（Iter10 收官后 · 分支 dev/code-review）

> 审查参数（用户 `/grill-me` 收敛）：全项目纵深审查；**正确性 + 契约一致为主视角**；
> 审完即修 P0/P1（P2 只报告不修）；P0 = 崩溃 / 数据丢失 / 契约违反，P1 = 泄漏 / 缺陷 / 测试脆弱；
> 修复不触碰 9 条锁契约，需动契约先问用户。
>
> 基线：Iter10 verified（300 测试）；本报告对应修复后 **301/0 全绿**（`godot --headless --run-tests`，TEST_EXIT=0）。

---

## 1. 结论摘要

- **P0 修复：3 项**（含 1 项数据丢失 + 1 项契约违反），全部已验证。
- **P1 修复：8 项**（含 1 项用户决策：主岛地形贴盒顶），全部已验证。
- **P2 报告：32 项**（只报告，不修，见 §4）。
- 9 条锁契约逐条核对：**全部保持**（见 §5）；其中 1 条（Game.tscn=产品）的审查结论即用户决策的主岛地形修复，不改变契约本身。

---

## 2. P0 —— 已修复（崩溃 / 数据丢失 / 契约违反）

| # | 位置 | 问题 | 修复 |
|---|------|------|------|
| P0-1 | `src/core/save/SaveService.cs` L187 `TryLoadMostRecent` | **Windows F5 覆盖写必崩**：`GetSaveFilesInfo` 返回 OS 绝对路径（`C:\...`），`_currentSaveFile` 原样存下；下次 `SaveGame` 用 `StripFolder`（按 `/` 切分）拼出垃圾 `"user://saves/C:\..."` 路径 → `File.WriteAllText` 抛异常（数据丢失+崩溃）。 | `ProjectSettings.LocalizePath(files[0].FullName)`（镜像 `BuildingSystem.LoadMostRecent` 的既有修法）。 |
| P0-2 | `src/core/save/GameSaveData.cs` `InventorySaveData` | **契约违反（读档错位）**：背包扩容（Iter8.5 T8.5.4，10→12 列）后存档无宽度信息，重开（默认 10 列）按行主序恢复 → 第 11、12 列内容错位到下一行，静默丢内容。 | 新增 `GridWidth` 字段（旧档缺字段 → 默认 0 → 不扩宽，回退安全）；`RestoreInventory` 先 `ExpandInventory(data.GridWidth)` 再恢复。 |
| P0-3 | `src/quest/StoryPointTrigger.cs` + `Game.tscn` / `IslandBuilder.cs` | **契约违反（教程/任务软锁）**：触发器一次性 + `QuestService.HandleProgress` 有 `IsCurrent` 门（QuestService.cs:252）→ 第一章提前进入 ruin 时事件被忽略但触发器已消费 → 章 2 `quest_ruin` + 教程步骤永远等不到触发。 | 新增 `[Export] Reusable`：`true` 每次进入都触发。`Game.tscn` 的 "ruin" 触发器与 `IslandBuilder` 的 "shark_king" 设 `true`；"radio" 保持一次性（`quest_radio` 开局即当前，首次进入必为预期消费）。 |

---

## 3. P1 —— 已修复（缺陷 / 泄漏 / 测试脆弱）

| # | 位置 | 问题 | 修复 |
|---|------|------|------|
| P1-1 | `src/world/island/IslandBuilder.cs` `PlaceEnemies` | **敌人永久失活**：`root.GetPathTo(player/dayNight)` 是相对树根的路径，`EnemyBase` 相对自身解析 → `_player` 为 null → 待机守卫（L132）使敌人永远不追击、夜间乘数失效（战斗内容静默缺失）。 | 改用 `player.GetPath()` / `dayNight.GetPath()` 绝对路径。 |
| P1-2 | `src/world/GroundLoot.cs` `Interact` | **满背包丢物**：`AddItem` 有余数时仍无条件 `QueueFree` → 拾取物静默消失（数据丢失）。 | 检查 `AddItem` 返回值，余数 > 0 则保留 GroundLoot 不掉落。 |
| P1-3 | `src/core/save/SaveService.cs` `LoadGame` | **位置匹配命中待释放旧实例**：`BuildingSystem.RestoreFromSnapshot` 的 `QueueFree` 生效于帧末，旧实例在帧末前仍是合法组员 → 存档状态可能写进即将销毁的实例（时序相关静默丢失）。 | 初版"清空三组"误伤独立组员（测试暴露）；改在 `FindNearest` 内跳过 `IsQueuedForDeletion()` 节点——精准排除垂死实例，保留正常组员。 |
| P1-4 | `src/player/PlayerController.cs` | **携带速度复利放大**：`velocity += carry` 使 `ComputeVelocity` 以含 carry 的旧速度为 lerp 基准 → 零输入时速度被反复放大（加速甩出木筏/地形穿透）。 | 先减后加：`ComputeVelocity(Velocity - carry, …)` 后 `velocity += carry`；抽 `ResolveRaftCarry()` 公共测试缝。 |
| P1-5 | `src/ui/CraftUI.cs` / `StorageUI.cs` `Close()` | **Esc 同趟关面板+暂停**：`_Input` 逆树序分发，CraftUI 先收 Esc 同步释放输入锁 → GameManager 同趟 `TogglePause`。 | 锁释放改 `CallDeferred(nameof(ReleaseInputLock))` 延迟到下一帧。测试适配：5 处锁断言加 `await TestScene.ProcessFrame(2)`。 |
| P1-6 | `src/core/save/SaveService.cs` `RestoreInventory` | **扩容后行错位**（与 P0-2 配套）：恢复前未对齐宽度。 | 先 `ExpandInventory(data.GridWidth)`（单向扩容，旧档更窄则读时钳制）。 |
| P1-7 | `src/world/island/WorldLayout.cs` `MainHeightScale` | **主岛地形压盖建造面**（用户决策修复）：R=32、H=5 → 地表最高 +2.5m，压盖 `Game.tscn` Ground 盒顶（y=0.5）与 BuildingSystem 网格（y=0.5）→ 建筑被埋、双重碰撞面、放置高度不一致。 | **用户选"压低主岛地形贴盒顶"**：`MainHeightScale 5→1`（地表 ≤ +0.5m，贴盒顶；保留轻微起伏）。 |
| P1-8 | `test/src/*`（CraftUILogicTest / StorageBoxTest / SaveServiceTest） | 测试脆弱：锁释放断言同步（与 P1-5 异步化冲突）；`SaveServiceTest` 缺扩容对齐用例。 | 5 处加帧等待；新增 `ExpandedInventoryRestoresAlignedAfterFreshLoad`（先有 7 失败 → 修复后 301/0）。 |
| P1-9 | `src/ui/CraftUI.cs` / `StorageUI.cs` 类注释 | **文档与实现矛盾（quest/ui 审查者）**：注释声称"教程持锁会阻止打开面板"（"e.g. the tutorial blocks opening"），但 `GameplayInputLocked` 只由 CraftUI/StorageUI 自己 Raise，TutorialUI 从不持锁也不检查——教程的独占性来自自身全屏 dim + `TutorialCompleted` 门 + 事件驱动步骤，不依赖输入锁。 | 修正两处类注释：如实说明教程不参与输入锁（教程期间储物箱可开，无软锁）；行为不改。 |

> **P0-4（quest/ui 审查者初判 P0，复核后降级 P1-9）**：该审查者把 CraftUI/StorageUI 类注释当作"契约 4"（实为文档描述），真实契约 4 是 WaterMesh headless 降级——已 PASS。教程不持锁不违反任何 9 条锁契约；章 1 强制教程期间 CraftUI 有 C 键门挡住、StorageUI 无门但教程早期玩家无储物箱，且教程步骤靠事件推进（非模态互斥），不会软锁。故按 P1 文档修复处理。

---

## 4. P2 —— 报告清单（32 项）

> **P2 高价值项修复（第一轮 2026-08-15，提交 5082857，验证 310/0）**：P2-05、P2-06、
> P2-08/P2-08b、P2-17、P2-18、P2-19、P2-24、P2-25、P2-27、P2-28、P2-30 部分、P2-30b——见 §4.2-4.6 标注。
>
> **P2 高价值项修复（第二轮 2026-08-15，提交 f30ac22，验证 313/0）**：
> P2-04（EnemyBase 单次解析缓存 PlayerStats）、P2-07（死亡门覆盖热栏+交互）、
> P2-01（WeaponVisual 加载失败隐藏而非陈旧网格）、P2-20（PlayBgm 同轨免重播）、
> P2-22（移除 BuildingSaved/BuildingLoaded 死 API）、P2-26（复核：FindNearest
> 自 P1-3 起已有 `node is not T` 类型校验，无需改动）。
>
> 其余 14 项仍只报告不修。

### 4.1 视觉 / 表现
- **P2-01** ~~`WeaponVisual` 模型加载失败时显示陈旧网格~~ → ✅ 已修（f30ac22：失败即隐藏+警告）
- **P2-02** `MouseObject` 墙体 0×0 幽灵瓦片（选中瞬间碰撞/网格为空）。
- **P2-03** `Main` 岛敌人与 Ground 盒/建造网格的整合仍依赖 P1-7 压低后的地形（已缓解，未做敌人路径与地形坡度的贴合）。

### 4.2 战斗 / 敌人
- **P2-04** ~~`EnemyBase` 双寻址~~ → ✅ 已修（f30ac22：`_Ready` 单次解析缓存 `_playerStats`）
- **P2-05** ~~`BossPhaseController` 召唤物~~ → ✅ 已修（5082857：类型守卫+夜间继承+生成点抬升）
- **P2-06** ~~`Projectile` 木筏/隧穿~~ → ✅ 已修（5082857：射线扫掠连续碰撞+木筏拦截）
- **P2-07** ~~死亡门未覆盖 E 键/热栏切换~~ → ✅ 已修（f30ac22：InventorySystem 热栏 + PlayerInteraction 均按存活门控）
- **P2-08** ~~spider 减速无消费者~~ → ✅ 已修（5082857：PlayerController 折入 SpeedMultiplier）
- **P2-08b** ~~`SpeedMultiplier` 无消费者~~ → ✅ 已修（同 P2-08）

### 4.3 建造 / 存档
- **P2-09** `BuildingSystem._currentSaveFile` / `LoadMostRecent` 死代码；`RaiseBuildingLoaded` 永不触发（无订阅者）。
- **P2-10** `BuildingSaveSystem.Restore` 不同步 `_freeObjectsList`（重复 restore 累积）。
- **P2-11** `BuildableInstance.Object3DModel!` 空引用（`GetAabb()` 裸调用）。
- **P2-12** `CombatLogic` "axe 分支"注释过期（代码已无该分支）；`BossPhase` 恰 30% 进 phase 3 的注释与实际阈值表述不符。

### 4.4 物理 / 世界
- **P2-13** `FloatingBody.ApplyForce(netForce, GlobalBasis*cell.LocalPosition)` 力臂方向在俯仰/横滚时错误（木筏倾覆时桨/帆力臂错位）。
- **P2-14** `WaterMesh` 5Hz 同步回读仍停顿（已知 TODO，headless 已降级不崩）。
- **P2-15** `WaveGenerator` MapSize 非 2 幂非法 dispatch（无校验）。
- **P2-16** `WorldLayout` 用 `System.Random`，跨运行时确定性无保证（仅同运行时确定）。

### 4.5 服务 / 事件
- **P2-17** ~~`WeatherService.SetWeather` 同值早退不发事件~~ → ✅ 已修（5082857：新增 `RestoreWeather` 读档必发）
- **P2-18** ~~`DayNightService.RestoreTime` 无条件 raise `DayChanged`~~ → ✅ 已修（5082857：仅天数变化才发）
- **P2-19** ~~`AudioManager.SfxPoolSize=0` 除零 + `GetNode` 非守卫~~ → ✅ 已修（5082857）
- **P2-20** ~~`PlayBgm` 每次重播~~ → ✅ 已修（f30ac22：同轨正在播放则 no-op）
- **P2-21** `SfxHook` Storm 注释与 Rain 代码漂移（注释描述与实际挂载不符）。
- **P2-22** ~~`GameEvents.BuildingSaved/BuildingLoaded` 死 API~~ → ✅ 已修（f30ac22：移除）

### 4.6 输入 / UI / 模态
- **P2-23** `GameplayInputLocked` 跨场景残留（静态 bool 在场景切换时不显式复位，依赖各 UI Cleanup）。
- **P2-24** ~~`SaveService` F5/F9 未按 `GameplayInputLocked` 门控~~ → ✅ 已修（5082857）
- **P2-25** ~~`SaveService.LoadGame(null)` 无守卫~~ → ✅ 已修（5082857）
- **P2-26** ~~位置匹配无 Type 校验~~ → ✅ 复核无需改（FindNearest 自 P1-3 起有 `node is not T` 校验）
- **P2-27** ~~`SaveService` SecondaryItemId 恢复后不清~~ → ✅ 已修（5082857）
- **P2-28** ~~`GameSaveData.Version` 不校验~~ → ✅ 已修（5082857：拒绝未来版本）
- **P2-29** `GuideService` 去重误吞（相同文本多次触发只显示一次；0.5s 窗口还会吞普通敌人台词）。
- **P2-30** `HUD` Tab ~~绕过输入锁~~（✅ 5082857 已修）；`CraftUI.Open`/`Toggle` ~~无互斥~~（✅ 5082857 已修）；其余未修：`WeaponSystem` Q 键（实际 `_UnhandledInput` 已有锁检查——复核为误报，无需改）、`FinalizeTutorial` 无条件 Captured 鼠标、`BuildChapters` 重入重复、`StorageUI` 守卫不对称（`Refresh` 未守卫 `_box`/`_playerInventory` 裸引用）、奖励假推进教程 step2（quest_radio 奖励 wood 触发教程 item 步骤）、`StoryInteractable` 无场景实例化且文档不符、deferred 重订阅边缘。
- **P2-30b** ~~`SpawnProjectile` 先设 `GlobalPosition` 再 `AddChild`~~ → ✅ 已修（5082857：先 AddChild 后设位置）。
- **P2-31** `GroundLoot` 类注释与 T8.5.7 代码矛盾（注释说拾取后销毁，代码现在保留余数）。

### 4.7 测试
- **P2-32** `IslandGeneratorTest` 用 `StartsWith` 无 `StringComparison`（CA1310，区域设置敏感）；`EquipmentVisualTest`/`AudioManagerTest` CA1001/CA1822 告警。

---

## 5. 锁契约核对表（9 条）

| # | 契约 | 核对结果 |
|---|------|---------|
| 1 | `PlayerDied` ≠ `GameOver`（死亡=存档点重生，不结束游戏） | ✅ 未动（P0-2 仅加存档字段） |
| 2 | `Game.tscn` = 产品；`ocean_test`/`storm_zone` = 实验室 | ✅ 未动场景职责（P1-7 只改 `WorldLayout` 常量，属产品场景数值，用户已批准） |
| 3 | AI 状态机 + `PlayerMotion` 不改 | ✅ 未动 |
| 4 | WaterMesh headless 降级（`_gpuEnabled=false` + `GetWaveHeight`→0 平海） | ✅ 未动 |
| 5 | 群岛确定性（同 seed 同布局；`System.Random` 跨运行时无保证属 P2-16 报告） | ✅ 未动生成逻辑 |
| 6 | 教程/模态不暂停昼夜 | ✅ 未动 |
| 7 | 事件订阅 `_ExitTree` 退订 | ✅ 未动（StoryPointTrigger 原有退订保留，新增字段不影响） |
| 8 | Godot 4.7：禁 `--`、NodePath 导出不加 `node_paths` | ✅ 未引入违规用法 |
| 9 | 许可纪律（CC0 + CC BY 署名 + SurvivalIsland 自用非商业） | ✅ 未涉及资产变更 |

---

## 6. 验证

- `dotnet build`：0 错误（24 警告，全部为既有 CA 分析告警）。
- `godot --headless --path . --run-tests --quit-on-finish`：**Passed 313 | Failed 0**（P0/P1 修复后 301 → P2 第一轮 310 → P2 第二轮 313，全绿）。
- 修复提交：`fix(code-review)`（P0/P1）、`docs(code-review)`、`chore(state)`、P2 第一轮 `5082857`（战斗/服务/存档/模态输入）、P2 第二轮 `f30ac22`（死亡门/寻址/音频/视觉）。
