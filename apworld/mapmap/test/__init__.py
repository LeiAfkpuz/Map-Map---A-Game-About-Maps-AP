"""
Map Map's own tests, run with Archipelago's test runner (see tools/run_ap_tests.py).

WorldTestBase builds a fresh seed for each test class (using that class's `options`) and also runs
Archipelago's standard checks on it automatically (everything reachable with all items, something
reachable with no items, the fill succeeds).
"""
from test.bases import WorldTestBase


class MapMapTestBase(WorldTestBase):
    game = "Map Map - A Game About Maps"
