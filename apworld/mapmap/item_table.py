"""
Every Map Map item, with its ID, classification and how many go in the pool.

Item IDs use the same BASE_ID as locations (items and locations are separate ID spaces in
Archipelago, so sharing the base is fine):

    BASE_ID + 1..20     Island 1..20 unlocks
    BASE_ID + 101..     tools and abilities
    BASE_ID + 201..     cosmetic filler (stickers, pencils)
    BASE_ID + 501       generic filler

Classification is written as a plain string here ("progression" / "useful" / "filler") and turned
into Archipelago's ItemClassification when the world is built. That keeps this file importable
without Archipelago (see tools/check_tables.py).

Plain Python, no Archipelago imports.
"""
from dataclasses import dataclass

from .location_table import BASE_ID

# Island 1 is where the game starts, so it's given at the start rather than shuffled.
STARTING_ISLAND = 1


@dataclass(frozen=True)
class ItemInfo:
    code: int
    classification: str   # "progression" | "useful" | "filler"
    count: int = 1         # copies placed in the pool (filler counts are adjusted at generation)
    game_id: str = ""      # what the plugin uses to grant it (tool enum name, ability, etc.)


def _islands() -> dict[str, ItemInfo]:
    return {
        f"Island {n}": ItemInfo(BASE_ID + n, "progression", game_id=f"island:{n - 1}")
        for n in range(1, 21)
    }


# game_id for tools = the game's ToolEnum name (see DESIGN.md 3.2).
# The basic map tools (Drawing, CountSteps, Grid, ScaleReference, CompassRose) are NOT items:
# the plugin always gives them, because randomizing them makes early missions miserable.
TOOLS: dict[str, ItemInfo] = {
    # Hard-required for every buried treasure and treasure mission.
    "Shovel":     ItemInfo(BASE_ID + 101, "progression", game_id="tool:Shovel"),
    # Helpful-tool logic for Islands 7-20 (option). The plugin also grants DividerDistanceText with it.
    "Dividers":   ItemInfo(BASE_ID + 102, "progression", game_id="tool:Dividers"),
    # Island 10's "take a picture of the map" story step needs it (DESIGN.md decision 7).
    "Camera":     ItemInfo(BASE_ID + 103, "progression", game_id="tool:Camera"),
    # Hard-required for all checks on the swamp Islands 12-16 (decision 11).
    "Build Tool": ItemInfo(BASE_ID + 104, "progression", game_id="tool:BuildTool"),
    # Helpful-tool logic for Islands 17-20 (option).
    "Telescope":  ItemInfo(BASE_ID + 105, "progression", game_id="tool:Telescope"),
    # Helpful on every island but never required, so it's kept out of logic (DESIGN.md decision 14).
    "Compass":    ItemInfo(BASE_ID + 106, "useful", game_id="tool:Compass"),
    # Normally given on Island 19, set 4. Nothing strictly needs it, so it's useful, not progression.
    "Sextant":    ItemInfo(BASE_ID + 107, "useful", game_id="tool:Sextant"),
    # An ability rather than a tool. Hard-required for Floor Is Lava on Islands 14-20 (decision 12).
    "Glide":      ItemInfo(BASE_ID + 108, "progression", game_id="ability:Glide"),
}

# Cosmetic unlocks the game normally hands out from treasures (counts from probe v0.2).
# The plugin grants "the next sticker/pencil you don't have yet", so these don't need individual IDs.
COSMETICS: dict[str, ItemInfo] = {
    "Sticker": ItemInfo(BASE_ID + 201, "filler", count=43, game_id="sticker:next"),
    "Pencil":  ItemInfo(BASE_ID + 202, "filler", count=6, game_id="pencil:next"),
}

# Fills whatever slots remain after everything else is placed. Does nothing in-game.
FILLER_NAME = "Ink Blot"
FILLER: dict[str, ItemInfo] = {
    FILLER_NAME: ItemInfo(BASE_ID + 501, "filler", count=0, game_id="nothing"),
}

ITEM_TABLE: dict[str, ItemInfo] = {**_islands(), **TOOLS, **COSMETICS, **FILLER}
