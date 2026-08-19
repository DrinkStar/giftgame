# 全项目只读性能审查报告

> 审查参数（grilling 已确认）：全仓 + 工作区脏文件；**主视角 = 性能 / 帧时间 / 内存**；**只出报告，不改** `.cs` / `.tscn` / `project.godot`；证据 = 静态热点 + 一次 `godot --headless --path . --quit-after 5`（headless 无真实 GPU，数字只作参考）。
>
> 基准：分支 `dev/crash-logging-20260817`，HEAD `d779b10`；工作区另有未提交 `src/ui/CraftUI.cs`、`src/ui/StorageUI.cs`（计入当前代码）。不覆盖 `code-review.md`。14 条锁契约保持；`PlayerMotion` / AI / WaterMesh headless 语义只点名不修。
>
> 分级：P0（性能）= 可玩性级卡顿、无界增长、每帧必经的同步 GPU / 大分配。P1 = 明显热点但已有缓解，或成本随内容线性放大。P2 = 微观优化或上次已记录仍在的项。崩溃 / 丢档仍标 P0 写进主报告（本次不修）。非性能进附录。

---

## 1. 结论摘要

- **性能 P0：1 项**（GPU 路径 cascade 追赶 hitch）。headless 按契约 4 关掉海洋 GPU，本次冒烟**验证不了**该卡顿，只作静态结论。
- **性能 P1：6 项**（含上次 P2-14 同步回读：频率已从 5 Hz 降到 2 Hz，机制仍在，升为 P1）。
- **性能 P2：若干**（常驻 `_Process`、小分配、已落地的 FSR/阴影缓解、MapSize 无 2 幂校验）。
- **无界增长**：静态扫描未坐实每帧无界分配或集合膨胀。`CrashLogWriter` 环形缓冲 200 条、会话文件 2 MB 轮转，有上界。
- **正确性 P0（本次不修）**：未发现新的必崩 / 丢档路径。上次未修的 `Object3DModel!` 空引用仍是潜在崩溃，记入主报告 §5。上次 code-review 的 P0 均已修。
- **锁契约**：14 条均未在本审查中改代码；挡住「改 AI / PlayerMotion / headless 降级语义」，**不挡住**修 GPU 路径 hitch 或异步回读（本次按协议仍不修）。
- **冒烟**：exit 0；`WaterMesh` GPU 降级 WARN（契约 4）；退出时 4 ObjectDB leak / 2 resources still in use（headless 拆树常见，不作实机帧率）。墙钟 ~26.5 s 是引擎 + C# 启动，不是 5 秒游戏循环（`--quit-after 5` = 5 **帧**）。

最大账在实机海洋：3 cascade FFT（`MapSize=512`，`UpdatesPerSecond=30`）+ 同步 `TextureGetData` 回读（2 Hz）+ 追赶时单帧打完剩余 cascade。CPU 侧其次是每物理帧射线 / `Array<Rid>` 分配、全员 `EnemyBase._PhysicsProcess`、HUD 每帧拼时间字符串。

---

## 2. 性能 P0

### P0-1 `WaveGenerator.Update` 落后 cascade 单帧补完 hitch

- **路径**：`src/world/ocean/WaveGenerator.cs` `Update`（约 L247–256）+ `_Process`（约 L142–154）；由 `WaterMesh.UpdateWater` 以 `UpdatesPerSecond=30` 调用。3 条 cascade（`WaterMesh._Ready`）。
- **机制**：注释写「每帧一条 cascade 以平衡 stutter」。`_Process` 每帧只 `UpdateOne` 一条。但 `Update()` 若发现上次 `_passNumCascadesRemaining != 0`，会在**当前调用**里 `ComputeListBegin` 后把剩余 cascade **全部 dispatch**，再把 remaining 重新设为 3。
- **为何影响帧**：30 Hz 的 `Update` 需要每秒约 90 条 cascade 工位（3×30），60 FPS 只有 60 个 `_Process` 槽，**永远落后**。60 FPS 时约每 33 ms 追赶 1 条；**30 FPS 时 `Update` 每帧都触发，追赶约 2 条 + 本帧 1 条 = 单帧 3 条 FFT**，负载均衡完全失效。低帧 → 更多追赶 → 更 hitch，是可玩性级卡顿反馈环。headless `_gpuEnabled=false` 时整条路径跳过（冒烟无法复现）。
- **锁契约**：契约 4 只锁 headless 降级（`_gpuEnabled=false` + `GetWaveHeight`→0）。改调度 / 降 `UpdatesPerSecond` / 限制追赶条数**不违反**该语义。本次协议：只报告不修。`PlayerMotion` / AI 无关。

