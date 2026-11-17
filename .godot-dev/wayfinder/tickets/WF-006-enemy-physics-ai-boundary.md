---
id: WF-006
title: 敌人/物理 CPU 优化边界——休眠调度与 AI 状态机契约
type: grilling
hitl: true
status: open
assignee:
blocked_by: ["WF-002"]
---

## Question

敌人随群岛线性增长（约 15–17 只，全员每物理帧 tick，无距离 LOD）。在**契约 3 禁止改 AI 状态机 / `PlayerMotion`** 的红线下，允许做到哪一步？需要和用户确认边界：

- 远距离敌人**跳过物理 tick**（在进入 `EnemyBase._PhysicsProcess` 的行为 switch 之前按距玩家距离/所在岛休眠）是否算"改 AI 状态机"？现有 `SleepDistance` 已对非 habitat 敌人做空转早退，能否推广为全员休眠+定时唤醒？
- 休眠粒度：整只停 tick（含重力）vs 仅停行为决策保留物理；跨岛/上木筏远离时的唤醒可靠性（不能出现靠近了敌人还冻着）。
- `new Array<Rid>` 缓存、射线频率降低等纯分配优化是否无争议直接做。

产出：一条明确的"允许/禁止"边界清单 + 选定的休眠调度方案（不重写 switch 分支逻辑本身），供后续执行迭代实现。
