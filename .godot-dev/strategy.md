# SeaAnomaly（海域异变）— 代码初步集成方案

> 生成日期：2026-08-13 ｜ 依据：`游戏设计-定稿.md` + `godot-refs/集成清单.md` + 骨架实测
> 更新：2026-08-14 ｜ Iter 6 verified 后只读审查，按 P0/P1/P2 补路线图与缺口清单
> 更新：2026-08-14 ｜ Grilling 代码审查结论（Q1 允许改路线图；**不重排**，冻结死亡/场景契约）→ `.godot-dev/iter6-code-review.md`
> 更新：2026-08-17 ｜ 崩溃日志迭代（不重排玩法）：分析 + 任务清单见 `.godot-dev/crash-log-plan.md`
> 状态：code-review 已收官（313/0，`dbfb2d0`）；当前待批 = **crash-logging**

## 0. 当前状态（Iter 0–6 已验证）

| 验证项 | 结果 |
|---|---|
| 工程骨架 | ✓ chickensoft 模板，重命名 SeaAnomaly（Godot 4.7.1 .NET + net8.0 + Forward Plus） |
| 已落地系统 | ✓ 玩家/相机、海洋+浮力、生存三指标、库存合成、网格建造、种植驯养、战斗框架 |
| 编译 | ✓ `dotnet build` 0 错误 |
| 冒烟测试 | ✓ `godot --headless --path . --quit-after 5` exit 0 |
| 单元测试 | ✓ GoDotTest Passed 135 Failed 0 |
| Git | ✓ `dev/iteration6`，checkpoint `phase3_iteration6_verified` |
| 明确未做 | 任务/`src/quest/`、GDSave、教程、`IslandGenerator`、世界采集、进食喝水、软惩罚复活 |

**Grilling 2026-08-14（不重排 Iter 7）**：`PlayerDied` ≠ `GameOver`；产品场景=`src/Game.tscn`；吃喝采集不提前。结论：`.godot-dev/iter6-code-review.md`。

**关键发现（已验证）**：Godot 4.7 下 GoDotTest 调用**不能用 `--` 分隔符**，参数直接跟在 godot 选项后：
```
godot --headless --path . --run-tests --quit-on-finish   # ✓ 正确
godot --headless --path . -- --run-tests ...             # ✗ 测试静默不执行
```

> **注**：复用策略以设计定稿 §13 为准（最大化复用 godot-refs）；缺失资源的外部获取渠道以定稿 §13.5 为准（见 §1 技术基线）。

## 1. 技术基线

- **引擎**：Godot 4.7.1 .NET 版（`D:\Godot\Godot_v4.7.1-stable_mono_win64`，窗口/headless 均可用）
- **渲染**：Forward Plus（海洋计算着色器硬性要求，已配置）
- **架构**：chickensoft 体系（GoDotTest 测试、GameTools 显示适配）+ **C# 事件信号总线**（SurvivalIsland 的 GameManager 模式——各系统通过静态 C# 事件解耦，见 §4）
- **存档**：GDSave（纯 C# 库，MIT，复制 .cs 进工程）
- **任务**：DotnetQuestSystem（MIT，NuGet 引入 `DotNetQuestSystem.Core` 或复制 addons）
- **资源获取（定稿 §13.5 规则）**：`godot-refs` 缺失的图片/图标/3D 模型/场景资源**仅从** opengameart.org、kenney.nl、craftpix.net、quaternius.com、opensource3dassets.com、game-icons.net 获取；音乐/音效/配乐**仅从** soundimage.org、freesound.org、aigei.com 获取。逐项核对许可（Kenney/Quaternius 为 CC0；game-icons.net 为 CC-BY 需署名），下载后记录来源 URL 与许可类型至 `.godot-dev/state.json` 与 Git 提交；带 NC（非商用）/ND（禁止演绎）条款的资源仅限原型占位，商用发布前必须替换或取得授权。

## 2. 集成矩阵（设计需求 × 参考仓库 × 集成方式）

| # | 系统（定稿要求） | 来源仓库 | 方式 | 语言/许可 | 风险 |
|---|---|---|---|---|---|
| 1 | 玩家控制器（第三人称*） | chickensoft-GameDemo | 参考移植 | C#/MIT | 低 |
| 2 | 海洋视觉 + 木筏浮力 | 2Retr0-GodotOceanWaves + ManickYoj（浮力思路） | shader 直接复用 + C# 重写胶水 | GLSL/GDScript→C# | 中（Forward+ 计算着色器） |
| 3 | 生存三指标/库存/合成/昼夜/天气 | srperens-SurvivalIsland | 代码级移植（用户决策：自用非商业化；原无许可仓库，风险已告知） | C# 架构蓝本 | 中（无许可⚠️，自用非商业） |
| 4 | 网格建造 | MarkoDM-GodotInGameBuildingSystem | 直接移植（26 .cs） | C#/MIT | 中（4.1→4.7 API 差异） |
| 5 | 种植/驯养 | 无现成 C# 组件 | 自写（概念参考 GDScript 项目） | — | 低 |
| 6 | 战斗（矛/弓/敌人/Boss） | 无现成 C# 组件 | 自写 | — | 中（工作量最大） |
| 7 | 任务/三章剧情 | TRUINGLol-DotnetQuestSystem | NuGet/直接集成 | C#/MIT | 低 |
| 8 | 存档 | dxdesjardins-GDSave | 复制 .cs 进工程 | C#/MIT | 低 |
| 9 | 程序化群岛世界 | SurvivalIsland ForestGenerator 思路 | 自写（A1 投放机制） | — | 中 |

