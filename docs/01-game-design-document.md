# WORLD TREE: Game Design Document

> Status: **Draft v0.2 (design review 1 applied), awaiting approval.** All numbers are starting values for tuning, not final balance.

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
- **Coordinates:** `(column, row)` with `(0, 0)` at the **top-left**.
- **Cell states:**
  - **Open:** empty or holding one item. Only Open items can be moved, tapped, sold, stored or delivered.
  - **Withered:** always holds exactly one item, covered by blight. The item is **visible** but **locked**.
  - **Sealed:** always holds exactly one item, covered by thorny roots. The item is **hidden** and **locked**.
- Withered and Sealed cells are never empty, never receive spawned items, and their items don't count toward orders or "board full" checks.
- The starting board is authored per area (`BoardLayoutDefinition`): some Open cells, and rings of Withered and Sealed cells with pre-placed items.

### 3.2 Interaction rules

A is always an **Open** item being dragged. "Same ID" means the same item (and therefore the same chain and level).

| Drag item A onto… | Result |
|---|---|
| Its own cell, or released within the tap threshold | **No-op** (counts as a tap) |
| Empty Open cell | **Move** |
| Open item B, same ID, level < max | **Merge**: A and B are consumed, the next level appears in **B's cell**, A's cell becomes empty |
| Open item B, same ID, max level | **Swap** (max-level items don't merge) |
| Open item B, different ID | **Swap** |
| **Withered** item B, same ID, level < max | **Cleansing merge**: A and B are consumed, the next level appears in B's cell, and B's cell becomes **Open** |
| Withered item B, different ID or max level | **Rejected**: A snaps back |
| A Sealed cell, or outside the board | **Rejected**: A snaps back |
| An order card in the UI | **Deliver**, if the order needs A (see §6) |

Generators follow the same table. Their merge specifics are in §4.3.

### 3.3 Cleansing and revealing (exact rules)

1. **Only merges cleanse.** Every successful merge (normal, cleansing, or generator merge) has a **result cell R**: the cell the new item appears in.
2. **Cleanse step:** the set `C` = R itself if it was Withered, plus every **Withered** cell orthogonally adjacent (up/down/left/right, not diagonal) to R. Every cell in `C` becomes **Open**, and its item is now free.
3. **Reveal step:** every **Sealed** cell orthogonally adjacent to *any* cell in `C` becomes **Withered** (its item is now visible, still locked).
4. **One step per merge, no cascade.** `C` is computed from the board state **before** the merge resolves. A cell revealed in step 3 is never cleansed by the same merge, even if it is adjacent to R. Reveals never trigger further reveals.
5. **What never cleanses:** moves, swaps, generator spawns, deliveries, selling, satchel store/retrieve, rejected drops.
6. The order of resolution is fixed: consume A and B → place the result in R → cleanse step → reveal step → events fire (`ItemMerged`, then `CellStateChanged` for each cell, in row-major order). This makes animation order and tests deterministic.
7. **Tapping a locked item** shows a hint ("Merge beside it to break the blight") and does nothing else. A locked **generator** can't be activated until its cell is Open.

**Example:** `W` = Withered, `S` = Sealed, `B` = the Open item that A is dropped on, `*` = the merge result cell.

```
Before          After merging A onto B
 S  S  S         S  W  S        (the S above the cleansed W is revealed)
 .  W  S         .  .  W        (both W next to * are cleansed; the S next to each becomes W)
 .  B  W         .  *  .
```

**Authoring rules (enforced by the data validator):** every Withered and Sealed cell holds an item; at least 6 Open cells exist at the start; every Sealed cell connects to a Withered cell through an orthogonal path of locked cells; every Withered cell has at least one orthogonal neighbour that is Open or can become Open. Together these guarantee that the whole board can always be unlocked.

- **Tap** an item to select it and show its info panel (name, level, chain preview, sell value). **Tap a generator** to produce items (§4).
- Drag starts after an 8 px (DPI-scaled) movement threshold. Below that, the input counts as a tap.
- While dragging, every same-ID item on the board gets a soft highlight and valid merge targets pulse.

### 3.4 Item chains and discovery

- Each item belongs to a **chain** with 5–12 levels. Higher levels are worth more in orders and sell value.
- **Grove Codex** (collection book): every chain is shown as silhouettes until discovered. **First discovery** of each item grants a small Glimmer + XP reward and a "New!" moment.
- Some max-level items are **special**. Merging up to them grants a bonus chest, or they act as rare generators (for example, *Elder Oak* drops Lifebloom seeds once per day).

### 3.5 Board capacity and inventory

- **Satchel:** off-board storage. It starts with 4 slots and more are unlocked by player level and Glimmer. Drag an item onto the satchel button to store it, and tap a slot to return the item to the nearest empty cell.
- **Return to Roots** (sell): tap an item → Sell for Glimmer (scaled by level). The last sale can be undone for 5 seconds.
- **Return to Roots** applies only to normal items and consumable generators. Permanent generators can't be sold, which prevents losing one by accident.
- **Board full** means no empty **Open** cell. Generator taps are blocked *before* Sap is spent, with a gentle "Board is full" prompt pointing to merge or satchel.

### 3.6 Merge feedback (target feel)

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
- **Charges and recharge (cycle model):** each permanent generator holds up to `capacity` charges for its level. A tap uses 1 charge. When charges reach **0**, recharge starts, and when it finishes the generator returns to **full capacity at once**. A generator with charges left never recharges partially. Recharge can be skipped for Moonpetals, or the remaining time halved once per cycle by a rewarded ad.
- **Timers are absolute:** a recharging generator stores `rechargeEndsUtc`. State is evaluated lazily from the clock, so recharge continues while the generator is in the satchel, while the app is closed, or after a save rollback. If the device clock moves backwards so that the remaining time exceeds the full recharge duration, the remaining time is clamped to the full duration (recharge is never faster, and never stuck).
- **Tap order of checks:** generator is Open → generator has a charge → an empty Open cell exists → Sap is available. If any check fails, **nothing** changes (no Sap or charge is spent).
- **Spawn placement:** the new item appears in the **nearest empty Open cell** by Manhattan distance from the generator. Ties go to the smaller row (higher on screen), then the smaller column (further left). It flies out of the generator with an arc animation.
- **Drop table:** a weighted list per generator level, mostly level-1 and level-2 items of its chain, with occasional higher levels and rare cross-chain or bonus drops.
- **Upgrade:** merging two generators of the same type and level gives the next level (max Lv5); see §4.3. Higher levels have more charges, shorter recharge and, most importantly, better drop tables. **Generator Sprouts** (a Lv1 generator item of a given type) drop from orders and chests, so players can upgrade without RNG frustration. Reaching Lv*N* takes 2^(N−1) sprouts.
- **Consumable generators** (Mystery Acorn, Reward Chests) have a fixed number of taps and **no recharge**, then vanish with a flourish. They never merge.

### 4.2 Starting values (revised in design review 1)

| Level | Capacity | Recharge | L1 / L2 / L3 / L4 drop % | L1-equivalents per tap |
|---|---|---|---|---|
| 1 | 40 | 20 min | 80 / 18 / 2 / 0 | 1.24 |
| 2 | 48 | 18 min | 70 / 25 / 5 / 0 | 1.40 |
| 3 | 56 | 16 min | 60 / 30 / 9 / 1 | 1.64 |
| 4 | 64 | 14 min | 50 / 35 / 13 / 2 | 1.88 |
| 5 | 80 | 12 min | 40 / 38 / 18 / 4 | 2.20 |

*L1-equivalents per tap* = the expected value of a tap, counting a level-*k* item as 2^(k−1) level-1 items. This is the generator's real Sap efficiency, and the main reason to upgrade.

**Why the values changed (v0.1 had 12 charges / 15 min at Lv1):** with v0.1 values the two starting generators held only 24 charges, so a 100-Sap session stalled after about 2 minutes. Sessions would have been limited by cooldowns, and Sap would have been mostly irrelevant. The revised values make **Sap the session limiter** (two Lv1 generators hold 80 charges, three hold 120). **Cooldowns** then only bite when a player taps a single chain heavily, which is their real design job: pushing variety across chains and orders. Between normal sessions (3+ hours apart) every generator is fully recharged. See §5.1.

### 4.3 Generator merging (exact rules)

| Situation | Result |
|---|---|
| Same type, same level L < 5, both permanent | **Merge** into Lv(L+1) in the target cell, at **full capacity** of the new level, not recharging |
| … one depleted and recharging, the other full | Full Lv(L+1). The pending recharge is discarded |
| … both depleted | Full Lv(L+1) |
| … both partly used | Full Lv(L+1). Leftover charges aren't carried over (the new level's capacity always exceeds either input) |
| Same type, both Lv5 | **Swap** (max level) |
| Different types, any level | **Swap** |
| A generator and a normal item | **Swap** |
| A consumable generator and anything | **Swap** (consumables never merge) |
| Target is a **Withered** generator of the same type and level | **Cleansing merge**: full Lv(L+1), the cell becomes Open (§3.3) |
| Target is a Withered generator that doesn't qualify, or a Sealed generator | **Rejected** |

