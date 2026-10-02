"""
All of Map Map's logic in one place. See DESIGN.md (decisions 3, 7, 11, 12, 14) for the reasons.

Two kinds of rules:
  * island_entry_rule  - what you need to travel to an island at all (checked on the
                         "World Map -> Island N" entrance, so it applies to everything on that island)
  * location_rule      - extra needs for one specific check on an island you can already reach

A rule is a function that takes the current collection `state` and returns True if the player
could do it with the items they have in that state.
"""
from typing import TYPE_CHECKING, Callable

from BaseClasses import CollectionState

from .location_table import Kind, LocationInfo

if TYPE_CHECKING:
    from .world import MapMapWorld

Rule = Callable[[CollectionState], bool]

# Decision 11: the five swamp islands need the Build Tool for their bridges.
BUILD_TOOL_ISLANDS = range(12, 17)        # Islands 12-16
# Decision 12: Floor Is Lava from Island 14 onward needs Glide.
GLIDE_FLOOR_IS_LAVA_FROM = 14
# Decision 7: Island 10's "take a picture of the map" story step comes before its second set.
CAMERA_ISLAND, CAMERA_FROM_SET_INDEX = 10, 1
# Decision 14 (helpful_tool_logic option): never strictly needed, but makes these islands fair.
HELPFUL_DIVIDERS_FROM = 7                 # Islands 7-20
HELPFUL_TELESCOPE_FROM = 17               # Islands 17-20


def island_entry_rule(world: "MapMapWorld", island: int) -> Rule:
    player = world.player
    needs = [f"Island {island}"]
    if island in BUILD_TOOL_ISLANDS:
        needs.append("Build Tool")
    if world.options.helpful_tool_logic:
        if island >= HELPFUL_DIVIDERS_FROM:
            needs.append("Dividers")
        if island >= HELPFUL_TELESCOPE_FROM:
            needs.append("Telescope")
    return lambda state: state.has_all(needs, player)


def needs_only_island_item(world: "MapMapWorld", island: int) -> bool:
    """True if travelling to this island needs nothing but its own Island item (no tools)."""
    if island in BUILD_TOOL_ISLANDS:
        return False
    if world.options.helpful_tool_logic and island >= HELPFUL_DIVIDERS_FROM:
        return False
    return True


def location_rule(world: "MapMapWorld", info: LocationInfo) -> Rule | None:
    """Extra requirement for a single check, or None if reaching the island is enough."""
    player = world.player
    needs: list[str] = []

    if info.kind == Kind.TREASURE:
        needs.append("Shovel")
    if info.kind in (Kind.MISSION, Kind.THREE_STARS):
        if info.mission.treasure:
            needs.append("Shovel")
        if info.island == CAMERA_ISLAND and info.mission.set_index >= CAMERA_FROM_SET_INDEX:
            needs.append("Camera")
    if info.kind == Kind.FLOOR_IS_LAVA and info.island >= GLIDE_FLOOR_IS_LAVA_FROM:
        needs.append("Glide")

    if not needs:
        return None
    return lambda state: state.has_all(needs, player)


def goal_cave_rule(world: "MapMapWorld") -> Rule:
    """Island 20's cave comes right after its treasure mission, which needs the Shovel."""
    player = world.player
    return lambda state: state.has("Shovel", player)