\*视角已决为第三人称（Iter 1）。矩阵 1–6 已落地；#7→Iter 7，#8→Iter 8，#9→Iter 8.5（见 §5 / §9）。*

## 3. 目录架构（目标结构）

```
game/
├── project.godot
├── SeaAnomaly.csproj / SeaAnomaly.sln
├── src/
│   ├── Main.cs / Main.tscn          # 入口（保留模板逻辑，仅场景路径可能调整）
│   ├── Game.cs / Game.tscn          # 主场景（替换为 3D 根场景）
│   ├── core/                        # 全局服务：GameManager(信号总线)、SaveService(GDSave)、
│   │                                #   DayNightService、WeatherService、QuestService(DotnetQuest)
│   ├── player/                      # PlayerController、PlayerStats、Camera、InteractionRay
│   ├── world/                       # OceanShader 集成、IslandGenerator、FloatingBody(浮力)、StormZone
│   ├── building/                    # MarkoDM 移植：GridSystem、Buildable、PlacementPreview、BuildingSave
│   ├── inventory/                   # ItemData、InventorySystem、CraftingSystem、RecipeDB
│   ├── farming/                     # CropPlot、CropData、AnimalPen(驯养)
│   ├── combat/                      # WeaponBase、Spear、Bow、EnemyBase、SharkKing(Boss)
│   ├── quest/                       # 引导者事件流 + DotnetQuestSystem 桥接
│   ├── progression/                 # 预留：ITalentTree / ISkill / ProgressionService（空实现）
│   └── ui/                          # HUD、InventoryUI、CraftUI、QuestUI、TutorialUI
├── assets/progression/              # 预留：talents/*.tres、skills/*.tres（首版可空）
├── test/src/                        # 每模块对应测试（GoDotTest）
└── scenes/                          # 场景文件（或与脚本同目录，随 chickensoft 惯例）
```

## 4. 信号总线约定（解耦核心）

沿用 SurvivalIsland 的静态 C# 事件模式，`GameManager` 为事件宿主：
- 生存事件：`OnHealthChanged / OnHungerChanged / OnThirstChanged / OnStaminaChanged / OnDeath`
- 世界事件：`OnDayNightChanged / OnWeatherChanged / OnTimeTick`
- 库存事件：`OnInventoryChanged / OnItemCrafted`
- 剧情事件：`OnQuestStarted / OnQuestProgress / OnQuestCompleted / OnStoryPointReached`
- 成长预留：`TalentUnlocked(id)` / `SkillActivated(id)`（raise-only；首版无订阅者）
各系统只依赖事件，不互相持有引用——这是三章剧情投放（A1 机制）与教程渐进教学的技术基础。

## 5. 迭代路线图（每迭代 = 计划→实现→验证→提交）

审查锁定顺序：Iter 6 → Iter 7（T7.0 复活槽 + 任务 ID）∥ **R1 成长接口预留** ∥ **R2 建筑耐久字段预留** → 8p-game（喝/采木+椰子/跑尸/床/火把灯）∥ 8p-art（最小可视包）→ Iter 8（教程硬等 8p-game；CraftUI + 存档总控）→ Iter 8.5（**先合海** → 群岛绑定剧情点 + 击杀掉落切地面 + 储物/夜间/日志/按章教程）→ Iter 9。

