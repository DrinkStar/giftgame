# SeaAnomaly — 深度分析 + 崩溃日志迭代（2026-08-17）

> 接续已完成的 `dev/code-review`（HEAD `dbfb2d0`，313/0）。本文件是 Phase 1/2 产物：现状分析、技术选型、任务清单。
> 批准后进入 Phase 3 实现。不重排既有玩法迭代，不触碰 9 条锁契约。

## 0. Phase 0 实测

| 项 | 值 |
|---|---|
| 任务延续 | 继续现有工程（不新建空状态）。上一检查点 `phase3_code_review_verified` |
| 引擎 | Godot **4.7.1.stable.mono.official**（`godot --version`） |
| .NET | 目标 **net8.0** / SDK **10.0.400** / Godot.NET.Sdk **4.7.1** |
| 主场景 | `res://src/Main.tscn` → 运行时切到 `res://src/Game.tscn` |
| 渲染 | Forward Plus；FSR `scaling_3d/scale=0.75` |
| Git | `game/.git`，当前分支 `dev/code-review`，HEAD `dbfb2d0` |
| 建议新分支 | `dev/crash-logging-20260817` |
| CodeGraph | 仓库根有 `.codegraph/`，已用 `codegraph_explore` |
| Godot 编辑器 MCP | 未开（`127.0.0.1:9920` 拒绝连接）→ 场景/工程改文本；编译走 headless |
| 测试 | GoDotTest；调用 **禁止 `--` 分隔符** |
| 规模 | 生产 C# **108**、测试 C# **50**、`.tscn` **35**（`state.json` 里 scene/script 计数已过时） |
| 工作区脏文件 | `CraftUI.cs` / `StorageUI.cs` 有未提交的锁释放实现微调（与本迭代无关，**不纳入**）；`.godot-dev/rel-test-full*.log` 不提交 |

## 1. 架构现状（与崩溃诊断相关）

启动路径：`Main._Ready` 判定 `--run-tests` → 否则 `ChangeSceneToFile(Game.tscn)`。`Game.cs` 几乎为空；`GameManager` 是场景接线枢纽，**不是**事件宿主。

解耦中枢是静态 `GameEvents`（约 86 处调用方）。各系统通过事件通信，死亡契约已锁：`PlayerDied` ≠ `GameOver`（死亡=重生，不是崩溃、不是结局）。

**没有 Autoload。** `AudioManager` 注释写明「autoload 候选，有意未注册」。所有服务都挂在 `Game.tscn` 树上——因此 **Main 切场景之前、以及 `Game.tscn` 加载失败时，没有任何游戏侧钩子能接到异常。**

持久化已有成熟模式：`JsonSaveSystem` 用 `ProjectSettings.GlobalizePath` + `System.IO` 写 `user://`，测试注入隔离目录。崩溃日志应复用同一路径约定。

### 现有「日志」缺口

| 现状 | 后果 |
|---|---|
| 散落 `GD.Print` / `GD.PushWarning` / `GD.PrintErr` | 只进编辑器输出；玩家/导出后看不到 |
| `project.godot` **无** `[debug]` / `file_logging` | 导出 PC 构建默认往往不落盘 `godot.log` |
| 无 `Logger` / `OS.AddLogger` | 引擎 error/warning 流无法被游戏侧拦截 |
| 无 `AppDomain.UnhandledException` 等钩子 | C# 未处理异常不落独立 crash 文件 |
| 无环形缓冲 | 崩溃当下没有「之前 200 条警告」上下文 |
| `PlayerDied` 打 `GD.Print` | 容易被误当成崩溃；本迭代必须明确区分 |

剩余 P2 里仍有潜在 NRE / 非法 GPU dispatch（如 `BuildableInstance.Object3DModel!`、`WaveGenerator` 非 2 幂），本迭代**只记录不修**，用 crash dump 给后续修复提供证据。

## 2. 技术选型（Librarian）

Godot 4.7 官方能力：

1. **内置文件日志**：`debug/file_logging/enable_file_logging`（编辑器）+ `.pc`（导出 PC）。默认 `user://logs/godot.log`，轮转最多 5 份。
2. **自定义 `Logger`**：继承 `Godot.Logger`，`OS.AddLogger`；覆盖 `_LogMessage` / `_LogError`。文档警告：**会从非主线程回调，必须 Mutex**；回调内禁止再 `print`/`push_error`（无限递归）。
3. **C# 异常**：Godot 4 已移除 `GD.UnhandledException`。`AppDomain.UnhandledException` 在 Godot.NET 上不可靠。实践上钩：
   - `UnhandledException`（能接到则写 dump）
   - `TaskScheduler.UnobservedTaskException`
   - **不要**用 `FirstChanceException` 写 crash 文件（每个被 catch 的异常都会喷，噪声极大）
4. **进程级崩溃**：Autoload `_Notification(MainLoop.NotificationCrash)` 尽量冲刷缓冲区。原生引擎段错误无法被 C# 保证捕获——因此必须同时打开内置 `godot.log`。
5. **不引入 NuGet**（Sentry / Chickensoft.Log.Godot）：现有 `JsonSaveSystem` 已能写 `user://`；本迭代目标是本地可复制的崩溃证据，不是远程上报。

推荐方案（一层 Autoload + 内置文件日志，不改 `GameEvents`）：

