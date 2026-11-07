# WORLD TREE: Game Design Document

> Status: **Draft v0.1, awaiting approval.** All numbers are starting values for tuning, not final balance.

## 1. Overview

| | |
|---|---|
| **Title** | WORLD TREE |
| **Genre** | Merge-2 puzzle with narrative progression, resource management, generators, orders and world restoration |
| **Platforms** | iOS and Android, portrait, phones first (tablets supported) |
| **Engine** | Unity 6 LTS, C# |
| **Audience** | Casual and mid-core players aged 25–55 who enjoy relaxing puzzles, collection, decoration and light story |
| **Session** | 3–10 minutes, several times a day, paced by energy and generator cooldowns |
| **Business model** | Free-to-play, with optional rewarded ads and IAP (Phase 8) |

**Pitch.** The ancient World Tree, whose roots hold the realm together, is withering under a creeping blight. You are the new Warden. You merge seeds, waters, crafts and charms into ever greater wonders, help the elves and creatures of the forest, and restore each corrupted grove to colour and life. With every grove you heal, the Tree recovers a lost memory, and the truth behind the blight gets closer.

### Design pillars

1. **Satisfying merges.** Every drag, snap and merge feels tactile and rewarding: animation, sound, haptics and particles.
2. **A world worth restoring.** Progress is visible. Grey, withered places burst back into vivid colour.
3. **A mystery that pulls you forward.** Short, charming story beats with a central question: *what is poisoning the Tree?*
4. **Respectful progression.** Generous, fair and fully completable without paying. Monetization saves time, never gates the story.
5. **Premium polish.** Consistent painterly art, clean readable UI, nothing that looks like generic placeholder.

## 2. Core loop

```
 ┌─────────────► Spend Sap (energy) ──► Tap generator ──► Items appear on board
 │                                                              │
 │                                                              ▼
 │  Restore area ◄── Lifebloom ◄── Fulfil orders ◄── Merge items into higher levels
 │       │                                                      ▲
 │       ▼                                                      │
 │  Story beat + new generators/chains/board space ─────────────┘
 │
 └── Sap regenerates over time · level-ups refill Sap · daily rewards
```

- **Micro loop (seconds):** drag → merge → feedback.
- **Session loop (minutes):** spend Sap, complete 2–5 orders, earn Lifebloom, complete a restoration task.
- **Meta loop (days):** restore a whole area, unlock the next area and its story chapter, new item chains and generators.

## 3. Merge system

### 3.1 Board

- **Grid:** 7 columns × 9 rows (63 cells) in portrait. Size comes from data, so other areas may vary.
- **Cell states:**
  - **Open:** empty or holding one item.
  - **Withered:** contains an item covered by blight. The item is visible but can't be moved. Merging any item **into an orthogonally adjacent cell** cleanses it, and the item becomes free.
  - **Sealed:** covered by thorny roots. The contents are hidden. It becomes *Withered* when an adjacent Withered cell is cleansed. This creates a gradual "unfurling" of the board.
- The starting board is authored per area (`BoardLayoutDefinition`): some open cells, a ring of withered and sealed cells, and pre-placed items.

### 3.2 Interaction rules