| 迭代 | 内容 | 依赖 | 产出 | 风险 |
|---|---|---|---|---|
| Iter 0 | 骨架 ✅ | — | 已验证 | 已消除 |
| **Iter 1** | 3D 玩家控制器 + 第三人称相机 + 基础测试场景 ✅ | GameDemo | 可跑动的 3D 角色 | 低 |
| **Iter 2** | 海洋（2Retr0 shader 移植）+ 简单岛屿 + 浮力 ✅ | Iter 1 | 可航行的海面（在 ocean_test） | 中 |
| **Iter 3** | 生存核心：三指标/库存/合成/昼夜/天气 ✅ | Iter 1 | 生存数值+HUD（进食喝水未接线，见 P0.3） | 中 |
| **Iter 4** | 网格建造（MarkoDM 移植）+ 建造存档 ✅ | Iter 3 | 可放置建筑（仅建筑 JSON） | 中 |
| **Iter 5** | 种植 6 作物 + 产出型驯养 ✅ | Iter 3/4 | 食物闭环（状态不持久化） | 低 |
| **Iter 6** | 战斗：矛/弓 + 8 敌人数据 + 鲨鱼王 Boss 数据 ✅ | Iter 1/3 | 可战斗（4 敌人行为占位） | 高 |
| **Iter 7** | 最小复活槽（T7.0）+ 任务/引导者/三章骨架（剧情点 ID，无坐标） | Iter 3-6 | 死了能爬起来；主线可订阅；不做跑尸/存档/采集 | 中 |
| **R1** | 天赋树/技能**接口预留**（空实现，无 UI） | Iter 6 | 战斗/生存可查询修饰符；首版倍率恒 1 | 低 |
| **R2** | 建筑 `MaxDurability` 字段预留 | Iter 4 | 蓝图/存档占位；首版不扣不坏 | 低 |
| **8p-game** | 喝水/采木+椰子/地面掉落/床改复活槽+睡回体力/火把照明 | Iter 7 | 教程 6 项玩法可走通 | 中 |
| **8p-art** | 最小可视包（可与 8p-game 并行） | Iter 7 | 教程可辨认；**不硬挡** T8.3 | 低 |
| **Iter 8** | 教程（15 分钟强制段，日长 20–30 分钟）+ GDSave 总控 + 最小 CraftUI | 8p-game | 可玩闭环；作物/驯养/库存/三指标/建筑同一套 F5 | 中 |
| **Iter 8.5** | **T8.5.0 合海** → 群岛/木筏/剩余建筑与工具/敌人特化/储物真库存/夜间加成/日志交互/按章教程 | Iter 8 | 定稿玩法广度达标 | 高 |
| **Iter 9** | 完整模型/图标/配乐替换 + 性能优化 + 导出 | 全部 | 可发布观感 | 低 |

**依赖关系**：Iter 7 先 T7.0 再接任务。R1 / R2 可与 Iter 7 / 8p-art 并行，**不挡教程**。教程 **硬等 8p-game**，不硬等 8p-art / R1 / R2。合海是 Iter 8.5 **第一项**（T8.5.0），不提前到教程前；群岛生成依赖合海。击杀掉落改走地面组件也在 8.5。完整天赋加点不做进首版。

**表现资产两刀（审查已锁）**：Iter 7 继续几何体占位；Iter 8 前补最小可视包；Iter 9 完整替换。不新开独立美术迭代插在 Iter 7 前。

## 6. 首批迭代详细任务清单（Phase 3 直接可执行）

### Iter 1：玩家控制器与相机
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T1.1 | 移植 GameDemo 第三人称控制器为 SeaAnomaly 风格 | `src/player/PlayerController.cs` | 无 | dotnet build |
| T1.2 | 创建 3D 测试场景（地面 + 光照 + 角色） | `src/Game.tscn`（重写；地面并入 Game.tscn） | T1.1 | 冒烟测试 |
| T1.3 | 输入映射（移动/跳跃/交互/攻击） | `project.godot` [input] | T1.1 | 场景运行 |
| T1.4 | 控制器单测（移动输入→速度） | `test/src/PlayerMotionTest.cs` | T1.1 | GoDotTest |

### Iter 2：海洋与浮力
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T2.1 | 移植 2Retr0 海浪着色器与 ShaderGlobals | `src/world/ocean/`（.gdshader 复用） | Iter 1 | 冒烟（渲染无错） |
| T2.2 | C# 重写海洋主循环（原 main.gd 是 GDScript） | `src/world/OceanController.cs` | T2.1 | dotnet build |
| T2.3 | 浮力组件（Area3D 探针 + RigidBody 施加力，参考 ManickYoj 格点思路） | `src/world/BuoyantBody.cs` | T2.2 | 单测（静水浮力≈重力） |
| T2.4 | 最小岛屿（静态网格体 + 海滩材质） | `scenes/island_test.tscn` | T2.1 | 冒烟 |

### Iter 3：生存核心
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T3.1 | GameManager 信号总线 | `src/core/GameManager.cs` | 无 | dotnet build |
| T3.2 | 三指标 PlayerStats（数值按定稿：饥饿/口渴/体力） | `src/player/PlayerStats.cs` | T3.1 | 单测（消耗/死亡） |
| T3.3 | 昼夜循环 | `src/core/DayNightService.cs` | T3.1 | 单测（时间推进） |
| T3.4 | 天气系统（含风暴） | `src/core/WeatherService.cs` | T3.3 | 单测 |
| T3.5 | 库存 + 合成（木/石/铁三阶） | `src/inventory/*` | T3.1 | 单测（配方） |
| T3.6 | HUD 基础显示 | `src/ui/HUD.cs` | T3.2-3.5 | 场景运行 |