- Generator merges **cleanse and reveal** like any merge (§3.3), unlock a Codex entry for a new generator level, and play the "upgrade" celebration.
- A pending ad speed-up or skip belongs to the consumed instance and is discarded.
- **Generator state belongs to the item instance, not the cell.** Moving, swapping or storing a generator in the satchel keeps its charges and `rechargeEndsUtc`.
- **Why the result is always full:** an upgrade is a reward moment, and a single rule is easy to understand and show in the UI. Carrying over or summing charges was considered and rejected: it's hard to explain, and the leftover charges are always worth less than the new level's capacity. **Exploit check:** merging two depleted generators skips at most one recharge cycle, and it costs a generator that took 2^(L−1) sprouts to build. Sprout drop rates are tuned in the Phase 4 balance simulator so that this never becomes a routine way to skip cooldowns.

## 5. Energy (Sap)

- **Sap** is the energy currency. **Cap 100.** It regenerates **+1 every 2 minutes** (full in about 3h20m) while below the cap. Regeneration continues offline and is calculated from UTC timestamps.
- Rewards (level-up, daily gift, chests, ads, purchases) can push Sap **above the cap**. The cap only limits regeneration.
- **Level-up** fully refills Sap (to at least the cap).
- **Daily Gift:** a 7-day calendar (Sap, Glimmer, generator sprouts, and a Moonpetals bundle on day 7). Missing a day doesn't reset progress, it just pauses the calendar.
- **Rewarded ad** *(optional, Phase 8)*: +25 Sap, max 5 per day. It is never required and never shown automatically.
- **Refill with Moonpetals:** +100 Sap. The cost rises each time within a day (10 → 20 → 40 …) and resets at local midnight.
- **Clock safety:** negative time deltas are ignored, and regeneration can never exceed what the elapsed time allows. The cap prevents abuse from clock skipping forward.
- **Notification** *(Phase 7)*: optional local push notification when Sap is full or a generator has recharged.

