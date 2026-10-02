using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace MapMapArchipelago;

// Everything the mod remembers about one seed + slot, stored as ap_state_<tag>.json next to that
// seed's game save (Map Map_Data\Savegame\MapMap\). One file per seed+slot, so seeds never mix.
public class ApState
{
    // Identity: which seed and slot this file belongs to.
    public string Seed;
    public string Slot;

    // Every location (check) the player has completed, by ID. Never shrinks. On each connect, any of
    // these the server hasn't recorded yet are sent again, so checks made while offline are not lost.
    public HashSet<long> CheckedLocations = new();

    // Every item ID received from the server, in order. Kept so items stay applied while offline
    // (the server re-sends the full list on every connect, which replaces this).
    public List<long> ReceivedItems = new();

    // Which sticker / pencil each received Sticker / Pencil item unlocked, in the order received.
    // Recorded so re-applying items always picks the same ones (see Items.ApplyStickers).
    public List<int> GrantedStickerIds = new();
    public List<int> GrantedPencilIds = new();

    // Missions finished in story mode, by the game's unique mission name (e.g. "Mission_Divider2_1_2").
    // The game itself forgets which missions of a set were done whenever the set restarts, so the mod
    // keeps track and resumes at the first unfinished one (see Travel.cs, MissionSet.StartSet).
    public HashSet<string> CompletedMissions = new();

    // Island number (1-20) -> index of the mission set to resume on that island (used from step 3.4).
    public Dictionary<int, int> IslandSetProgress = new();

    // Set once the goal has been reported, so it's only sent once.
    public bool GoalSent;

    [JsonIgnore] public string FilePath;

    public static string SaveFolder => Path.Combine(Application.dataPath, "Savegame", "MapMap");

    // A short, file-name-safe identifier for a seed + slot, e.g. "05526932476300038771_Tester".
    public static string MakeTag(string seed, string slot) =>
        Regex.Replace($"{seed}_{slot}", @"[^A-Za-z0-9_\-]", "_");

    public static ApState LoadOrCreate(string seed, string slot)
    {
        string path = Path.Combine(SaveFolder, $"ap_state_{MakeTag(seed, slot)}.json");
        ApState state = null;
        if (File.Exists(path))
        {
            try { state = JsonConvert.DeserializeObject<ApState>(File.ReadAllText(path)); }
            catch (Exception e)
            {
                // Keep the unreadable file for troubleshooting instead of silently overwriting it.
                string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(path, backup, true);
                Plugin.Log.LogError($"State file unreadable ({e.Message}); copied to {backup} and starting fresh.");
            }
        }
        bool isNew = state == null;
        state ??= new ApState { Seed = seed, Slot = slot };
        state.FilePath = path;
        if (isNew) state.Save();  // create the file right away, so it's visible from the first connect
        Plugin.Log.LogInfo($"State: {path} ({state.CheckedLocations.Count} checks recorded)");
        return state;
    }

    // Write to a temporary file first, then swap it in, so a crash mid-write can't leave a half-written file.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not save state file {FilePath}: {e.Message}");
        }
    }
}
