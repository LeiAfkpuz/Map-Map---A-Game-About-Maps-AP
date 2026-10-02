"""
Sanity-checks and prints the Map Map item/location tables. Needs only plain Python 3.10+.

    python tools/check_tables.py                    # summary + checks
    python tools/check_tables.py --list             # also print every location, island by island
    python tools/check_tables.py --update-snapshot  # accept NEW names into the ID snapshot

ID snapshot (tools/id_snapshot.json): IDs must never change after a release, because servers,
trackers and in-progress seeds identify items/locations by ID. The snapshot records every name -> ID
and this script fails if an existing entry's ID changes, an entry disappears, or an ID gets reused
by a different name. Brand-new entries are allowed but must be accepted with --update-snapshot.

It loads only the data files (islands.py, location_table.py, item_table.py), never the package's
__init__.py, so it works without an Archipelago install.
"""
import importlib
import json
import sys
import types
from collections import Counter
from pathlib import Path

APWORLD_DIR = Path(__file__).resolve().parent.parent / "apworld" / "mapmap"

# Register an empty "mapmap" package pointing at the folder, so `from .islands import ...` inside the
# data files works, but the real __init__.py (which needs Archipelago) is never run.
pkg = types.ModuleType("mapmap")
pkg.__path__ = [str(APWORLD_DIR)]
sys.modules["mapmap"] = pkg

islands = importlib.import_module("mapmap.islands")
loc = importlib.import_module("mapmap.location_table")
itm = importlib.import_module("mapmap.item_table")

problems: list[str] = []


def check(ok: bool, message: str) -> None:
    if not ok:
        problems.append(message)


# --- Locations ----------------------------------------------------------------------------------
table = loc.LOCATION_TABLE
by_kind = Counter(info.kind for info in table.values())

ids = [info.code for info in table.values()]
check(len(ids) == len(set(ids)), "duplicate location IDs")

# Every ID must decode back to the island/kind/index it was built from.
for name, info in table.items():
    rest = info.code - loc.BASE_ID
    decoded = (rest // 1000, (rest % 1000) // 100, rest % 100)
    check(decoded == (info.island, int(info.kind), info.index), f"ID does not decode: {name} = {info.code}")

# Expected totals from the probe dump (2026-09-30). If islands.py changes, update these on purpose.
expected = {
    loc.Kind.MISSION: 68,        # 69 missions minus the goal mission
    loc.Kind.THREE_STARS: 61,    # 68 minus 7 treasure missions
    loc.Kind.TREASURE: 40,
    loc.Kind.FLOOR_IS_LAVA: 18,
    loc.Kind.LANDMARK: 12,       # Islands 2-7, 12, 14-17, 20 (an earlier hand count of 13 was wrong)
}
for kind, want in expected.items():
    check(by_kind[kind] == want, f"{kind.name}: expected {want}, got {by_kind[kind]}")

goal_missions = [m for isl in islands.ISLANDS for m in isl.missions if m.goal]
check(len(goal_missions) == 1, f"expected exactly 1 goal mission, found {len(goal_missions)}")
check([i.number for i in islands.ISLANDS] == list(range(1, 21)), "islands must be numbered 1-20 in order")

# --- Items --------------------------------------------------------------------------------------
items = itm.ITEM_TABLE
item_ids = [info.code for info in items.values()]
check(len(item_ids) == len(set(item_ids)), "duplicate item IDs")

shuffled_islands = 20 - 1  # Island 1 is the starting island, not in the pool
tools = len(itm.TOOLS)
cosmetics = sum(i.count for i in itm.COSMETICS.values())
non_filler = shuffled_islands + tools
total_locations = len(table)
generic_filler = total_locations - non_filler - cosmetics
check(generic_filler >= 0, "more items than locations")

# --- ID snapshot --------------------------------------------------------------------------------
SNAPSHOT = Path(__file__).resolve().parent / "id_snapshot.json"
current_ids = {
    "items": {name: info.code for name, info in items.items()},
    "locations": {name: info.code for name, info in table.items()},
}
new_entries: list[str] = []

if SNAPSHOT.exists():
    # utf-8-sig also accepts files saved by editors that add a byte-order mark (e.g. Notepad).
    saved = json.loads(SNAPSHOT.read_text(encoding="utf-8-sig"))
    for section in ("items", "locations"):
        old, new = saved.get(section, {}), current_ids[section]
        for name, old_id in old.items():
            if name not in new:
                check(False, f"{section}: '{name}' ({old_id}) was removed - released IDs must stay")
            elif new[name] != old_id:
                check(False, f"{section}: '{name}' changed ID {old_id} -> {new[name]}")
        old_by_id = {v: k for k, v in old.items()}
        for name, new_id in new.items():
            if name not in old:
                if new_id in old_by_id:
                    check(False, f"{section}: new '{name}' reuses ID {new_id} of '{old_by_id[new_id]}'")
                else:
                    new_entries.append(f"{section}: {name} = {new_id}")
else:
    new_entries = [f"(no snapshot yet: {len(current_ids['items'])} items, {len(current_ids['locations'])} locations)"]

if new_entries and "--update-snapshot" not in sys.argv:
    check(False, f"{len(new_entries)} new name(s) not in the ID snapshot; review them, then rerun with "
                 f"--update-snapshot:\n      " + "\n      ".join(new_entries[:20]))
elif "--update-snapshot" in sys.argv and not [p for p in problems]:
    SNAPSHOT.write_text(json.dumps(current_ids, indent=1, sort_keys=True), encoding="utf-8")
    print(f"Snapshot written: {SNAPSHOT.name}")

# --- Report -------------------------------------------------------------------------------------
print("Locations by kind:")
for kind in loc.Kind:
    print(f"  {kind.name:<14} {by_kind[kind]:>4}")
print(f"  {'TOTAL':<14} {total_locations:>4}")
print()
print("Item pool (all options on):")
print(f"  Island unlocks  {shuffled_islands:>4}   (Island {itm.STARTING_ISLAND} is given at start)")
print(f"  Tools/abilities {tools:>4}   ({', '.join(itm.TOOLS)})")
print(f"  Stickers/pencils{cosmetics:>5}")
print(f"  {itm.FILLER_NAME:<15} {generic_filler:>4}   (generic filler for the rest)")
print(f"  TOTAL           {shuffled_islands + tools + cosmetics + generic_filler:>4}")
print()

if "--list" in sys.argv:
    current = None
    for name, info in table.items():
        if info.island != current:
            current = info.island
            isl = islands.ISLANDS[current - 1]
            print(f"--- Island {current} ({isl.scene})")
        extra = f'   "{info.mission.text}"' if info.mission and info.kind == loc.Kind.MISSION else ""
        print(f"  {info.code}  {name}{extra}")
    print()

if problems:
    print("PROBLEMS:")
    for p in problems:
        print("  - " + p)
    sys.exit(1)
print("All checks passed.")
