using System.Linq;
using HarmonyLib;
using UnityEngine;
using _MapMap.Scripts.Game.Gameplay.UI;
using _MapMap.Scripts.Game.Structure.Missions;
using WorldMapIsland = _MapMap.Scripts.Game.Gameplay.WorldMap.WorldMapIsland;

namespace MapMapArchipelago;

// Lets the player play islands in any order, each in normal story mode.
//
// The game keeps ONE story position: progressMissionUnit (which island) + progressMissionIndex (which
// mission set on it). The mod keeps a set position for EVERY island in the state file
// (ApState.IslandSetProgress) and, whenever an island is loaded, points the game's story position at that
// island and its saved set. The probe tests (2026-09-30) proved that setting the pointer and then calling
// LoadCurrentMission plays any island in story mode, with dialogue and chained missions.
//
// Island numbers here are 1-20; the game's "unit" index is island - 1.
public static class Travel
{
    // True while the mod itself is calling the game's travel/load methods, so our own patches let it through.
    private static bool redirecting;

    private static ApState State => Plugin.Connection.ApState;

    private static int SetCount(MissionManager mm, int island) => mm.missionUnits[island - 1].missionSets.Count;

    public static int SavedSet(int island) => State.IslandSetProgress.TryGetValue(island, out int set) ? set : 0;

    public static bool IsComplete(MissionManager mm, int island) => SavedSet(island) >= SetCount(mm, island);

    // Point the game's single story position at this island and the set we saved for it.
    //
    // Deliberately NOT setting currentMissionUnit (the probe did): LoadMissionUnit first sends
    // LeaveMissionMessage for the island currentMissionUnit points at - the island being LEFT - and each
    // island's story script (LevelSequenceN) tears itself down on that message. Setting it to the
    // destination early meant the left island's script never tore down, and its tutorial/"new tool" popups
    // kept firing on later islands (found in testing 2026-10-02). LoadMissionUnit updates it itself.
    private static void PointStoryAt(MissionManager mm, int island)
    {
        int unit = island - 1, set = SavedSet(island);
        mm.progressMissionUnit = unit;
        mm.progressMissionIndex = set;
        mm.missionUnits[unit].currentMissionSet = set;
        Plugin.Log.LogInfo($"Story position -> Island {island}, set {set + 1} of {SetCount(mm, island)}");
    }

    private static void RecordSet(int island, int set, string why)
    {
        if (SavedSet(island) >= set) return;
        State.IslandSetProgress[island] = set;
        State.Save();
        Plugin.Log.LogInfo($"Island {island}: progress -> set index {set} ({why})");
        Goal.Check();  // completing an island can complete the islands_completed goal
    }

    [HarmonyPatch]
    public static class Patches
    {
        // World map travel. With no specific mission (missionSetIndex < 0) the game would use FreeRoam for any
        // island other than its story island. Instead: unfinished owned island -> story mode at its saved set.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.TravelToIsland))]
        private static bool TravelToIsland(MissionManager __instance, bool save, int missionUnitIndex,
                                           int missionSetIndex, bool forceCustomMission)
        {
            if (!Items.ApActive || redirecting || missionSetIndex >= 0 || forceCustomMission) return true;
            int island = missionUnitIndex + 1;

            if (!Items.OwnsIsland(island))
            {
                // The map only offers owned islands, so this would be the game trying to move on by itself.
                Plugin.Log.LogWarning($"Blocked travel to Island {island} (not received from AP)");
                return false;
            }
            if (IsComplete(__instance, island))
            {
                Plugin.Log.LogInfo($"Travel to Island {island}: already complete, free roam");
                return true;  // the game's normal FreeRoam revisit (treasures, Floor Is Lava, etc. still work)
            }

            Plugin.Log.LogInfo($"Travel to Island {island}: story mode");
            PointStoryAt(__instance, island);
            redirecting = true;
            try { __instance.LoadCurrentMission(save); }
            finally { redirecting = false; }
            return false;
        }

        // The world map's travel button. Vanilla: a picked mission -> replay it; the game's story island ->
        // LoadCurrentMission; any other island -> TravelToIsland (FreeRoam). In AP every island is "a story
        // island", so with no mission picked we always go through TravelToIsland above, which makes the one
        // story-mode-or-free-roam decision. Replaying a specific picked mission stays vanilla.
        [HarmonyPrefix, HarmonyPatch(typeof(WorldMapIsland), nameof(WorldMapIsland.LoadIsland))]
        private static bool MapTravel(WorldMapIsland __instance)
        {
            if (!Items.ApActive || MissionManager.Instance == null) return true;
            if (__instance.infoPanel != null && __instance.infoPanel.GetSelectedMission() != null) return true;

            // Vanilla hides the island's highlight and shine first; keep that.
            __instance.selectHighlight?.SetActive(false);
            __instance.shine?.SetActive(false);
            MissionManager.Instance.TravelToIsland(true, __instance.islandID);
            return false;
        }

