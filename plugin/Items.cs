using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using _MapMap.Scripts.Game.Gameplay.CharacterController;
using _MapMap.Scripts.Game.Gameplay.Tools;
using _MapMap.Scripts.Game.Gameplay.UI.Map;
using _MapMap.Scripts.Game.Gameplay.UI.Stickers;
using _MapMap.Scripts.Game.Structure.Messages.Tools;
using _MapMap.Scripts.Game.Structure.Savegame;
using WorldMapScreen = _MapMap.Scripts.Game.Gameplay.WorldMap.WorldMap;
using WorldMapIsland = _MapMap.Scripts.Game.Gameplay.WorldMap.WorldMapIsland;

namespace MapMapArchipelago;

// Applies received items to the game, and stops the game from handing out AP-controlled things itself.
//
// Every item is an on/off unlock (an island, a tool, Glide) or "the first N" of something (stickers,
// pencils), so applying the full received list again is always harmless. That's why the mod simply
// re-applies everything whenever something might have reset it (island load, map load, save load).
//
// Everything here only acts on an AP save (SaveRedirect.ActiveTag != null). On the player's normal
// save the gates let everything through, so vanilla play is unchanged.
public static class Items
{
    public static bool ApActive => SaveRedirect.ActiveTag != null;

    // What the player owns, rebuilt from the received item list by Recompute().
    private static readonly HashSet<int> ownedIslands = new() { ItemIds.StartingIsland };
    private static readonly HashSet<int> ownedToolIds = new();
    private static bool ownsGlide;
    private static int stickerCount, pencilCount;

    // Set while the mod itself is unlocking something, so our own gates let it through.
    private static bool granting;

    public static bool OwnsIsland(int islandNumber) => ownedIslands.Contains(islandNumber);

    // Rebuild ownership from the full list of received item IDs, then apply it to the game.
    public static void Recompute(IReadOnlyList<long> received)
    {
        ownedIslands.Clear();
        ownedIslands.Add(ItemIds.StartingIsland);
        ownedToolIds.Clear();
        ownsGlide = false;
        stickerCount = pencilCount = 0;

        foreach (long id in received)
        {
            if (ItemIds.IsIsland(id)) ownedIslands.Add(ItemIds.IslandNumber(id));
            else if (ItemIds.ToolItems.TryGetValue(id, out var toolIds)) ownedToolIds.UnionWith(toolIds);
            else if (id == ItemIds.Glide) ownsGlide = true;
            else if (id == ItemIds.Sticker) stickerCount++;
            else if (id == ItemIds.Pencil) pencilCount++;
            // Ink Blot (and anything unknown) does nothing.
        }

        Plugin.Log.LogInfo($"Items: islands [{string.Join(",", ownedIslands.OrderBy(n => n))}], " +
                           $"tool ids [{string.Join(",", ownedToolIds.OrderBy(n => n))}], glide {ownsGlide}, " +
                           $"stickers {stickerCount}, pencils {pencilCount}");
        ApplyAll(animate: true);
    }

    // Push the current ownership into whatever game objects exist right now.
    // Each part is skipped quietly if its game object isn't loaded (e.g. on the main menu).
    public static void ApplyAll(bool animate)
    {
        // On the main menu the game's tool/character objects aren't fully set up (applying there threw
        // a NullReferenceException in testing). Everything is applied once an island loads instead.
        if (!ApActive || SaveRedirect.OnMainMenu) return;
        ApplyTools();
        ApplyGlide();
        ApplyIslands(animate);
        ApplyStickers();
        ApplyPencils();
    }

    // ------------------------------------------------------------------ tools

    private static bool loggedToolList;

