# Asset Sources & License Ledger

Traceability ledger for all third-party assets in this project (design spec §13.5).
Rule: CC0 / free-commercial only. No NC assets. No .blend sources (Godot 4.7 native import: glTF/GLB preferred, FBX accepted).

## Models

All models downloaded 2026-08-14.

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| wolf (灰狼) | `models/enemies/wolf/Wolf.gltf` | Quaternius "Ultimate Animated Animals" (glTF export), mirrored at https://github.com/Tekh-ops/Ultimate-Animated-Animals-July-2021 (raw.githubusercontent.com/Tekh-ops/Ultimate-Animated-Animals-July-2021/main/glTF/Wolf.gltf); pack page https://quaternius.com/packs/ultimateanimatedanimals.html | CC0 1.0 | ok |
| boar (野猪) | `models/enemies/boar/Pig.glb` | Quaternius Pig via Poly Pizza https://poly.pizza/m/TNvG3QUFlp | CC0 1.0 | ok (substitute: CraftPix free wild animal pack requires account login; Quaternius pack hosted on Google Drive unreachable from this machine; opengameart.org CC0 boar is .blend-only — Pig used as boar stand-in, same author, CC0) |
| shark (鲨鱼) | `models/enemies/shark/Shark.glb` | Quaternius Pirate Kit via Poly Pizza https://poly.pizza/m/AyHTK3zUSG | CC0 1.0 | ok |
| spider (蜘蛛) | `models/enemies/spider/Spider.glb` | Quaternius Easy Enemy pack via Poly Pizza https://poly.pizza/m/yRYJiAJyiM | CC0 1.0 | ok |
| bat (蝙蝠) | `models/enemies/bat/Bat.fbx` | Quaternius "Monster Pack Animated" (Animated Monster, Aug 2018), mirrored at https://github.com/beep2bleep/FreeAssetsByKenneyNLandQuaternius | CC0 1.0 | ok |
| storm_beast (海兽) | `models/enemies/storm_beast/Squidle.glb` | Quaternius Ultimate Monsters pack, mirrored at https://github.com/Dallolz/moorfall-assets | CC0 1.0 | ok (substitute: Cute Monsters pack Cthulhu unavailable — Quaternius downloads are Google Drive links, unreachable from this machine; opengameart.org has no CC0 3D cthulhu. Squidle (squid sea-creature, CC0 Quaternius) used as storm_beast stand-in) |
| mutant (异化者) | `models/enemies/mutant/Alien.glb` | Quaternius Alien via Poly Pizza https://poly.pizza/m/sUTLXji0aL | CC0 1.0 | ok (Cute Monsters alien unavailable — Google Drive blocked; this is the Ultimate Animated Animals alien, same author, CC0) |
| shark_king (鲨鱼王) | `models/enemies/shark_king/Shark.glb` | Gobkit Free Animal Pack Shark (https://gobkit.com/freebies/animal/Shark.glb); raw master timeline kept as `SharkKing_gobkit_raw.glb`; split into Idle/Attack/Death/Walk via `tools/split_gobkit_shark.py` (frames 0–29 / 30–59 / 60–89 / 90–119 @ 24fps); import `root_scale=0.002` (cm→~1.4m); scale 2.0 in EnemyData | CC0 1.0 | ok (land-arena boss) |
| shark_pup (鲨鱼王小兵) | same Gobkit GLB as shark_king | Boss phase-2 minion; `shark_pup.tres` MeleeChase HP50/伤10; model mounted via `BossPhaseController.MinionModelScene` | CC0 1.0 | ok |
| spear (矛) | `models/weapons/spear/Spear.fbx` | Quaternius Medieval Weapons Pack (Sept 2018), mirrored at https://github.com/beep2bleep/FreeAssetsByKenneyNLandQuaternius | CC0 1.0 | ok (deviation: opengameart.org "19 low-poly fantasy weapons" is CC0 but ships only a .blend, excluded by project rule) |
| bow (弓) | `models/weapons/bow/Bow_Wooden.glb` | Quaternius Medieval Weapons Pack (GLB conversion), mirrored at https://github.com/Dallolz/moorfall-assets | CC0 1.0 | ok |

Channel notes:
- Quaternius official downloads are Google Drive folder links — drive.google.com is unreachable on this machine; CC0 mirrors on GitHub (codeload/raw) and Poly Pizza (static.poly.pizza) were used instead. All content remains Quaternius CC0 1.0.
- craftpix.net: free download requires account login (JS-gated) — not automatable; no asset taken.
- opengameart.org: reachable; CC0 3D entries for boar/spear are .blend-only; no asset taken (per .blend exclusion rule).
- Wolf.gltf note: glTF export contains no texture images (flat-color materials) — acceptable for wiring; materials can be added in a later task.

## Audio

All audio downloaded 2026-08-14. Formats: Ogg Vorbis (Godot 4.7 native import).

**ATTRIBUTION REQUIRED — BGM (soundimage.org):** all three BGM tracks are music by Eric Matyas (www.soundimage.org), used under his custom license (≈CC-BY). Games must credit: **"Music by Eric Matyas / www.soundimage.org"**. Add this credit line to the in-game credits screen; keep it in this ledger until then.

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| BGM island (岛屿氛围) | `audio/bgm/island.ogg` | soundimage.org "The Voyage Begins" (Eric Matyas) — page https://soundimage.org/epic-battle/; file https://soundimage.org/wp-content/uploads/2025/10/The-Voyage-Begins.ogg | Custom ≈CC-BY, attribution required: "Music by Eric Matyas / www.soundimage.org" | ok |
| BGM night (夜晚紧张) | `audio/bgm/night.ogg` | soundimage.org "Darkness Approaches" (Eric Matyas, looping version) — page https://soundimage.org/dark-ominous/; file https://soundimage.org/wp-content/uploads/2025/10/Darkness-Approaches_Looping.ogg | same as above | ok |
| BGM boss (Boss 战) | `audio/bgm/boss.ogg` | soundimage.org "Tower Defense" (Eric Matyas, looping version) — page https://soundimage.org/epic-battle/; file https://soundimage.org/wp-content/uploads/2025/10/Tower-Defense_Looping.ogg | same as above | ok |
| SFX ocean_waves | `audio/sfx/ocean_waves.ogg` | freesound.org CC0 search "ocean waves": sound 827530 "Ocean_coast_04_092025_0659AM" https://freesound.org/s/827530/ | CC0 1.0 | ok (hq ogg preview via public direct link https://freesound.org/data/previews/827/827530_2718606-hq.ogg — freesound full downloads require a free account; original sound page recorded for manual full-quality confirmation) |
| SFX storm | `audio/sfx/storm.ogg` | freesound.org thunderstorm by bruno.auzet CC0, sound 531041 https://freesound.org/people/bruno.auzet/sounds/531041/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks (CC-Sounds pack) https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/environment/weather/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX melee_swing | `audio/sfx/melee_swing.ogg` | freesound.org sword swing by qubodup CC0, sound 59992 https://freesound.org/people/qubodup/sounds/59992/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/melee_swing/big_cutting/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX bow_shoot | `audio/sfx/bow_shoot.ogg` | freesound.org bow by Hanbaal CC0, sound 178872 https://freesound.org/people/Hanbaal/sounds/178872/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/fire_gun/bows/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX enemy_hit | `audio/sfx/enemy_hit.ogg` | freesound.org stabbing flesh by magnuswaker CC0, sound 522091 https://freesound.org/people/magnuswaker/sounds/522091/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/melee_hit_flesh/big_stabbing/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX enemy_die | `audio/sfx/enemy_die.ogg` | freesound.org zombie death by tonsil5 CC0, sound 555412 https://freesound.org/people/tonsil5/sounds/555412/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/mon_death/zombie_death/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX chop | `audio/sfx/chop.ogg` | freesound.org axe by super8ude CC0, sound 442538 https://freesound.org/s/442538/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/tool/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX ui_click | `audio/sfx/ui_click.ogg` | Kenney "Interface Sounds" pack (click_001.ogg) — https://opengameart.org/content/interface-sounds; zip https://opengameart.org/sites/default/files/kenney_interfaceSounds.zip | CC0 1.0 (Kenney, www.kenney.nl; credit optional) | ok |

Channel notes:
- soundimage.org: direct downloads reachable; 2025/10 Ogg exports (incl. `_Looping` variants) used. See BGM attribution requirement above.
- freesound.org: site reachable and CC0-filterable, but full downloads require a free account. Strategy: fetch CC0-filtered sounds via (a) freesound's public hq preview direct links (`/data/previews/...-hq.ogg`) — used for ocean_waves, or (b) the CC0 mirror Fris0uman/CDDA-Soundpacks whose per-folder `credits.md` records the exact freesound URL + per-file license — used for storm/melee_swing/bow_shoot/enemy_hit/enemy_die/chop. Only per-file CC0 items were taken (CC-BY items in the same pack were rejected).
- opengameart.org: Kenney Interface Sounds (CC0, .ogg) used for ui_click; OGA CC0 ocean packs (beach-ocean-waves, water-waves) are FLAC-only, which Godot 4.7 does not import — skipped.
- The CDDA-Soundpacks repository LICENSE.txt is CC-BY-SA 4.0 for the collection; the per-folder credits.md files (linked above) take precedence per file and mark every file we took as CC0.

## Iter8p art (T8p.5 minimal visual pack)

All assets downloaded 2026-08-14 (plan todo 4 / W4). Every asset below has **status ok** — nothing is pending.

### Models

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| crab (海蟹) | `models/enemies/crab/Crab.glb` | Quaternius "Crab Enemy" (Easy Enemy pack) via Poly Pizza https://poly.pizza/m/Gs3yfsV5lB; file https://static.poly.pizza/b9bbf6bd-2b21-4013-bc38-0f5e524ac12c.glb | CC0 1.0 | ok (fills the iter8p-plan gap: crab model was missing from the iter6.1 batch) |
| player (女主) | `models/player/Woman.glb` | Quaternius "Animated Woman" via Poly Pizza https://poly.pizza/m/nIItLV9nxS; file https://static.poly.pizza/46d6db5a-3c9f-4238-8cdf-8eb7194498dc.glb | CC0 1.0 | ok |
| survival protagonist (程序人形) | `models/player/survival_protagonist/SurvivalProtagonist.glb` | procedural / write_gltf.py (`humanoid --quality high --style forest`) | original | ok (sample only; not wired over Woman.glb) |
| campfire (篝火) | `models/buildings/campfire/Campfire.glb` | Kenney "Campfire" via Poly Pizza https://poly.pizza/m/i6UFAevfcu; file https://static.poly.pizza/bf4a5ed8-486b-4863-a654-fade1a9eaa39.glb | CC0 1.0 | ok |
| bed (床) | `models/buildings/bed/Bed.glb` | Quaternius "Bed Single" via Poly Pizza https://poly.pizza/m/ianC28eMOF; file https://static.poly.pizza/eac4fc76-244d-44ff-8848-ef0348379bf6.glb | CC0 1.0 | ok |

### Icons (game-icons.net — CC BY 3.0, attribution required)

All icons are white-on-transparent 512px PNGs fetched from `https://game-icons.net/icons/ffffff/transparent/1x1/<author>/<name>.png`.
**Attribution: "Game icons by Delapouite and Lorc — https://game-icons.net (CC BY 3.0)".** Add this credit line to the in-game credits screen; keep it in this ledger until then.

| Asset | File | Source (game-icons.net) | License | Status |
|---|---|---|---|---|
| icon wood | `icons/wood.png` | delapouite/wood-pile | CC BY 3.0 (Delapouite) | ok |
| icon stone | `icons/stone.png` | delapouite/stone-pile | CC BY 3.0 (Delapouite) | ok |
| icon coconut | `icons/coconut.png` | delapouite/coconuts | CC BY 3.0 (Delapouite) | ok (replaced palm-tree stand-in 2026-08-20) |
| icon berries | `icons/berries.png` | delapouite/berries-bowl | CC BY 3.0 (Delapouite) | ok |
| icon stone_axe | `icons/stone_axe.png` | lorc/stone-axe | CC BY 3.0 (Lorc) | ok |
| icon wooden_spear | `icons/wooden_spear.png` | delapouite/spear-feather | CC BY 3.0 (Delapouite) | ok |
| icon torch | `icons/torch.png` | delapouite/torch | CC BY 3.0 (Delapouite) | ok |

### Audio (SFX, freesound CC0)

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| SFX wood_chop | `audio/sfx/wood_chop.ogg` | Reuse of the iter6.1 chop asset (super8ude, freesound 442538) — copied file per plan "chop 已有——补 wood_chop 复用" | CC0 1.0 | ok (reuse) |
| SFX eat_drink | `audio/sfx/eat_drink.ogg` | freesound.org "Crispy bite" by JoMungus CC0, sound 718593 https://freesound.org/people/JoMungus/sounds/718593/, fetched via public hq preview direct link https://cdn.freesound.org/previews/718/718593_11865776-hq.ogg | CC0 1.0 | ok |
| SFX place_building | `audio/sfx/place_building.ogg` | freesound.org smash_success_wood by FiveBrosStopMosYT CC0 1.0, sound 676613 https://freesound.org/people/FiveBrosStopMosYT/sounds/676613/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/smash_success/wood_furn/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX melee_hit | `audio/sfx/melee_hit.ogg` | freesound.org unarmed_hit_flesh by deleted_user_7146007 CC0, sound 383882 https://freesound.org/people/deleted_user_7146007/sounds/383882/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/melee_hit_flesh/default/credits.md | CC0 1.0 (per-file credits.md) | ok |
| SFX death_respawn | `audio/sfx/death_respawn.ogg` | freesound.org zombie_death_3 by bananplyte CC0 1.0, sound 452347 https://freesound.org/people/bananplyte/sounds/452347/, fetched via CC0 mirror Fris0uman/CDDA-Soundpacks https://github.com/Fris0uman/CDDA-Soundpacks/blob/main/sound/CC-Sounds/mon_death/zombie_death/credits.md | CC0 1.0 (per-file credits.md) | ok |

Channel notes (Iter8p):
- Poly Pizza: the model page embeds the direct file link `https://static.poly.pizza/<uuid>.glb` (CC0 assets only, license re-verified per page); the search API requires an API key, so pages were located via web search.
- game-icons.net: `/icons/<fg>/transparent/1x1/<author>/<name>.png` renders the white-on-transparent variant (the `000000` background variant is opaque black, rejected).
- freesound.org: apiv2 now requires an API token; sound pages expose `https://cdn.freesound.org/previews/<id>/<id>_<uid>-hq.ogg` public hq previews (direct, no account) — used for eat_drink. All other SFX came from the iter6.1-proven CDDA-Soundpacks CC0 mirror, per-file CC0 verified in credits.md.

## Iter9 art (T9.1 building kit reuse)

All assets downloaded 2026-08-15. Every asset below has **status ok**.

### Models — Kenney building kit (CC0, reused from godot-refs, no download)

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| sifi building kit (13 pieces used) | `models/buildings/kenney/kenny_sifi.glb` + `kenny_sifi_colormap.png` | Kenney "Sci-fi" kit, vendored inside godot-refs/MarkoDM-GodotInGameBuildingSystem/BuildingSystem/assets (itself from https://kenney.nl/assets) | CC0 1.0 (Kenney) | ok (13 buildings extract named meshes: table/structure-panel/structure-panel-big/rail/computer/table-display-planet) |
| survival building kit (2 pieces used) | `models/buildings/kenney/kenny_survival.glb` + `kenny_survival_colormap.png` | Kenney "Survival" kit (same origin as above) | CC0 1.0 (Kenney) | ok (campfire-pit for cooking_stove/furnace) |

Building visual wiring: `src/building/KenneyBuildingVisual.cs` extracts a named mesh from the GLB at runtime; 13 buildable scenes (storage_box/workbench/workbench_t2/workbench_t3/cooking_stove/drying_rack/water_purifier/furnace/loom/research_table/lighthouse/trap/beehive) mount it. Colormap material rebuilt in code (the glb's original material path points into godot-refs).

### Icons (game-icons.net — CC BY 3.0, attribution required)

All downloaded 2026-08-15 from `https://game-icons.net/icons/ffffff/transparent/1x1/<author>/<name>.png` (white-on-transparent 512px). Attribution: "Game icons by Delapouite and Lorc — https://game-icons.net (CC BY 3.0)" — keep in the in-game credits screen.

| Item | game-icons slug | Item | game-icons slug |
|---|---|---|---|
| pickaxe | delapouite/mining-helmet | sickle | delapouite/sickle |
| fishing_rod | lorc/fishing-hook | arrow | lorc/arrow-flights |
| wooden_bow / iron_bow | lorc/high-shot | backpack | delapouite/backpack |
| cloth_armor | delapouite/chest-armor | leather_armor | lorc/leather-boot |
| iron_armor | delapouite/armor-upgrade | iron_spear | delapouite/spear-feather |
| raw_meat / grilled_meat / salted_meat | delapouite/steak | cooked_meat | lorc/roast-chicken |
| raw_fish | lorc/fishing-hook | cooked_fish / fish_soup / vegetable_stew / mushroom_soup | delapouite/cooking-pot |
| egg | delapouite/fried-eggs | bread / cornbread | delapouite/bread |
| carrot / carrot_seed | delapouite/carrot | corn / corn_seed | delapouite/corn |
| potato / potato_seed | delapouite/potato | mushroom | lorc/mushroom |
| wheat | lorc/wheat | wheat_flour | delapouite/flour |
| honey | lorc/honeycomb | wool | delapouite/wool |
| iron_ore | delapouite/stone-pile | iron_ingot | lorc/anvil |
| berry_juice / berry_seed / fruit_salad | delapouite/berries-bowl | mushroom_seed / wheat_seed | copies of the crop icons (mushroom/wheat) |

`milk` uses rihlsul/milk-carton (CC BY 3.0) — see Iter9p / S+M audit fix below. All 49 items carry icons (2026-08-20).

### Models — weapon kit (Quaternius CC0, via beep2bleep GitHub mirror)

Downloaded 2026-08-15 from the `FreeAssetsByKenneyNLandQuaternius` mirror (Quaternius "Medieval Weapons Pack - Sept 2018", CC0). The pack is the same source as the existing bow/spear models.

| Item model | File | Source (mirror path) | License | Status |
|---|---|---|---|---|
| pickaxe | `models/weapons/pickaxe/Pickaxe.glb` | Quaternius "Stone Pickaxe" via Poly Pizza https://poly.pizza/m/pvOeJ5EcpW ; file https://static.poly.pizza/da91e155-332b-459d-86c2-66149fd05604.glb | CC0 (Quaternius) | ok (replaced Axe_Small stand-in 2026-08-20) |
| sickle | `models/weapons/sickle.fbx` | .../FBX/Scythe.fbx | CC0 | ok |
| fishing_rod | `models/weapons/fishing_rod/FishingRod.glb` | Quaternius "Fishing Rod" via Poly Pizza https://poly.pizza/m/9AOHhRPHE7 ; file https://static.poly.pizza/d02f4dd8-c33d-4995-b044-06786446879c.glb | CC0 (Quaternius) | ok (replaced Spear stand-in 2026-08-20) |
| iron_spear | `models/weapons/iron_spear.fbx` | .../FBX/Spear.fbx | CC0 | ok |
| iron_bow | `models/weapons/iron_bow.fbx` | .../FBX/Bow_Evil.fbx | CC0 | ok |
| stone_axe | `models/weapons/stone_axe.fbx` | Quaternius Medieval Weapons Pack Axe_Small.fbx (kept when pickaxe was replaced) | CC0 | ok |

Display wiring: `src/player/WeaponVisual.cs` shows the selected weapon/tool's model at a hand offset on the player (hotbar selection + inventory refresh).

## S+M audit fix (2026-08-20)

Icons + weapon semantic replacements from asset-style audit §5 (W+I scope).

| Asset | File | Source | License | Status |
|---|---|---|---|---|
| icon milk | `icons/milk.png` | rihlsul/milk-carton — https://game-icons.net/1x1/rihlsul/milk-carton.html | CC BY 3.0 (Rihlsul) | ok |
| icon coconut | `icons/coconut.png` | delapouite/coconuts (see Iter8p row above) | CC BY 3.0 | ok |
| pickaxe model | `models/weapons/pickaxe/Pickaxe.glb` | Quaternius Stone Pickaxe (see Iter9 row above) | CC0 | ok |
| fishing_rod model | `models/weapons/fishing_rod/FishingRod.glb` | Quaternius Fishing Rod (see Iter9 row above) | CC0 | ok |

CreditsUI now lists Gobkit (CC0 shark_king) and Rihlsul (milk icon) alongside existing attributions.

## Island vegetation (tutorial ridges)

All assets downloaded 2026-08-18. CC0 only. Extracted a small low-poly subset (not the full 330-piece kit) so the tutorial island stays cheap to draw.

| Asset | File | Source URL | License | Status |
|---|---|---|---|---|
| inland oak | `models/vegetation/tree_oak.glb` | Kenney Nature Kit 2.1 (GLTF) — https://kenney.nl/assets/nature-kit ; zip https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip ; also mirrored at https://opengameart.org/content/nature-kit | CC0 1.0 (Kenney, www.kenney.nl) | ok |
| ridge pine | `models/vegetation/tree_pineTallA.glb` | same Nature Kit | CC0 1.0 | ok |
| grass clump | `models/vegetation/grass.glb` + `grass_large.glb` | same Nature Kit | CC0 1.0 | ok |
| shrub | `models/vegetation/plant_bush.glb` + `plant_bushSmall.glb` | same Nature Kit | CC0 1.0 | ok |
| rock | `models/vegetation/rock_smallA.glb` + `rock_tallA.glb` | same Nature Kit | CC0 1.0 | ok |
| coconut palm | `models/vegetation/palm-detailed-straight.glb` + `Textures/colormap.png` | Kenney Pirate Kit 2.1 (GLB) — https://kenney.nl/assets/pirate-kit ; zip via OpenGameArt https://opengameart.org/sites/default/files/kenney_pirate-kit_2.1.zip | CC0 1.0 (Kenney) | ok |
| beach sand albedo | `textures/sand_01_diff_1k.jpg` | Poly Haven `sand_01` 1K JPEG — https://polyhaven.com/a/sand_01 ; file https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/sand_01/sand_01_diff_1k.jpg | CC0 1.0 (Poly Haven) | ok (texture on existing beach vertex splat; no extra sand mesh) |

Kit licenses copied beside the GLBs: `models/vegetation/License-Kenney-NatureKit.txt`, `License-Kenney-PirateKit.txt`. Credit "Kenney.nl" is optional under CC0; Poly Haven sand is CC0 (no attribution required). SurvivalIsland pack was **not** used.

Placement: `IslandVegetation.Analyze` reads slope + ridge direction from the heightmap Laplacian; trees/grass yaw along the contour. Applied on Spawn / Main / Tutorial; Storm (boss arena) is unchanged. Tutorial completeness (5 wood trees, 2 palms, plateau, one beach crab) is preserved.

## Exploration islands (post-story free sail)

All assets downloaded 2026-08-18. CC0 only. Wired by `IslandBuilder` on optional Wild / Atoll / Wreck islets (not on the quest chain; no `radio` / `ruin` / `shark_king` story points).

| Asset | File | Source URL | License | Used on |
|---|---|---|---|---|
| dark oak | `models/vegetation/tree_oak_dark.glb` | Kenney Nature Kit 2.1 (GLTF) — https://kenney.nl/assets/nature-kit ; zip https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip | CC0 1.0 (Kenney) | Wild extra trees |
| default pine | `models/vegetation/tree_pineDefaultA.glb` | same Nature Kit | CC0 1.0 | Wild extra trees |
| large rock | `models/vegetation/rock_largeA.glb` | same Nature Kit | CC0 1.0 | Wild rocks |
| driftwood log | `models/vegetation/log.glb` + `log_large.glb` | same Nature Kit | CC0 1.0 | Wreck shoreline |
| old stump | `models/vegetation/stump_old.glb` | same Nature Kit | CC0 1.0 | Wreck inland |
| bent palm | `models/vegetation/palm-detailed-bend.glb` | Kenney Pirate Kit 2.1 (GLB) — https://kenney.nl/assets/pirate-kit ; zip via OpenGameArt https://opengameart.org/sites/default/files/kenney_pirate-kit_2.1.zip | CC0 1.0 (Kenney) | Atoll palms |
| sand rocks | `models/vegetation/rocks-sand-a.glb` | same Pirate Kit | CC0 1.0 | Atoll beach rocks |
| barrel | `models/props/barrel.glb` + `Textures/colormap.png` | same Pirate Kit | CC0 1.0 | Wreck salvage |
| crate | `models/props/crate.glb` | same Pirate Kit | CC0 1.0 | Wreck salvage |
| chest | `models/props/chest.glb` | same Pirate Kit | CC0 1.0 | Wreck landmark |
| stylized wooden chest | `models/props/wooden_chest/WoodenChest.glb` | procedural / write_gltf.py (low-poly wood + iron bands) | original | tutorial island landmark (`scenes/world/wooden_chest.tscn` + `WoodenChest.cs`); Kenney `chest.glb` remains the wreck landmark |
| ship wreck | `models/props/ship-wreck.glb` | same Pirate Kit | CC0 1.0 | Wreck visible landmark |
| coast sand albedo | `textures/coast_sand_01_diff_1k.jpg` | Poly Haven `coast_sand_01` 1K JPEG — https://polyhaven.com/a/coast_sand_01 ; file https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/coast_sand_01/coast_sand_01_diff_1k.jpg | CC0 1.0 (Poly Haven) | Atoll terrain |
| rocks ground albedo | `textures/rocks_ground_01_diff_1k.jpg` | Poly Haven `rocks_ground_01` 1K JPEG — https://polyhaven.com/a/rocks_ground_01 ; file https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/rocks_ground_01/rocks_ground_01_diff_1k.jpg | CC0 1.0 (Poly Haven) | Wreck terrain |

Kit licenses: Nature Kit already at `models/vegetation/License-Kenney-NatureKit.txt`; Pirate Kit copied to `models/props/License-Kenney-PirateKit.txt`. SurvivalIsland pack was **not** used.

## Easter-egg islands (volcano / polar)

All assets downloaded 2026-08-18. CC0 only. Wired by `IslandBuilder` on optional Volcano / Polar islets (not on the quest chain; no `radio` / `ruin` / `shark_king`). Presence is 1 or 2 islands per world seed (`rng.Next(3)` after the exploration ring).

| Asset | File | Source URL | License | Used on |
|---|---|---|---|---|
| volcanic ground albedo | `textures/rock_ground_02_diff_1k.jpg` | Poly Haven `rock_ground_02` 1K JPEG — https://polyhaven.com/a/rock_ground_02 ; file https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/rock_ground_02/rock_ground_02_diff_1k.jpg | CC0 1.0 (Poly Haven) | Volcano terrain |
| snow albedo | `textures/snow_02_diff_1k.jpg` | Poly Haven `snow_02` 1K JPEG — https://polyhaven.com/a/snow_02 ; file https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/snow_02/snow_02_diff_1k.jpg | CC0 1.0 (Poly Haven) | Polar terrain |

Lava pools are emissive CSG cylinders (no extra mesh pack). Polar pines reuse Kenney Nature Kit `tree_pineTallA` / `tree_pineDefaultA` already on disk. Volcano scorched trees reuse `tree_oak_dark.glb`. SurvivalIsland pack was **not** used.


