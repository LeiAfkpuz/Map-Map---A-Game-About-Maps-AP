from dataclasses import dataclass

from Options import Choice, DeathLink, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range


class Goal(Choice):
    """
    What you need to do to finish your game.

    island_20: Enter the Raven King's Treasure Cave on Island 20 (the game's ending and credits).
    islands_completed: Complete every mission on a number of islands (see Islands Required).
    all_three_stars: Earn a 3-star rating on every mission that has one. Forces Star Checks on.
    """
    display_name = "Goal"
    option_island_20 = 0
    option_islands_completed = 1
    option_all_three_stars = 2
    default = 0


class IslandsRequired(Range):
    """
    Only used with the islands_completed goal: how many islands you must fully complete
    (every mission on the island; treasures, Floor Is Lava and landmarks don't count).
    """
    display_name = "Islands Required"
    range_start = 1
    range_end = 20
    default = 10


class StarChecks(DefaultOnToggle):
    """
    Adds a check for earning a 3-star rating on each mission (61 checks).
    Treasure-digging missions have no rating and never get a star check.
    """
    display_name = "3-Star Checks"


class TreasureChecks(DefaultOnToggle):
    """
    Adds a check for digging up each buried treasure (40 checks). Needs the Shovel.
    Treasure-digging MISSIONS are always checks; this only covers the optional buried treasures.
    """
    display_name = "Treasure Checks"


class FloorIsLavaChecks(DefaultOnToggle):
    """
    Adds a check for completing each Floor Is Lava challenge (18 checks).
    The ones on Islands 14-20 need Glide, and the later ones can be tough.
    """
    display_name = "Floor Is Lava Checks"


class HelpfulToolLogic(DefaultOnToggle):
    """
    Tools that are never strictly needed but make some islands much easier are put into logic:
    Dividers for Islands 7-20, Telescope for Islands 17-20.
    Turn this off if you're comfortable playing those islands without them.
    (Required tools - Shovel, Build Tool, Camera, Glide - are always in logic.)
    """
    display_name = "Helpful Tool Logic"


class MapMapDeathLink(DeathLink):
    """
    When you drown, everyone else with Death Link dies too; when someone else dies, the island you're on reloads
    and you're sent back to its starting point (your progress is kept). Careful if you're mid-measurement!
    """


@dataclass
class MapMapOptions(PerGameCommonOptions):
    goal: Goal
    islands_required: IslandsRequired
    star_checks: StarChecks
    treasure_checks: TreasureChecks
    floor_is_lava_checks: FloorIsLavaChecks
    helpful_tool_logic: HelpfulToolLogic
    death_link: MapMapDeathLink


option_groups = [
    OptionGroup("Goal", [Goal, IslandsRequired]),
    OptionGroup("Checks", [StarChecks, TreasureChecks, FloorIsLavaChecks]),
    OptionGroup("Logic", [HelpfulToolLogic]),
    OptionGroup("Death Link", [MapMapDeathLink]),
]
