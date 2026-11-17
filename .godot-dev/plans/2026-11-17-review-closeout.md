# 2026-11-17 审查收口执行清单

> 来源：`基于 codegraph 审查当前项目代码` 结论（P0=0，P1=4，P2=13）。
> 契约基线：`state.json > contracts[]`（9 条锁定契约全部未违背）。
> 验收口径：`dotnet build` 0 error；`godot --headless --path . --run-tests` 输出 `Failed: 0`。

## 目标

把挂了两个月的未提交改动集（36 文件 / +1072 −177）落地，补上 P1 覆盖缺口，修掉性能回退，再谈结构改造。

## 任务 1：落地改动集

- [ ] 1.1 `.gitignore` 增 `*.log`、`.godot-dev/*.log`；删除根目录 `probe_chest.gd` / `probe_chest.gd.uid`
  - 验收：`git status --short` 输出不含 `*.log` 与 `probe_chest.*`
- [ ] 1.2 生成 `test/src/PlayerCameraTest.cs.uid`
  - 验收：`(Get-ChildItem test/src -Filter *.uid).Count` == `(Get-ChildItem test/src -Filter *.cs).Count`
- [ ] 1.3 全量构建 + 测试
  - `dotnet build SeaAnomaly.csproj -v minimal` → `0 个错误`
  - `godot --headless --path . --run-tests` → `Failed: 0`
- [ ] 1.4 提交（含 `PlayerCameraTest.cs`、`.godot-dev/wayfinder/`、SharkKing raw glb/png、state.json）
  - 验收：`git status --short` 干净
- [ ] 1.5 `state.json.last_stable_commit` 更新为新提交短哈希，二次提交

## 任务 2：补 P1 覆盖

- [ ] 2.1 TDD：先写 `WeatherFogTest`（4 天气 × 昼/夜 = 8 组，断密度/散射/空气透视/`Enabled == !night`），再抽 `WeatherFog.Resolve(WeatherType, bool night)` 纯函数供 `WeatherService.ApplyFog` 调用
  - 文件：Create `src/core/WeatherFog.cs`、`test/src/WeatherFogTest.cs`；Modify `src/core/WeatherService.cs:114-166`
  - 验收：新测试 red → green
- [ ] 2.2 `AimQueryTest` 补 `IntersectPiercing` 三例（物理 rig）
  - 用例：`IntersectPiercing_SkipsRejectedArea_AndHitsTargetBehind`、`IntersectPiercing_WorldBodyStopsRay`、`IntersectPiercing_GivesUpAfterMaxPierceSteps`
  - 验收：3 例通过，且不改变既有 4 例
- [ ] 2.3 `PlayerCameraTest` 补 boom 集成用例：放置 World box 于 pivot 后方，`_PhysicsProcess` 若干帧后 `camera.Position` 沿 boom 缩短到 `hit − margin`
  - 验收：断言通过；`_currentBoom` 不越过 `MinBoomLength`
- [ ] 2.4 构建 + 测试 + 提交

## 任务 3：修性能回退（perf-review P1-4 / state T-P.4）

- [ ] 3.1 `AimQuery.IntersectPiercing` 去掉内部 `new Array<Rid>`：改为接收调用方可变 exclude，追加的 Area RID 在返回前弹出；`PlayerInteraction` 复用 `_rayExclude`
  - 验收：`grep -n "new Godot.Collections.Array<Rid>" src/player/AimQuery.cs` 为空；行为由任务 2.2 的测试守护
- [ ] 3.2 `WeaponVisual` 缓存 `PlayerModel` 引用、yaw 未变不写 transform；`CharacterAnimator` 缓存 `Camera3D`
  - 验收：两处 `_Process` 内无 `GetNodeOrNull`
- [ ] 3.3 构建 + 测试 + 提交

## 任务 4：结构改造（待确认后另排）

`HUD`/`IslandBuilder` 拆分、`GameEvents` 一事件一语义、场景默认值去重、`game/` 与 `pirate-arcade/` 分离 codegraph 索引。
