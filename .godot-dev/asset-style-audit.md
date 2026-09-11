# SeaAnomaly 资产画风与清晰度审计

- **日期**：2026-08-20
- **主轴**：Quaternius 卡通低模（女主 / 多数敌人 / 武器）
- **范围**：全量（角色、敌人、建筑、植被道具、地表、UI 图标；音频台账一笔带过）
- **证据**：[`assets/sources.md`](../assets/sources.md) + 磁盘抽样（文件体积 / 贴图分辨率）
- **不做**：不改代码、不换资产、不写统一画风改造路线图

---

## 1. 总判

产品视觉是三层拼装，而不是单一画风：

1. **Quaternius 卡通低模** — 玩家、多数敌人、武器（战斗与角色视线的主轴）
2. **Kenney 积木低模** — 植被、海盗道具、Sci-fi/Survival 建造件（环境与建造）
3. **Poly Haven 写实 1K albedo** — 沙滩 / 礁石 / 火山 / 雪地地表

相对主轴 Q，最大偏移是 **写实地表贴在低模岸线** 与 **Kenney Sci-fi 工作台进生存岛**；次级偏移是 **Gobkit 鲨鱼王**（同为低模但尺度/材质包与 Q 鲨鱼不同）以及 **game-icons 线稿 UI**（平面符号语言，与 3D 卡通不冲突但也不统一）。

许可纪律（契约）：CC0 + CC BY 署名 + SurvivalIsland 自用非商业。`CreditsUI` 已覆盖 Eric Matyas BGM、game-icons、Kenney、Quaternius、Poly Haven；**未单列 Gobkit**。

---

## 2. 画风矩阵（相对 Quaternius）

偏移：低 = 同属卡通低模族；中 = 低模但作者/材质语言不同；高 = 写实或主题错位。

| 族 | 代表资产 | 来源 | 风格标签 | 相对 Q | 备注 |
|---|---|---|---|---|---|
| 玩家 | `models/player/Woman.glb` (~1.4 MB) | Quaternius Animated Woman | 卡通低模 + 骨骼动画 | 主轴 | 契约禁覆盖 |
| 玩家样例 | `SurvivalProtagonist.glb` (~0.75 MB) | 程序 glTF | 森林风程序人形 | 中 | **未接线**，不得盖掉 Woman |
| 敌人（陆） | wolf / boar(Pig) / spider / bat / crab / mutant(Alien) | Quaternius 各包 | 卡通低模 | 低 | Wolf.gltf 台账：无贴图、flat-color（~3.0 MB 几何） |
| 敌人（海） | `shark/Shark.glb` (~76 KB) | Quaternius Pirate Kit | 卡通低模 | 低 | ocean_test 游泳鲨 |
| 敌人（Boss） | `shark_king/Shark.glb` (~165 KB) + `Shark_0.png` 512 | Gobkit Free Animal Pack | 低模 + 单贴图 | 中 | Scale=2.0；pup 同模 |
| 海兽替身 | `storm_beast/Squidle.glb` | Quaternius Ultimate Monsters | 卡通低模 | 低 | 鱿鱼替 Cthulhu |
| 武器 | spear / bow + pickaxe/sickle/… FBX | Quaternius Medieval | 卡通低模 | 低 | 镐=斧、钓竿=矛 语义替身 |
| 建筑单体 | campfire / bed GLB | Kenney / Quaternius | 积木低模 | 中 | 床是 Q；篝火 Kenney |
| 建造套件 | `kenny_sifi.glb` / `kenny_survival.glb` + colormap 512 | Kenney Sci-fi / Survival | 积木 + 共享 colormap | **高** | Sci-fi 面板/电脑进生存玩法 |
| 植被 | Nature Kit 橡/松/草/灌/石 | Kenney | 积木低模 + colormap 512 | 中 | 岛上密度主视觉 |
| 棕榈/海岛 | Pirate Kit palm / barrel / crate / chest / ship-wreck | Kenney | 积木低模 + colormap 512 | 中 | 与 Nature 同作者，内部一致 |
| 教程箱 | `wooden_chest/WoodenChest.glb` | 程序写 glTF | 低模木箱 | 中 | 与 Kenney chest 并存（用途不同） |
| 地表 | `textures/*_1k.jpg` 全部 1024² | Poly Haven | 写实 PBR albedo | **高** | 仅 diff，无完整 PBR 套 |
| UI 图标 | `assets/icons/*.png` 48 张 512² | game-icons.net | 白线稿平面 | 中（平面层） | CC BY；`milk` 无图 |
| 音频 | BGM ×3 + SFX ×13 | soundimage / freesound / Kenney | 氛围/拟音 | n/a | BGM 需署名 |

