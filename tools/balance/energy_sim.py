#!/usr/bin/env python3
"""WORLD TREE - deterministic 7-day Sap (energy) and generator simulation.

Design-time balance tool (no game dependency, no randomness). It reproduces the
energy numbers in docs/01-game-design-document.md section 5.1 from explicit
session schedules, and checks whether sessions are limited by Sap or by
generator charges under several player tap strategies.

Usage:
    python3 tools/balance/energy_sim.py > docs/balance/energy-simulation.md

The Phase 4 balance simulator (C#, built on WorldTree.Core) will supersede this
script and must reproduce its numbers for the same inputs. Until then, any
change to Sap or generator values must be re-run here.

Model (mirrors GDD sections 4 and 5 exactly):
- Time is simulated in whole seconds. Sap is an integer.
- Regeneration: +1 Sap every REGEN_S seconds while Sap < CAP. The regen timer
  starts when Sap drops below the cap (spend at t -> next tick at t + REGEN_S)
  and stops when Sap reaches the cap. Time spent at the cap generates nothing
  and is reported as "regen lost".
- A session starts at a scheduled time. The player makes one generator tap
  every SECONDS_PER_SAP seconds (merging and delivering happen between taps).
  Each tap costs 1 Sap and 1 generator charge.
- The session ends at the first of: Sap = 0 ("sap"), the strategy finds no
  generator it is willing to tap ("generator" stall), or MAX_SESSION_S elapsed
  ("time"). Sap left at a generator stall is "stranded": it isn't lost, it is
  carried into the next session, but it shortens the time until the bar is
  full again, so it costs regeneration.
- Generator refill rules (see RULES):
    "on-empty":      the refill timer starts only when charges reach 0 (GDD v0.2).
    "on-first-tap":  a fixed refresh timer starts at the first tap taken while
                     the generator is full; when it ends the generator returns
                     to full capacity, however many charges are left (GDD v0.3+).
- Orders (deterministic): an endless queue repeats ORDER_PATTERNS[pattern].
  Each order needs N taps from one generator's chain (a simplification of
  "items worth N level-1 equivalents"). Three orders are active at a time, and
  order progress persists across sessions. A tap counts toward the oldest
  active order of the tapped chain; a tap no active order needs is "off-order"
  (stockpiling). Completed orders are replaced from the queue at once.
- Player tap strategies (see STRATEGIES):
    "balanced":      tap the generator with the most charges left (spreads
                     taps, so each generator empties as late as possible).
    "focused":       tap only the Seed Pouch (generator 0) all session, even
                     when other generators have charges.
    "order-driven":  tap the chain of the oldest active order; if that
                     generator is empty, switch to the chain of the next active
                     order that has charges; stop if no active order's
                     generator has a charge (the player won't stockpile).
    "strict-order" (sensitivity): only ever tap the oldest active order's
                     chain; stop as soon as that generator is empty.
- Only regeneration is counted. Level-up refills, Daily Gift and chests are
  excluded so the baseline is conservative and reproducible.
"""

from __future__ import annotations

from dataclasses import dataclass, field

CAP = 100
REGEN_S = 120
MAX_SESSION_S = 20 * 60
DAY_S = 24 * 3600
DAYS = 7
ACTIVE_ORDERS = 3
REFILL_S = 20 * 60  # Lv1 refresh timer

RULES = ("on-empty", "on-first-tap")
STRATEGIES = ("balanced", "focused", "order-driven", "strict-order")
GEN_NAMES = ("Seed Pouch", "Moonwell Spring")


def hm(h: int, m: int = 0) -> int:
    return h * 3600 + m * 60


PROFILES: dict[str, list[int]] = {
    "Light (2 sessions)": [hm(8), hm(20)],
    "Core (4 sessions)": [hm(8), hm(12, 30), hm(17, 30), hm(21, 30)],
    "Engaged (6 sessions)": [hm(8), hm(10, 30), hm(13), hm(15, 30), hm(18), hm(20, 30)],
    # Reference only: 8 sessions at 00:00, 03:20, ... 20:00, 23:20, so Sap is
    # never at the cap. Shows how close real play can get to 720/day.
    "Reference: every 3h20m, 24/7": [i * hm(3, 20) for i in range(7)] + [hm(23, 20)],
}