> Iter 4-6 已在各 `.omo/plans/sea-anomaly-iterN-*.md` 落地。以下为 Iter 7 起审查补入的任务清单（执行前仍按该格式出正式计划）。

### Iter 7：最小复活 + 任务骨架（世界继续占位）
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T7.0 | 最小复活：Game.tscn 接线 `PlayerSpawnPoint`；**当前复活点槽**（初始=该节点，床以后可改）；死亡停输入；传送当前槽；满血、体力满、饿渴安全线默认 30；**不** `RaiseGameOver`；不掉包 | `GameManager`、`PlayerController`、`PlayerStats`、`Game.tscn` | P0.1 最小集 | 单测：死一次→活着、不发 GameOver、饿渴≥30；槽可被测试替换 |
| T7.1 | 接入 DotnetQuestSystem + `src/quest/` 桥接 | `src/core/QuestService.cs`、`src/quest/` | T7.0、Iter 3 事件总线 | dotnet build |
| T7.2 | GameEvents 补 Quest 事件（Started/Progress/Completed/StoryPointReached） | `src/core/GameEvents.cs` | T7.1 | 单测退订 |
| T7.3 | 引导者旁白流（只闻其声，无 NPC）订阅现有 `EnemyDied`/`CropHarvested`/`BossDefeated` | `src/quest/` | T7.2 | 场景运行 |
| T7.4 | 三章任务数据：只引用剧情点 ID（如 `ch1.radio` / `ch2.ruin` / `ch3.shark_king`），完成条件走事件；**不写世界坐标** | `assets/quests/` | T7.1 | 单测：按 ID 加载；无 Vector3 |
| T7.OUT | **禁止死亡=任务失败**；不做跑尸掉包、GDSave、采集、模型、合海、天赋 UI | — | — | 范围保真 |

### R1：天赋树 / 技能接口预留（可与 Iter 7、8p-art 并行，不挡教程）

首版**不开**加点 UI、技能栏、等级、经验。只留契约，默认空实现（倍率恒 1）。完整树留续作/首版后，见定稿 §11。

| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| R1.1 | 数据蓝图：`TalentData`（Id/DisplayName/Requires[]/StatModifiers[]）、`SkillData`（Id/StaminaCost/CooldownSeconds/EffectId） | `src/progression/*.cs`、`assets/progression/`（可空目录） | 无 | 可 `GD.Load`；无 UI 资源也可 |
| R1.2 | 接口：`IModifierSource.GetMultiplier(statId)→float`；`ITalentTree`（`IsUnlocked`/`Unlock`/`AllIds`）；`ISkill`（`CanActivate(SkillContext)`/`Activate`）；`ISkillHost.TryActivate(skillId)`；`SkillContext`（Player/Target/SelectedItem） | `src/progression/` | R1.1 | 单测：Null 实现 Unlock 不抛、倍率=1、TryActivate=false |
| R1.3 | `ProgressionService`（Node）：持有 `ITalentTree`+`ISkillHost`，默认 `NullTalentTree`/`NullSkillHost`；对外 `GetMultiplier` | `src/progression/ProgressionService.cs` | R1.2 | 未接线时等同恒 1，不崩 |
| R1.4 | GameEvents raise-only：`TalentUnlocked(string)`、`SkillActivated(string)` | `src/core/GameEvents.cs` | R1.2 | 单测退订；零订阅合法 |
| R1.5 | 调用点只读接口（不改战斗规则）：近战/投掷/弓伤害、体力消耗乘 `GetMultiplier("melee_damage"|"throw_damage"|"ranged_damage"|"stamina_cost")`；服务未接线则跳过（×1） | `WeaponSystem`、`PlayerStats`/`PlayerController` | R1.3 | 现有 Combat 测试仍全绿；注入假树倍率=2 时伤害翻倍 |
| R1.OUT | 无天赋 UI、无技能热键、无 XP/等级、不填真实天赋节点、不改敌人 AI | — | — | 范围保真 |

约定 `statId`（接口稳定，首版全部走默认 1）：`melee_damage`、`throw_damage`、`ranged_damage`、`stamina_cost`、`hunger_rate`、`thirst_rate`、`move_speed`、`max_health`。

### R2：建筑耐久字段预留（可与 Iter 7 / 8p-art 并行，不挡教程）

定稿 §3/§11：无基地袭击，耐久字段预留。首版不扣血、不摧毁、无维修 UI。真扣血留给 8.5 风暴打木筏。

| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| R2.1 | `BuildableResource.MaxDurability`（0=无敌）+ 实例运行时当前耐久；存档 DTO 预留字段 | `src/building/`、Save DTO | Iter 4 | 可导出；缺字段读档不崩 |
| R2.OUT | 不扣耐久、不摧毁建筑、无维修、不接风暴伤害 | — | — | 范围保真 |

