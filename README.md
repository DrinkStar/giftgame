# SeaAnomaly（海域异变）

Godot 4.7.1 .NET (C#, net8.0) 单机生存建造游戏。介于《木筏求生》与《森林》之间的生存建造核心循环：探索收集 → 建造基地 → 种植补给 → 战斗防守 → 剧情推进。

设计文档：`游戏设计-定稿.md`（仓库根）· 开发路线图/状态：`game/.godot-dev/` · 数值初稿：`game/.godot-dev/balance.md`

## 环境要求

- Godot **4.7.1 stable mono**（`D:\Godot\Godot_v4.7.1-stable_mono_win64\godot.exe`，或加入 PATH 为 `godot`）
- .NET SDK 8.0+（本项目目标 net8.0，global.json 已配置 rollForward）
- 渲染：Forward Plus（海洋计算着色器硬性要求）

## 构建与运行

在 `game/` 目录执行：

```powershell
dotnet build                                   # C# 编译
godot --headless --path . --import             # 首次/新增资产时导入
godot --path .                                 # 编辑器/运行（有窗口环境）
```

运行测试（GoDotTest，**禁用 `--` 分隔符**）：

```powershell
godot --headless --path . --run-tests --quit-on-finish
```

冒烟（headless 加载主场景 5 帧；无 GPU 时海洋自动降级为平海）：

```powershell
godot --headless --path . --quit-after 5
```

> 注：headless 下 Godot 需要写 `%APPDATA%\Godot`（user://）；若在受限沙箱中运行报段错误，用 `--user-data-dir <可写路径>` 重定向。

## 崩溃与运行日志

崩溃后把下面目录里的文件一并带走即可复现修复（**不要**把 `saves/` 存档当日志提交）。

| 文件 | 含义 |
|---|---|
| `user://logs/godot.log` | 引擎内置输出（含轮转） |
| `user://logs/seaanomaly.log` | 游戏会话日志（Info 面包屑 + Warning + Error） |
| `user://logs/crash-*.log` | 一次未处理异常 / 引擎崩溃通知一份 dump |

Windows 默认目录：

`%APPDATA%\Godot\app_userdata\SeaAnomaly\logs\`

`PlayerDied`（角色死亡重生）**不是**崩溃，不会生成 `crash-*.log`。跑 GoDotTest 时不会往上述目录写会话文件。

## 导出 Windows 构建

```powershell
./build-release.ps1        # 一键：Release 构建 + 导出 + 产物核验
```

或手动：

```powershell
dotnet build -c Release
godot --headless --path . --export-release "Windows Desktop" build/SeaAnomaly.exe
```

产物：`game/build/SeaAnomaly.exe` + `SeaAnomaly.pck`（两件套同目录）。默认非 self-contained——目标机需 .NET 8 Desktop Runtime；如需免安装，将 `export_presets.cfg` 的 `dotnet/self_contained` 改为 `true` 后重新导出。

## 操作

- WASD 移动 · 空格跳 · Shift 冲刺 · E 交互 · F 使用物品（吃喝/装备/鱼竿/背包） · 左键近战 · 右键投掷/射击 · Q 切换武器槽 · B 建造 · R 旋转 · G 木筏锚 · M 木筏桨/帆切换 · C 合成 · Tab 背包 · Esc 暂停（暂停时显示署名）
- 新手教程强制 6 步；第二/三章按章节任务触发进阶教程

## 项目结构

```
game/
├── src/                 # C# 代码（core/player/world/building/inventory/combat/
│                        #   farming/quest/progression/ui/save）
├── scenes/              # 场景（Game.tscn 产品场景；ocean_test/storm_zone 实验室）
├── assets/              # 物品/配方/建筑/模型/图标/音频/shaders（sources.md 台账）
├── test/src/            # GoDotTest 测试（300+）
└── .godot-dev/          # 状态/路线图/平衡表（开发真相源）
```

## 许可与署名

第三方资产台账与来源见 `game/assets/sources.md`。游戏内暂停界面已含署名：Eric Matyas（BGM）、game-icons.net（CC BY）。SurvivalIsland 架构移植为自用非商业（详见 `游戏设计-定稿.md` §13）。