# (generator index, taps needed). Sizes follow GDD 5.1: an early order costs
# roughly 8-20 Sap.
ORDER_PATTERNS: dict[str, list[tuple[int, int]]] = {
    "even": [(0, 12), (1, 10), (0, 8), (1, 14), (0, 16), (1, 8), (0, 10), (1, 12)],
    "a-heavy": [(0, 12), (0, 8), (1, 10), (0, 16), (0, 10), (1, 8), (0, 14), (1, 12), (0, 20)],
    # A 60% demand cap with at most 2 consecutive orders from one chain
    # (shown to be insufficient).
    "capped-60": [(0, 12), (0, 8), (1, 10), (0, 16), (1, 8), (0, 10), (0, 14), (1, 12), (1, 10)],
    # GDD 6.2 demand rule: no chain above 55% of order demand, at most 2
    # consecutive orders from one chain.
    "capped-55": [(0, 12), (1, 10), (0, 8), (0, 14), (1, 12), (0, 10), (1, 9), (0, 16), (1, 18)],
}


def pattern_share(pattern: str) -> float:
    p = ORDER_PATTERNS[pattern]
    return sum(n for g, n in p if g == 0) / sum(n for _, n in p)


@dataclass
class Generator:
    capacity: int
    refill_s: int
    rule: str = "on-first-tap"
    charges: int = -1
    refill_end: int | None = None

    def __post_init__(self) -> None:
        if self.charges < 0:
            self.charges = self.capacity

    def refresh(self, t: int) -> None:
        if self.refill_end is not None and t >= self.refill_end:
            self.charges = self.capacity
            self.refill_end = None

    def tap(self, t: int) -> None:
        if self.rule == "on-first-tap":
            if self.refill_end is None:
                self.refill_end = t + self.refill_s
            self.charges -= 1
        else:
            self.charges -= 1
            if self.charges == 0:
                self.refill_end = t + self.refill_s


@dataclass
class Energy:
    sap: int = CAP
    next_tick: int | None = None
    last_t: int = 0
    regenerated: int = 0
    seconds_at_cap: int = 0

    def advance(self, t: int) -> None:
        cursor = self.last_t
        while self.next_tick is not None and self.next_tick <= t:
            cursor = self.next_tick
            self.sap += 1
            self.regenerated += 1
            if self.sap >= CAP:
                self.next_tick = None
            else:
                self.next_tick += REGEN_S
        if self.next_tick is None and self.sap >= CAP:
            self.seconds_at_cap += t - cursor
        self.last_t = t

    def spend(self, t: int) -> None:
        self.advance(t)
        was_at_cap = self.sap >= CAP
        self.sap -= 1
        if was_at_cap and self.sap < CAP:
            self.next_tick = t + REGEN_S


class OrderBoard:
    def __init__(self, pattern: str) -> None:
        self.pattern = ORDER_PATTERNS[pattern]
        self.next_index = 0
        self.active: list[list[int]] = []  # [generator, remaining], oldest first
        self.completed = 0
        while len(self.active) < ACTIVE_ORDERS:
            self._draw()

    def _draw(self) -> None:
        g, n = self.pattern[self.next_index % len(self.pattern)]
        self.next_index += 1
        self.active.append([g, n])

    def apply_tap(self, g: int) -> bool:
        for order in self.active:
            if order[0] == g:
                order[1] -= 1
                if order[1] == 0:
                    self.active.remove(order)
                    self.completed += 1
                    self._draw()
                return True
        return False  # off-order tap