    // Unlock owned tools through each tool's own TriggerUnlockMessage(), which broadcasts the game's
    // "tool unlocked" message exactly like the story does. Several parts of the game listen for it
    // (ToolManager AND the tool wheel's buttons, UiToolOption). Calling ToolManager directly, as a first
    // version did, unlocked the Shovel but left it unusable in the wheel (tested 2026-10-01).
    //
    // KNOWN, HARMLESS: the first time after an island loads, the GAME's own unlock code throws a
    // NullReferenceException while unlocking the Drawing tool (id 5) outside its normal story moment. It
    // shows in the log as "During invoking native->managed trampoline". It's caught by the game's message
    // system, the tool still unlocks, and everything after it runs normally (verified in testing).
    private static void ApplyTools()
    {
        if (SaveRedirect.OnMainMenu) return;
        var tools = Resources.FindObjectsOfTypeAll<Tool>().Where(t => t.gameObject.scene.IsValid()).ToList();
        if (tools.Count == 0) return;
        LogToolListOnce(tools);
        var wanted = new HashSet<int>(ItemIds.AlwaysGivenToolIds.Concat(ownedToolIds));
        granting = true;
        try
        {
            foreach (var tool in tools)
            {
                if (!wanted.Contains(tool.id) || tool.isUnlocked) continue;
                tool.TriggerUnlockMessage();
                Plugin.Log.LogInfo($"Unlocked tool id {tool.id} ({tool.ToolName})");
            }
        }
        finally { granting = false; }
    }

    // One-time log of every tool the game has (confirmed 2026-10-01: ids match the ToolEnum values).
    private static void LogToolListOnce(List<Tool> tools)
    {
        if (loggedToolList) return;
        loggedToolList = true;
        foreach (var tool in tools)
            Plugin.Log.LogInfo($"Game tool: id={tool.id} name='{tool.ToolName}' unlocked={tool.isUnlocked}");
    }

    // ------------------------------------------------------------------ glide

    private static void ApplyGlide()
    {
        var character = Find<MapMapCharacterController>();
        if (character != null && ownsGlide && !character.glidingEnabled)
        {
            character.glidingEnabled = true;
            Plugin.Log.LogInfo("Glide enabled");
        }
    }

    // ------------------------------------------------------------------ islands

    private static void ApplyIslands(bool animate)
    {
        var map = Find<WorldMapScreen>();
        if (map == null) return;
        var toUnlock = Resources.FindObjectsOfTypeAll<WorldMapIsland>()
            .Where(i => i.gameObject.scene.IsValid() && !i.isUnlocked && OwnsIsland(i.islandID + 1))
            .ToList();
        // An unlock animation pans the map camera to the island and blocks input until it finishes. Running
        // many at once (e.g. 19 islands released after a goal) left the map stuck in testing (2026-10-02),
        // so only a single newly received island gets the animation.
        bool skipAnimation = !animate || toUnlock.Count > 1;
        granting = true;
        try
        {
            foreach (var island in toUnlock)
            {
                island.Unlock(map.newIslandSprite, skipAnimation, null);
                island.ShowDiscoveredIsland();
                Plugin.Log.LogInfo($"World map: unlocked Island {island.islandID + 1}{(skipAnimation ? "" : " (animated)")}");
            }
        }
        finally { granting = false; }
    }

    // ------------------------------------------------------------------ stickers & pencils

    private static bool stickerBoxOpenedThisLoad;

