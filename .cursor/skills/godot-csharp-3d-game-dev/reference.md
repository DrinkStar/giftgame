# Phase 0–6、回滚、工具与 state.json

大任务或需要逐步工作流时 **Read 本文件**。日常硬规则在 [SKILL.md](SKILL.md)。Godot 4.7：**禁止 `--` 分隔符**；**没有 `--scene`**。

在含 `project.godot` 的目录执行命令（常不是仓库根）。

## 工作流

### Phase 0: 项目初始化与状态加载

**仅大任务或确认这是尚未初始化的新工程时执行。** 小/中任务：若 `.godot-dev/state.json` 已在，读它继续当前迭代即可。

1. 检查 `.godot-dev/state.json`：存在则询问继续还是新建（仅大任务 / 用户说开新任务时）。
2. **定位 `project.godot`**（仓库根或其子目录）。不存在则询问是否初始化（`godot --headless --path . --editor --quit` 或使用模板）；存在则读取项目配置（名称、主场景、C# 配置）。后续命令都在该目录执行。
3. 仅大任务且已有 git 时，可创建分支（如 `dev/<feature>-<timestamp>`）。无 `.git` 则跳过，不 `git init`。
4. 运行 `godot --version` 记录实际版本；引擎绝对路径可记入 `state.json`。
5. **CodeGraph 探测**：若项目根存在 `.codegraph/` 目录则可用 codegraph MCP 工具；否则跳过并在报告中注明「未索引，使用 Grep/Read 降级」。索引的创建/更新由用户决定，Agent 不擅自执行。
6. 输出项目状态报告：场景数量、脚本数量、Godot 版本、.NET 版本、测试框架（是否为 GoDotTest）。

### Phase 1: 需求分析与战略规划

仅 **大任务**。并行委派 Explore 与 Librarian：

- **Explore**：Grep/Read 扫描项目；有索引时用 codegraph 查符号与调用关系，识别核心类和耦合点（如静态事件总线）。输出场景清单、脚本清单、依赖关系图。
- **Librarian**：按需求查 Godot 4.7 C# 最佳实践、3D 性能、物理注意事项，给技术选型建议。

Prometheus 拆分模块、排迭代序、标高风险，产出 `.godot-dev/strategy.md`。

**审批**：用户批准路线图后进入 Phase 2；批准的约束写入 `contracts[]`，并镜像到项目 `.cursor/rules/`（注明源与解锁条件）。

### Phase 2: 详细技术设计与审批

仅 **大任务**。Oracle 为当前迭代设计：场景树结构、C# 类设计（继承、字段、方法、信号/事件）、资源管理方案、模块接口。

- 公共 API 变更须附调用方清单（有索引用 `codegraph_callers`，无索引用 Grep 全仓搜索）。
- 产出任务清单：文件路径、操作类型（创建/修改/删除）、摘要、依赖、验证标准（编译通过、场景加载成功、GoDotTest 通过）、风险与回滚点。
- 任务清单必须**显式列出**本次迭代涉及的全部公共 API / 事件总线变更与场景结构变更——这些在批准后视为已预审，执行期不再弹窗。
- **审批**：必须获得用户明确批准才进入 Phase 3。

**审批放权**：计划内预审的 API/场景变更执行期不弹窗；计划外变更按推荐方案执行并在报告中说明；回滚不弹窗，见下文。

### Phase 3: 迭代开发执行

- 按任务清单顺序实现。提交规则见 SKILL「增量提交」，不要无 git 仍 `git commit`。
- **C# 脚本**：用 Write/StrReplace，遵循仓库现有命名与分层约定（Node 层只做 I/O，纯 C# 逻辑可单测，数据走 `.tres`）。清单内公共 API 变更同步全部调用方。
- **场景文件（`.tscn`）**：先 Read 再改。编辑器已开且 Agent Tools 可用时，优先 MCP `scene_*`。
- **`project.godot`**：按需更新输入映射、自动加载、渲染设置。
- **导入资源**：开源包复制进项目；无合适开源资产时按 SKILL「资产获取顺序」调用 `gltf-2-generate`。挂上网格后走 SKILL「模型接入」。
- 派发子代理：`contracts[]` + 禁止回滚的近期落地 + 任务相关数值。

### Phase 4: 验证与自动修复循环

失败均 ≤3 次后进入安全回滚。

