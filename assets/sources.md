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