### 5.1 Energy budget analysis (design review 1)

**Decision: keep cap 100 and +1 per 2 min.** The reasoning is below. Generator values were changed instead (§4.2).

**Raw numbers:** 30 Sap per hour, full from empty in **3 h 20 min**, a theoretical maximum of **720 per day** (only reachable by checking in every 3 h 20 min around the clock).

**Daily Sap from regeneration only** (spending to 0 every session, 8-minute sessions):

| Player profile | Sessions | Sap per day |
|---|---|---|
| Light | 2 (morning, evening) | ~210 |
| **Core (design target)** | **4** (08:00, 12:30, 17:00, 21:30) | **~415** |
| Engaged | 6 (every 2.5 h while awake) | ~480 |
| Theoretical maximum | every 3 h 20 min, 24/7 | ~700 |

Other sources (level-up refills, Daily Gift, chests) add an estimated +10–20% on a typical day, and much more on days 1–3 when levels come fast.

**Session length check:** each Sap is about 1 tap plus about 1 merge drag (plus occasional deliveries), at roughly 1.5–2.5 s per action. That's **3–5 s per Sap**, so **one full cap of 100 Sap ≈ 5–8 minutes**. This is inside the 3–10 minute session target, and a core player gets about 4 × 6.5 ≈ 26 minutes of play per day.

