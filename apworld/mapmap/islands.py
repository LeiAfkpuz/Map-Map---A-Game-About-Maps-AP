"""
Island data for Map Map, transcribed from the probe dump (BepInEx/mapmap_probe_dump.txt, 2026-09-30).

This file is plain Python with no Archipelago imports, so tools/check_tables.py can load and print it
without an Archipelago install.

Numbering:
  * Island numbers here are 1-20, as players see them.  In the game's code the same island is
    "missionUnit" / "islandID" (island - 1), e.g. Island 16 = unit 15.
  * Each island has mission sets; a set holds 1+ missions.  The game's island panel numbers them
    "2" for a set with one mission and "2.1", "2.2" for a set with several.  `display` stores that
    string so location names match what the player sees on screen.
"""
from dataclasses import dataclass, field


@dataclass(frozen=True)
class Mission:
    set_index: int        # 0-based set index in the game (missionUnits[u].missionSets[set_index])
    mission_index: int    # 0-based mission index inside that set
    display: str          # panel numbering, e.g. "2.3"
    internal_name: str    # MissionSO.uniqueName, used by the plugin to identify the mission
    text: str             # the game's mission description (for trackers/docs; not used in names)
    treasure: bool = False  # treasure missions: need the Shovel, and have no star rating
    goal: bool = False      # the ending trigger; becomes the goal event instead of a location


@dataclass(frozen=True)
class Island:
    number: int           # 1-20
    scene: str            # Unity scene name (matches IslandManager.allIslands)
    missions: list[Mission] = field(default_factory=list)
    treasures: int = 0    # buried treasures (TreasureCounter total for this island)
    landmarks: int = 0
    floor_is_lava: int = 0


def _single(set_index: int, internal_name: str, text: str, **flags) -> Mission:
    """A set containing exactly one mission: displayed as "<set+1>"."""
    return Mission(set_index, 0, str(set_index + 1), internal_name, text, **flags)


def _multi(set_index: int, entries: list[tuple[str, str]]) -> list[Mission]:
    """A set with several missions: displayed as "<set+1>.<mission+1>"."""
    return [
        Mission(set_index, i, f"{set_index + 1}.{i + 1}", name, text)
        for i, (name, text) in enumerate(entries)
    ]


