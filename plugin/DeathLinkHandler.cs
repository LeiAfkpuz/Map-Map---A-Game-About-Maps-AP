using System;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using UnityEngine;

namespace MapMapArchipelago;

// Death Link (only when the seed's `death_link` option is on; it arrives in slot data).
//
//   Outgoing: drowning is Map Map's "death". The game's PlayerDrowning.Respawn() runs once per real drowning
//             (it fades out and puts the player back on safe ground), so that's where a Death Link is sent.
//   Incoming: someone else died -> the current island is reloaded, putting the player back at its starting
//             point (a loading screen; progress is kept). It waits until the player is walking around an
//             island (not on the menu, the world map, a mission result screen, mid-respawn or mid-load).
//
// Echo guard: a respawn CAUSED by an incoming Death Link never sends one back out; otherwise two Death Link
// players would kill each other forever.
public static class DeathLinkHandler
{
    private static DeathLinkService service;
    private static string slotName;
    private static volatile bool pendingDeath;  // received (set on the network thread), waiting for a safe moment
    private static string pendingFrom = "";
    private static bool applyingIncoming;   // set while WE trigger a respawn (the echo guard)

    // Called on the main thread after every successful connect.
    public static void OnConnected(ArchipelagoSession session, string slot, bool enabled)
    {
        service = null;
        slotName = slot;
        if (!enabled) return;
        service = DeathLinkProvider.CreateDeathLinkService(session);
        service.EnableDeathLink();
        // Fires on a network thread; just remember it (a bool and a string) and handle it on the main thread.
        service.OnDeathLinkReceived += deathLink =>
        {
            if (deathLink.Source == slotName) return;  // our own death bounced back
            pendingFrom = deathLink.Source;
            pendingDeath = true;
        };
        Plugin.Log.LogInfo("Death Link enabled");
    }

    public static void OnDisconnected() => service = null;

    // Called every frame from PluginRunner.Update(): applies a received death once it's safe to.
    public static void Update()
    {
        if (!pendingDeath || !Items.ApActive || SaveRedirect.OnMainMenu) return;
        var drowning = UnityEngine.Object.FindObjectOfType<PlayerDrowning>();
        if (drowning == null || !drowning.active || drowning.respawning || drowning.inResultScreen) return;
        var map = UnityEngine.Object.FindObjectOfType<_MapMap.Scripts.Game.Gameplay.WorldMap.WorldMap>();
        if (map != null && map.isOpen) return;

        pendingDeath = false;
        Plugin.Log.LogInfo($"Death Link received from {pendingFrom} - drowning the player");
        Notifications.Show($"Death Link: {pendingFrom} died!");
        // The penalty (owner's choice): back to the island's starting point, done by RELOADING the current island
        // through the mod's normal travel (Travel.cs: story mode at the saved set, or free roam if finished).
        // That's fully tested and always lands at the island's real arrival point.
        //
        // Tried first and rejected (testing 2026-10-04): the game's Respawn() only hops the player to the
        // nearest ground, even with lastPos changed; calling PlayerDrowning.Teleport directly moved the player
        // somewhere unexpected AND left their controls disabled (Respawn normally turns them back on).
        var mm = _MapMap.Scripts.Game.Structure.Missions.MissionManager.Instance;
        if (mm == null || mm.isLoading) { pendingDeath = true; return; }  // try again next frame
        int unit = mm.currentMissionUnit;
        Plugin.Log.LogInfo($"Death Link: reloading Island {unit + 1} (back to its starting point)");
        applyingIncoming = true;
        try { mm.TravelToIsland(true, unit); }
        finally { applyingIncoming = false; }
    }

    private static void SendDeath()
    {
        if (service == null) return;
        var deathLink = new DeathLink(slotName, $"{slotName} drowned while mapping.");
        var s = service;
        System.Threading.Tasks.Task.Run(() =>
        {
            try { s.SendDeathLink(deathLink); Plugin.Log.LogInfo("Death Link sent"); }
            catch (Exception e) { Plugin.Log.LogWarning($"Sending Death Link failed: {e.Message}"); }
        });
    }

    [HarmonyPatch]
    public static class Patches
    {
        // Runs on each step of the drowning-respawn coroutine; step 0 is the start of a respawn (a real drowning,
        // or one we triggered above). Hooking Respawn() itself didn't work: it only creates this coroutine
        // object, and the compiled game had inlined it, so the hook never ran (testing 2026-10-04). The
        // coroutine's own MoveNext is a real method. Unity runs a coroutine's first step immediately inside
        // StartCoroutine, so our applyingIncoming flag is still set when an incoming death gets here.
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerDrowning._Respawn_d__41), nameof(PlayerDrowning._Respawn_d__41.MoveNext))]
        private static void Drowned(PlayerDrowning._Respawn_d__41 __instance)
        {
            if (__instance.__1__state != 0) return;  // only the first step of each respawn
            if (!Items.ApActive || service == null || applyingIncoming) return;
            Plugin.Log.LogInfo("Player drowned - sending Death Link");
            SendDeath();
        }
    }
}
