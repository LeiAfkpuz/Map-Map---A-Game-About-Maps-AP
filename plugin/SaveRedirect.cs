using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using _MapMap.Scripts.Game.Gameplay.UI;
using _MapMap.Scripts.Game.Structure.Savegame;

namespace MapMapArchipelago;

// Gives every AP seed its own game save, separate from the player's normal save.
//
// The game reads and writes its save through four methods on StandaloneSavegameManager, each given a
// file name ("savegame00"). While a seed is active, these patches change that name to
// "savegame00_ap_<seed>_<slot>", so the game saves to a different file. When no seed is active,
// nothing is changed and the normal save is used.
public static class SaveRedirect
{
    private const string MainMenuScene = "MainMenuScene";

    // The seed+slot tag whose save is in use, or null for the player's normal save.
    public static string ActiveTag { get; private set; }

    public static bool OnMainMenu => SceneManager.GetSceneByName(MainMenuScene).isLoaded;

    public static void Activate(string tag)
    {
        ActiveTag = tag;
        Plugin.Log.LogInfo($"Save redirect ON: AP save for {tag}");
        ReloadFromNewFile();
    }

    public static void Deactivate()
    {
        if (ActiveTag == null) return;
        Plugin.Log.LogInfo("Save redirect OFF: normal save");
        ActiveTag = null;
        ReloadFromNewFile();
    }

    // The game reads its save into memory as soon as the main menu loads (before the player can
    // connect), and "Continue" uses that in-memory copy. So after switching files, re-read the save
    // from the file now in use, so what's in memory matches the file the game will write to.
    //
    // We repeat exactly what the main menu does at startup: CheckForExistingSavegameInitial checks
    // the file exists, reads it into memory (both go through our redirect), and shows Continue.
    // NOTE: do NOT use SavegameManager.LoadGame here - that STARTS PLAYING with whatever is already
    // in memory (found the hard way in testing, 2026-10-01).
    //
    // If the file doesn't exist yet (a brand-new seed), nothing is read: Continue is hidden and the
    // player starts with New Game, which resets the in-memory save itself.
    private static void ReloadFromNewFile()
    {
        if (!OnMainMenu) return;
        var manager = SavegameManager.Instance;
        var menu = Object.FindObjectOfType<UIBehaviour>();
        if (manager == null || menu == null) return;
        bool hasSave = manager.CheckForExistingSavegameInitial(menu);
        Plugin.Log.LogInfo($"Re-read save from the active file (save exists: {hasSave})");
        RefreshContinueButton(hasSave);
    }

    private static string Redirect(string fileName) =>
        ActiveTag == null ? fileName : $"{fileName}_ap_{ActiveTag}";

    // The main menu decides whether to show "Continue" when it first loads, before the player connects.
    // Update it so it reflects the save that will actually be used (this seed's, or the normal one).
    private static void RefreshContinueButton(bool hasSave)
    {
        var menu = Object.FindObjectOfType<UIBehaviour>();
        if (menu == null || menu.continueButton == null) return;
        menu.continueButton.SetActive(hasSave);
        Plugin.Log.LogInfo($"Continue button {(hasSave ? "shown" : "hidden")} (save exists: {hasSave})");
    }

    [HarmonyPatch(typeof(StandaloneSavegameManager))]
    public static class Patches
    {
        [HarmonyPrefix, HarmonyPatch(nameof(StandaloneSavegameManager.SaveData))]
        private static void Save(ref string fileName) => fileName = Log("save", Redirect(fileName));

        [HarmonyPrefix, HarmonyPatch(nameof(StandaloneSavegameManager.LoadData))]
        private static void Load(ref string fileName) => fileName = Log("load", Redirect(fileName));

        [HarmonyPrefix, HarmonyPatch(nameof(StandaloneSavegameManager.DeleteData))]
        private static void Delete(ref string fileName) => fileName = Log("delete", Redirect(fileName));

        [HarmonyPrefix, HarmonyPatch(nameof(StandaloneSavegameManager.CheckForExistingSavegame))]
        private static void Exists(ref string fileName) => fileName = Redirect(fileName);

        private static string Log(string action, string fileName)
        {
            Plugin.Log.LogInfo($"Game {action}: {fileName}");
            return fileName;
        }
    }
}