| Drag item A onto… | Result |
|---|---|
| Empty open cell | **Move** |
| Item B with the same item ID, level < max | **Merge**: A and B are consumed, the next level appears in B's cell |
| Item B with the same ID at max level | **Swap** (max-level items don't merge) |
| A different item B (movable) | **Swap** |
| A Withered or Sealed cell, or outside the board | **Rejected**: A snaps back |
| An order card in the UI | **Deliver**, if the order needs A (see §6) |

- **Tap** an item to select it and show its info panel (name, level, chain preview, sell value). **Tap a generator** to produce items (§4).
- Drag starts after an 8 px (DPI-scaled) movement threshold. Below that, the input counts as a tap.
- While dragging, every same-ID item on the board gets a soft highlight and valid merge targets pulse.

### 3.3 Item chains and discovery

- Each item belongs to a **chain** with 5–12 levels. Higher levels are worth more in orders and sell value.
- **Grove Codex** (collection book): every chain is shown as silhouettes until discovered. **First discovery** of each item grants a small Glimmer + XP reward and a "New!" moment.
- Some max-level items are **special**. Merging up to them grants a bonus chest, or they act as rare generators (for example, *Elder Oak* drops Lifebloom seeds once per day).

### 3.4 Board capacity and inventory

- **Satchel:** off-board storage. It starts with 4 slots and more are unlocked by player level and Glimmer. Drag an item onto the satchel button to store it, and tap a slot to return the item to the nearest empty cell.
- **Return to Roots** (sell): tap an item → Sell for Glimmer (scaled by level). The last sale can be undone for 5 seconds.
- **Board full:** generator taps are blocked *before* Sap is spent, with a gentle "Board is full" prompt pointing to merge or satchel.

### 3.5 Merge feedback (target feel)

- Snap: the dragged item eases into the target (≈90 ms), the two items squash together, then the result **pops** (scale 0 → 1.15 → 1.0, ≈220 ms) with a radial light burst and leaf/sparkle particles that match the chain.
- Pitch-rising chime per level within the chain. Light haptic on merge, medium haptic on max-level or first discovery.
- Cleansing a Withered cell plays a blight-dissolve shader effect and a soft bell.

## 4. Generators

Generators are board items that produce chain items when tapped.

| Generator | Produces | Unlock |
|---|---|---|
| **Seed Pouch** | Nature chain (seeds → great trees) | Start |
| **Moonwell Spring** | Water chain (dewdrop → moonwell fountain) | Start (Area 1) |
| **Woodwright's Stump** | Craft chain (twig → elven lantern) | Area 1, mid |
| **Hearthstone Oven** | Food chain (wild berry → festival feast) | Area 2 |
| **Glimmer Geode** | Crystal chain (shard → star crystal) | Area 4 |
| **Mystery Acorn** *(consumable)* | Random mix, expires after N taps | Rewards |

### 4.1 Rules

- **Tap cost:** 1 Sap per tap (some special generators are free).
- **Charges:** each generator has `charges` per cycle (for example 12). When they're empty it goes into **recharge** (for example 15 min at Lv1). Recharge can be skipped for Moonpetals, or sped up once by a rewarded ad.
- **Spawn placement:** the new item appears in the nearest empty open cell, found by breadth-first search from the generator (ties: up, right, down, left). It flies out of the generator with an arc animation.
- **Drop table:** a weighted list per generator level, mostly level-1 and level-2 items of its chain, with occasional higher levels and rare cross-chain or bonus drops.
- **Upgrade:** merging two generators of the same level gives the next level (max Lv5). Higher levels have more charges, shorter recharge and better drop tables. Generator pieces (*Generator Sprouts*) also drop from orders and chests, so players can upgrade without RNG frustration.
- **Consumable generators** (Mystery Acorn, Reward Chests) have a fixed number of taps, then vanish with a flourish.

### 4.2 Starting values

| Level | Charges | Recharge | L1 / L2 / L3 / L4 drop % |
|---|---|---|---|
| 1 | 12 | 15 min | 80 / 18 / 2 / 0 |
| 2 | 16 | 12 min | 70 / 25 / 5 / 0 |
| 3 | 20 | 10 min | 60 / 30 / 9 / 1 |
| 4 | 24 | 8 min | 50 / 35 / 13 / 2 |
| 5 | 30 | 6 min | 40 / 38 / 18 / 4 |

## 5. Energy (Sap)

- **Sap** is the energy currency. **Cap 100.** It regenerates **+1 every 2 minutes** (full in about 3h20m) while below the cap. Regeneration continues offline and is calculated from UTC timestamps.
- Rewards (level-up, daily gift, chests, ads, purchases) can push Sap **above the cap**. The cap only limits regeneration.
- **Level-up** fully refills Sap (to at least the cap).
- **Daily Gift:** a 7-day calendar (Sap, Glimmer, generator sprouts, and a Moonpetals bundle on day 7). Missing a day doesn't reset progress, it just pauses the calendar.
- **Rewarded ad** *(optional, Phase 8)*: +25 Sap, max 5 per day. It is never required and never shown automatically.
- **Refill with Moonpetals:** +100 Sap. The cost rises each time within a day (10 → 20 → 40 …) and resets at local midnight.
- **Clock safety:** negative time deltas are ignored, and regeneration can never exceed what the elapsed time allows. The cap prevents abuse from clock skipping forward.
- **Notification** *(Phase 7)*: optional local push notification when Sap is full or a generator has recharged.

## 6. Orders and quests

### 6.1 Customer orders

- **Order slots:** 3 at the start (4 at player level 8). Each order comes from a character of the current area and requests **1–3 items**.
- When the requested items exist on the board, the order card shows a ✓ and those items get a subtle glow. Tapping **Deliver** removes them, or the player can drag items onto the card.
- **Rewards:** Glimmer + XP + **Lifebloom** (restoration currency), and sometimes a chest or generator sprout.
- Orders never expire. A non-story order can be **swapped out** once every 30 minutes for free.

### 6.2 Order generation

- **Story orders** are authored in `AreaDefinition` and always take precedence while their quest is active.
- **Procedural orders** are drawn from chains the player has *unlocked and can currently generate*. The requested level targets the player's "comfort level" for that chain (based on the highest level they've reached), with occasional stretch requests.
- **Feasibility guarantee:** an order must never request an item from a chain whose generator the player doesn't have.
- The reward value scales with the total "merge cost" of the requested items (level-1 equivalents), so rewards feel fair.

