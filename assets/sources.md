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
| shark_king (鲨鱼王) | `models/enemies/shark_king/Shark.glb` | Copy of the shark model above (Pirate Kit via Poly Pizza https://poly.pizza/m/AyHTK3zUSG); scale 2.0 applied in code | CC0 1.0 | ok (reuse of shark asset per plan) |
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