### 8p-game：教程玩法债（硬挡 T8.3）
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T8p.1 | 手持 Food/Drink 使用 → `Eat`/`Drink` 并消耗 1。**本任务不读 `RequiresCooking`**（烹饪门=T8.4b） | `src/player/` 或 HUD 热键 | P0.3 | 单测+场景；浆果可解渴 |
| T8p.2 | 世界采集 **木头和椰子都做**（`IInteractable`）。木供造床闭环；椰子满足海滩教程期保底 | `src/world/` 或 `src/player/` | P0.3 | 两种资源射线都能采 |
| T8p.3 | 通用地面掉落组件（场景+拾取）。死亡掉部分背包走它。**击杀仍进背包**，改期 Iter 8.5 | `src/world/` 或 `src/inventory/` | T7.0 | 单测：死→地上有物→捡回；不改 EnemyBase |
| T8p.4 | 床：跳过夜晚 + 把 T7.0 复活点槽改到该床 + **跳过夜晚时体力回满**。日常体力仍被动回复。耗木，数量须大于开局造完篝火后的余量（0） | `assets/buildables/`、`src/building/` | T7.0 槽 | 造床后死亡落到床；睡一次体力满；F9 后床仍在 |
| T8p.6 | 手持 `torch` 在玩家下挂灯，切走关掉。**仍可当弱近战**（不改武器匹配） | `src/player/` 或 `src/combat/` | 定稿 §3 照明 | 持火把时 OmniLight 开；切斧灯关 |

### 8p-art：最小可视包（与 8p-game 并行，不硬挡教程）
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T8p.5 | 女主角/海蟹/野猪/篝火模型 + 7 个物品图标 + 5 个短音效 | `assets/`，渠道见 §9 P2 | 定稿 §13.5 | 帧非胶囊；state.json 记许可。无资产时 HUD 仍可用 DisplayName |

### Iter 8：教程 + GDSave + 最小合成 UI
| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T8.1 | 复制 GDSave；**`SaveService` 总控**。F5/F9 与启动自动读只调它；内部复用现有 `BuildingSaveSystem`（不重写建筑序列化）。覆盖库存/三指标/昼夜/天气；DTO **预留** `UnlockedTalentIds: []`（可空，不读树） | `src/core/SaveService.cs`、`BuildingInput` | P0.2 | 一键存：房子+肚子都在；缺字段不崩；不再出现两套档 |
| T8.2 | 农田 `_crop/_elapsed/_ready` 与驯养 `_state/_produceElapsed` 进档 | `src/farming/`、Save DTO | T8.1 | 种下→存→读仍在 |
| T8.3 | 15 分钟强制教程 6 项：移动、采集、喝水、造篝火、造床、基础战斗。**`Game.tscn` 把 `DayDurationMinutes` 调到 20–30 真实分钟**（定稿 §12 数值仍待定，此处只保教程能教完）。不暂停昼夜 | `src/ui/TutorialUI` + 任务 | **8p-game**、T7.0 复活槽 | 出生点/床复活可用；无模型也可跑；15 分钟内日夜不超过约 1 轮 |
| T8.4 | 最小 CraftUI：C 键列出当前可做配方；靠近篝火/工作台才显示对应项 | `src/ui/CraftUI` | T3.5、StationLinker | 场景：近篝火能做熟肉；远离不能 |
| T8.4b | `Eat` 拒绝 `RequiresCooking==true`（提示「需要烹饪」）。T8p.1 故意不拦，本任务在 CraftUI 之后接线 | `src/player/` | T8.4、T8p.1 | 生肉不能生吃；熟肉可以；浆果仍可吃 |

第一章强制 UI 教程仅此一次。按章第二套/第三套在 Iter 8.5。

### Iter 8.5：P1 内容补全（合海第一）
**禁止**把合海提前到教程前。`ocean_test.tscn` 保持实验室。

