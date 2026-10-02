"""
The Map Map world: turns the data tables + options into regions, locations, items and a goal.

Archipelago calls these methods in order during generation:
  generate_early -> create_regions -> create_items -> set_rules -> ... fill ... -> fill_slot_data
"""
from typing import Any

from BaseClasses import Item, ItemClassification, Location, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from . import rules
from .islands import ISLANDS
from .item_table import COSMETICS, FILLER_NAME, ITEM_TABLE, STARTING_ISLAND, TOOLS
from .location_table import LOCATION_TABLE, Kind, LocationInfo
from .options import Goal, MapMapOptions, option_groups

# The full title is the game name players put in YAMLs and see on servers/trackers.
# The apworld file and code folder use the short form "mapmap" (like tloz_oos for Oracle of Seasons).
GAME_NAME = "Map Map - A Game About Maps"

CLASSIFICATIONS = {
    "progression": ItemClassification.progression,
    "useful": ItemClassification.useful,
    "filler": ItemClassification.filler,
}

# Event (non-check) names. Events have no ID; they only exist inside generation to express goals.
VICTORY = "Victory"
ISLAND_COMPLETE = "Island Complete"
CAVE_EVENT = "Island 20 - Enter the Raven King's Treasure Cave"


class MapMapItem(Item):
    game = GAME_NAME


class MapMapLocation(Location):
    game = GAME_NAME


class MapMapWeb(WebWorld):
    theme = "ocean"
    option_groups = option_groups
    # The game info page is docs/en_<game name>.md; tutorials are listed here.
    tutorials = [Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up Map Map - A Game About Maps for Archipelago.",
        "English",
        "setup_en.md",
        "setup/en",
        ["LeiAfkpuz"],
    )]