**Fit with player habits:** a 3 h 20 min refill matches the intended 3–5 check-ins per day. Overnight regeneration is lost after the cap is reached. That's deliberate: it makes a "morning session" feel full without rewarding all-night play.

**Progression check (core player ≈ 450 Sap per day including bonuses):**

| Area | Sap budget | Cumulative | Days (core player) |
|---|---|---|---|
| 1 Sunlit Glade | 1,000 | 1,000 | ~2 (day-1 level-up refills make day 1 larger) |
| 2 Emberroot Village | 2,500 | 3,500 | ~8 |
| 3 Moonmere | 4,500 | 8,000 | ~18 |
| 4 Crystal Roots | 6,000 | 14,000 | ~31 |
| 5 The Heartwood | 8,000 | 22,000 | ~49 (7 weeks) |

This meets the §9 targets (Area 1 in 2–3 days, Area 5 in 6–8 weeks). Order sizes, Lifebloom rewards and task costs will be **derived from these budgets** (for example, an order's Sap cost ≈ the L1-equivalents of its items ÷ the generator's L1-equivalents per tap).

**Why not change the cap or the rate:**
- Raising the cap (for example to 150) would lengthen sessions to 8–12 min and reduce the pull to return. It would be the right fix only if playtests show sessions ending too early.
- Faster regeneration (for example 1 per 90 s) adds about +33% daily Sap for core players, which would shorten the content runway by about 2 weeks.

**Remaining uncertainty:** the "3–5 s per Sap" estimate needs real measurement. The Phase 1 prototype includes a debug stats overlay (Sap spent, taps and merges per minute) to measure it. The Phase 4 simulator then validates the area budgets. If they're off, order rewards and task costs are tuned first, and cap and rate are only changed as a last resort.

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

Balance target: a non-paying player completes **Area 1 in about 2–3 days** and **Area 5 in about 6–8 weeks** of regular play. The per-area Sap budgets that meet this are in §5.1. They'll be validated by a simulation tool in Phase 4.

## 10. Visual and UX direction

- **Art style:** hand-painted, soft-lit fantasy with saturated jewel tones (emerald, gold, twilight violet, warm amber). Rounded, friendly shapes and expressive characters with a slight chibi proportion.
- **Corruption vs restoration:** corrupted areas are desaturated grey-violet with drifting ash motes. Restored areas are vivid, with sun shafts, fireflies and gentle motion. Restoration *is* the reward.
- **Item icons:** 256×256 master, a strong silhouette readable at 64 px, a consistent light source (top-left) and outline weight, and a chain-specific colour accent. Level shown through size, richness and glow, never by colour alone.
- **UI language:** carved living-wood panels, gold-leaf filigree accents, leaf-shaped buttons, a parchment info panel, and a rounded humanist font. The primary action colour is a consistent emerald. Rewards fly to their HUD counters.
- **Motion:** everything eases (no linear tweens). Idle items have a subtle breathing scale, generators pulse when ready, and the HUD counters roll.
- **Audio:** an acoustic fantasy score (harp, flute, soft strings) that changes per area. Sound effects use organic material: wood, water, glass chimes.
- **Screen layout:** portrait regions, cell sizing and device validation are specified in [`06-mobile-layout.md`](06-mobile-layout.md). The 7 × 9 board keeps cells ≥ 44 pt on every supported phone.
- **Accessibility:** colour-blind-safe design (shape + level pips), toggles for haptics, music and SFX, a reduced-motion option, scalable text for dialogue, and left-hand UI mirroring as a stretch goal.

## 11. Monetization (Phase 8)

- **IAP:** starter pack (one-time), Moonpetal bundles, Sap bundles, an optional "Warden's Pass" season track with free and premium tiers, and themed generator bundles.
- **Rewarded ads:** opt-in only, for Sap, recharge speed-up, or doubling a chest. Daily cap.
- **No:** interstitials during board play, energy walls that force a purchase in the story, paid random rewards without published odds, or fake countdown urgency.

## 12. Out of scope for launch (live-ops backlog)

Timed events with their own boards, a decoration and customisation mode, social features (gifting, friend helps), additional seasons.