def choose(strategy: str, gens: list[Generator], orders: OrderBoard) -> int | None:
    if strategy == "balanced":
        best = max(range(len(gens)), key=lambda i: (gens[i].charges, -i))
        return best if gens[best].charges > 0 else None
    if strategy == "focused":
        return 0 if gens[0].charges > 0 else None
    if strategy == "order-driven":
        for g, _ in orders.active:
            if gens[g].charges > 0:
                return g
        return None
    if strategy == "strict-order":
        g = orders.active[0][0]
        return g if gens[g].charges > 0 else None
    raise ValueError(strategy)


@dataclass
class DayStats:
    sessions: int = 0
    spent: int = 0
    off_order: int = 0
    regenerated: int = 0
    seconds_at_cap: int = 0
    orders_completed: int = 0
    session_seconds: list[int] = field(default_factory=list)
    end_reasons: dict[str, int] = field(default_factory=lambda: {"sap": 0, "generator": 0, "time": 0})
    stranded: int = 0


@dataclass(frozen=True)
class Scenario:
    capacities: tuple[int, ...] = (60, 60)
    rule: str = "on-first-tap"
    strategy: str = "order-driven"
    pattern: str = "even"
    seconds_per_sap: int = 4


def simulate(schedule: list[int], sc: Scenario) -> list[DayStats]:
    energy = Energy()
    gens = [Generator(c, REFILL_S, sc.rule) for c in sc.capacities]
    orders = OrderBoard(sc.pattern)
    days = [DayStats() for _ in range(DAYS)]
    for d in range(DAYS):
        day = days[d]
        regen_before, cap_before, orders_before = energy.regenerated, energy.seconds_at_cap, orders.completed
        for start in sorted(schedule):
            t = d * DAY_S + start
            energy.advance(t)
            day.sessions += 1
            session_start = t
            reason = "time"
            while t - session_start < MAX_SESSION_S:
                energy.advance(t)
                for g in gens:
                    g.refresh(t)
                if energy.sap <= 0:
                    reason = "sap"
                    break
                pick = choose(sc.strategy, gens, orders)
                if pick is None:
                    reason = "generator"
                    day.stranded += energy.sap
                    break
                energy.spend(t)
                gens[pick].tap(t)
                if not orders.apply_tap(pick):
                    day.off_order += 1
                day.spent += 1
                t += sc.seconds_per_sap
            day.end_reasons[reason] += 1
            day.session_seconds.append(t - session_start)
        energy.advance((d + 1) * DAY_S)
        day.regenerated = energy.regenerated - regen_before
        day.seconds_at_cap = energy.seconds_at_cap - cap_before
        day.orders_completed = orders.completed - orders_before
    return days


# ---------------------------------------------------------------- reporting

def metrics(days: list[DayStats]) -> dict[str, float]:
    steady = days[1:]
    n = len(steady)
    stalls = sum(s.end_reasons["generator"] for s in days)
    sessions = sum(s.sessions for s in days)
    spent_steady = sum(s.spent for s in steady)
    return {
        "spent_day": spent_steady / n,
        "spent_total": sum(s.spent for s in days),
        "cap_h": sum(s.seconds_at_cap for s in steady) / n / 3600,
        "session_min": sum(sum(s.session_seconds) for s in steady) / sum(len(s.session_seconds) for s in steady) / 60,
        "stalls": stalls,
        "sessions": sessions,
        "stranded_per_stall": (sum(s.stranded for s in days) / stalls) if stalls else 0.0,
        "orders_day": sum(s.orders_completed for s in steady) / n,
        "off_order_pct": 100 * sum(s.off_order for s in steady) / spent_steady if spent_steady else 0.0,
    }


HEADER = ("| {first} | Sap spent per day (days 2–7) | 7-day total | Generator stalls | Stranded Sap per stall | "
          "Avg session (min) | Hours at cap per day | Orders per day | Off-order taps |")
DIVIDER = "|---|---|---|---|---|---|---|---|---|"


def row(label: str, m: dict[str, float]) -> str:
    return (f"| {label} | {m['spent_day']:.0f} | {m['spent_total']:.0f} | {m['stalls']} of {m['sessions']} | "
            f"{m['stranded_per_stall']:.0f} | {m['session_min']:.1f} | {m['cap_h']:.1f} | "
            f"{m['orders_day']:.1f} | {m['off_order_pct']:.0f}% |")


