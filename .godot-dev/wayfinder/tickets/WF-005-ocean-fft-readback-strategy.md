---
id: WF-005
title: 海洋 FFT 调度与回读的修复策略选型
type: grilling
hitl: true
status: open
assignee:
blocked_by: ["WF-004"]
---

## Question

针对 P0-1（cascade 追赶单帧补完 hitch）与 P1-1（同步 TextureGetData 回读 stall），选哪种修复策略、各自对视觉与契约的影响如何？候选方向（需在票内 grill 到一个决策）：

- **调度**：严格"每帧最多一条 cascade、落后就丢弃追赶而非补完"（消除 hitch 但海浪更新延迟）；或降低 `UpdatesPerSecond`/cascade 数使供给≤60 槽；或时间分片预算（单帧 FFT 耗时上限）。
- **回读**：改异步回读（`Image.GetTexture` / RenderingDevice 路径上一帧映射）消除 stall；或降低回读频率/分辨率；木筏浮力能否只用 CPU 近似波高而完全去掉 GPU 回读。
- **分辨率**：`MapSize` 512 是否按质量档降到 256，cascade 3→2 的视觉差异能否接受；是否加非 2 幂校验（perf-review P2-4）。

约束：不改 headless 降级语义（契约 4）；需保持木筏浮力手感与现有水体观感；同 seed 群岛确定性不受影响。产出：选定的调度+回读策略、质量档参数、验收帧时间目标。