---

## 3. 性能 P1

### P1-1 同步 `TextureGetData` 回读 stall（上次 P2-14，仍成立，升 P1）

- **路径**：`src/world/ocean/WaterMesh.cs` `_Process`（约 L181–189）→ `WaveGenerator.RetrieveDisplacementImage`（`TextureGetData` + `Image.CreateFromData` + `Convert(Rgbaf)`）。`DisplacementReadbackPerSecond=2`（注释写明从 5 降到 2）；TODO 仍写要改异步。
- **机制**：同步回读会 stall 渲染线程。每次分配约 512²×8 B（Rgbah）再转 512²×16 B（Rgbaf）≈ 2 MB + 4 MB CPU 图，**每秒 2 次**。Raft 注释仍写「10 Hz」，与代码 2 Hz 漂移（见 P2-3）。
- **为何影响帧 / 内存**：不是每帧，但是周期性整帧 stall；频率已砍 60%，机制未消失。headless 不走此路径。
- **锁**：不挡住改异步回读；契约 4 只锁降级语义。本次不修。

### P1-2 三 cascade FFT + 512 图 GPU 常驻带宽

- **路径**：`WaveGenerator.InitGpu`：`fft_buffer = numCascades * MapSize² * 4 * 2 * 2 * 4`。默认 3×512²×64 ≈ **48 MB** GPU 存储缓冲；另 spectrum / displacement / normal 纹理约数十 MB 量级。dispatch 组 `MapSize/16`。
- **机制**：每条 cascade 的 spectrum_modulate → FFT → transpose → unpack。正常一帧一条；与 P0-1 叠加时一帧多条。
- **为何影响帧 / 内存**：实机海洋的固定 GPU 税。提交 `dce10a0` 已用 FSR 0.75 + 阴影 2048 做渲染侧缓解，**未减 FFT 分辨率**。无界增长不成立（一次性分配）。
- **锁**：不挡住降 MapSize / cascade 数；headless 不创建这些缓冲。本次不修。

### P1-3 敌人数量随群岛线性涨，全员每物理帧跑 AI

- **路径**：`EnemyBase._PhysicsProcess`（距离、行为 switch、`MoveAndSlide`）。`Game.tscn` 已有 2 野猪 + 2 狼 + 1 蟹；`IslandBuilder.TierEnemies`：Spawn 岛 2 crab、Main 2 boar + 2 wolf、Storm 2 storm_beast。`WorldLayout`：1 Main + 2–3 Spawn + 1 Storm → 运行时约 **15–17** 只，均挂 GLB + 物理。
- **机制**：无距离剔除 / LOD；全图敌人每 tick 都算。
- **为何影响帧**：CPU 物理 + 动画随岛数线性放大。远岛敌人仍 tick。
- **锁**：**契约 3 挡住改 AI 状态机 / `PlayerMotion`**。减数量、加距离休眠、改生成表不必然碰状态机，但本次不修。只点名。

### P1-4 每帧 `new Array<Rid>` 做射线排除

- **路径**：
  - `PlayerInteraction._Process` → `UpdateInteractionTarget`：`query.Exclude = new Array<Rid> { _player.GetRid() }`（每渲染帧）。
  - `PlayerController.ResolveRaftCarry`：同样每物理帧分配（木筏携带探测）。
  - `Projectile._PhysicsProcess`：存活弹体每物理帧分配（连续碰撞扫掠）。
- **机制**：Godot 集合跨托管边界，每帧分配 + 射线查询。
- **为何影响帧 / 内存**：主循环必经的托管分配，增加 GC 压力。弹体多时随弹数放大。缓存 `Array<Rid>` 即可缓解，非锁文件。本次不修。

### P1-5 HUD 每帧 `GetTimeString()` 写 Label

- **路径**：`HUD._Process` → `DayNightService.GetTimeString()`（`$"{hours:D2}:{minutes:D2}"`）。HUD **不**订 `TimeChanged`。
- **机制**：每帧插值字符串 + 设 `Label.Text`（即便文本未变也可能触发 UI 脏标记，取决于控件实现）。
- **为何影响帧 / 内存**：小但必经的托管分配。分钟才变，却每帧拼。本次不修。

### P1-6 `WeatherService.ApplyFog` 每帧写 Environment

