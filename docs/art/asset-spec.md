# WORLD TREE: Art and Audio Asset Spec

> For artists and sound designers. Everything in the game currently uses **generated stand-ins** (roadmap D42). Real assets drop in without code changes: assign them in the data assets or the Inspector, and the stand-in is no longer used. Check what is still a stand-in with **WorldTree → Art → Report Placeholders**.

## 1. Style (GDD §10)

Hand-painted, soft-lit fantasy with saturated jewel tones (emerald, gold, twilight violet, warm amber). Rounded, friendly shapes. Light from the **top-left**, consistent outline weight. Blighted areas are desaturated grey-violet; restored areas are vivid. All art must be original or licensed for commercial use, and every non-original asset is listed in `Assets/WorldTree/Art/CREDITS.md`.

## 2. Item and generator icons

| Spec | Value |
|---|---|
| Size | 256 × 256 PNG, transparent background, centred, about 8% padding |
| Readability | Strong silhouette that reads at 64 px |
| Levels | Level shown by size, richness and glow, never by colour alone (colour-blind safe) |
| Folder | `Assets/WorldTree/Art/Items/<Chain>/` and `Assets/WorldTree/Art/Generators/` |
| Naming | `<chain>_<LL>_<name>.png`, for example `nature_03_sapling.png`, `gen_seed_pouch_2.png` |
| Import | Texture Type **Sprite (2D and UI)**, Sprite Mode Single, Mesh Type Full Rect, mipmaps off, Max Size 256 |
| Hook-up | Open `Assets/WorldTree/Data/Chains/Chain_<Name>.asset` and drag the sprite into **Icon** for that level |

Needed now (IDs in `PrototypeContent` and the chain assets):

| Chain | Levels | Items |
|---|---|---|
| Nature | 8 | Glimmerseed, Sprout, Sapling, Blossom Shrub, Young Oak, Elder Oak, Silverbark Tree, Great Elderwood |
| Water | 8 | Dewdrop, Water Flask, Moon Pitcher, Spring Basin, Crystal Fount, Moonwell, Starlit Pool, Moonwell Fountain |
| Craft | 6 | Twig, Twig Bundle, Carved Plank, Wooden Crate, Lantern Frame, Elven Lantern |
| Food | 6 | Wild Berry, Berry Bowl, Berry Tart, Honey Cake, Festival Pie, Festival Feast |
| Generators | 5 each | Seed Pouch, Moonwell Spring, Woodwright's Stump, Hearthstone Oven (levels 1–5: same object, grander per level) |
| Consumable | 1 | Mystery Acorn |

## 3. UI

| Element | Spec |
|---|---|
| Panels and buttons | 9-slice PNGs authored at 2× (for example 128 × 128 with 40 px borders): carved living wood with gold-leaf accents; primary buttons emerald |
| Font | A rounded humanist typeface licensed for app embedding (OFL preferred), with its licence in `Assets/WorldTree/Fonts/`. It must include ✓, ▶ and — if we want those symbols back (the current default font lacks them) |
| Icons | Sap, Glimmer, Moonpetals, Lifebloom, XP: 128 × 128 PNG |

## 4. Areas

One layered scene per area: a **blighted** layer and a **restored** layer for each of its 10 restoration tasks, so each completed task can cross-fade. Portrait, 1080 × 1350 at least. Areas: Sunlit Glade, Emberroot Village (then Moonmere, Crystal Roots, The Heartwood).

## 5. Characters

1024 × 1024 PNG portraits with expression variants (neutral, happy, worried, surprised): Wren, Elder Thalen, Pip, Marra Hollowbrook, Sorrel, Fen, and the Pale Gardener (later).

## 6. Audio

| Sound | Spec | Hook-up |
|---|---|---|
| Music | One seamless loop per area, OGG Vorbis, −14 LUFS, acoustic fantasy (harp, flute, soft strings) | `AudioService` → **Area Music** list, in area order |
| SFX | WAV, short, organic (wood, water, glass chimes): merge chime (the game pitches it up per level), spawn pop, cleanse bell, reward, level-up, reject, button | Replace the generated clips in `AudioService` (slots to be added with the first real SFX) |