### 6.3 Quests

- Each area has a **quest line**: a sequence of restoration tasks (for example, *Clear the withered spring*, *Rebuild the lantern bridge*). Each task costs Lifebloom and sometimes needs a specific story delivery.
- **Side goals:** "Reach Lv6 in the Water chain", "Discover 5 new items", and so on, with small rewards. They also act as soft tutorials.

## 7. Progression and world restoration

### 7.1 Player level

- XP comes from orders, first discoveries and restoration tasks. Levelling up gives: a full Sap refill, Glimmer, and possibly new satchel slots, order slots or generator unlocks.

### 7.2 Areas (launch scope: 5)

| # | Area | Theme | New content | Story beat |
|---|---|---|---|---|
| 1 | **Sunlit Glade** | Meadow at the Tree's base, mild blight | Seed Pouch, Moonwell Spring, Woodwright's Stump | Tutorial. Wren meets Elder Thalen and learns the Tree is fading |
| 2 | **Emberroot Village** | Elven treetop village, hearths gone cold | Hearthstone Oven, food chain | The villagers' memories are fading too. The first Memory Leaf |
| 3 | **Moonmere** | Lake of reflections under perpetual dusk | Upgraded water chain, Moonmere spirits | Reflections show the Tree as it once was |
| 4 | **Crystal Roots** | Glowing caverns beneath the Tree | Glimmer Geode, crystal chain | The blight's source runs deep, and something is *sealed* below |
| 5 | **The Heartwood** | Inside the Tree's living core | Heartwood charms | Revelation (see §8) and the season-one finale |

- Each area has **8–15 restoration tasks**. Each completed task changes the area art: a **corrupted layer dissolves** and a restored layer fades in (fountain flowing, lanterns lit, creatures returning).
- Area completion: a short celebration, the chapter-end story scene, and the next area unlocks. Completed areas stay visitable, and their generators stay available.

## 8. Story

### 8.1 Premise

For ten thousand years the **World Tree** has bound the realm of **Ylmera**. Now its leaves fall grey, and a creeping **Witherblight** spreads from its roots, draining colour, warmth and memory from everything it touches. The elder wardens have vanished. Only an apprentice remains.

### 8.2 Characters