def detail(name: str, schedule: list[int], sc: Scenario) -> str:
    days = simulate(schedule, sc)
    lines = [
        f"#### {name}",
        "",
        "| Day | Sessions | Sap spent | Sap regenerated | Hours at cap | Regen lost at cap | Avg session (min) | "
        "Ends: sap / generator / time | Stranded Sap | Orders completed |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for i, s in enumerate(days, 1):
        avg = sum(s.session_seconds) / len(s.session_seconds) / 60
        r = s.end_reasons
        lines.append(f"| {i} | {s.sessions} | {s.spent} | {s.regenerated} | {s.seconds_at_cap / 3600:.1f} | "
                     f"{s.seconds_at_cap // REGEN_S} | {avg:.1f} | {r['sap']} / {r['generator']} / {r['time']} | "
                     f"{s.stranded} | {s.orders_completed} |")
    lines.append(f"| **7-day total** | {sum(s.sessions for s in days)} | **{sum(s.spent for s in days)}** | "
                 f"{sum(s.regenerated for s in days)} | | | | | {sum(s.stranded for s in days)} | "
                 f"{sum(s.orders_completed for s in days)} |")
    return "\n".join(lines) + "\n"


def main() -> None:
    base = Scenario()
    core = PROFILES["Core (4 sessions)"]
    out = [
        "# WORLD TREE: 7-Day Energy Simulation",
        "",
        "> **Generated by `tools/balance/energy_sim.py`. Don't edit by hand; re-run the script.**",
        "> Deterministic: no randomness, whole-second time steps. The full model is in the script header.",
        "",
        f"**Constants:** cap {CAP} Sap, +1 per {REGEN_S} s while below the cap, regeneration only (no level-up, gift or chest Sap). "
        f"The simulation starts at 00:00 on day 1 with a full bar and full generators. One tap every 4 s unless stated. "
        f"Sessions end at Sap 0, at a generator stall, or after {MAX_SESSION_S // 60} min. "
        f"Generators: {GEN_NAMES[0]} and {GEN_NAMES[1]}, Lv1, {REFILL_S // 60} min refresh timer. "
        f"{ACTIVE_ORDERS} active orders, refilled from a repeating queue.",
        "",
        "**Theoretical ceiling:** 86,400 s ÷ 120 s = 720 Sap per day, reachable only if Sap is never at the cap.",
        "",
        "**Strategies:** *balanced* taps the generator with the most charges · *focused* taps only the Seed Pouch · "
        "*order-driven* taps for the oldest active order and switches to another active order's generator when that one is empty, "
        "never stockpiling · *strict-order* (sensitivity) only taps for the oldest order and stops when its generator is empty.",
        "",
        "**Order demand patterns** (share of taps needed from the Seed Pouch): "
        + " · ".join(f"*{k}* {pattern_share(k) * 100:.0f}%" for k in ORDER_PATTERNS)
        + ". *capped-55* follows the GDD §6.2 demand rule.",
        "",
    ]

    out += ["## 1. Current values (2 × 60 charges, first-tap refresh), by strategy and profile", ""]
    for pattern in ORDER_PATTERNS:
        out += [f"### Order demand: {pattern} ({pattern_share(pattern) * 100:.0f}% Seed Pouch)", "",
                HEADER.format(first="Profile · strategy"), DIVIDER]
        for pname, sched in PROFILES.items():
            for strat in STRATEGIES:
                m = metrics(simulate(sched, Scenario(strategy=strat, pattern=pattern)))
                out.append(row(f"{pname.split(' (')[0].split(':')[0]} · {strat}", m))
        out.append("")

    out += ["## 2. Sensitivity: core player, seconds per Sap (even demand)", "",
            HEADER.format(first="Strategy · s/Sap"), DIVIDER]
    for strat in STRATEGIES:
        for sps in (3, 4, 5):
            out.append(row(f"{strat} · {sps}", metrics(simulate(core, Scenario(strategy=strat, seconds_per_sap=sps)))))
    out.append("")

    out += ["## 3. Generator rules and capacities compared: core player, even demand", "",
            HEADER.format(first="Variant · strategy"), DIVIDER]
    variants = [
        ("A. v0.2: 40 + 40, refill on empty", (40, 40), "on-empty"),
        ("B. 60 + 60, refill on empty", (60, 60), "on-empty"),
        ("C. 40 + 40, first-tap refresh", (40, 40), "on-first-tap"),
        ("D. current: 60 + 60, first-tap refresh", (60, 60), "on-first-tap"),
        ("E. 105 + 105, first-tap refresh", (105, 105), "on-first-tap"),
    ]
    for label, caps, rule in variants:
        for strat in STRATEGIES:
            out.append(row(f"{label} · {strat}", metrics(simulate(core, Scenario(capacities=caps, rule=rule, strategy=strat)))))
    out.append("")
    out += ["Variant E shows the capacity a *single* generator would need (≥ 100 Sap bar + in-session regen) "
            "for a fully focused player never to stall.", ""]

    out += ["## 4. Daily detail: core player, current values", ""]
    for strat, pattern in (("order-driven", "even"), ("order-driven", "a-heavy"), ("focused", "even"), ("balanced", "even")):
        out.append(detail(f"Core · {strat} · {pattern} demand", core, Scenario(strategy=strat, pattern=pattern)))
    out.append(detail("Reference 24/7 · balanced · even demand", PROFILES["Reference: every 3h20m, 24/7"],
                      Scenario(strategy="balanced")))
    print("\n".join(out))


def reference_scenarios():
    """Every first-tap scenario in the report, as (name, schedule, Scenario)."""
    out = []
    for pname, sched in PROFILES.items():
        for pattern in ORDER_PATTERNS:
            for strat in STRATEGIES:
                out.append((f"{pname.split(' (')[0].split(':')[0]}|{pattern}|{strat}|60,60|4", sched, Scenario(strategy=strat, pattern=pattern)))
    core = PROFILES["Core (4 sessions)"]
    for strat in STRATEGIES:
        for sps in (3, 5):
            out.append((f"Core|even|{strat}|60,60|{sps}", core, Scenario(strategy=strat, seconds_per_sap=sps)))
        for caps in ((40, 40), (105, 105)):
            out.append((f"Core|even|{strat}|{caps[0]},{caps[1]}|4", core, Scenario(capacities=caps, strategy=strat)))
    return out


def main_csharp() -> None:
    """Writes the reference totals the C# port must reproduce (roadmap D22)."""
    lines = [
        "// <auto-generated>",
        "// Generated by: python3 tools/balance/energy_sim.py --csharp > Assets/WorldTree/Tests/Balance/PythonReference.cs",
        "// Reference output of the Python energy simulation (roadmap D22). Don't edit by hand.",
        "// </auto-generated>",
        "namespace WorldTree.Tests.Balance",
        "{",
        "    public static class PythonReference",
        "    {",
        "        /// <summary>Name, 7-day Sap spent, Sap regenerated, generator stalls, Sap stranded at stalls, orders completed.</summary>",
        "        public static readonly (string Name, int Spent, int Regenerated, int Stalls, int Stranded, int Orders)[] Totals =",
        "        {",
    ]
    for name, sched, sc in reference_scenarios():
        days = simulate(sched, sc)
        spent = sum(d.spent for d in days)
        regen = sum(d.regenerated for d in days)
        stalls = sum(d.end_reasons["generator"] for d in days)
        stranded = sum(d.stranded for d in days)
        orders = sum(d.orders_completed for d in days)
        lines.append(f'            ("{name}", {spent}, {regen}, {stalls}, {stranded}, {orders}),')
    lines += ["        };", "    }", "}"]
    print("\n".join(lines))


if __name__ == "__main__":
    import sys
    if "--csharp" in sys.argv:
        main_csharp()
    else:
        main()
