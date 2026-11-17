# Wayfinder 地图：SeaAnomaly 运行时性能与帧预算加固

- 创建：2026-10-10
- Tracker：本地 markdown（`gh` 不可用、仓库托管在 GitHub 而非 Cursor Origin，按 wayfinder 约定回退）
- 票文件：`tickets/WF-*.md`（frontmatter 记录 `status / type / hitl / blocked_by / assignee`；开放票不在此正文罗列，用票文件查询）

## Destination

在**不改玩法体验目标**的前提下，产出一份可直接交付执行迭代的「运行时性能加固路线」，使 SeaAnomaly 在 **1080p 中端 PC（Intel Iris Xe 核显 / GTX 1060 量级）稳定 60 FPS、帧预算 16.6 ms**。本地图只做**决策**（测量方法 → 热点归因 → 各热点优化策略与取舍 → 预算分配与回归门槛），不落地优化代码；廉价探针/原型除外。GPU 真机数据由**用户后续手动采集**（本机只有 headless 可被 agent 自动跑，GPU 海洋路径在 headless 下被契约 4 关闭），相关海洋决策标记为被手动采集票阻塞；headless 可测的 CPU/逻辑侧先行。

## Notes

- 领域：Godot 4.7.1-stable mono / net8.0 / Forward+；工程根 `game/`；构建测试 `dotnet build` + `godot --headless --path . --run-tests --quit-on-finish` + `--quit-after 5` 冒烟（禁用 `--` 分隔符）。
- 每次会话必读既有证据：`.godot-dev/perf-review.md`（2026-08-17 只读性能审查，基线提交 `d779b10`，此后世界生成/武器/Boss 有大改，结论需以新证据复核）、`.godot-dev/state.json` 契约全文。
- 硬契约（解锁需用户显式确认）：不改 `PlayerMotion`；不改 AI 状态机（但数量/休眠调度/距离剔除在允许边缘，需逐票确认不碰 switch 本身）；不改 WaterMesh headless 降级语义（`_gpuEnabled=false` + `GetWaveHeight→0`），但 cascade 调度/异步回读/分辨率档位不被该契约锁；群岛同 seed 同布局；不新增 NuGet（性能探针优先用引擎内置 `Performance` 监视器 / BCL `Stopwatch`，不引第三方 profiling 包）。
- 编排：遵循项目 skill `godot-csharp-3d-game-dev`（小/中任务分流；探针走临时代码或测试，不进产品提交除非该票决策要求）。
- 已知静态热点（来自 perf-review.md，待新证据复核）：P0-1 cascade 追赶单帧补完 hitch；P1-1 同步 `TextureGetData` 回读（2 Hz）；P1-2 三 cascade 512 FFT ≈48 MB；P1-3 15–17 敌人全员每物理帧 tick、无距离 LOD；P1-4 每帧 `new Array<Rid>`；P1-5 HUD 每帧拼时间字符串；P1-6 天气每帧写 Environment；另有群岛植被逐株 `MeshInstance3D.AddChild`（draw call/node 数）疑似热点，perf-review 未单列。
- 目标硬件事实：本机 GPU = Intel Iris Xe（驱动 31.0.101.3358）+ OrayIddDriver 虚拟显示器（远程显示，测量时需标注其开销）。

## Decisions so far

<!-- 每张关闭的票一行：gist + 票文件链接 -->

（暂无）

## Not yet specified

- **质量分级矩阵**：FSR 已 0.75、阴影 2048；低/中/高档位与自动检测、哪些旋钮入档（FFT MapSize、阴影、视距、植被密度）、设置如何持久化——依赖预算切分与 GPU 基线数字，尚不能精确成票。
- **加载/群岛生成 hitch**：4–5 岛在加载期建 ArrayMesh + HeightMapShape3D；是异步/分帧生成还是加加载画面，严重度未测，问题还不够尖。
- **植被渲染批处理**：IslandBuilder 目前逐株独立 Node3D/MeshInstance3D；是否合 MultiMesh / 加距离剔除，需 draw call 与节点数基线后才能定取舍。
- **显存预算**：FFT ≈48 MB + GLB/纹理总量上限与中端 iGPU（共享系统内存）预算，待 GPU 采集后成票。

## Out of scope

- 导出/发布流水线（用户在本轮范围选择中明确排除）。
- 存档健壮性与存档迁移（同上排除）。
- 代码整洁、分析器警告（src 10 条）、测试日志 harness 噪音清理（同上排除；除非某探针票必须触碰）。
- 玩法深度/内容广度扩充、商业化与 SurvivalIsland 授权处理（属不同目的地）。
- 修改 `PlayerMotion`、AI 行为状态机本身、WaterMesh headless 降级语义（契约永久锁，不随本地图毕业）。