    // Each received Sticker unlocks the lowest-numbered sticker the player doesn't have yet. The choice
    // is recorded in the state file (GrantedStickerIds), so applying again re-unlocks the SAME stickers
    // instead of picking new ones - safe to repeat, never double-grants.
    // (A first version unlocked "the first N by ID", but those are the starter stickers every save
    // already has, so nothing new appeared - found in testing 2026-10-01.)
    private static void ApplyStickers()
    {
        if (stickerCount == 0) return;
        var manager = Find<UiStickerManager>();
        if (manager == null || manager.stickers == null)
        {
            Plugin.Log.LogInfo($"Stickers: {stickerCount} owned, but the sticker manager isn't loaded yet");
            return;
        }
        // The sticker box itself is normally opened by the story partway through the game (the
        // UnlockStickerbox component sends ActivateStickerMenuMessage). If the player has AP stickers,
        // open the box too, or the stickers would be unreachable. Once per island load.
        if (!stickerBoxOpenedThisLoad)
        {
            stickerBoxOpenedThisLoad = true;
            manager.ActivateStickerMenu(new _MapMap.Scripts.Game.Structure.Messages.UI.ActivateStickerMenuMessage(null));
            Plugin.Log.LogInfo("Opened the sticker box");
        }
        var granted = Plugin.Connection.ApState.GrantedStickerIds;
        if (granted.Count < stickerCount)
        {
            // UiSticker.Unlocked is the game's own "owned" flag (it's what the save stores). A first
            // version used object visibility instead, but every sticker counts as visible.
            var available = manager.stickers
                .Where(s => !s.Unlocked && !granted.Contains(s.StickerId))
                .Select(s => s.StickerId).OrderBy(id => id).ToList();
            granted.AddRange(available.Take(stickerCount - granted.Count));
            Plugin.Connection.ApState.Save();
        }
        foreach (var sticker in manager.stickers)
            if (granted.Contains(sticker.StickerId) && !sticker.Unlocked) manager.UnlockSticker(sticker.StickerId, true);
        Plugin.Log.LogInfo($"Stickers: {stickerCount} received, AP-granted ids [{string.Join(",", granted)}] of {manager.stickers.Length}");
    }

    private static void ApplyPencils()
    {
        if (pencilCount == 0) return;
        var pencilCase = Find<UiPencilCase>();
        if (pencilCase == null || pencilCase.pencils == null)
        {
            Plugin.Log.LogInfo($"Pencils: {pencilCount} owned, but the pencil case isn't loaded yet");
            return;
        }
        // Same approach as stickers: the lowest-numbered pencil not owned yet, recorded in the state file.
        var granted = Plugin.Connection.ApState.GrantedPencilIds;
        if (granted.Count < pencilCount)
        {
            var available = pencilCase.pencils
                .Where(p => !p.unlocked && !granted.Contains(p.pencilID))
                .Select(p => p.pencilID).OrderBy(id => id).ToList();
            granted.AddRange(available.Take(pencilCount - granted.Count));
            Plugin.Connection.ApState.Save();
        }
        foreach (var pencil in pencilCase.pencils)
            if (granted.Contains(pencil.pencilID) && !pencil.unlocked) pencilCase.UnlockPencil(pencil.pencilID, true);
        Plugin.Log.LogInfo($"Pencils: {pencilCount} received, AP-granted ids [{string.Join(",", granted)}] of {pencilCase.pencils.Length}");
    }

    // ------------------------------------------------------------------ helpers

    // The game's objects can be inactive (e.g. the world map while closed), which FindObjectOfType skips,
    // so search everything and keep only objects that live in a loaded scene (not prefab assets).
    private static T Find<T>() where T : Object =>
        Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(o => o is Component c && c.gameObject.scene.IsValid());

    // ------------------------------------------------------------------ gates (Harmony patches)
    // Each gate returns false to cancel the game's own call. They only block on an AP save, only things
    // the player doesn't own, and never the mod's own grants (`granting`).

    [HarmonyPatch]
    public static class Patches
    {
        // Tools: the island's story setup tries to unlock tools you'd have at this point in the vanilla story.
        [HarmonyPrefix, HarmonyPatch(typeof(ToolManager), nameof(ToolManager.OnUnlockTool))]
        private static bool ToolUnlock(ToolUnlockedMessage message)
        {
            if (!ApActive || granting || !ItemIds.ApControlledToolIds.Contains(message.toolID)) return true;
            if (ownedToolIds.Contains(message.toolID)) return true;
            Plugin.Log.LogInfo($"Blocked game tool unlock: id {message.toolID} (not received from AP)");
            return false;
        }

