using System.Collections.Generic;

namespace MapMapArchipelago;

// Item IDs, matching apworld/mapmap/item_table.py (BASE_ID + offset). If one side changes, so must the other.
public static class ItemIds
{
    public const long BaseId = 6_260_000;

    // Island N (1-20) = BaseId + N. The game numbers islands from 0, so game islandID = N - 1.
    public static bool IsIsland(long id) => id > BaseId && id <= BaseId + 20;
    public static int IslandNumber(long id) => (int)(id - BaseId);

    public const long Sticker = BaseId + 201;
    public const long Pencil = BaseId + 202;
    public const long Glide = BaseId + 108;
    public const long InkBlot = BaseId + 501;

    // AP tool item -> the game's tool id(s) (the ToolEnum value). Dividers also bring the distance readout.
    public static readonly Dictionary<long, int[]> ToolItems = new()
    {
        { BaseId + 101, new[] { 7 } },        // Shovel
        { BaseId + 102, new[] { 8, 10 } },    // Dividers + DividerDistanceText
        { BaseId + 103, new[] { 84 } },       // Camera
        { BaseId + 104, new[] { 20 } },       // Build Tool
        { BaseId + 105, new[] { 6 } },        // Telescope
        { BaseId + 106, new[] { 1 } },        // Compass
        { BaseId + 107, new[] { 9 } },        // Sextant
    };

    // Every game tool id controlled by AP (only unlocked when the matching item is owned).
    public static readonly HashSet<int> ApControlledToolIds = new() { 7, 8, 10, 84, 20, 6, 1, 9 };

    // Basic map tools: CountSteps, CompassRose, Grid, ScaleReference, Drawing. Always given (DESIGN.md 3.2).
    public static readonly int[] AlwaysGivenToolIds = { 0, 2, 3, 4, 5 };

    public const int StartingIsland = 1;
}
