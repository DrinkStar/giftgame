---
id: WF-003
title: 给出一套用户可照做的真机 GPU 帧数据采集手册
type: task
hitl: true
status: open
assignee:
blocked_by: []
---

## Question

GPU 真机数据用户选择**后续手动采集**，agent 无法在 headless 取得。需要交付一份精确、可照做、低成本的采集清单（HITL task：agent 产出手册，用户执行后把数字回填）。要定：

- 在这台 Iris Xe（+ OrayIddDriver 虚拟显示器）上，用编辑器自带调试器/监视器还是跑一次 Windows 导出包更可信？是否需要接物理显示器/关远程桌面以排除虚拟显示开销？
- 具体步骤：如何打开 Godot Profiler/Monitors、看哪些项（FPS、帧时间、`RENDER_TOTAL_DRAW_CALLS_IN_FRAME`、`RENDER_VIDEO_MEM_USED`、`RENDER_TEXTURE_MEM_USED`、Raster 时间）、在哪些锚点场景读数（出生岛静帧 / 满编敌人战斗 / 木筏远海看海洋 / 群岛全景）、每点读多久取平均。
- 是否需要一个随包自带的轻量屏幕帧时间叠加层（不改玩法）来读数；若需要，归 WF-001 探针复用还是单独小原型。
- 1080p 中端 60 FPS 的通过线如何从读数判定。

产出：步骤手册 + 一张待填的结果表。用户回填后此票关闭，并解除 WF-004/005/008 的阻塞。