| ID | 任务 | 文件 | 依赖 | 验证 |
|---|---|---|---|---|
| T8.5.0 | 把 Iter 2 海洋 + 浮力并入 `Game.tscn`；`ocean_test` 仍作实验室 | `src/Game.tscn`、`src/world/ocean/` | Iter 2、Iter 8 | 产品场景有海；浮力箱/木筏可测；ocean_test 不删 |
| T8.5.1 | 程序化群岛 + **按 T7.4 剧情点 ID 投放** | `src/world/` | T8.5.0、T7.4 | 无硬编码世界坐标；`ch1.radio`/`ch2.ruin`/`ch3.shark_king` 能解析到节点 |
| T8.5.2 | 木筏桨→帆→锚 | `src/world/` | T8.5.0 | 能离开出生岛 |
| T8.5.3 | 剩余功能建筑（烹饪灶、晾晒架、净水器、T2/T3、熔炉、纺织机、研究台、灯塔、捕兽陷阱、蜂箱） | `assets/buildables/` | Iter 4 | 能造；熔炉接铁锭配方 |
| T8.5.4 | 镐/镰/鱼竿、铁矛铁弓、护甲三档、背包扩容；**投掷矛不消耗**（生成抛体，背包矛仍在；弓仍耗箭） | `assets/items/`、`WeaponSystem` | P1.4 | 投一次矛热键仍有矛 |
| T8.5.5 | 4 敌人行为特化 + **夜间伤害/移速乘导出常数（默认 1.25）**，白天还原；不改 AI 状态机 | `src/combat/` | DayNightService | 夜里打一下比白天疼；白天乘数=1 |
| T8.5.6 | 料理扩到 12+；铁锭改挂熔炉 | `assets/recipes/` | T8.4、T8.5.3 | 工作台不再出铁锭 |
| T8.5.7 | 击杀掉落改走 T8p.3 地面组件 | `EnemyBase` | T8p.3 | 杀怪地上有物，不直接进包 |
| T8.5.8 | 储物箱独立库存 + 打开时与玩家背包互转；箱内物品走 T8.1 总控进档 | `src/building/`、Save DTO | T8.1 | 存入→存档→读档箱内仍在 |
| T8.5.9 | 通用 `StoryInteractable`（剧情点 ID + 文本 + `StoryPointReached`）；群岛投放把 `ch2.ruin` 绑上。壁画同一组件换文本 | `src/world/` 或 `src/quest/` | T7.2、T8.5.1 | 走近读日志 → 事件；任务可 Complete |
| T8.5.10 | 第二章强制 TutorialUI：种一格农田 + 与 `ch2.ruin` 日志交互 | `src/ui/TutorialUI` | T8.3、T8.5.9、Iter 5 | 能跳过则须先完成两步 |
| T8.5.11 | 第三章强制 TutorialUI：登上木筏并前进一段。**鲨鱼王不开强制教程** | `src/ui/TutorialUI` | T8.5.2 | 离岛一段距离即完成 |

仍不塞进 Iter 7。双武器槽**不排期**（热键冒充，见 P1.5）。

### Iter 9：完整表现 + 打磨
9 敌人模型、15 建筑、主角三档换装、四群系环境、其余物品图标、配乐；性能与导出。NC/ND 商用前替换。

## 7. 待决问题（需要用户确认）

1. **视角**：**已决（Iter 1）**——第三人称（GameDemo 移植，`src/player/PlayerCamera.cs`）。
2. **SurvivalIsland 授权**：**已决（2026-08-13）**——直接代码级移植，用户决策 2026-08-13（自用非商业化，不申请授权；原无许可仓库，风险已告知并接受；若未来商业化须先取得作者授权）。
3. **场景组织惯例**：chickensoft 推荐"场景与脚本同目录同名"（利于 VSCode 调试配置）。方案默认采用。
4. **渲染目标**：Forward Plus 已定，但低端机回退（gl_compatibility）是否要做首版？（海洋 shader 依赖 Forward+，回退即无海——默认首版不做回退。）

## 8. 审批门

- 批准本方案 → 进入 Phase 3，按 Iter 1 任务清单执行（每个迭代完成后提交 Git + 更新 state.json + 汇报）。
- 需要调整 → 指出修改点，我更新方案后重新提交。
- 视角/授权等 §7 问题请一并表态。
- Iter 6 之后：7（T7.0→任务 ID）∥ R1 ∥ R2 → 8p-game ∥ 8p-art → 8（硬等 8p-game）→ 8.5（先 T8.5.0 合海）→ 9。
- **成长预留**：首版装备成长不变；R1 只做 `ITalentTree`/`ISkill`/修饰符查询 + Null 实现。完整天赋树不做进首版（定稿 §11）。
- **Grilling 2026-08-14 再审已锁**：① T7.0=可替换复活点槽（初始出生点，床改槽）；② T8p.3=通用地面掉落，击杀仍进包至 8.5；③ 教程不硬等可视包；④ T7.4 只用剧情点 ID。产品场景=`src/Game.tscn`。
- **Grilling 2026-08-14 遗漏审查已锁**：见 §6 新任务 ID；摘要：T8p.2 木+椰子、T8p.4 睡回体力、T8p.6 火把灯（仍可近战）、T8.1 存档总控、T8.3 日长 20–30 分钟、T8.4 CraftUI、T8.4b 烹饪门、T8.5.0 合海第一、8.5 储物/夜间/日志/投矛不耗/按章教程、R2 耐久字段、P1.5 热键冒充双槽延期。

## 9. 缺口清单（2026-08-14 Iter6 后只读审查）

