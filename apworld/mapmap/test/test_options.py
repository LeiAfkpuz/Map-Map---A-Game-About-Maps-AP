"""
Option combinations: each class generates a seed with those options (the standard reachability and
fill checks run on it too), plus the specific assertions below.
"""
from ..location_table import LOCATION_TABLE, Kind
from ..world import ISLAND_COMPLETE
from . import MapMapTestBase


def _count(world_test, kind: Kind) -> int:
    names = {loc.name for loc in world_test.multiworld.get_locations(world_test.player)}
    return sum(1 for name, info in LOCATION_TABLE.items() if info.kind == kind and name in names)


class TestAllChecksOn(MapMapTestBase):
    def test_location_counts(self) -> None:
        self.assertEqual(_count(self, Kind.MISSION), 68)
        self.assertEqual(_count(self, Kind.THREE_STARS), 61)
        self.assertEqual(_count(self, Kind.TREASURE), 40)
        self.assertEqual(_count(self, Kind.FLOOR_IS_LAVA), 18)
        self.assertEqual(_count(self, Kind.LANDMARK), 12)

    def test_island_1_mission_not_prefilled(self) -> None:
        # Star checks on -> no pre-fill: Island 1's mission is filled normally (not a locked item).
        location = self.multiworld.get_location("Island 1 - Mission 1", self.player)
        self.assertFalse(location.locked)


class TestMinimalSolo(MapMapTestBase):
    options = {
        "goal": "islands_completed",
        "islands_required": 20,
        "star_checks": False,
        "treasure_checks": False,
        "floor_is_lava_checks": False,
    }

    def test_optional_checks_removed(self) -> None:
        self.assertEqual(_count(self, Kind.THREE_STARS), 0)
        self.assertEqual(_count(self, Kind.TREASURE), 0)
        self.assertEqual(_count(self, Kind.FLOOR_IS_LAVA), 0)
        self.assertEqual(_count(self, Kind.MISSION), 68)

    def test_solo_prefill_gives_tool_free_island(self) -> None:
        # DESIGN.md decision 16: solo + star checks off -> Island 1's mission holds an island
        # that needs no tools (Islands 2-6 with helpful tool logic on).
        item = self.multiworld.get_location("Island 1 - Mission 1", self.player).item
        self.assertIn(item.name, [f"Island {n}" for n in range(2, 7)])

    def test_needs_all_twenty_islands(self) -> None:
        # Leave out the "Island Complete" events too, or collect_all_but would hand over all 20 of them.
        self.collect_all_but(["Island 20", ISLAND_COMPLETE])
        self.assertBeatable(False)
        self.collect_by_name(["Island 20"])
        self.assertBeatable(True)


class TestOneIslandGoal(MapMapTestBase):
    options = {"goal": "islands_completed", "islands_required": 1}

    def test_beatable_from_start(self) -> None:
        # Island 1's single mission needs nothing, so completing 1 island is possible immediately.
        self.assertBeatable(True)


class TestAllStarsGoalForcesStarChecks(MapMapTestBase):
    options = {"goal": "all_three_stars", "star_checks": False}

    def test_star_checks_forced_on(self) -> None:
        self.assertEqual(int(self.world.options.star_checks.value), 1)
        self.assertEqual(_count(self, Kind.THREE_STARS), 61)
