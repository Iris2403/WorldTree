#!/usr/bin/env python3
"""WORLD TREE - deterministic 7-day Sap (energy) and generator simulation.

Design-time balance tool (no game dependency, no randomness). It reproduces the
energy numbers in docs/01-game-design-document.md section 5.1 from explicit
session schedules, and checks whether sessions are limited by Sap or by
generator charges.

Usage:
    python3 tools/balance/energy_sim.py > docs/balance/energy-simulation.md

The Phase 4 balance simulator (C#, built on WorldTree.Core) will supersede this
script. Until then, any change to Sap or generator values must be re-run here.

Model (mirrors GDD section 5 exactly):
- Time is simulated in whole seconds. Sap is an integer.
- Regeneration: +1 Sap every REGEN_S seconds while Sap < CAP. The regen timer
  starts when Sap drops below the cap (spend at t -> next tick at t + REGEN_S)
  and stops when Sap reaches the cap. Time spent at the cap generates nothing
  and is reported as "regen lost".
- A session starts at a scheduled time. The player performs one generator tap
  every SECONDS_PER_SAP seconds (merging/delivering happens between taps).
  Each tap costs 1 Sap and 1 generator charge.
- The session ends at the first of: Sap = 0 ("sap"), no generator has a charge
  ("generator"), or MAX_SESSION_S elapsed ("time").
- Generator refill rule (selectable, see RULES):
    "on-empty":     recharge starts only when charges reach 0 (GDD v0.2).
    "on-first-tap": the refill timer starts at the first tap taken while the
                    generator is full; when it ends the generator refills to
                    full capacity, whatever is left (GDD v0.3 proposal).
- Tap policy "balanced": always tap the generator with the most charges
  (worst case for stalls, because all generators run dry together).
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


def hm(h: int, m: int = 0) -> int:
    return h * 3600 + m * 60


PROFILES: dict[str, list[int]] = {
    "Light (2 sessions)": [hm(8), hm(20)],
    "Core (4 sessions)": [hm(8), hm(12, 30), hm(17, 30), hm(21, 30)],
    "Engaged (6 sessions)": [hm(8), hm(10, 30), hm(13), hm(15, 30), hm(18), hm(20, 30)],
    # Reference only: a session every 3 h 20 min around the clock, so Sap is
    # (almost) never at the cap. Shows how close real play can get to 720/day.
    "Reference: every 3h20m, 24/7": [i * hm(3, 20) for i in range(8)][:7] + [hm(23, 20)],
}


RULES = ("on-empty", "on-first-tap")


@dataclass
class Generator:
    capacity: int
    recharge_s: int
    rule: str = "on-first-tap"
    charges: int = -1
    recharge_end: int | None = None

    def __post_init__(self) -> None:
        if self.charges < 0:
            self.charges = self.capacity

    def refresh(self, t: int) -> None:
        if self.recharge_end is not None and t >= self.recharge_end:
            self.charges = self.capacity
            self.recharge_end = None

    def tap(self, t: int) -> None:
        if self.rule == "on-first-tap":
            if self.recharge_end is None:
                self.recharge_end = t + self.recharge_s
            self.charges -= 1
        else:
            self.charges -= 1
            if self.charges == 0:
                self.recharge_end = t + self.recharge_s


@dataclass
class Energy:
    sap: int = CAP
    next_tick: int | None = None
    last_t: int = 0
    regenerated: int = 0
    seconds_at_cap: int = 0

    def advance(self, t: int) -> None:
        # Integrate time at cap between last_t and t, applying regen ticks.
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


@dataclass
class DayStats:
    sessions: int = 0
    spent: int = 0
    regenerated: int = 0
    seconds_at_cap: int = 0
    session_seconds: list[int] = field(default_factory=list)
    end_reasons: dict[str, int] = field(default_factory=lambda: {"sap": 0, "generator": 0, "time": 0})
    sap_left_at_generator_stall: int = 0


def simulate(schedule: list[int], capacities: list[int], recharge_s: int, seconds_per_sap: int, rule: str) -> list[DayStats]:
    energy = Energy()
    gens = [Generator(c, recharge_s, rule) for c in capacities]
    days = [DayStats() for _ in range(DAYS)]
    for d in range(DAYS):
        day = days[d]
        regen_before = energy.regenerated
        cap_before = energy.seconds_at_cap
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
                best = max(gens, key=lambda g: g.charges)
                if best.charges <= 0:
                    reason = "generator"
                    day.sap_left_at_generator_stall += energy.sap
                    break
                energy.spend(t)
                best.tap(t)
                day.spent += 1
                t += seconds_per_sap
            day.end_reasons[reason] += 1
            day.session_seconds.append(t - session_start)
        energy.advance((d + 1) * DAY_S)
        day.regenerated = energy.regenerated - regen_before
        day.seconds_at_cap = energy.seconds_at_cap - cap_before
    return days


def fmt_reasons(r: dict[str, int]) -> str:
    return " / ".join(f"{r[k]}" for k in ("sap", "generator", "time"))


def report_profile(name: str, schedule: list[int], capacities: list[int], recharge_s: int, sps: int, rule: str) -> str:
    days = simulate(schedule, capacities, recharge_s, sps, rule)
    lines = [
        f"#### {name}",
        "",
        "| Day | Sessions | Sap spent | Sap regenerated | Hours at cap | Regen lost at cap | Avg session (min) | Ends: sap / generator / time | Sap stranded by generator stalls |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for i, s in enumerate(days, 1):
        avg = sum(s.session_seconds) / len(s.session_seconds) / 60
        lines.append(
            f"| {i} | {s.sessions} | {s.spent} | {s.regenerated} | {s.seconds_at_cap / 3600:.1f} | "
            f"{s.seconds_at_cap // REGEN_S} | {avg:.1f} | {fmt_reasons(s.end_reasons)} | {s.sap_left_at_generator_stall} |"
        )
    steady = days[1:]
    n = len(steady)
    lines.append(
        f"| **Days 2–7 avg** | {sum(s.sessions for s in steady) / n:.0f} | **{sum(s.spent for s in steady) / n:.0f}** | "
        f"{sum(s.regenerated for s in steady) / n:.0f} | {sum(s.seconds_at_cap for s in steady) / n / 3600:.1f} | "
        f"{sum(s.seconds_at_cap for s in steady) / n / REGEN_S:.0f} | "
        f"{sum(sum(s.session_seconds) for s in steady) / sum(len(s.session_seconds) for s in steady) / 60:.1f} | "
        f"{fmt_reasons({k: sum(s.end_reasons[k] for s in steady) for k in ('sap', 'generator', 'time')})} | "
        f"{sum(s.sap_left_at_generator_stall for s in steady) / n:.0f} |"
    )
    lines.append(f"| **7-day total** | {sum(s.sessions for s in days)} | **{sum(s.spent for s in days)}** | "
                 f"{sum(s.regenerated for s in days)} | | | | | |")
    return "\n".join(lines) + "\n"


def summary_row(name: str, schedule: list[int], capacities: list[int], recharge_s: int, sps: int, rule: str) -> str:
    days = simulate(schedule, capacities, recharge_s, sps, rule)
    steady = days[1:]
    n = len(steady)
    gen_ends = sum(s.end_reasons["generator"] for s in days)
    sessions = sum(s.sessions for s in days)
    return (f"| {name} | {sum(s.spent for s in steady) / n:.0f} | {sum(s.spent for s in days)} | "
            f"{sum(s.seconds_at_cap for s in steady) / n / 3600:.1f} h | "
            f"{sum(sum(s.session_seconds) for s in steady) / sum(len(s.session_seconds) for s in steady) / 60:.1f} | "
            f"{gen_ends} of {sessions} |")


def main() -> None:
    variants = [
        ("A. v0.2: 2 x 40 charges, refill on empty", [40, 40], "on-empty"),
        ("B. 2 x 60 charges, refill on empty", [60, 60], "on-empty"),
        ("C. 2 x 40 charges, refill timer from first tap", [40, 40], "on-first-tap"),
        ("D. v0.3 proposal: 2 x 60 charges, refill timer from first tap", [60, 60], "on-first-tap"),
    ]
    proposal = variants[-1]
    out = [
        "# WORLD TREE: 7-Day Energy Simulation",
        "",
        "> **Generated by `tools/balance/energy_sim.py`. Don't edit by hand; re-run the script.**",
        "> Deterministic: no randomness, whole-second time steps. The model and assumptions are in the script header.",
        "",
        f"Constants: cap **{CAP}** Sap, **+1 per {REGEN_S} s** while below the cap, regeneration only (no level-up, gift or chest Sap). "
        f"Simulation starts at 00:00 on day 1 with a full bar of {CAP} Sap and full generators. One tap every N seconds (N = 4 unless stated). "
        f"Sessions end at Sap 0, at a generator stall, or after {MAX_SESSION_S // 60} min. Generators: 20 min refill, tap policy \"balanced\" (worst case).",
        "",
        "Theoretical regeneration ceiling: 86,400 s ÷ 120 s = **720 Sap per day**, reachable only if Sap is never at the cap. "
        "The reference row shows how close a 24/7 schedule gets.",
        "",
        "## Summary: Sap spent per day, by generator rule (4 s per Sap)",
        "",
    ]
    for title, caps, rule in variants:
        out += [f"### {title}", "",
                "| Profile | Sap spent per day (days 2–7 avg) | 7-day total spent | Time at cap per day | Avg session (min) | Sessions ended by a generator stall |",
                "|---|---|---|---|---|---|"]
        for name, sched in PROFILES.items():
            out.append(summary_row(name, sched, caps, 20 * 60, 4, rule))
        out.append("")
    out += ["## Sensitivity: core player, seconds per Sap", "",
            "| Variant | Seconds per Sap | Sap spent per day (days 2–7 avg) | 7-day total | Time at cap per day | Avg session (min) | Generator-stall endings |",
            "|---|---|---|---|---|---|---|"]
    for title, caps, rule in variants:
        for sps in (3, 4, 5):
            row = summary_row(title.split(":")[0].split(" 2 x")[0].strip(), PROFILES["Core (4 sessions)"], caps, 20 * 60, sps, rule)
            cells = row.split(" | ")
            out.append(" | ".join([cells[0], str(sps)] + cells[1:]))
    out += ["", f"## Daily detail: {proposal[0]}, 4 s per Sap", ""]
    for name, sched in PROFILES.items():
        out.append(report_profile(name, sched, proposal[1], 20 * 60, 4, proposal[2]))
    out += [f"## Daily detail: {variants[0][0]}, core player", ""]
    out.append(report_profile("Core (4 sessions)", PROFILES["Core (4 sessions)"], variants[0][1], 20 * 60, 4, variants[0][2]))
    print("\n".join(out))


if __name__ == "__main__":
    main()
