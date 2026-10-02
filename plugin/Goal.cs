using System.Linq;
using _MapMap.Scripts.Game.Structure.Missions;

namespace MapMapArchipelago;

// Decides when the player has won, based on the seed's `goal` option (sent in slot data):
//   0 = island_20:          finish the Raven King's cave mission (it plays the ending)
//   1 = islands_completed:  fully complete `islands_required` islands (every mission set done)
//   2 = all_three_stars:    every 3-star location in this seed has been checked
// Matches the apworld's goal events (world.py). Re-checked after anything that could complete a goal.
public static class Goal
{
    private const int GoalIsland20 = 0, GoalIslandsCompleted = 1, GoalAllThreeStars = 2;
    private const long ThreeStarKind = 1;

    private static int SlotInt(string key, int fallback)
    {
        var data = Plugin.Connection.SlotData;
        return data != null && data.TryGetValue(key, out var value) ? System.Convert.ToInt32(value) : fallback;
    }

    // Safety net for the island_20 goal: if the ending cutscene scene loads, the cave was entered, even if
    // the mission-completed hook somehow didn't fire. Checked about once a second from PluginRunner.Update.
    private const string OutroScene = "Outro Cutscene";
    private static float nextOutroCheck;

    public static void PollForEnding()
    {
        var state = Plugin.Connection.ApState;
        if (state == null || state.GoalSent || UnityEngine.Time.realtimeSinceStartup < nextOutroCheck) return;
        nextOutroCheck = UnityEngine.Time.realtimeSinceStartup + 1f;
        if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(OutroScene).isLoaded) return;
        if (state.CompletedMissions.Add(Checks.GoalMission))
        {
            Plugin.Log.LogInfo("Ending cutscene detected - counting the Raven King's cave as done");
            state.Save();
        }
        Check();
    }

    public static void Check()
    {
        var state = Plugin.Connection.ApState;
        var mm = MissionManager.Instance;
        if (state == null || state.GoalSent || Plugin.Connection.SlotData == null) return;

        bool won;
        switch (SlotInt("goal", GoalIsland20))
        {
            case GoalIslandsCompleted:
                if (mm == null) return;
                int done = Enumerable.Range(1, 20).Count(n => Travel.IsComplete(mm, n));
                won = done >= SlotInt("islands_required", 10);
                break;
            case GoalAllThreeStars:
                var session = Plugin.Connection.Session;
                if (session == null) return;  // needs the seed's location list; re-checked on connect
                won = session.Locations.AllLocations
                    .Where(id => (id - ItemIds.BaseId) % 1000 / 100 == ThreeStarKind)
                    .All(id => state.CheckedLocations.Contains(id));
                break;
            default:
                won = state.CompletedMissions.Contains(Checks.GoalMission);
                break;
        }
        if (!won) return;

        Plugin.Log.LogInfo("GOAL COMPLETE");
        state.GoalSent = true;
        state.Save();
        Notifications.Show("Goal complete!");
        Plugin.Connection.SendGoal();
    }
}
