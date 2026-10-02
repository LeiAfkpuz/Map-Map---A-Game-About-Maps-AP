using System.Linq;
using HarmonyLib;
using _MapMap.Scripts.Game.Gameplay.Treasure_Hunt;
using _MapMap.Scripts.Game.Gameplay.UI.Map;
using _MapMap.Scripts.Game.Gameplay.UI.Stickers;
using _MapMap.Scripts.Game.Structure.Basic_Game_Logic.IslandManagement;
using _MapMap.Scripts.Game.Structure.Messages;
using _MapMap.Scripts.Game.Structure.Missions;

namespace MapMapArchipelago;

// Detects completed checks in the game and reports them (ApConnection.SendCheck saves them and sends
// them, now or on the next connect).
//
// Location IDs use the same formula as the apworld (location_table.py):
//     BaseId + island * 1000 + kind * 100 + index      (index is 1-based within island + kind)
//
// Only things done live in an AP save count: these hooks fire when something happens, never by scanning
// the save, so progress from before the seed (or from the normal save) is never sent (DESIGN.md decision 18).
public static class Checks
{
    private const int KindMission = 0, KindThreeStars = 1, KindTreasure = 2, KindFloorIsLava = 3, KindLandmark = 4;

    // The ending trigger. It's the goal (step 3.6), not a location.
    public const string GoalMission = "Mission_Compass4_4";

    private static long LocationId(int island, int kind, int index) =>
        ItemIds.BaseId + island * 1000 + kind * 100 + index;

    private static bool Live => Items.ApActive && !SaveRedirect.OnMainMenu;

    // A mission's index on its island: its position counting through every set in order (1-based).
    // This is the same order the apworld's islands.py lists missions in (both come from the game's data).
    private static int MissionIndex(MissionManager mm, int island, string uniqueName)
    {
        int index = 0;
        foreach (var set in mm.missionUnits[island - 1].missionSets)
            foreach (var mission in set.missions)
            {
                index++;
                if (mission.missionSO?.uniqueName == uniqueName) return index;
            }
        return -1;
    }

    private static void Send(long id, string what)
    {
        Plugin.Log.LogInfo($"CHECK: {what} -> location {id}");
        Plugin.Connection.SendCheck(id);
    }

    // Set while the game's "you found a treasure" popup hands out its reward (see the patches below).
    private static bool handingOutTreasureReward;

    [HarmonyPatch]
    public static class Patches
    {
        // ---------------------------------------------------------------- missions + 3 stars
        // MissionSet.MissionFinished runs once per successfully completed mission (not on misses).
        [HarmonyPostfix, HarmonyPatch(typeof(MissionSet), nameof(MissionSet.MissionFinished))]
        private static void MissionCompleted(MissionSet __instance)
        {
            var mm = MissionManager.Instance;
            if (!Live || mm == null) return;
            var state = mm.currentMissionState;
            if (state != MissionManager.MissionState.Progression && state != MissionManager.MissionState.ReplayLvl) return;

            var wrapper = __instance.missions[__instance.currentMission];
            var so = wrapper.missionSO;
            if (so == null || so.uniqueName == GoalMission) return;

            int island = mm.currentMissionUnit + 1;
            int index = MissionIndex(mm, island, so.uniqueName);
            if (index < 0) { Plugin.Log.LogWarning($"Mission {so.uniqueName} not found on Island {island}"); return; }

            Send(LocationId(island, KindMission, index), $"Island {island} mission {so.uniqueName}");

            // 3 stars: ask the game's own star calculation. A distance below 0 means "no result yet" -
            // the island panel shows 3 stars for those, but they must never count (DESIGN.md decision 13).
            if (so.isTreasureMission) return;  // treasure missions have no rating
            float distance = wrapper.result != null ? wrapper.result.distance : -1f;
            int stars = distance >= 0 ? StarRating.GetStarRating(distance) : 0;
            Plugin.Log.LogInfo($"Island {island} mission {so.uniqueName}: distance {distance:F2} = {stars} star(s)");
            if (stars >= 3)
                Send(LocationId(island, KindThreeStars, index), $"Island {island} mission {so.uniqueName} 3 stars");
        }