        // Continue loads "the current story position". If that's an island the player doesn't own, or one
        // that's finished, pick a better one first.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.LoadCurrentMission))]
        private static bool LoadCurrentMission(MissionManager __instance, bool save)
        {
            if (!Items.ApActive || redirecting) return true;

            // After the ending credits the game sets its story position one PAST the last island (its "game
            // finished" marker) and calls this; vanilla then free-roams Island 20. Leave that alone: redirecting
            // to another island in the middle of the ending crashed in the game's save code and left the player
            // on an endless loading screen (testing 2026-10-02).
            if (__instance.progressMissionUnit >= __instance.missionUnits.Count)
            {
                Plugin.Log.LogInfo("Load: game-finished marker after the ending - letting the game handle it");
                return true;
            }

            int island = __instance.progressMissionUnit + 1;

            // The save's own set index is the most up-to-date for the island it was last saved on.
            if (Items.OwnsIsland(island)) RecordSet(island, __instance.progressMissionIndex, "from save");

            if (Items.OwnsIsland(island) && !IsComplete(__instance, island))
            {
                PointStoryAt(__instance, island);
                return true;
            }

            // Otherwise: the lowest-numbered owned island that isn't finished yet.
            int? target = Enumerable.Range(1, 20)
                .Where(n => Items.OwnsIsland(n) && !IsComplete(__instance, n))
                .Select(n => (int?)n).FirstOrDefault();
            if (target != null)
            {
                Plugin.Log.LogInfo($"Load: Island {island} is {(Items.OwnsIsland(island) ? "complete" : "not owned")}, " +
                                   $"loading Island {target} instead");
                PointStoryAt(__instance, target.Value);
                return true;
            }

            // Every owned island is finished: revisit Island 1 in free roam and let the player pick from the map.
            Plugin.Log.LogInfo("Load: all owned islands complete, free roam on Island 1");
            redirecting = true;
            try { __instance.TravelToIsland(save, ItemIds.StartingIsland - 1); }
            finally { redirecting = false; }
            return false;
        }

        // Record progress whenever a mission set starts in story mode, so leaving and coming back resumes there.
        [HarmonyPostfix, HarmonyPatch(typeof(MissionUnit), nameof(MissionUnit.StartMissionSet))]
        private static void MissionSetStarted(int setIndex)
        {
            var mm = MissionManager.Instance;
            if (!Items.ApActive || mm == null || mm.currentMissionState != MissionManager.MissionState.Progression) return;
            RecordSet(mm.currentMissionUnit + 1, setIndex, "set started");
        }

        // A mission was completed successfully (the game sets its "completed" flag here; misses don't get
        // here). Remember it, since the game forgets it whenever the set restarts.
        [HarmonyPostfix, HarmonyPatch(typeof(MissionSet), nameof(MissionSet.MissionFinished))]
        private static void MissionCompleted(MissionSet __instance)
        {
            var mm = MissionManager.Instance;
            if (!Items.ApActive || mm == null || mm.currentMissionState != MissionManager.MissionState.Progression) return;
            string name = __instance.missions[__instance.currentMission].missionSO?.uniqueName;
            if (name != null && State.CompletedMissions.Add(name))
            {
                State.Save();
                Plugin.Log.LogInfo($"Mission completed: {name} (Island {mm.currentMissionUnit + 1})");
                Goal.Check();  // the Raven King's cave mission is the island_20 goal
            }
        }

        // Starting a mission set. Vanilla resets every mission in the set to "not done" and starts the first,
        // so leaving an island mid-set meant redoing missions (seen in testing 2026-10-01). Instead: re-mark
        // the missions recorded above as done and start the first unfinished one.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionSet), nameof(MissionSet.StartSet))]
        private static bool SetStarting(MissionSet __instance)
        {
            var mm = MissionManager.Instance;
            if (!Items.ApActive || mm == null || mm.currentMissionState != MissionManager.MissionState.Progression) return true;

            var missions = __instance.missions;
            int firstOpen = -1;
            for (int i = 0; i < missions.Count; i++)
            {
                bool done = State.CompletedMissions.Contains(missions[i].missionSO?.uniqueName ?? "");
                missions[i].isCompleted = done;
                if (!done && firstOpen < 0) firstOpen = i;
            }
            if (firstOpen < 0)
            {
                // All recorded as done, but the set itself never finished (e.g. the game was closed right
                // after the last mission). Replay just the last one so the set can finish normally.
                firstOpen = missions.Count - 1;
                missions[firstOpen].isCompleted = false;
            }
            if (firstOpen > 0) Plugin.Log.LogInfo($"Resuming set at mission {firstOpen + 1} of {missions.Count}");
            __instance.StartMission(firstOpen);
            return false;
        }

        // An island's last set is done. Let the game's own "island finished" flow run (it re-enables input
        // and so on; replacing it entirely froze the player in testing 2026-10-01), just record it here.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.OnMissionUnitFinished))]
        private static void MissionUnitFinished(MissionManager __instance)
        {
            if (!Items.ApActive || __instance.currentMissionState != MissionManager.MissionState.Progression) return;
            int island = __instance.currentMissionUnit + 1;
            RecordSet(island, SetCount(__instance, island), "island complete");
            Plugin.Log.LogInfo($"Island {island} complete");
        }

        // The last step of the game's "island finished" flow: load the NEXT island in vanilla order. Instead,
        // reload the island just finished in free roam (an ordinary travel), then open the world map.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.NextMissionUnit))]
        private static bool NextMissionUnit(MissionManager __instance)
        {
            if (!Items.ApActive) return true;
            int island = __instance.currentMissionUnit + 1;
            Plugin.Log.LogInfo($"Not moving on to Island {island + 1}; reloading Island {island} in free roam, then the world map");
            openMapAfterLoad = true;
            __instance.TravelToIsland(true, island - 1);  // complete -> our TravelToIsland lets the game free-roam it
            return false;
        }

        private static bool openMapAfterLoad;

        [HarmonyPostfix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.OnIslandLoaded))]
        private static void IslandLoaded()
        {
            if (!openMapAfterLoad) return;
            openMapAfterLoad = false;
            Plugin.Connection.RunNextFrame(() =>
            {
                var ui = Object.FindObjectOfType<UIBehaviour>();
                if (ui != null) ui.OpenWorldMap(false);
            });
        }
    }
}
