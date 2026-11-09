# WORLD TREE: Mobile Layout Specification

> Status: **Implemented in Phase 6** (`BoardLayoutSolver` in Core with tests for every device in §4; `LayoutController` in Game). Units are points (pt) on iOS and density-independent pixels (dp) on Android. They're treated as equal here.

## 1. Goals

- The **7 × 9 board** is always fully visible, with **cells ≥ 44 pt** (Apple HIG minimum touch target) on every supported phone.
- Portrait only, one-handed play: the board sits low, in the thumb zone. Low-frequency UI goes at the top.
- Nothing interactive sits inside system insets (notch, Dynamic Island, home indicator, Android navigation bar).
- Larger screens get a more comfortable board and an optional scene banner. They never get stretched UI.

## 2. Screen regions (top → bottom)

| # | Region | Content | Height: Regular / Compact / Minimum |
|---|---|---|---|
| — | Top safe inset | System | from `Screen.safeArea` |
| 1 | **HUD bar** | Player level/XP ring · Sap + next-tick timer · Glimmer · Moonpetals (+) · settings | 56 / 48 / 44 |
| 2 | **Scene banner** *(optional)* | Area art and character portrait, story prompt | Leftover space, shown only if ≥ 60. Otherwise the leftover space is split evenly above and below the orders strip |
| 3 | **Orders strip** | 3 order cards visible. A 4th slot scrolls horizontally with a 24 pt peek | 104 / 76 / 64 |
| 4 | **Board** | 7 × 9 cells, 8 pt side margin | `9 × cell` |
| 5 | **Context bar** | **Navigation** (World · Codex · Satchel · Shop) when nothing is selected. **Item info + Sell/Store** when an item is selected. One slot, so the board never shrinks for the info panel | 60 / 52 / 48 |
| — | Bottom safe inset | System | from `Screen.safeArea` |

Chrome height (regions 1 + 3 + 5): **Regular 220 · Compact 176 · Minimum 156**.

## 3. Cell size algorithm

```
widthCell  = (safeWidth − 2 × 8) / 7
heightCell = (safeHeight − chrome(tier)) / 9
cell       = clamp(min(widthCell, heightCell), 44, 80)

tier = Regular  if Regular gives cell ≥ 48
       Compact  else if Compact gives cell ≥ 46
       Minimum  otherwise (cell ≥ 44 still guaranteed by the reference set below)
```

- The board is centred horizontally and **anchored to the top of the context bar**. Leftover vertical space goes to the scene banner (or padding).
- At tablet sizes the cell is capped at **80 pt**, so items stay crisp at the 256 px icon resolution and the board doesn't feel sparse. The orders strip and context bar are limited to the board width + 40 pt and centred.
- This algorithm is pure math. It will live as `BoardLayoutSolver` in `WorldTree.Core.Layout` (engine-free) and be unit-tested against every device in §4. Presentation (`LayoutController`) only measures the safe area and applies the result: it positions the uGUI regions and sets the board camera's orthographic size and position so the world-space board fits exactly into the board rect.

## 4. Reference device validation (computed)

| Device (portrait) | Screen | Safe insets top/bottom | Safe height | Tier | Cell | Board (w × h) | Leftover |
|---|---|---|---|---|---|---|---|
| iPhone SE (2nd/3rd gen) | 375 × 667 | 20 / 0 | 647 | Compact | **51.3** (width) | 359 × 462 | 9 |
| Android 16:9 with 3-button nav bar *(worst case)* | 360 × 640 | 24 / 48 | 568 | Minimum | **45.8** (height) | 320 × 412 | 0 |
| Android 20:9 (for example a mid-range A-series) | 360 × 800 | 24 / 16 | 760 | Regular | **49.1** (width) | 344 × 442 | 98 → banner |
| iPhone 13 mini | 375 × 812 | 50 / 34 | 728 | Regular | **51.3** (width) | 359 × 462 | 46 |
| iPhone 15 / 16 | 393 × 852 | 59 / 34 | 759 | Regular | **53.9** (width) | 377 × 485 | 54 |
| iPhone 15/16 Pro Max | 430 × 932 | 59 / 34 | 839 | Regular | **59.1** (width) | 414 × 532 | 87 → banner |
| iPad (10th gen) | 820 × 1180 | 24 / 20 | 1136 | Regular | **80** (cap) | 560 × 720 | 196 → banner |

Every reference device meets the 44 pt minimum. The worst case is a 16:9 Android with a visible 3-button navigation bar: it falls back to the Minimum tier at 45.8 pt. Devices narrower than 360 pt (for example the original 320 pt iPhone SE) are **best effort and not supported**. They would reach about 43 pt cells.

## 5. Touch and drag ergonomics

- **Hit area = the full cell** (no gaps). The item art fills about 88% of the cell, with a 2–3 pt visual gutter.
- **Finger occlusion:** on touch input, the dragged item renders lifted by **0.35 × cell** above the touch point and scaled ×1.15. The **drop target is the cell under the finger**, and it's highlighted with a clear outline, so the player always sees where the item will land. Mouse input has no lift.
- Tap vs drag threshold: 8 pt of movement (DPI-scaled).
- Delivering to an order is a drag *upward* onto a card. Cards enlarge their drop zone by 12 pt while a matching item is being dragged.
- The bottom board row sits above the context bar, never next to the iOS home indicator or Android gesture area. This avoids accidental system gestures.
- Minimum text: 12 pt labels, 14 pt body, 16 pt+ for HUD numbers. Numbers use tabular figures so counters don't jitter.

## 6. Measuring physical size

`Screen.safeArea` is in pixels. Pixels are converted to points with `Screen.dpi / 160`. If `Screen.dpi` is 0 or implausible (known to be unreliable on some Android devices), the fallback assumes the screen's shorter side is 360 pt. This is a **⚠ remaining uncertainty** to verify on real devices in Phase 7.

## 7. Validation checklist (⚠ Requires Unity Editor validation)

Use **Window → General → Device Simulator** with these devices: iPhone SE (3rd gen), iPhone 13 mini, iPhone 15, iPhone 15 Pro Max, a 20:9 Samsung Galaxy, iPad (10th gen), plus a custom 1080 × 1920 Android profile with a 48 dp bottom inset (the worst case).

- [ ] The whole 7 × 9 board is visible, and cell size matches the table in §4 (±1 pt)
- [ ] No interactive UI overlaps the notch, Dynamic Island, home indicator or navigation bar
- [ ] The orders strip shows 3 cards, and the 4th slot peeks and scrolls
- [ ] Selecting an item swaps the context bar to item info without moving the board
- [ ] Items in the corner cells (top-left, bottom-right) can be dragged and dropped comfortably
- [ ] Dragging onto an order card works from the bottom board row
- [ ] The scene banner appears only where §4 predicts it
- [ ] The orientation is locked to portrait (Player Settings → Resolution and Presentation)
- [ ] Physical device spot check: one small Android and one iPhone SE-class device (Phase 7)
