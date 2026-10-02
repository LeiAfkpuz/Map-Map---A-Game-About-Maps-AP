"""
One test per logic rule in rules.py / DESIGN.md.

assertAccessDependency(locations, [[items]]) collects every item EXCEPT the listed ones and asserts that
exactly `locations` are unreachable while every other location is reachable; then it adds the items
and asserts `locations` become reachable. So each test proves the rule hits exactly the right checks,
no more and no fewer. The expected sets are computed from the island data (not hand-typed), so the
tests cross-check rules.py against islands.py for every check in the game.
"""
from ..location_table import LOCATION_TABLE, Kind
from ..world import CAVE_EVENT, VICTORY
from . import MapMapTestBase


def _names(world_test, predicate) -> list[str]:
    """Names of this seed's check locations whose LocationInfo matches `predicate`."""
    present = {loc.name for loc in world_test.multiworld.get_locations(world_test.player)}
    return [name for name, info in LOCATION_TABLE.items() if name in present and predicate(info)]


def _on_islands(world_test, islands, include_cave: bool) -> list[str]:
    names = _names(world_test, lambda info: info.island in islands)
    return names + ([CAVE_EVENT] if include_cave else [])


class TestDefaultRules(MapMapTestBase):
    # Default options: all check types on, helpful_tool_logic on, goal = Island 20 cave.

    def test_shovel(self) -> None:
        # Every buried treasure, every treasure mission, and the cave (right after Island 20's treasure mission).
        expected = _names(self, lambda i: i.kind == Kind.TREASURE
                          or (i.kind == Kind.MISSION and i.mission.treasure)) + [CAVE_EVENT]
        self.assertAccessDependency(expected, [["Shovel"]])

    def test_build_tool(self) -> None:
        # Everything on the swamp Islands 12-16 (decision 11). Island 17 is not included.
        self.assertAccessDependency(_on_islands(self, range(12, 17), False), [["Build Tool"]])

    def test_camera(self) -> None:
        # Island 10, second set onward, missions and their star checks (decision 7).
        expected = _names(self, lambda i: i.island == 10 and i.mission is not None and i.mission.set_index >= 1)
        self.assertAccessDependency(expected, [["Camera"]])

    def test_glide(self) -> None:
        # Floor Is Lava on Islands 14-20 (decision 12). Island 13's are not included.
        expected = _names(self, lambda i: i.kind == Kind.FLOOR_IS_LAVA and i.island >= 14)
        self.assertAccessDependency(expected, [["Glide"]])

    def test_helpful_dividers(self) -> None:
        # helpful_tool_logic: everything on Islands 7-20, including the cave.
        self.assertAccessDependency(_on_islands(self, range(7, 21), True), [["Dividers"]])

    def test_helpful_telescope(self) -> None:
        # helpful_tool_logic: everything on Islands 17-20, including the cave.
        self.assertAccessDependency(_on_islands(self, range(17, 21), True), [["Telescope"]])

    def test_each_island_item(self) -> None:
        # Each Island N item gates exactly that island's checks (plus the cave for Island 20).
        for n in range(2, 21):
            with self.subTest(island=n):
                self.assertAccessDependency(_on_islands(self, [n], n == 20), [[f"Island {n}"]])

    def test_compass_and_sextant_never_required(self) -> None:
        self.collect_all_but(["Compass", "Sextant"])
        for location in self.multiworld.get_locations(self.player):
            self.assertTrue(location.can_reach(self.multiworld.state), location.name)

    def test_goal_needs_island_20(self) -> None:
        # collect_all_but also hands over pre-placed EVENT items, including "Victory" itself, which
        # would win instantly. Leave it out so the test has to actually reach the cave to win.
        self.collect_all_but(["Island 20", VICTORY])
        self.assertBeatable(False)
        self.collect_by_name(["Island 20"])
        self.assertBeatable(True)


class TestHelpfulToolLogicOff(MapMapTestBase):
    options = {"helpful_tool_logic": False}

    def test_dividers_and_telescope_not_required(self) -> None:
        self.collect_all_but(["Dividers", "Telescope"])
        for location in self.multiworld.get_locations(self.player):
            self.assertTrue(location.can_reach(self.multiworld.state), location.name)
