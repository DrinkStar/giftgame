---
name: godot-csharp-3d-game-dev
description: Orchestrates long-running multi-agent workflows for 3D game development with C# and Godot 4.7 .NET, covering project init, scene building, scripting, asset integration, GoDotTest testing, and build export, with incremental commits, contract locks, auto-fix (max 3 retries), and safe rollback. 使用 C# 和 Godot 4.7 进行 3D 游戏开发的长时运行多 Agent 编排工作流。Use when developing 3D games with Godot and C#, when the user mentions "开发 Godot 3D 游戏", "使用 C# 开发 Godot 游戏", "Godot 3D 游戏开发", "C# Godot 游戏项目", or when editing project.godot / .tscn files.
disable-model-invocation: true
---

# C# + Godot 4.7 3D 游戏开发 Skill（编排硬规则）

Phase 0–6、回滚、工具与 `state.json` 示例见同目录 [reference.md](reference.md)。**大任务**或逐步工作流时再 Read；小/中/发包只遵守本文。

Godot 4.7：**禁止 `--` 分隔符**；**没有 `--scene`**。先找 `project.godot`，在该目录跑 `godot` / `dotnet`。

`disable-model-invocation: true` **保持**。编排硬规则须镜像到项目 `.cursor/rules/`（`alwaysApply: true`），勿把 Phase 全文抄进 rules。本仓库示例：`.cursor/rules/godot-dev.mdc`。

缺网格：**开源 CC0/CC BY 优先**；没有合适的再 Read 仓库 `.cursor/skills/gltf-2-generate/SKILL.md`。禁止付费图生 3D、禁止 PNG 当模型。

## 角色

| 角色 | Cursor 实现 |
|---|---|
| Sisyphus | 主 Agent + `TodoWrite`；一次一个 `in_progress` |
| Prometheus / Oracle | Plan 或 `generalPurpose`；**仅大任务** Phase 1–2 + `AskQuestion` |
| Explore | `explore`（只读可并行） |
| Hephaestus | `generalPurpose`；写同一模块必须串行 |
| Hephaestus·网格 | 无合适开源网格时遵循 `gltf-2-generate` |
| Librarian | `WebSearch`；Kenney / Poly Haven / Quaternius |
| Mnemosyne | `dotnet build` + GoDotTest |

**子代理 prompt（硬）**：`contracts[]` 全文 + 禁止回滚的近期落地 + 关键数值。子代理看不到会话、`state.json`、`.cursor/rules/`。

## 任务分流

不要默认 Phase 0 + `strategy.md`。

| 档 | 典型说法 | 做 | 不做 |
|---|---|---|---|
| **小** | 修 bug、改配额、补测、画图 | 改代码 + 编译 + 相关测试 | Phase 0、审批、新分支 |
| **中** | 加玩法、加密植被、Hurtbox、补网格 | 实现 + 测试；可写 `state.json` 备注 | `strategy.md`、Phase 1–2 弹窗 |
| **大** | 新子系统、事件总线、多迭代 | **Read [reference.md](reference.md)** Phase 0→4 | — |
| **发包** | 「编译发包」「重新发包」 | **只走 Phase 5**（见 reference） | 当新功能迭代 |

## 契约、git、grilling

- 批准的决策写入 `.godot-dev/state.json` 的 `contracts[]`，并镜像到 `.cursor/rules/`（注明源、解锁需用户确认）。
- **无 `.git`**：不 commit、不 `git init`。有 git：仅用户要求或本任务明确要提交时 commit。禁止提交 `build/`。
- 编译/测试失败自动修 ≤3 次；超限回滚步骤见 reference（无 git 只报告现场）。
- grilling 被换题打断：结论写入 `pending_todos`，换题时 **复述一条**。未确认不当契约执行。
- 仅大任务两处审批：Phase 1 路线图、Phase 2 任务清单。

## 并行（文件锁）

只读可并行。世界生成族（`WorldLayout` / `IslandBuilder` / `IslandHeightmap` / 植被）**同一时刻一个写入者**。已有子代理在改：必须 `resume`/`interrupt` **同一 id**，禁止再开 `generalPurpose`。prompt 写清不要回滚已落地岸线/植被/hurtbox。

## 资产获取顺序

1. 仓库已有 `res://assets/models/`（或同等目录）
2. Kenney / Poly Haven / Quaternius 等 CC0/CC BY，记 `sources.md`
3. 「没有合适的」：公开站无同功能网格，或只有占位且用户要可见模型，或绑定对不上 Godot Humanoid
4. Read `gltf-2-generate` → `validate` → 分类目录 → import → `sources.md`（程序网格 `original`，不是 CC0）→ 拟合 Hurtbox → 接到**产品**主场景

静物 `box`/`compose`；人形 `humanoid`（必须 `skins`）。有机生物优先现成 CC0。不要覆盖用户指定保留的角色 GLB。

## 检查清单

**Hurtbox**：按物种休息姿态可见体拟合，禁止全员同一胶囊；走路胶囊与受击盒分离；视觉 ExtraYaw 不转盒；不用动画 AABB 极值；脚底原点则中心在 `height/2`。测试：不同 id 不同尺寸。

**资源**：先分 404 / 未导入 / 刻意占位 / 产品未用。占位不是导入失败；要模型且开源没有则走生成。主线闭环必须在产品场景，不要只放实验室。

**生成**：轮廓不要默认正圆（同 seed 同布局）；草密灌中乔木留空；可砍配额与装饰实例分开；测试断言不变量，不要锁死 `草==16`。

**发包**：读 `export_presets.cfg` 的预设名与路径；分发 exe + pck + `data_*`；报告 pck vs dll 谁变了。细节 [reference.md](reference.md) Phase 5。