- **路径**：`WeatherService._Process` 每帧 `ApplyFog()`（按天气设 `FogDensity`）。`TimeChanged` 订阅方只缓存小时（廉价）；真正的每帧成本是雾参数写入。
- **机制**：属性写入即使值不变也走引擎 setter。
- **为何影响帧**：比事件总线本身更重。可脏标记，非锁。本次不修。

---

## 4. 性能 P2

### P2-1 脏 UI：`CraftUI` / `StorageUI` 常驻 `_Process`（工作区未提交）

- **路径**：未提交的 `CraftUI._Process` / `StorageUI._Process`：比较 `_releaseLockAtFrame` 与 `Engine.GetProcessFrames()`。面板关闭后回调仍在跑。
- **机制**：从 `CallDeferred` / `ProcessFrame` 信号改为帧计数锁释放（Release 下 `CallDeferred(nameof private)` 会 Method not found）。正确性收益大，性能上是两条常开空转 `_Process`。
- **为何影响帧**：成本极低（一次 ulong 比较）。属微观。详见 §7。
- **锁**：不挡。本次不改、不提交这两文件。

### P2-2 `DayNightService` 每帧 `RaiseTimeChanged`

- **路径**：`DayNightService._Process` → `GameEvents.RaiseTimeChanged`；现订阅方主要是 `WeatherService.OnTimeChanged`（写 `_currentHour`）。
- **机制**：静态 multicast，每帧一次。HUD 未订此事件。
- **为何影响帧**：1 个廉价订阅者，可忽略。若以后 HUD / 多个系统跟订，会变成 P1。不锁。

### P2-3 Raft 注释 10 Hz vs 代码 2 Hz

- **路径**：`Raft._PhysicsProcess` 注释仍写 `DisplacementReadbackPerSecond (10 Hz)`；`WaterMesh` 已是 2 Hz。5 个 `DefaultCells`，每物理帧 `GetWaveHeight`（双线性 4×`GetPixel`）+ lerp。
- **机制**：采样本身便宜；注释误导后续调参。5 cell × 4 pixel × 60 Hz ≈ 1200 `GetPixel`/s，相对 GPU 回读可忽略。
- **锁**：不挡。P2-13 力臂方向是正确性，进附录。

### P2-4 `WaveGenerator` MapSize 非 2 幂无校验（上次 P2-15，仍成立）

- **路径**：`InitGpu`：`numFftStages = (int)(Log(MapSize)/Log(2))`，dispatch `MapSize/16`。默认导出 **512 = 2⁹**，当前配置安全。
- **机制**：若场景把 MapSize 改成非 2 幂，FFT / workgroup 非法，潜在 GPU 失败或 hitch。
- **为何影响帧**：潜伏项，不是当前可玩性卡顿。不锁。

### P2-5 渲染设置已缓解（对照 `dce10a0`）

- **路径**：`project.godot`：`forward_plus`；`scaling_3d/mode=1`（FSR）；`scaling_3d/scale=0.75`；`directional_shadow/size=2048`。
- **机制**：提交 `dce10a0 perf(rendering): reduce ocean FFT cost, shadow range, enable FSR scaling` 已落地。本审查视为**已缓解的基线**，不是新债。
- **锁**：不挡继续降档。本次不改 `project.godot`。

### P2-6 CrashLog 热路径：Warning 才进 Mutex + `AutoFlush` 刷盘

- **路径**：`EngineLogSink._LogMessage` 跳过非 error 的普通 print；`_LogError`（含 Warning）→ `CrashLogWriter.WriteLine`（`lock` + `StreamWriter.AutoFlush=true`）。
- **机制**：战斗 / 海洋下若 `PushWarning` 风暴，会在任意线程抢锁并同步写 `user://logs/seaanomaly.log`。普通 `GD.Print`（昼夜启动日志等）**不**进会话文件。环形 200、会话 2 MB 轮转，有界。
- **为何影响帧**：空转时不是热点；警告风暴时可能 stall。冒烟仅 1 条 WaterMesh WARN。不锁（契约 10–14 管内容与路径，不管 flush 频率）。

### P2-7 其它常驻但早退的 `_Process` / `_PhysicsProcess`