- **4.1 编译**：`dotnet build` 或 `godot --headless --path . --build-solutions --quit`。
- **4.2 场景冒烟**：`godot --headless --path . --quit-after 5`。不要编造 `--scene`。
- **4.3 GoDotTest**：`godot --headless --path . --run-tests --quit-on-finish`（禁止 `--` 分隔符）。
- **4.4 性能抽查（可选）**：明显问题排入后续迭代。

### Phase 5: 构建与导出

用户说「编译发包 / 重新发包」时 **直接从这里开始**（不要 Phase 0–2）。

1. 读取 `export_presets.cfg`：预设 **name**、`export_path`、`export_filter`。用真实预设名，不要假设 `game.exe`。
2. 在 `project.godot` 所在目录：`dotnet build`，然后  
   `godot --headless --path . --export-release "<预设名>" <export_path>`
3. 检查导出日志。失败修复 ≤3 次（缺模板时写明对应 Godot 版本的 export templates 路径）。
4. 报告：exe / pck / `data_*` 路径与体积；pck 与 dll 谁变了；三件套一起分发。PC 导出保持 `file_logging`（若项目契约要求）。
5. 不要 `git add build/`。`export_filter=all_resources` 会打进 addons / 实验室，产品包应排除或注明。

### Phase 6: 最终交付与报告

仅 **大任务** 收尾或用户要总结时。

1. 汇总 Git 提交（若有）、验证结果、导出产物。
2. 生成 `.godot-dev/final-report.md`（含 `pending_todos` 里未完成的 grilling）。
3. 询问是否打标签（无 git 则跳过）。
4. 更新 `state.json` 标记完成。

## 安全回滚机制（禁止 git reset --hard）

Phase 4/5 修复超 3 次仍失败时：

- **无 git**：停止改动，报告失败现场与已改文件，不编造分支。
- **有 git**：自主执行（不弹窗）：
  1. `git add -A && git commit -m "WIP: failed state before rollback"`（勿加入 `build/`）。
  2. `git branch failed-dev-<timestamp>`。
  3. 推荐 `git revert --no-commit <bad>..HEAD` 后提交；或从 `state.json.last_stable_commit` 新建分支。
  4. 报告失败分支名与方案。

## 工具使用说明

- **Read / Grep / Glob**：场景、脚本、配置、日志。
- **Write / StrReplace**：脚本、场景、配置、报告。
- **Shell**：Godot、`dotnet`、git、测试。禁止 `git reset --hard`、`push --force`（除非用户显式要求）。禁止无请求时 `git init`。
- **Task**：`explore` 只读可并行；`generalPurpose` 写同一模块必须串行（`resume` / `interrupt`）。
- **TodoWrite / AskQuestion**：AskQuestion 仅大任务 Phase 1–2。
- **CodeGraph**（仅当存在 `.codegraph/`）：一律传 `projectPath`；不擅自 `codegraph init`。
- **gltf-2-generate**：仅开源网格不合适时。子代理 prompt 带上该 SKILL.md 路径，要求 `validate` + `sources.md`。
- **Canvas / Archify（可选）**：输出到 `.godot-dev/diagrams/`。

常用验证：

```
dotnet build
godot --headless --path . --import
godot --headless --path . --quit-after 5
godot --headless --path . --run-tests --quit-on-finish
```

## 状态文件示例（`.godot-dev/state.json`）

值按项目实际填写。`contracts` 只能来自用户批准。`pending_todos` 可放未完成 grilling 原文。

```json
{
  "task_id": "godot-dev-<yyyymmdd>-<seq>",
  "created_at": "<ISO8601>",
  "updated_at": "<ISO8601>",
  "stage": "phase0|phase1|phase2|phase3|phase4|phase5|phase6",
  "current_iteration": "<iteration_name>",
  "git_branch": "dev/<feature>-<timestamp>",
  "last_stable_commit": "<short_sha>",
  "godot_exe": "<optional absolute path to godot.exe>",
  "contracts": [],
  "project": {
    "godot_version": "<godot --version 实测值>",
    "csharp_version": "net8.0",
    "test_framework": "GoDotTest",
    "main_scene": "res://<path/to/main.tscn>",
    "project_godot_dir": "<dir that contains project.godot>",
    "scene_count": 0,
    "script_count": 0
  },
  "iterations": [],
  "pending_todos": [],
  "checkpoint": "<stage>_<iteration>_<marker>"
}
```