对照 `游戏设计-定稿.md`。P0 挡 Iter 8 教程；P1 首版要有但未排期（进 Iter 8.5）；P2 按已锁两刀补资产。证据路径相对 `game/`。

### P0 系统骨架（下一迭代必须面对）

| ID | 缺口 | 定稿 | 现状（证据） | 排入 |
|---|---|---|---|---|
| P0.1 | 死亡软惩罚 | §4 基地复活、掉部分背包可跑尸 | 现 GameOver；死后仍可操作 | **T7.0 复活槽**；跑尸=T8p.3 地面组件 |
| P0.2 | 全局存档 | §13.1 GDSave；Iter 8 | 仅建筑 JSON；与 F5 双档 | **T8.1 总控**（F5/F9 走 SaveService）+ T8.2 |
| P0.3 | 喝水 + 采集 | §6 / §9 教程 | Eat/Drink 未接线；无世界采集 | **8p-game** T8p.1；T8p.2=木**和**椰子 |
| P0.4 | 床 | §5 复活点+跳过夜晚；§6 体力靠睡 | 库里无床；体力被动回 | **T8p.4**（改槽 + 跳夜 + 体力回满；被动回复保留） |
| P0.5 | 任务/引导者 | §7 | 无 quest | Iter 7；T7.4 仅 ID |
| P0.6 | 教程钩子 | §9 15 分钟 + 按章渐进 | 无 TutorialUI | T8.3（硬等 8p-game，日长 20–30 分钟）；按章强制 UI=T8.5.10/11 |
| P0.7 | 死亡流契约 | 不可焊成硬结束 | T7.0 起不 RaiseGameOver；禁止死亡=任务失败 | T7.0 + T7.OUT |

**P0 代码质量（不修，只记账；随对应迭代还）：**

- 矛投掷 `RemoveItem` 后热键空，定稿矛应近战+投掷仍在手。**已锁**：Iter 8.5 T8.5.4 投掷不耗矛。
- 击杀掉落直接进背包（`EnemyBase.TryDropLoot`），与跑尸地上捡冲突。**已锁**：T8p.3 先做通用地面组件（仅死亡用）；击杀切过去 = T8.5.7。
- `ResolveAttack` 靠 `Contains("spear"|"bow"|"axe")`，任务发奖应按 Id 表。（仍记账，不单开任务）
- 火把无光源。**已锁**：T8p.6 手持发光；**仍可弱近战**。
- 风暴只改雾，不损坏木筏。**已锁**：R2 先占 `MaxDurability`；真扣血随 8.5 风暴打木筏。
- 储物箱无独立库存。**已锁**：T8.5.8。
- 铁锭配方挂工作台，定稿是熔炉。**已锁**：T8.5.6。
- `Game.tscn` 与 `ocean_test.tscn` 未合并；主场景无海。**已锁**：Game.tscn=产品，ocean_test=实验室；**T8.5.0 合海第一**，不提前到教程前。
- SurvivalIsland 无许可：商业化前授权或重写。
- 无 CraftUI，C 键休眠。**已锁**：T8.4；烹饪门 T8.4b（8p-game 的 Eat 不拦生肉）。
- `DayDurationMinutes=0.5`。**已锁**：T8.3 调到 20–30 真实分钟。
- 敌人不读昼夜。**已锁**：T8.5.5 夜间乘数。
- 无日志/遗迹交互。**已锁**：T8.5.9 `StoryInteractable`。

### P1 玩法广度（首版要有，进 Iter 8.5）

| ID | 缺口 | 定稿 | 现状 | 说明 |
|---|---|---|---|---|
| P1.1 | 程序化群岛 + A1 投放 | §3 | 无岛；任务先 ID | T8.5.1（依赖 T8.5.0 合海） |
| P1.2 | 木筏桨→帆→锚 | §5 | 无木筏；浮力只在 ocean_test 箱子 | T8.5.2 |
| P1.3 | 其余功能建筑 | §5 共 15 | 已有：篝火、储物箱（摆设）、工作台 T1、驯养栏；农田不在 15 列表。缺：烹饪灶、晾晒架、净水器、床（已 P0.4）、T2/T3、熔炉、纺织机、研究台、灯塔、捕兽陷阱、蜂箱 | 床=T8p.4；储物真库存=T8.5.8；其余=T8.5.3 |
| P1.4 | 工具/武器/护甲 | §8 | 有石斧、火把、木矛、木弓、箭。缺镐、镰、鱼竿、铁矛铁弓、布/皮/铁甲、背包扩容 | T8.5.4（含投矛不耗） |
| P1.5 | 双武器槽 | §4 | 5 格热键，无武器槽 | **延期**：首版热键冒充；Iter 8/8.5 不排任务 |
| P1.6 | 敌人特化 | §4；夜间强化 | 4 行为占位；不读昼夜 | T8.5.5（特化 + 夜间乘数） |
| P1.7 | 料理 12+ | §6 | 5 配方；无 CraftUI；生肉无门 | T8.4 UI；T8.4b 烹饪门；扩容 T8.5.6 |
| P1.8 | 四群系/洞穴/远海 | §3 | 无 | 跟岛生成 |
| P1.9 | 工作台科技闸门 | §5 木→石/金属 | 仅 `RequiresCampfire/Workbench` 布尔 | 熔炉/T2 时扩展 |
| P1.10 | 环境叙事交互 | §7 日志/遗迹 | 仅剧情点 ID | T8.5.9 `StoryInteractable` |
| P1.11 | 按章强制教程 | §9 渐进教学 | 仅规划 15 分钟 6 项 | T8.5.10 种田+遗迹；T8.5.11 木筏；Boss 不进强制教程 |