| Character | Role |
|---|---|
| **Wren Ashleaf** | Young elf apprentice warden. The player's companion and voice: earnest, curious, a little clumsy |
| **Elder Thalen** | Ancient keeper of the Tree. A slow, kind and secretive stag-antlered dryad |
| **Pip** | A glowmoth sprite who guides tutorials and comments on discoveries. Comic relief |
| **Marra Hollowbrook** | Burrowkin (badger-folk) merchant. Practical and funny. Gives many orders |
| **Sorrel & Fen** | Twin fox-sprites who cause mischief and sometimes leave reward chests |
| **The Pale Gardener** | A mysterious hooded figure glimpsed tending the blight. Antagonist, or so it seems |

### 8.3 The mystery (season-one arc)

Each restored area returns a **Memory Leaf**: a short vignette of the Tree's past. Together they reveal that the Witherblight isn't an outside poison. It is the Tree's own **sealed grief** for a lost seedling-sibling that withered long ago. The Pale Gardener turns out to be the last vanished warden, trying to keep the grief contained, and failing. The finale reframes restoration from *fighting* the blight to *healing* it, and sets up season two: a seed of the lost sibling, still alive somewhere beyond the realm.

### 8.4 Delivery

- Short dialogue scenes (2–6 lines) with character portraits and expressions, shown at quest start and completion. Always skippable. Replayable from the Codex.
- Area-completion vignettes: illustrated panels with light parallax animation.
- Ambient barks on order cards ("The kettle's been cold for weeks!").

## 9. Economy

| Currency | Earned from | Spent on |
|---|---|---|
| **Sap** (energy) | Regeneration, level-ups, daily gift, chests, ads, IAP | Generator taps |
| **Glimmer** (soft) | Orders, selling, discoveries, chests | Satchel slots, generator sprouts, shop items |
| **Lifebloom** (progress) | Orders, story deliveries | Restoration tasks |
| **Moonpetals** (premium) | Small amounts in play (level-ups, Daily Gift day 7, achievements), IAP | Skip recharge, Sap refills, special bundles |
| **XP** | Orders, discoveries, tasks | Player level (not spendable) |

Balance target: a non-paying player completes **Area 1 in about 2–3 days** and **Area 5 in about 6–8 weeks** of regular play. To be validated by a simulation tool in Phase 4.

## 10. Visual and UX direction

- **Art style:** hand-painted, soft-lit fantasy with saturated jewel tones (emerald, gold, twilight violet, warm amber). Rounded, friendly shapes and expressive characters with a slight chibi proportion.
- **Corruption vs restoration:** corrupted areas are desaturated grey-violet with drifting ash motes. Restored areas are vivid, with sun shafts, fireflies and gentle motion. Restoration *is* the reward.
- **Item icons:** 256×256 master, a strong silhouette readable at 64 px, a consistent light source (top-left) and outline weight, and a chain-specific colour accent. Level shown through size, richness and glow, never by colour alone.
- **UI language:** carved living-wood panels, gold-leaf filigree accents, leaf-shaped buttons, a parchment info panel, and a rounded humanist font. The primary action colour is a consistent emerald. Rewards fly to their HUD counters.
- **Motion:** everything eases (no linear tweens). Idle items have a subtle breathing scale, generators pulse when ready, and the HUD counters roll.
- **Audio:** an acoustic fantasy score (harp, flute, soft strings) that changes per area. Sound effects use organic material: wood, water, glass chimes.
- **Accessibility:** colour-blind-safe design (shape + level pips), toggles for haptics, music and SFX, a reduced-motion option, scalable text for dialogue, and left-hand UI mirroring as a stretch goal.

## 11. Monetization (Phase 8)

- **IAP:** starter pack (one-time), Moonpetal bundles, Sap bundles, an optional "Warden's Pass" season track with free and premium tiers, and themed generator bundles.
- **Rewarded ads:** opt-in only, for Sap, recharge speed-up, or doubling a chest. Daily cap.
- **No:** interstitials during board play, energy walls that force a purchase in the story, paid random rewards without published odds, or fake countdown urgency.

## 12. Out of scope for launch (live-ops backlog)

Timed events with their own boards, a decoration and customisation mode, social features (gifting, friend helps), additional seasons.
