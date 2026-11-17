---
id: WF-002
title: 满负载下 CPU 热点归因与敌人/物理/射线占比
type: research
hitl: false
status: open
assignee:
blocked_by: ["WF-001"]
---

## Question

用 WF-001 的探针，在固定满负载场景下，CPU 帧时间究竟花在哪、各占多少？需用新证据复核 perf-review.md 的静态嫌疑（基线提交 `d779b10` 之后世界生成/Boss 已大改）：

- `EnemyBase._PhysicsProcess` 全员 tick（约 15–17 敌人、无距离休眠）在物理帧占比？
- 每帧/每物理帧 `new Array<Rid>`（`PlayerInteraction`、`PlayerController.ResolveRaftCarry`、`Projectile`）造成的 GC 压力（分配率/Gen0 GC 频次）？
- `HUD._Process` 每帧拼时间字符串、`WeatherService.ApplyFog` 每帧写 Environment 的实际成本？
- 物理 `MoveAndSlide` / 射线查询在总帧时间的占比？

产出：按占比排序的 CPU 热点表 + 每项"优化后预计回收多少 ms"的粗估，作为后续优化票的取舍依据。
