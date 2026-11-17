---
id: WF-007
title: 群岛植被/道具的 draw call 与节点数治理策略
type: grilling
hitl: true
status: open
assignee:
blocked_by: ["WF-004"]
---

## Question

`IslandBuilder` 把植被/道具逐株 `AddChild` 为独立 `MeshInstance3D`/`VegetationProp`（橡/松/草/灌/石/棕榈等，密度不低）。若真机 draw call/node 数被 WF-004 证实为瓶颈，治理到什么程度、用什么方案？

- **MultiMesh 合批**：按模型+材质把装饰实例合为 `MultiMeshInstance3D`（可砍植被与纯装饰需分开——契约：可砍配额与装饰实例分离；交互株要保留碰撞/脚本，不能合进不可点的 MultiMesh）。
- **可见性/距离剔除**：`VisibilityOnScreen` / 距离 LOD / 以岛为单位的休眠；与世界生成写入互斥契约的关系（IslandBuilder 一族同一时刻一个写入者）。
- 保持**同 seed 同布局**（契约 5）：合批只能改变渲染表达，不能改变实例位置/数量确定性。
- 节点数本身对 `_Process`/树遍历的开销是否也需处理。

产出：哪些进 MultiMesh、哪些保留独立节点、剔除/LOD 档位方案，以及对生成确定性与可砍交互零影响的验收条件。