```
Main / 引擎启动
  └─ Autoload CrashLogService  （项目里第一个、也是目前唯一的 autoload）
       ├─ ctor: OS.AddLogger(EngineLogSink)
       ├─ 钩 UnhandledException / UnobservedTaskException
       ├─ 会话文件 user://logs/seaanomaly.log
       └─ 崩溃文件 user://logs/crash-yyyyMMdd-HHmmss.log
```

## 3. 设计要点

### 3.1 文件

| 文件 | 内容 | 轮转 |
|---|---|---|
| `user://logs/godot.log` | 引擎内置全量输出 | 引擎自己管（max 8） |
| `user://logs/seaanomaly.log` | 结构化会话：Info 面包屑 + Warning + Error | 启动轮转；保留 5 份；单文件上限 2MB |
| `user://logs/crash-*.log` | 一次崩溃一份：环境头 + 异常/引擎错误 + 环形缓冲 + 轻量游戏快照 | 保留最近 10 份 |

Windows 实际路径大致：`%APPDATA%\Godot\app_userdata\SeaAnomaly\logs\`。README 写明。

### 3.2 Crash dump 头（每次崩溃必写）

- UTC 时间、会话 id
- `Engine.GetVersionInfo()`、OS 名、命令行
- 是否 headless / 是否 `--run-tests` / 是否导出运行
- 当前场景路径、`Time.GetTicksMsec()`
- 轻量快照（可空）：昼夜小时、天气、暂停、玩家世界坐标——**禁止**写背包明细、存档 JSON、物品数量表

### 3.3 何谓崩溃（写 dump）vs 何谓玩法

写 dump：未处理 C# 异常、`Logger.ErrorType` 为 Error/Script、`NotificationCrash`。

**不写 dump**：`PlayerDied`、任务失败、警告、`GD.Print`、海洋 headless 降级（契约 4 的预期行为）。

### 3.4 测试

- Autoload 在 GoDotTest 时**仍会加载**（`Main._Ready` 设 `IsTesting` 发生在 autoload 之后）。
- 用命令行检测 `--run-tests`：测试期间默认**只留内存环形缓冲**，不写 `user://logs/seaanomaly.log`，避免 300+ 测试刷盘。
- `CrashLogWriter` 的目录可注入；单测写 `user://logs_test`，Cleanup 删除。
- 测试崩溃仍允许写 `crash-*.log` 到测试目录（便于修测试本身）。

### 3.5 线程与关闭

- `EngineLogSink` 所有入口进 Mutex。
- 写文件用 `FileShare.ReadWrite`，崩溃时 `Flush(true)`。
- `_ExitTree`：`OS.RemoveLogger`、卸异常钩子、flush。
- Logger 回调内不用 `GD.Print*`。

### 3.6 公共 API（Phase 2 预审，执行期不再弹窗）

| API | 说明 |
|---|---|
| `CrashLog.Info/Warn/Error(string)` | 静态门面，转给 Autoload 实例；无实例时 no-op |
| `CrashLog.ReportException(Exception, string? context)` | 写 dump + 会话 Error 行 |
| `CrashLogWriter` | 纯文件/轮转/环形缓冲，目录可注入 |
| Autoload `CrashLogService` | `project.godot` `[autoload]` 第一项 |
| **不改** `GameEvents` | 日志不是玩法事件 |

场景结构：不改 `Game.tscn` 树。只改 `project.godot`（autoload + debug/file_logging）。

## 4. 任务清单（批准后按序执行）

| ID | 文件 | 操作 | 依赖 | 验证 |
|---|---|---|---|---|
| T-L.1 | `src/core/logging/CrashLogWriter.cs` | 创建：环形缓冲、轮转、crash dump、可注入目录 | — | 单测写隔离目录 |
| T-L.2 | `src/core/logging/CrashLog.cs` + `EngineLogSink.cs` + `CrashLogService.cs` | 创建：静态门面、Logger 子类、Autoload | T-L.1 | 编译；钩子可卸 |
| T-L.3 | `project.godot` | 修改：autoload；`enable_file_logging` + `.pc`；`log_path`；`max_log_files=8` | T-L.2 | 冒烟后 `user://logs` 出现文件（非测试路径） |
| T-L.4 | `src/Main.cs` `src/core/GameManager.cs` | 修改：启动/切场景/死亡面包屑 Info（死亡≠crash） | T-L.2 | 单测：PlayerDied 不产生 crash 文件 |
| T-L.5 | `test/src/CrashLogTest.cs` | 创建 | T-L.1–4 | GoDotTest 通过 |
| T-L.6 | `README.md` | 修改：日志路径与如何提交崩溃文件 | T-L.3 | 文档与实现一致 |
| T-L.OUT | — | 禁止：Sentry/新 NuGet、改 GameEvents、改死亡契约、改 Game.tscn 结构、修 P2 功能债、提交脏的 CraftUI/StorageUI | — | 审查 |

每完成一项立即 `git commit`（仓库风格 `feat(logging): ...`）。

## 5. 建议锁定的契约（批准后写入 `state.json` + 镜像 `.cursor/rules/`）

**既有 9 条保持。** 本迭代新增：

10. `PlayerDied` 不是崩溃，禁止为其写 `crash-*.log`。
11. 崩溃日志禁止写入存档正文 / 背包明细 / 物品数量表。
12. `--run-tests` 期间不向 `user://logs/` 写会话文件（测试用注入目录）。
13. 本迭代不新增 NuGet、不把日志事件打进 `GameEvents`。
14. 原生引擎段错误不保证被 C# 捕获；内置 `godot.log` 必须在编辑器与 PC 导出中开启。