---

## 3. 清晰度档位

按「贴图分辨率 / 材质类型 / 体量观感」分档（非精确面数审计）。

| 档 | 材质语言 | 分辨率 / 体量 | 出现位置 |
|---|---|---|---|
| A 顶点色 / 无贴图 | flat albedo | 无图；Wolf ~3 MB glTF | 部分 Q 敌人 |
| B Kenney colormap | 顶点色 + 共享 atlas | **512×512**（~10–11 KB PNG） | 建造、植被、海盗道具 |
| C Gobkit 单贴图 | 简单 UV 贴图 | **512×512**（Shark_0.png） | shark_king / pup |
| D 角色/怪物 GLB | 烘焙或内嵌材质 | Woman ~1.4 MB；Spider ~0.4 MB；Bat FBX ~0.7 MB | 战斗主视线 |
| E 写实地表 | JPEG albedo | **1024×1024（1K）**，0.3–1.0 MB/张 | 沙/礁/火/雪 biomes |
| F UI 图标 | 线稿透明 PNG | **512×512** | HUD / 背包（已接线 48/49） |

观感结论：

- **岛面写实 1K** 与 **Kenney 512 colormap 植被** 同框时，地表细于道具——清晰度档位故意错层。
- **Boss Gobkit 512** 与 **Q 无贴图狼** 同属低模清晰度带，但材质语言不同（有图 vs 纯色）。
- UI 512 对 HUD 足够；与 3D 清晰度无直接可比性。

---

## 4. 混搭热点（玩家视线优先）

1. **女主（Q）站在 Poly Haven 沙滩 + Kenney 棕榈/草丛** — 角色卡通、地面写实、植物积木，第一印象混搭最强。
2. **Kenney Sci-fi 工作台 / 灯塔件** 放在生存岛屿 — 主题错位（科幻面板 vs 荒岛），偏移高。
3. **Q 游泳鲨 vs Gobkit 陆战鲨王** — 同名「鲨」两套作者；Boss Scale 2.0 再放大差异。
4. **Q 床 + Kenney 篝火** 近距离并置 — 同为低模，作者语言略异，中等。
5. **教程 WoodenChest（程序） vs 残骸 Kenney chest** — 可接受（用途分离），但木箱风格不统一。
6. **装备视觉** — `EquipmentVisual` 只染 `Woman.glb` 材质，不换模；盔甲不会引入第三套模型，混搭风险低。

---

## 5. 缺口清单（事实，不排期）

| 项 | 现状 |
|---|---|
| 图标 | **已修（2026-08-20）**：49/49 挂 Icon（`milk`←rihlsul/milk-carton；`coconut`←delapouite/coconuts） |
| `ItemData.cs` 注释 | **已修**：改为反映 icons 已接线 |
| 未接线样例 | `SurvivalProtagonist.glb` 仅样例，产品用 `Woman.glb`（有意保留） |
| 语义替身 | **W+I 已修**：pickaxe / fishing_rod / coconut 图标。仍存：boar→Pig；storm_beast→Squidle（范围外） |
| Boss 署名 | **已修**：`CreditsUI` 含 Gobkit（CC0） |
| 建造主题 | Sci-fi kit 与生存叙事不对齐（范围外） |
| 地表 PBR | 仅 diff 1K（范围外） |
| 狼贴图 | Wolf.gltf 无纹理图（台账可接受） |
| 音频 | 台账齐全 |

磁盘核对摘要（2026-08-20 S+M 后）：

- 武器：`pickaxe/Pickaxe.glb`、`fishing_rod/FishingRod.glb`、`stone_axe.fbx`（保留原斧）
- 图标：49 PNG @ 512；物品 `.tres` 全部挂 Icon
- 音频：3 BGM + 13 SFX `.ogg`

---

## 6. 契约提醒（只读）

后续若真换资产（本审计**不**发起）：

- 仍须 CC0 / CC BY（署名进 `CreditsUI` + `sources.md`）
- 禁止付费图生 3D；禁止 PNG 当模型
- 不覆盖 `Woman.glb`
- 群岛同 seed 同布局；不改 AI 状态机 / `PlayerMotion`
- SurvivalIsland 概念自用非商业

---

## 7. 一句话结论

SeaAnomaly 是 **Quaternius 角色战斗主轴**，外包 **Kenney 环境/建造** 与 **Poly Haven 1K 写实地表**；清晰度上环境贴图（1K）高于 Kenney colormap（512），画风混搭主要来自「写实地表 × 积木植被 × 卡通角色」与 Sci-fi 建造主题错位，而不是分辨率不足。
