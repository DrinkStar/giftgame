# Iter 6 代码审查结论（Grilling 2026-08-14）

> 依据：`游戏设计-定稿.md` + 当前 `game/` 源码。Q1 用户已选「允许改路线图」。Q2–Q4 按代码证据取推荐答案。本次**不改游戏代码**。

## 共识（四问）

| 问 | 锁定 |
|---|---|
| Q1 审查产出 | 允许改路线图：默认不动顺序，若代码会把错误契约焊进 Iter 7 则重排 |
| Q2 死亡契约 | **冻结语义，不提前实现复活**：`PlayerDied` ≠ `GameOver`。Iter 7 **禁止**订阅 `GameOver` 作任务失败 |
| Q3 产品场景 | **`src/Game.tscn` 是产品**；`scenes/ocean_test.tscn` 是实验室。Iter 7 只绑 Game.tscn。不合海、不提前做群岛 |
| Q4 生存断环 | **不打断 Iter 7**。吃喝/采集/床仍属 Iter 8 前置。**复活已改期，见下方补丁** |

**路线图结论（已被后续 grilling 部分覆盖）：** 当时结论是不重排。2026-08-14 二次 grilling 已改：只把最小复活提前为 Iter 7 T7.0，其余 8 前置不动。以 `strategy.md` §5/§6/§9 为准。

### 补丁（2026-08-14 二次 grilling：负债 vs 步骤）

| 锁定 | 内容 |
|---|---|
| 不整段前移 | 喝水/采集/床/可视包仍 Iter 8 前置 |
| T7.0 最小复活 | 停输入 → `PlayerSpawnPoint`（并入原 T8.4）→ 满血、体力满、饿渴安全线默认 30 → 不 RaiseGameOver → **不掉包** |
| T8p.3 | 改为补跑尸掉包（地上可捡） |
| T7.OUT | 禁止死亡=任务失败；不做跑尸/GDSave/采集/模型 |

### 补丁（2026-08-14 三次 grilling：再审执行清单）

| 锁定 | 内容 |
|---|---|
| 复活点槽 | T7.0 实现可替换当前复活点（初始=PlayerSpawnPoint）；T8p.4 造床只改槽 + 跳过夜晚 |
| 掉落 | T8p.3=通用地面掉落组件，仅死亡用；击杀仍进背包，Iter 8.5 再切 |
| 8 前置拆包 | 8p-game 硬挡教程；8p-art 可并行、不硬等 |
| 任务点 | T7.4 只用剧情点 ID，8.5 群岛按 ID 投放 |

以 `strategy.md` 为准。

---

## Q2 死亡契约（证据）

定稿 §4：基地/出生点复活，掉部分背包可跑尸，装备与建筑无损。

现状：

- `PlayerStats.Health` 归零时 `_deathNotified` 保证 `RaisePlayerDied()` **只发一次**（已修上游每帧重复发射）。
- `GameManager.OnPlayerDied` 打印后立刻 `RaiseGameOver()`。
- 全工程 `GameOver` **零订阅**（仅 `GameEvents` 定义 + `GameManager` 这一处 Raise）。
- `PlayerController._PhysicsProcess` **不读** `Stats.IsAlive`：死后仍走、跳、冲刺、攻击。
- `PlayerSpawnPoint` 导出存在，Game.tscn 未接线。

判定：这是 Iter 7 唯一会焊死的错误模型。任务系统一旦把 `GameOver`/`PlayerDied` 当成章节失败，软惩罚就无法无损接入。

锁定：

1. `PlayerDied` = 角色死亡（将来复活的钩子）。
2. `GameOver` = 真结局/不可恢复结束；首版仅「击败鲨鱼王后的后日谈」之前不用。
3. Iter 7 可订阅 `PlayerDied` 做旁白，**不得**订阅 `GameOver`，**不得**把死亡写成任务失败。
4. **已改期**：最小复活是 Iter 7 **T7.0**（不是「去掉 Raise 就算完」）。跑尸掉包仍是 T8p.3。

---

## Q3 产品场景（证据）