| 节点 | 行为 | 成本 |
|------|------|------|
| `WaterFollow` | 每帧把海面 mesh XZ 跟玩家 | 一次 `GlobalPosition` 写入 |
| `WeaponSystem` | 每帧 `SelectedItem?.Id == "torch"` | 热栏查询 |
| `GuideService` | 去重窗口计时 | 早退为主；正确性见附录 P2-29 |
| `TutorialUI` | 仅 `IsTutorialActive` | 非教程早退 |
| `CraftingSystem` | 仅 `_isCrafting` | 空闲早退 |
| `FarmPlot` / `Livestock` | 无作物 / 非 Producing 早退 | 空闲廉价 |
| `BuildingSystem` | 仅建造/拆除模式射线 | 非建造模式跳过 |
| `StationLinker` | `PollInterval=0.5` | 非每帧全扫 |
| `PlayerStats` | 饥渴/体力 tick | 算术，无分配 |
| `FloatingBody` | 5 cell `ApplyForce` | CPU 轻；GPU 高度来自缓存图 |
| `PlayerMotion` | 被 `PlayerController` 调用 | 扫描无热点；**契约 3 禁止改** |

群岛网格：`IslandHeightmap.DefaultResolution=129` → 约 16k 顶点 / 岛 × 4–5 岛，**加载期** ArrayMesh + HeightMapShape3D，非每帧。属加载 hitch，不升 P1。

---

## 5. 正确性 P0（本次不修）

协议要求：崩溃 / 丢档即使不是性能问题，仍标 P0 写进主报告，本次不修。

| # | 位置 | 问题 | 状态 |
|---|------|------|------|
| 正确性-P0-1 | `BuildableInstance.Initialize` / `MouseObject.UpdateVisual`：`Object3DModel!` | 资源缺模型时 `Instantiate` NRE（上次 P2-11） | **仍在**。正常 `.tres` 都有模型，属防御性崩溃。 |
| 上次 P0-1/2/3 | 存档路径、背包宽度、教程触发器 | 已在 code-review 修复 | **已过时**（不重复开单） |

未发现新的必崩路径或静默丢档。`WaveGenerator` 非 2 幂 dispatch（P2-4）是潜伏 GPU 失败，现配置 512 不触发。

---

## 6. 附录：上次未修 P2 复核（非性能）

对照 `code-review.md` §4。只标注「仍在 / 已过时 / 部分已修」。已修项不展开。

| 编号 | 结论 | 说明 |
|------|------|------|
| P2-01 | 已过时 | f30ac22 已修 WeaponVisual |
| **P2-02** | **仍在** | `MouseObject` 墙体选中瞬间 0×0 幽灵瓦片 |
| **P2-03** | **仍在** | 主岛敌人 vs Ground 盒/网格贴合（已缓解地形高度，未做坡度寻路） |
| P2-04–P2-08b | 已过时 | 5082857 / f30ac22 |
| **P2-09** | **已过时** | `BuildingLoaded` 事件已删（P2-22）；`LoadMostRecent` 由 `GameManager` 调用，不是死代码。`_currentSaveFile` 仍服务覆盖写路径 |
| **P2-10** | **仍在** | `BuildingSaveSystem.Restore` 与 `_freeObjectsList` 不同步（重复 restore 累积）——正确性/泄漏边缘，非每帧无界 |
| **P2-11** | **仍在** | 见 §5 正确性 P0 |
| **P2-12** | **仍在** | 注释过期 |
| **P2-13** | **仍在** | `FloatingBody.ApplyForce(..., GlobalBasis * cell.LocalPosition)` 俯仰/横滚时力臂方向错——正确性，不是帧成本 |
| P2-14 | **升主报告 P1-1** | 回读仍同步；频率 5→2 Hz |
| P2-15 | **升主报告 P2-4** | 默认 512 安全，无校验 |
| **P2-16** | **仍在** | `WorldLayout` 用 `System.Random`；契约 5 是同 seed 同布局（同运行时），跨 CLR 无保证属已知债 |
| P2-17–P2-20, P2-22 | 已过时 | 已修 |
| **P2-21** | **仍在** | `SfxHook` Storm 注释 vs Rain 代码漂移 |
| **P2-23** | **部分已修** | CraftUI/StorageUI `_ExitTree` 同步放锁（含未提交工作区）；静态 `GameplayInputLocked` 仍无场景级总复位 |
| P2-24–P2-28, P2-30b | 已过时 / 复核无需改 | 见原报告 |
| **P2-29** | **仍在** | `GuideService` 去重误吞（正确性） |
| **P2-30 剩余** | **仍在** | `FinalizeTutorial` 无条件 Captured 鼠标、`BuildChapters` 重入、`StorageUI.Refresh` 守卫不对称、奖励假推进 step2、`StoryInteractable` 无场景实例、deferred 重订阅边缘。Q 键锁检查为误报 |
| **P2-31** | **仍在** | `GroundLoot` 类注释仍写拾取后销毁节点；`Interact` 在 `remaining > 0` 时保留节点但不把 `Amount` 改成余数，注释与余数契约都不完整 |
| **P2-32** | **仍在** | 测试 CA1310 / CA1001 / CA1822 |

