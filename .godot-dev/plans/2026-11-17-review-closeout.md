# 2026-11-17 审查收口执行清单

> 来源：`基于 codegraph 审查当前项目代码` 结论（P0=0，P1=4，P2=13）。
> 契约基线：`state.json > contracts[]`（9 条锁定契约全部未违背）。
> 验收口径：`dotnet build` 0 error；`godot --headless --path . --run-tests --quit-on-finish` 输出 `Failed: 0`。

## 目标

把挂了两个月的未提交改动集（36 文件 / +1072 −177）落地，补上 P1 覆盖缺口，修掉性能回退，再谈结构改造。

## 任务 1：落地改动集

- [x] 1.1 `.gitignore` 增 `*.log`、`.godot-dev/*.log`；删除根目录 `probe_chest.gd` / `probe_chest.gd.uid`
  - 验收：`git status --short` 输出不含 `*.log` 与 `probe_chest.*`
- [x] 1.2 生成 `test/src/PlayerCameraTest.cs.uid`
  - 验收：`(Get-ChildItem test/src -Filter *.uid).Count` == `(Get-ChildItem test/src -Filter *.cs).Count`
- [x] 1.3 全量构建 + 测试
  - `dotnet build SeaAnomaly.csproj -v minimal` → `0 个错误`
  - `godot --headless --path . --run-tests` → `Failed: 0`
- [x] 1.4 提交（含 `PlayerCameraTest.cs`、`.godot-dev/wayfinder/`、SharkKing raw glb/png、state.json）
  - 验收：`git status --short` 干净
- [x] 1.5 `state.json.last_stable_commit` 更新为新提交短哈希，二次提交

## 任务 2：补 P1 覆盖

- [x] 2.1 TDD：先写 `WeatherFogTest`（4 天气 × 昼/夜 = 8 组，断密度/散射/空气透视/`Enabled == !night`），再抽 `WeatherFog.Resolve(WeatherType, bool night)` 纯函数供 `WeatherService.ApplyFog` 调用
  - 文件：Create `src/core/WeatherFog.cs`、`test/src/WeatherFogTest.cs`；Modify `src/core/WeatherService.cs:114-166`
  - 验收：新测试 red → green
- [x] 2.2 `AimQueryTest` 补 `IntersectPiercing` 三例（物理 rig）
  - 用例：`IntersectPiercing_SkipsRejectedArea_AndHitsTargetBehind`、`IntersectPiercing_WorldBodyStopsRay`、`IntersectPiercing_GivesUpAfterMaxPierceSteps`
  - 验收：3 例通过，且不改变既有 4 例
- [x] 2.3 `PlayerCameraTest` 补 boom 集成用例：放置 World box 于 pivot 后方，`_PhysicsProcess` 若干帧后 `camera.Position` 沿 boom 缩短到 `hit − margin`
  - 验收：断言通过；`_currentBoom` 不越过 `MinBoomLength`
- [x] 2.4 构建 + 测试 + 提交

## 任务 3：修性能回退（perf-review P1-4 / state T-P.4）

- [x] 3.1 `AimQuery.IntersectPiercing` 去掉内部 `new Array<Rid>`：改为接收调用方可变 exclude，追加的 Area RID 在返回前弹出；`PlayerInteraction` 复用 `_rayExclude`
  - 验收：`grep -n "new Godot.Collections.Array<Rid>" src/player/AimQuery.cs` 为空；行为由任务 2.2 的测试守护
- [x] 3.2 `WeaponVisual` 缓存 `PlayerModel` 引用、yaw 未变不写 transform；`CharacterAnimator` 缓存 `Camera3D`
  - 验收：两处 `_Process` 内无 `GetNodeOrNull`
- [x] 3.3 构建 + 测试 + 提交

## 结果

- 任务 1：commit `d3dd314`（53 文件 / +1532 −177）+ `d335541`（state）；日志与探针不再进 git；`test/src` uid/cs 66/66。
- 任务 2：commit `a3b1923`；新增 9 例（4 WeatherFog / 3 IntersectPiercing / 2 PlayerCamera boom）。
  - 附带修掉 `PlayerInputSimulationTest` 的首帧 flake：`MainMenuUI` 在 `_Ready` 里锁玩法输入，而旧 `Game` 实例被释放时的解锁可能晚于新场景的加锁（终结器/帧时序决定），导致首帧速度恒为 0。改为 `Setup` 里调 `MainMenuUI.Dismiss()`，按产品路径进入「已开局」状态。