ISLANDS: list[Island] = [
    Island(1, "1_Island Small", [
        _single(0, "Mission1_1", "Mark the tree on the map"),
    ]),
    Island(2, "2_Island Round", [
        _single(0, "Mission2_1", "Mark the sandcastle on the beach"),
    ], landmarks=1),
    Island(3, "island_03_midSize_New", [
        _single(0, "Mission3_1", "Mark the stones piled on top of each other"),
        _single(1, "Mission3_2", "Mark the cartoglyph"),
        _single(2, "Mission3_3", "Mark the rabbit burrow on the east beach"),
    ], landmarks=1),
    Island(4, "island_04_firstRuinEncounter", [
        _single(0, "Mission4_1", "Mark the cluster of starfish by the ruin"),
        _single(1, "Mission4_2", "Mark the ruin in the north"),
        _single(2, "Mission4_3", "Follow the treasure map in the ruin and dig up the treasure", treasure=True),
    ], landmarks=1),
    Island(5, "Island_05_gridRuins", [
        _single(0, "Mission5_1", "Mark the flower that is not white or yellow"),
        _single(1, "Mission5_2", "Mark the corner of the ruin by the dead tree"),
        _single(2, "Mission5_3", "Mark the cartoglyph"),
        _single(3, "Mission5_4", "Mark the ruin that's 3x2 steps long"),
        _single(4, "Mission5_5", "Dig up the treasure 13 steps north and 7 steps west of the tower", treasure=True),
    ], treasures=3, landmarks=1),
    Island(6, "island_06_dividerOnboarding", [
        _single(0, "Mission_Divider1_1", "Mark the highest point with flowers"),
        _single(1, "Mission_Divider1_2", "Mark the wooden planks on the beach"),
        _single(2, "Mission_Divider1_3", "Mark the stone basket with the hammer"),
    ], treasures=1, landmarks=1),
    Island(7, "Island_07_Divider2", [
        *_multi(0, [
            ("Mission_Divider2_1_1", "Mark the red rosebush"),
            ("Mission_Divider2_1_2", "Mark the red poppy flower in the grass"),
            ("Mission_Divider2_1_3", "Mark the red hanging flowers"),
        ]),
    ], treasures=3, landmarks=1),
    Island(8, "Island_08_Divider3_Zwiwa", [
        *_multi(0, [
            ("Mission_Divider3_1_1", "Mark the school of fish near the basalt rocks"),
            ("Mission_Divider3_1_2", "Mark the missing statue"),
        ]),
    ], treasures=2),
    Island(9, "Island_09_Divider4_Tower", [
        _single(0, "Mission_Divider4_1", "Mark the tall tower"),
        *_multi(1, [
            ("Mission_Divider4_2_1", "Mark the new pink flower type"),
            ("Mission_Divider4_2_2", "Mark the X under the bridge"),
            ("Mission_Divider4_2_3", "Mark the meadow where the deers graze"),
        ]),
    ], treasures=3),
    Island(10, "Island_10_Divider5_CityMap", [
        _single(0, "Mission_Divider5_1", "Mark the city map"),
        *_multi(1, [
            ("Mission_Divider5_2_1", "Mark the tower ruin"),
            ("Mission_Divider5_2_2", "Mark this house"),
            ("Mission_Divider5_2_3", "Mark the smithy"),
        ]),
    ], treasures=4),
    Island(11, "Island_11_Ankorwat1", [
        _single(0, "Mission_Divider6_1", "Mark the stone with the motif of a broken pickaxe"),
        _single(1, "Mission_Divider6_2", "Mark the broken statue"),
        _single(2, "Mission_Divider6_3", "Mark the cartoglyph"),
        _single(3, "Mission_Divider6_4", "Dig up the treasure in the goddess's garden", treasure=True),
    ], treasures=2),
    Island(12, "Island_12_Swamp1", [
        _single(0, "Mission_Swamp1_1", "Mark the new tree species"),
    ], treasures=1, landmarks=1),
    Island(13, "Island_13_Swamp2", [
        _single(0, "Mission_Swamp2_1", "Mark the square rice paddy in the water"),
        _single(1, "Mission_Swamp2_2", "Mark the broken flag pole"),
    ], treasures=2, floor_is_lava=2),
    Island(14, "Island_14_Swamp_3", [
        _single(0, "Mission_Swamp3_1", "Mark the raven flower"),
        _single(1, "Mission_Swamp3_2", "Mark the cartoglyph"),
        _single(2, "Mission_Swamp3_3", "Place the island piece at the right spot"),
    ], treasures=2, landmarks=1, floor_is_lava=2),
    Island(15, "Island_15_Swamp_4_CityMap", [
        *_multi(0, [
            ("Mission_Swamp4_1_1", "Place the island piece at the right spot"),
            ("Mission_Swamp4_1_2", "Place the island piece at the right spot"),
        ]),
        _single(1, "Mission_Swamp4_2", "Mark the middle between the two kings"),
        _single(2, "Mission_Swamp4_3", "Dig up the treasure in the middle of the kings", treasure=True),
    ], treasures=3, landmarks=1, floor_is_lava=3),
    Island(16, "Island_16_Swamp5_Holz", [
        *_multi(0, [
            ("Mission_Swamp5_1_1", "Mark the vessel filled with water"),
            ("Mission_Swamp5_1_2", "Mark the goddess's shrine"),
            ("Mission_Swamp5_1_3", "Mark the center of the tallest tree stump"),
            ("Mission_Swamp5_1_4", "Mark this tree stump"),
        ]),
    ], treasures=1, landmarks=1, floor_is_lava=2),
    Island(17, "Island_17_Ankorwat2", [
        *_multi(0, [
            ("Mission_Swamp6_1_1", "Place the island piece in the right spot"),
            ("Mission_Swamp6_1_2", "Mark the stone with the motif of a raven chopping wood"),
            ("Mission_Swamp6_1_3", "Mark the tree growing on the second floor"),
        ]),
        _single(1, "Mission_Swamp6_2", "Dig up the treasure 22 steps from the tower and 14 steps from the wood-chopping raven", treasure=True),
    ], treasures=2, landmarks=1, floor_is_lava=1),
    Island(18, "Island_18_Compass1", [
        _single(0, "Mission_Compass1_1", "Mark the fox den"),
        *_multi(1, [
            ("Mission_Compass1_2_1", "Mark the circle of red mushrooms"),
            ("Mission_Compass1_2_2", "Place the following house"),
            ("Mission_Compass1_2_3", "Mark the tallest monument"),
        ]),
    ], treasures=3, floor_is_lava=3),
    Island(19, "Island_19_Compass2_CityMap", [
        _single(0, "Mission_Compass3_1", "Mark the city map"),
        *_multi(1, [
            ("Mission_Compass3_2_1", "Mark the smallest hot spring"),
            ("Mission_Compass3_2_2", "Mark the trapdoor leading to the warehouse basement"),
            ("Mission_Compass3_2_3", "Mark the fire pit"),
        ]),
        _single(2, "Mission_Compass3_3", "Place a flag in the center of the city and dig up the treasure at the end of its shadow", treasure=True),
        *_multi(3, [
            ("Mission_Compass3_4_1", "Mark the peak of the highest mountain"),
            ("Mission_Compass3_4_2", "Mark the center of the crater"),
        ]),
    ], treasures=4, floor_is_lava=3),
    Island(20, "Island_20_Ankorwat3", [
        *_multi(0, [
            ("Mission_Compass4_1_1", "Place the island piece in the right spot"),
            ("Mission_Compass4_1_2", "Place the island piece in the right spot"),
            ("Mission_Compass4_1_3", "Place the island piece in the right spot"),
        ]),
        *_multi(1, [
            ("Mission_Compass4_2_1", "Place the island piece in the right spot"),
            ("Mission_Compass4_2_2", "Place the island piece in the right spot"),
        ]),
        _single(2, "Mission_Compass4_3", "Find the Raven Kings' treasure using your map", treasure=True),
        # Flagged excludeFromCount in the game; entering the cave plays the ending and credits.
        _single(3, "Mission_Compass4_4", "Enter the Raven King's Treasure Cave", treasure=True, goal=True),
    ], treasures=4, landmarks=1, floor_is_lava=2),
]