---

## 7. 脏文件专节（CraftUI / StorageUI）

工作区 `git status`：

```
 M src/ui/CraftUI.cs
 M src/ui/StorageUI.cs
?? .godot-dev/rel-test-full-err.log
?? .godot-dev/rel-test-full.log
```

`CraftUI.cs` / `StorageUI.cs` 相对 HEAD 约 −91/+50 行：锁释放从引擎 `CallDeferred` / `ProcessFrame` 改为 **纯 C# 帧计数** + 常驻 `_Process`；`_ExitTree` 仍同步 `RaiseGameplayInputLockChanged(false)`。

**性能**：两条节点在 `Game.tscn` 生命周期内 `_Process` 常开。关闭面板时 `_releaseLockAtFrame == ulong.MaxValue`，每帧一次比较后返回。相对海洋 / 敌人可忽略（P2-1）。

**正确性**（附录，非本审查主视角）：该改动是 c7a4ff9 同类修复的延续（Release 下 private `CallDeferred` 失败）。审查范围包含脏文件，但**按计划不提交、不继续改**。

`rel-test-full*.log` 与性能无关，忽略。

---

## 8. 冒烟摘录

命令（在 `game/`）：

```
godot --headless --path . --quit-after 5
```

| 项 | 值 |
|----|----|
| 退出码 | **0** |
| 墙钟 | **26510 ms**（启动 + 导入/C# + 5 帧，不是 5 秒玩法） |
| 引擎 | Godot 4.7.1.stable.mono |
| GPU 海洋 | **未跑**（契约 4） |

相关输出：

```
WARNING: WaterMesh: ocean GPU init failed (Object reference not set to an instance of an object.); simulation disabled.
   at: void SeaAnomaly.WaterMesh._Ready() (res://src/world/ocean/WaterMesh.cs:141)
WARNING: 4 ObjectDB instances were leaked at exit (run with `--verbose` for details).
ERROR: 2 resources still in use at exit (run with --verbose for details).
```

解读：

- WaterMesh 降级符合契约 4；headless **不能**用来给 P0-1 / P1-1 / P1-2 打帧时间数字。
- 退出 ObjectDB / Resource leak 是 headless 拆树常见现象，**不**证明运行时泄漏。未加 `--verbose`，未定位 4 个实例。
- 未加探针、未改主场景、未跑导出。

---

## 9. 锁契约核对（只读）

| # | 契约 | 本审查 |
|---|------|--------|
| 1 | `PlayerDied` ≠ `GameOver` | 未改 |
| 2 | `Game.tscn` = 产品 | 未改场景 |
| 3 | AI 状态机 + `PlayerMotion` 不改 | 只点名 P1-3 / 扫描无 PlayerMotion 热点 |
| 4 | WaterMesh headless 降级 | 冒烟确认 WARN + `_gpuEnabled=false`；GPU hitch 只静态报告 |
| 5 | 群岛确定性 | 未改生成；P2-16 仍在附录 |
| 6 | 教程不暂停昼夜 | 未改 |
| 7 | `_ExitTree` 退订 | 脏 UI 仍退订；未改产品提交 |
| 8 | 禁 `--` / NodePath 不加 `node_paths` | 冒烟命令无 `--` 分隔符 |
| 9 | 许可纪律 | 未引入新资源 |
| 10–14 | 日志：PlayerDied 非崩溃、不写存档/背包、测试不写会话文件、不打进 GameEvents、原生崩溃靠 godot.log | `EngineLogSink` 跳过普通 print；热路径见 P2-6 |

---

## 10. 明确未做

- 未改任何产品代码、未覆盖 `code-review.md`、未 `git commit`。
- 未修 P0/P1（含崩溃标记、海洋 hitch、回读、敌人数量）。
- 未改 `PlayerMotion` / AI / WaterMesh 降级语义。
- 未提交 CraftUI / StorageUI。
- 未跑 `--run-tests`、未导出、未加性能探针。
