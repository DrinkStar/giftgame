---
id: WF-001
title: CPU/逻辑帧预算用什么方法在 headless 下测成可比较基线
type: research
hitl: false
status: open
assignee:
blocked_by: []
---

## Question

在 agent 只能跑 `godot --headless`（GPU 海洋被契约 4 关闭）的约束下，如何对 **CPU/逻辑侧**（物理、敌人 tick、射线、UI、C# GC）建立一套**可重复、可比较**的帧时间/分配基线？需要定：

- 用引擎内置 `Performance` 监视器（`TIME_PROCESS`/`TIME_PHYSICS_PROCESS`/`OBJECT_ORPHAN_NODE_COUNT` 等）还是 BCL `Stopwatch` 包一个 headless 自检场景/测试？
- 采样负载场景如何固定（出生岛 / 满编 15–17 敌人 / 多弹体 / 建造+农田+驯养全开），使各次跑分可比？
- 测哪些指标、跑多少帧、如何剔除启动与 shader 编译预热、如何输出成证据文件（落 `.godot-dev/`，遵守测试不写 `user://logs` 契约）？
- 如何让这套测量以后能在 CI/headless 回归中复用（不新增 NuGet）？

产出：测量方法 + 一个能跑出数字的最小探针（prototype 允许），给出 CPU 侧基线数字。不接触 GPU/FFT（归 WF-003/004）。