        // Tools are all locked when an island starts loading; give back what's owned on the NEXT frame.
        // Not immediately: ToolManager hears "lock all" first, and the tool wheel hears the same message
        // after it. Unlocking straight away meant the wheel processed our unlock and THEN the lock, so
        // owned tools showed greyed out with a lock icon (found in testing 2026-10-01).
        [HarmonyPostfix, HarmonyPatch(typeof(ToolManager), nameof(ToolManager.OnLockTools))]
        private static void ToolsLocked() => Plugin.Connection.RunNextFrame(ApplyTools);

        // Glide: Island 14's story turns it on.
        [HarmonyPrefix, HarmonyPatch(typeof(MapMapCharacterController), nameof(MapMapCharacterController.OnGliderActivated))]
        private static bool GliderActivated(ActivateGliderMessage message)
        {
            if (!ApActive || !message.enable || ownsGlide) return true;
            Plugin.Log.LogInfo("Blocked game Glide unlock (not received from AP)");
            return false;
        }

        // Glide: the save stores the flag; make it match AP when a save loads.
        [HarmonyPostfix, HarmonyPatch(typeof(MapMapCharacterController), nameof(MapMapCharacterController.LoadData))]
        private static void CharacterLoaded(MapMapCharacterController __instance)
        {
            if (ApActive) __instance.glidingEnabled = ownsGlide;
        }

        // Islands, "finished an island" path: WorldMap.UnlockIsland(next) BLOCKS MAP INPUT, then runs the island's
        // unlock animation, whose completion callback unblocks input. Cancelling only the animation (the
        // WorldMapIsland.Unlock gate below) left input blocked forever - the map froze after finishing an island
        // (testing 2026-10-02). So cancel the whole call up front, before input is blocked.
        [HarmonyPrefix, HarmonyPatch(typeof(WorldMapScreen), nameof(WorldMapScreen.UnlockIsland))]
        private static bool MapUnlockIsland(int islandID)
        {
            if (!ApActive || granting || OwnsIsland(islandID + 1)) return true;
            Plugin.Log.LogInfo($"Blocked game island unlock: Island {islandID + 1} (not received from AP)");
            return false;
        }

        // Islands, map-load path: WorldMap.LoadData unlocks everything up to your story position without touching input.
        [HarmonyPrefix, HarmonyPatch(typeof(WorldMapIsland), nameof(WorldMapIsland.Unlock))]
        private static bool IslandUnlock(WorldMapIsland __instance) => AllowIsland(__instance, "unlock");

        [HarmonyPrefix, HarmonyPatch(typeof(WorldMapIsland), nameof(WorldMapIsland.ShowDiscoveredIsland))]
        private static bool IslandDiscovered(WorldMapIsland __instance) => AllowIsland(__instance, null);

        private static bool AllowIsland(WorldMapIsland island, string logAction)
        {
            if (!ApActive || granting || OwnsIsland(island.islandID + 1)) return true;
            if (logAction != null) Plugin.Log.LogInfo($"Blocked game island {logAction}: Island {island.islandID + 1} (not received from AP)");
            return false;
        }

        // After the map builds itself from the save, add every island owned from AP.
        [HarmonyPostfix, HarmonyPatch(typeof(WorldMapScreen), nameof(WorldMapScreen.LoadData))]
        private static void MapLoaded() => ApplyIslands(animate: false);

        // After an island finishes loading (its sticker case, pencil case, character and tools now exist), re-apply.
        [HarmonyPostfix, HarmonyPatch(typeof(_MapMap.Scripts.Game.Structure.Missions.MissionManager),
                                      nameof(_MapMap.Scripts.Game.Structure.Missions.MissionManager.OnIslandLoaded))]
        private static void IslandLoaded()
        {
            stickerBoxOpenedThisLoad = false;
            ApplyAll(animate: false);
        }

        // After any save loads (stickers, pencils, the character...), re-apply everything.
        [HarmonyPostfix, HarmonyPatch(typeof(SavegameManager), nameof(SavegameManager.SavegameLoaded))]
        private static void SaveLoaded() => ApplyAll(animate: false);
    }
}
