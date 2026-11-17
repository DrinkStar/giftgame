---
id: WF-004
title: GPU/渲染热点归因（海洋 FFT、draw call、植被、阴影）
type: research
hitl: true
status: open
assignee:
blocked_by: ["WF-003"]
---

## Question

拿到真机 GPU 数据后，帧时间/显存花在哪、谁是 60 FPS 的首要约束？用新证据复核 perf-review.md：

- P0-1 cascade 追赶 hitch：3 cascade × 30 Hz 在 60 FPS 下永远落后、30 FPS 下单帧补 3 条 FFT —— 真机帧时间尖峰是否复现、幅度多少？
- P1-1 同步 `TextureGetData` 回读（2 Hz）造成的整帧 stall 实测多少 ms？
- P1-2 三 cascade 512 FFT ≈48 MB 缓冲 + 纹理在 Iris Xe（共享显存）上的占用与带宽压力？
- draw call 数：群岛植被逐株独立 `MeshInstance3D`（IslandBuilder 多处 AddChild）、15–17 敌人 GLB、建筑件——`RENDER_TOTAL_DRAW_CALLS` 多少、是否成为瓶颈？
- 阴影 2048、FSR 0.75 当前的实际收益/代价。

产出：GPU 侧按成本排序的热点表 + 与 16.6 ms 预算的差距，作为 WF-006/007/008 的输入。