### P-reserve 成长接口（首版只预留，R1）

定稿 §8 首版仍装备成长。§11 已加天赋树/技能钩子。R1 **不挡** Iter 7 / 教程。

| ID | 预留 | 接口 | 首版行为 | 明确不做 |
|---|---|---|---|---|
| PR.1 | 天赋树 | `TalentData` + `ITalentTree` | `NullTalentTree`：`IsUnlocked`=false，`Unlock` no-op 或仅测缝 | 加点 UI、经验、等级、节点图 |
| PR.2 | 主动技能 | `SkillData` + `ISkill` / `ISkillHost` + `SkillContext` | `TryActivate`=false | 技能栏、热键、特效 |
| PR.3 | 数值修饰 | `IModifierSource.GetMultiplier(statId)` | 恒 1；调用点在伤害/体力 | 改写 CombatLogic 公式本身 |
| PR.4 | 事件 | `TalentUnlocked` / `SkillActivated` | raise-only | 音效/UI 订阅 |
| PR.5 | 存档槽 | `UnlockedTalentIds: []` | Iter 8 DTO 可空 | 按树结算 |
| PR.6 | 建筑耐久 | `BuildableResource.MaxDurability`（R2） | 0=无敌；不扣血 | 摧毁、维修、基地袭击 |

`statId` 稳定集：`melee_damage`、`throw_damage`、`ranged_damage`、`stamina_cost`、`hunger_rate`、`thirst_rate`、`move_speed`、`max_health`。

R2 建筑耐久见 §6；`MaxDurability=0` 视为无敌。

### P2 表现资产（两刀）

现状：几乎全是几何体 + `.tres`。唯一成套视觉是 Iter 2 海洋 shader/clipmap。`ItemData.Icon` 有槽未填。无 wav/ogg/glb。`state.json` 无 §13.5 下载记录。

**8p-art 最小可视包（T8p.5）** — 与 8p-game 并行，**不硬挡**教程。渠道：模型 Kenney/Quaternius（CC0 优先）；图标 Kenney 或 game-icons.net（CC-BY 需署名）；音效 freesound/soundimage。每次下载记 URL+许可到 `state.json`。NC/ND 仅原型。

| ID | 资产 | 替换对象 | 用途 |
|---|---|---|---|
| P2.m1 | 女性人形 | `src/Game.tscn` 玩家胶囊 | 教程移动可辨认 |
| P2.m2 | 海蟹、野猪 | `scenes/combat/enemy.tscn` 单胶囊 | 教程基础战斗 |
| P2.m3 | 可辨认篝火（含光） | `scenes/building/buildables/campfire.tscn` 棕色圆柱 | 造篝火 + 夜间 |
| P2.m4 | 床模型 | 随 T8p.4 | 造床 |
| P2.i1 | 物品图标 | `wood` `stone` `coconut` `berries` `stone_axe` `wooden_spear` 的 `ItemData.Icon` | 热键可读 |
| P2.i2 | 建筑菜单图 | 篝火、床 | 建造 UI |
| P2.s1 | 短音效 | 采集、喝水/吃、放置、近战命中、死亡/复活 | 挂现有 `ItemAdded`/`EnemyDied`/`PlayerDied` 等 |

**Iter 9 完整替换**

| ID | 资产 | 渠道 |
|---|---|---|
| P2.f1 | 9 敌人模型+动画（含鲨鱼王体型） | Quaternius / opengameart |
| P2.f2 | 15 建筑 + 农田/驯养栏/鸡/羊 | Kenney / Quaternius |
| P2.f3 | 主角 + 布/皮/铁三套换装 | 定稿 §8 装备改外观 |
| P2.f4 | 四群系环境套件 | 浅海海滩、森林、洞穴、远海风暴 |
| P2.f5 | 其余 ~29 物品图标与熔炉等 UI | Kenney / game-icons.net |
| P2.f6 | 配乐与环境声（海、夜、风暴） | soundimage / freesound / 爱给 |

商用发布前替换一切 NC/ND。