- `Main.RunScene()` 固定切到 `res://src/Game.tscn`。
- Game.tscn：平地 + 玩家 + 生存/建造/种植/战斗；**无海、无浮力、无木筏**。
- `ocean_test.tscn`：海浪 shader + `FloatingBody` 箱子；鲨鱼实例 **Player 未接线** → `EnemyBase` 在 `_player==null` 时速度清零（实验室契约，不是产品行为）。
- `Game.cs` 是空壳；世界行为全在子节点。

锁定：Quest / 教程 / 全局存档只绑 Game.tscn。ocean_test 保持实验室。合海与木筏仍 Iter 8.5（P1.1/P1.2）。Iter 7 投放点写死在平地。

---

## Q4 生存断环不打断 Iter 7（证据）

已有、但玩法未闭环：

| 能力 | 代码 | 缺口 |
|---|---|---|
| 吃/喝 API | `PlayerStats.Eat`/`Drink` | 仅测试调用；无热键/使用 |
| 椰子 | `assets/items/coconut.tres` Type=Drink, ThirstRestore=25 | 无世界采集物；`IInteractable` 只有 `FarmPlot`/`Livestock` |
| 饥饿/口渴 | `_Process` 持续扣，空了扣血 | 玩家无法补充 → 终将死，然后变成 Q2 的假 GameOver |
| 床 | 无 | 体力靠冲刺后自动回 |

判定：断环会让「活着玩任务」在长时间运行中失败，但 Iter 7 范围是旁白+事件骨架，不依赖吃喝采集。用死亡契约（Q2）挡住焊死即可，不必把 P0.3/P0.4 提前。

---

## 架构是否扛得住剩余定稿

**能扛：** 静态 `GameEvents` 总线 + 纯逻辑抽取（`CombatLogic`/`CropLogic`/`LivestockLogic`/`DayNightMath`/`WeatherLogic`/`BuoyancyMath`/`GridMath`）+ GoDotTest 135 全过。任务系统按设计应只订事件、不持有系统引用——现有 `EnemyDied`/`BossDefeated`/`CropHarvested` 已是 raise-only，正好给 T7.3 用。

**扛不住、必须记账（不在 Iter 7 修）：**

1. **存档分裂**：只有 `BuildingSaveSystem`（F5/F9 → `user://buildings`）。作物/驯养/库存/三指标/昼夜不进档。GDSave 仍 Iter 8。
2. **`BuildableResource` 无耐久字段**（定稿 §11 说预留）。风暴只改雾。
3. **击杀掉落进背包**（`EnemyBase.TryDropLoot`），与跑尸地上捡冲突。Iter 8 软惩罚前必须改成世界掉落或明确「死亡掉落≠击杀掉落」。
4. **武器匹配用 `Contains("spear"|"bow")`**：任务发奖、铁矛 `iron_spear` 会碰巧命中；火把是 Tool 会近战挥砍。发奖必须走 Id 表，不能靠子串。
5. **矛投掷 `RemoveItem` 后热键空**，定稿是近战+投掷仍在手。
6. **铁锭配方 `RequiresWorkbench=true`**，定稿是熔炉。
7. **SurvivalIsland 无许可**：自用非商业已决；商用前授权或重写。

---

## Iter 7 开工清单（审查冻结；含 T7.0 补丁）

IN：T7.0 最小复活（含出生点接线）→ T7.1–T7.4（Quest 桥接、Quest 事件、引导者订现有死亡/收获/Boss 事件、三章数据写死在平地）。

OUT（硬）：

- 不得把 `PlayerDied` 写成任务失败或硬结束。
- 不做跑尸掉包（T8p.3）、GDSave、采集、模型、合海。
- 可订阅 `PlayerDied` 做旁白。

T7.0 必须：停输入、传送 `PlayerSpawnPoint`、满血、体力满、饿渴≥安全线（默认 30）、不 `RaiseGameOver`。

- Quest 奖励按物品 Id，禁止 `Contains("spear")` 式匹配。

下一步：按 `strategy.md` §5 写 Iter 7 正式计划并开工。