- 任务 3：commit `b67a258`；`IntersectPiercing` 不再每帧 `new Array<Rid>`，`WeaponVisual` / `CharacterAnimator` 缓存节点引用。
- 验收：`dotnet build` 0 error；`godot --headless --path . --run-tests --quit-on-finish` **484 passed / 0 failed**（并行与 `--sequential` 各跑一次）。
- 环境坑：Godot 不会自动重编译 C#，改完测试必须先 `dotnet build` 再跑 `--run-tests`，否则跑的是旧程序集（曾误判一次 flake）。
- `last_stable_commit = b67a258`；任务 4 见下节。

## 任务 4 结果：GameEvents 收敛（方案 B）

- 设计与计划：`.godot-dev/specs/2026-11-17-gameevents-convergence-design.md`、`.godot-dev/plans/2026-11-17-gameevents-convergence.md`。
- 语义纠偏：新增 `ProjectileHit` / `RangedAttack`，攻击分派抽成纯静态 `CombatFeedback.RaiseAttack(AttackType)`；`Projectile.ItemId` 携带发射武器 id；`SfxHook` 按 `RangedSfxId` 路由（弓 → `bow_shoot`，投掷 → `melee_swing`，与改动前一致）。commit `ecfd0e8` + `2de3c1a`。
- 死事件清账：`LivestockTamed` / `LivestockProduced` / `BossPhaseChanged` / `TalentUnlocked` / `SkillActivated` 连同 raise helper 与全部调用点删除，声明数 54 → 51，`src/` 全库 0 命中；`BossPhaseTest` 两例改断 `CurrentPhase`（序列 `[2,3,3]`、>70% 停在 1）。commit `650e744`。
- UI 接线：`CraftingProgress` → CraftUI `CraftProgress` 进度条（0/终态隐藏）；`GameSaved` / `GameLoaded` → HUD `ToastLabel` + 1.5s toast；订阅均在 `_Ready`、退订在 `_ExitTree`。`GameOver` 保留并标注真结局专用。
- 卫生守卫：`GameEventsHygieneTest` 三例（每文件 `+=`/`-=` 配对、每个事件有生产者+消费者且 `GameOver` 双侧白名单、声明名唯一），声明数硬断言 51，生产者正则收窄为 `GameEvents\.Raise(\w+)\(`；变异自检留档（`t5-run-mut.log` 报 `HUD.cs:GameSaved +=1 -=0`，`t5-run-green.log` 493/0）。commit `f2f3ed3` + `9050b19`。
- 终审：整分支评审 **Ready to merge = Yes**，0 Critical / 0 Important；修复波 commit `574cebf`（正则收窄 + `ProjectileHit` 注释对准 `Projectile.ApplyHit`），scoped 复审全 ADDRESSED。
- 验收：`dotnet build -t:Rebuild` **79 warnings / 0 errors**（与基线一致）；`godot --headless --path . --run-tests --quit-on-finish` **493 passed / 0 failed**（基线 484，+9）。
- 遗留：7 条 Minor（doc 语义、调用点覆盖、`MouseFilter`、`BossId` write-only、卫生测试键路径/注释过滤/常量名）全部 park，建议合并后开一张小清理票；`IslandGeneratorTest::BuilderPlacesEnemiesPerTierWhenPlayerExists` 偶发超时建议单开票。
- `last_stable_commit = 306b35d`。

## 集成与交付

- 远端 `dev/perf-p0-p1-20260817` 上存在本地缺失的 PR #1（CodeGraph 工具、无头冒烟、熔岩池兜底、LookGood/DRM 兼容），已 merge 进本分支（`306b35d`）；唯一冲突 `.godot-dev/state.json` 保留双方字段并刷新场景/脚本/测试计数。
- 合并结果重新验证：Rebuild 79 warnings / 0 errors；全量测试 **495 passed / 0 failed**。
- 推送评审分支 `review/gameevents-convergence`，PR **#5**：https://github.com/DrinkStar/giftgame/pull/5 （base `dev/perf-p0-p1-20260817`）。

## 任务 4 原始范围说明（其余结构改造仍在待排）

`HUD`/`IslandBuilder` 拆分、`GameEvents` 一事件一语义、场景默认值去重、`game/` 与 `pirate-arcade/` 分离 codegraph 索引。
