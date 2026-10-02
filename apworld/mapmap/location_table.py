"""
Builds every Map Map location (check) from the island data in islands.py.

Location IDs are readable on purpose, so a support report like "location 6276302 didn't send"
can be decoded by hand (6276302 -> Island 16, Floor Is Lava 2):

    id = BASE_ID + island * 1000 + kind * 100 + index

    BASE_ID  6_260_000   (any fixed number works; this one just has to never change once released)
    island   1-20
    kind     0 mission, 1 three-star, 2 treasure, 3 floor is lava, 4 landmark
    index    1-based within that island and kind

    Example: Island 16, Floor Is Lava 2  ->  6_260_000 + 16_000 + 300 + 2 = 6_276_302

Plain Python, no Archipelago imports (see islands.py).
"""
from dataclasses import dataclass
from enum import IntEnum

from .islands import ISLANDS, Island, Mission

BASE_ID = 6_260_000


class Kind(IntEnum):
    MISSION = 0
    THREE_STARS = 1
    TREASURE = 2
    FLOOR_IS_LAVA = 3
    LANDMARK = 4


@dataclass(frozen=True)
class LocationInfo:
    code: int
    island: int
    kind: Kind
    index: int                     # 1-based within island + kind
    mission: Mission | None = None  # set for MISSION and THREE_STARS


def location_id(island: int, kind: Kind, index: int) -> int:
    return BASE_ID + island * 1000 + int(kind) * 100 + index


def _island_locations(isl: Island) -> dict[str, LocationInfo]:
    out: dict[str, LocationInfo] = {}
    prefix = f"Island {isl.number}"

    # Missions and their 3-star checks share the same index (the mission's position on the island),
    # so "Mission 1.3" and "Mission 1.3 (3 Stars)" have matching IDs apart from the kind digit.
    for i, m in enumerate(isl.missions, start=1):
        if m.goal:
            continue  # the ending trigger is the goal event, not a location
        out[f"{prefix} - Mission {m.display}"] = LocationInfo(
            location_id(isl.number, Kind.MISSION, i), isl.number, Kind.MISSION, i, m)
        if not m.treasure:  # treasure missions have no star rating
            out[f"{prefix} - Mission {m.display} (3 Stars)"] = LocationInfo(
                location_id(isl.number, Kind.THREE_STARS, i), isl.number, Kind.THREE_STARS, i, m)

    for i in range(1, isl.treasures + 1):
        out[f"{prefix} - Treasure {i}"] = LocationInfo(
            location_id(isl.number, Kind.TREASURE, i), isl.number, Kind.TREASURE, i)

    for i in range(1, isl.floor_is_lava + 1):
        out[f"{prefix} - Floor Is Lava {i}"] = LocationInfo(
            location_id(isl.number, Kind.FLOOR_IS_LAVA, i), isl.number, Kind.FLOOR_IS_LAVA, i)

    # No island has more than one landmark, so it isn't numbered.
    for i in range(1, isl.landmarks + 1):
        name = f"{prefix} - Landmark" if isl.landmarks == 1 else f"{prefix} - Landmark {i}"
        out[name] = LocationInfo(
            location_id(isl.number, Kind.LANDMARK, i), isl.number, Kind.LANDMARK, i)

    return out


def build_location_table() -> dict[str, LocationInfo]:
    table: dict[str, LocationInfo] = {}
    for isl in ISLANDS:
        table.update(_island_locations(isl))
    return table


LOCATION_TABLE: dict[str, LocationInfo] = build_location_table()
