# SeaAnomaly（海域异变）— 代码初步集成方案

> 生成日期：2026-08-13 ｜ 依据：`游戏设计-定稿.md` + `godot-refs/集成清单.md` + 骨架实测
> 状态：Phase 1/2 产出，**待用户审批后进入 Phase 3 执行**

## 0. 当前状态（骨架已验证）

| 验证项 | 结果 |
|---|---|
| 工程骨架 | ✓ chickensoft 模板，重命名 SeaAnomaly（Godot 4.7.1 .NET + net8.0 + Forward Plus） |
| 编译 | ✓ `dotnet build` 0 错误 |
| 冒烟测试 | ✓ `godot --headless --path . --quit-after 5` exit 0 |
| 单元测试 | ✓ GoDotTest 1/1 通过（headless 0.7s） |
| Git | ✓ main（骨架）+ dev/skeleton 分支 |

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

*\*视角为待决项：定稿未明确，推荐第三人称（弓/矛战斗 + 女性主角外观展示），见 §7。*

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
│   └── ui/                          # HUD、InventoryUI、CraftUI、QuestUI、TutorialUI
├── test/src/                        # 每模块对应测试（GoDotTest）
└── scenes/                          # 场景文件（或与脚本同目录，随 chickensoft 惯例）
```

## 4. 信号总线约定（解耦核心）

沿用 SurvivalIsland 的静态 C# 事件模式，`GameManager` 为事件宿主：
- 生存事件：`OnHealthChanged / OnHungerChanged / OnThirstChanged / OnStaminaChanged / OnDeath`
- 世界事件：`OnDayNightChanged / OnWeatherChanged / OnTimeTick`
- 库存事件：`OnInventoryChanged / OnItemCrafted`
- 剧情事件：`OnQuestStarted / OnQuestProgress / OnQuestCompleted / OnStoryPointReached`
各系统只依赖事件，不互相持有引用——这是三章剧情投放（A1 机制）与教程渐进教学的技术基础。

## 5. 迭代路线图（每迭代 = 计划→实现→验证→提交）

| 迭代 | 内容 | 依赖 | 产出 | 风险 |
|---|---|---|---|---|
| Iter 0 | 骨架 ✅ | — | 已验证 | 已消除 |
| **Iter 1** | 3D 玩家控制器 + 第三人称相机 + 基础测试场景 | GameDemo | 可跑动的 3D 角色 | 低 |
| **Iter 2** | 海洋（2Retr0 shader 移植）+ 简单岛屿 + 浮力 | Iter 1 | 可航行的海面 | 中 |
| **Iter 3** | 生存核心：三指标/库存/合成/昼夜/天气 | Iter 1 | 完整生存循环 | 中 |
| **Iter 4** | 网格建造（MarkoDM 移植）+ 建造存档 | Iter 3 | 可放置建筑 | 中 |
| **Iter 5** | 种植 6 作物 + 产出型驯养 | Iter 3/4 | 食物闭环 | 低 |
| **Iter 6** | 战斗：矛/弓 + 8 敌人 + 鲨鱼王 Boss | Iter 1/3 | 可战斗 | 高 |
| **Iter 7** | 任务系统 + 引导者 + 三章剧情骨架 | Iter 3-6 | 主线可推进 | 中 |
| **Iter 8** | 教程（15 分钟强制段）+ GDSave 存档整合 | 全部 | 可玩闭环 | 中 |
| **Iter 9** | 性能优化 + 导出 + 打磨 | 全部 | 可发布 | 低 |

**依赖关系**：Iter 2、3、6 可并行（不同分支）；Iter 4 依赖 3（合成产出建材）；Iter 7 依赖 3-6（任务目标引用的系统）。

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

> Iter 4-8 的逐任务清单在各迭代开始前按本格式产出（Phase 2 细化）。

## 7. 待决问题（需要用户确认）

1. **视角**：第三人称（推荐，弓/矛战斗 + 女性主角外观展示 + GameDemo 可参考）vs 第一人称（SurvivalIsland 可直接参考）？
2. **SurvivalIsland 授权**：**已决（2026-08-13）**——直接代码级移植，用户决策 2026-08-13（自用非商业化，不申请授权；原无许可仓库，风险已告知并接受；若未来商业化须先取得作者授权）。
3. **场景组织惯例**：chickensoft 推荐"场景与脚本同目录同名"（利于 VSCode 调试配置）。方案默认采用。
4. **渲染目标**：Forward Plus 已定，但低端机回退（gl_compatibility）是否要做首版？（海洋 shader 依赖 Forward+，回退即无海——默认首版不做回退。）

## 8. 审批门

- 批准本方案 → 进入 Phase 3，按 Iter 1 任务清单执行（每个迭代完成后提交 Git + 更新 state.json + 汇报）。
- 需要调整 → 指出修改点，我更新方案后重新提交。
- 视角/授权等 §7 问题请一并表态。