        // ---------------------------------------------------------------- treasures
        // The game's treasure IDs are NOT numbered the same way on every island (Island 5 uses 1,2,3;
        // Island 18 uses 0,1,2 - found in testing 2026-10-02). So "Treasure N" = the N-th lowest treasure ID
        // among the island's buried (non-mission) treasures, read from the treasure objects in the loaded scene.
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.OnUnlockTreasure))]
        private static void TreasureFound(FindTreasureMessage message)
        {
            if (!Live || message.isMission) return;  // treasure MISSIONS count as missions (above)
            var islands = IslandManager.Instance;
            int island = islands != null ? islands.GetIslandIDForName(message.islandName) + 1
                                         : MissionManager.Instance.currentMissionUnit + 1;

            var ids = UnityEngine.Resources.FindObjectsOfTypeAll<TreasureLocation>()
                .Where(t => t.gameObject.scene.IsValid() && !t.isMission)
                .Select(t => t.Id).Distinct().OrderBy(id => id).ToList();
            int position = ids.IndexOf(message.treasureId) + 1;
            if (position == 0)
            {
                Plugin.Log.LogWarning($"Treasure id {message.treasureId} not among Island {island}'s treasures [{string.Join(",", ids)}]; not sent");
                return;
            }
            Send(LocationId(island, KindTreasure, position),
                 $"Island {island} treasure id {message.treasureId} (#{position} of [{string.Join(",", ids)}])");
        }

        // ---------------------------------------------------------------- floor is lava
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.OnCompleteFloorIsLava))]
        private static void FloorIsLavaDone(MissionManager __instance, CompleteFloorIsLavaMessage message)
        {
            if (!Live) return;
            int island = __instance.currentMissionUnit + 1;
            // Floor Is Lava IDs have been 0, 1, 2... so far. If one is ever out of range, say so loudly
            // rather than send a wrong location (the treasure numbering turned out to vary by island).
            int total = __instance.GetTotalFloorIsLavaCount(island - 1);
            if (message.id < 0 || message.id >= total)
            {
                Plugin.Log.LogWarning($"Floor Is Lava id {message.id} out of range for Island {island} (has {total}); not sent - please report");
                return;
            }
            Send(LocationId(island, KindFloorIsLava, message.id + 1), $"Island {island} floor is lava id {message.id}");
        }

        // ---------------------------------------------------------------- landmarks
        [HarmonyPrefix, HarmonyPatch(typeof(MissionManager), nameof(MissionManager.OnUnlockLandmark))]
        private static void LandmarkFound(MissionManager __instance, LandmarkMessage message)
        {
            // "Silent" landmark messages restore already-discovered landmarks when a save loads; skip those.
            if (!Live || message.isSilent || message.landmarkData == null) return;
            int island = __instance.currentMissionUnit + 1;
            int id = message.landmarkData.id, total = __instance.GetTotalLandmarkCount(island - 1);
            if (id < 0 || id >= total)
            {
                Plugin.Log.LogWarning($"Landmark id {id} out of range for Island {island} (has {total}); not sent - please report");
                return;
            }
            Send(LocationId(island, KindLandmark, id + 1), $"Island {island} landmark id {id}");
        }

        // ---------------------------------------------------------------- no vanilla treasure rewards
        // The treasure popup (TreasureUI.DisplayUI) is what hands out a treasure's sticker or pencil.
        // In an AP save the treasure is a check instead (DESIGN.md decision 18), so sticker/pencil unlocks
        // are blocked while that popup runs. Starter stickers, the game's own progress gifts and AP grants
        // all happen outside it and are unaffected.
        [HarmonyPrefix, HarmonyPatch(typeof(TreasureUI), "DisplayUI")]
        private static void TreasurePopupStart(TreasureUI __instance)
        {
            handingOutTreasureReward = true;
            // Show the optional custom image (ap_treasure.png) instead of the sticker the player won't get.
            var sprite = TreasureImage.Get();
            if (Items.ApActive && sprite != null) __instance.currentLootSprite = sprite;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(TreasureUI), "DisplayUI")]
        private static void TreasurePopupEnd() => handingOutTreasureReward = false;

        [HarmonyPrefix, HarmonyPatch(typeof(UiStickerManager), nameof(UiStickerManager.UnlockSticker))]
        private static bool StickerReward(int id) => AllowReward("sticker", id);

        [HarmonyPrefix, HarmonyPatch(typeof(UiPencilCase), nameof(UiPencilCase.UnlockPencil))]
        private static bool PencilReward(int id) => AllowReward("pencil", id);

        private static bool AllowReward(string what, int id)
        {
            if (!Items.ApActive || !handingOutTreasureReward) return true;
            Plugin.Log.LogInfo($"Blocked vanilla treasure reward: {what} {id} (the treasure is an AP check)");
            return false;
        }
    }
}