class MapMapWorld(World):
    """
    Map Map - A Game About Maps: explore islands and draw your own maps to find what you're asked to find.
    """
    game = GAME_NAME
    web = MapMapWeb()
    options_dataclass = MapMapOptions
    options: MapMapOptions
    origin_region_name = "Menu"

    # Fixed for every player and every option set (see location_table.py / tools/check_tables.py).
    item_name_to_id = {name: info.code for name, info in ITEM_TABLE.items()}
    location_name_to_id = {name: info.code for name, info in LOCATION_TABLE.items()}

    # Universal Tracker: UT generates with default options, then hands us the real slot_data via
    # interpret_slot_data and regenerates; generate_early applies it (same approach as WSR).
    ut_can_gen_without_yaml = True

    # ------------------------------------------------------------------ generation steps

    def generate_early(self) -> None:
        passthrough = getattr(self.multiworld, "re_gen_passthrough", {})
        if GAME_NAME in passthrough:
            slot_data = passthrough[GAME_NAME]
            for key in ("goal", "islands_required", "star_checks", "treasure_checks",
                        "floor_is_lava_checks", "helpful_tool_logic"):
                if key in slot_data:
                    getattr(self.options, key).value = int(slot_data[key])

        # The 3-star goal is impossible without star checks, so turn them on.
        if self.options.goal == Goal.option_all_three_stars and not self.options.star_checks:
            self.options.star_checks.value = 1

    def create_regions(self) -> None:
        menu = Region("Menu", self.player, self.multiworld)
        world_map = Region("World Map", self.player, self.multiworld)
        self.multiworld.regions += [menu, world_map]
        menu.connect(world_map)

        self.island_regions: dict[int, Region] = {}
        for isl in ISLANDS:
            region = Region(f"Island {isl.number}", self.player, self.multiworld)
            self.multiworld.regions.append(region)
            self.island_regions[isl.number] = region
            world_map.connect(region, f"Travel to Island {isl.number}",
                              rules.island_entry_rule(self, isl.number))

        for name, info in LOCATION_TABLE.items():
            if self._included(info):
                region = self.island_regions[info.island]
                region.locations.append(MapMapLocation(self.player, name, info.code, region))

        self._create_goal_events()

    def create_item(self, name: str) -> MapMapItem:
        info = ITEM_TABLE[name]
        return MapMapItem(name, CLASSIFICATIONS[info.classification], info.code, self.player)

    def get_filler_item_name(self) -> str:
        return FILLER_NAME

    def create_items(self) -> None:
        # Island 1 is where the game starts, so the player simply has it.
        self.multiworld.push_precollected(self.create_item(f"Island {STARTING_ISLAND}"))

        islands_to_shuffle = [n for n in range(1, 21) if n != STARTING_ISLAND]

        # SOLO seeds only: with star checks off, Island 1 has exactly ONE check at the start. If the
        # generator puts something there that opens nothing new, a solo seed runs out of room
        # ("No more spots to place"). So in that one case Island 1's mission gives a second island
        # that needs no tools. In a multiworld, other games' starting checks give the generator
        # room, so Island 1's mission stays fully random (it may hold anyone's item).
        if not self.options.star_checks and self.multiworld.players == 1:
            free_islands = [n for n in islands_to_shuffle if rules.needs_only_island_item(self, n)]
            first = self.random.choice(free_islands)
            islands_to_shuffle.remove(first)
            self.multiworld.get_location(f"Island {STARTING_ISLAND} - Mission 1", self.player) \
                .place_locked_item(self.create_item(f"Island {first}"))

        pool = [self.create_item(f"Island {n}") for n in islands_to_shuffle]
        pool += [self.create_item(name) for name in TOOLS]

        # Count only empty check locations (a pre-placed item above already uses one).
        slots = len([loc for loc in self.multiworld.get_locations(self.player)
                     if loc.address is not None and loc.item is None])
        if len(pool) > slots:
            raise Exception(f"{GAME_NAME}: {len(pool)} required items but only {slots} locations "
                            f"for player {self.player_name}")

        # Cosmetics fill free slots first (randomly chosen if they don't all fit), then Ink Blot.
        cosmetics = [name for name, info in COSMETICS.items() for _ in range(info.count)]
        self.random.shuffle(cosmetics)
        free = slots - len(pool)
        pool += [self.create_item(name) for name in cosmetics[:free]]
        pool += [self.create_item(FILLER_NAME) for _ in range(slots - len(pool))]

        self.multiworld.itempool += pool

    def set_rules(self) -> None:
        for name, info in LOCATION_TABLE.items():
            if not self._included(info):
                continue
            rule = rules.location_rule(self, info)
            if rule is not None:
                self.multiworld.get_location(name, self.player).access_rule = rule

        self.multiworld.completion_condition[self.player] = self._completion_condition()

    def fill_slot_data(self) -> dict[str, Any]:
        # Everything the game plugin needs to know about this seed's settings.
        return {
            "goal": int(self.options.goal.value),
            "islands_required": int(self.options.islands_required.value),
            "star_checks": int(self.options.star_checks.value),
            "treasure_checks": int(self.options.treasure_checks.value),
            "floor_is_lava_checks": int(self.options.floor_is_lava_checks.value),
            "helpful_tool_logic": int(self.options.helpful_tool_logic.value),
            "starting_island": STARTING_ISLAND,
        }

    @staticmethod
    def interpret_slot_data(slot_data: dict[str, Any]) -> dict[str, Any]:
        return slot_data

    # ------------------------------------------------------------------ helpers

    def _included(self, info: LocationInfo) -> bool:
        """Is this check part of the seed with the current options?"""
        if info.kind == Kind.THREE_STARS:
            return bool(self.options.star_checks)
        if info.kind == Kind.TREASURE:
            return bool(self.options.treasure_checks)
        if info.kind == Kind.FLOOR_IS_LAVA:
            return bool(self.options.floor_is_lava_checks)
        return True

    def _add_event(self, region: Region, location_name: str, item_name: str, rule=None) -> None:
        """An event: a location with no ID holding a locked item with no ID. Used only for goals."""
        location = MapMapLocation(self.player, location_name, None, region)
        if rule is not None:
            location.access_rule = rule
        location.place_locked_item(MapMapItem(item_name, ItemClassification.progression, None, self.player))
        region.locations.append(location)

    def _create_goal_events(self) -> None:
        goal = self.options.goal
        if goal == Goal.option_island_20:
            self._add_event(self.island_regions[20], CAVE_EVENT, VICTORY, rules.goal_cave_rule(self))
        elif goal == Goal.option_islands_completed:
            # One "Island Complete" per island, collectable once every mission there is doable.
            for isl in ISLANDS:
                mission_infos = [info for info in LOCATION_TABLE.values()
                                 if info.island == isl.number and info.kind == Kind.MISSION]
                self._add_event(self.island_regions[isl.number], f"Island {isl.number} - All Missions Done",
                                ISLAND_COMPLETE, self._all_doable_rule(mission_infos))

    def _all_doable_rule(self, infos: list[LocationInfo]):
        """True when every given check could be done (its own rule passes; island access is
        already implied because the event lives in that island's region)."""
        checks = [rules.location_rule(self, info) for info in infos]
        checks = [c for c in checks if c is not None]
        return lambda state: all(c(state) for c in checks)

    def _completion_condition(self):
        goal = self.options.goal
        player = self.player
        if goal == Goal.option_island_20:
            return lambda state: state.has(VICTORY, player)
        if goal == Goal.option_islands_completed:
            required = int(self.options.islands_required.value)
            return lambda state: state.has(ISLAND_COMPLETE, player, required)
        # all_three_stars: every star check must be reachable.
        star_locations = [self.multiworld.get_location(name, player)
                          for name, info in LOCATION_TABLE.items() if info.kind == Kind.THREE_STARS]
        return lambda state: all(loc.can_reach(state) for loc in star_locations)
