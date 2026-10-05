using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace MapMapArchipelago;

// Entry point. BepInEx calls Load() once when the game starts.
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "mapmap.archipelago";
    public const string Name = "Map Map Archipelago";
    public const string Version = "0.1.0";

    // Must match GAME_NAME in the apworld (world.py) exactly, or the server rejects the login.
    public const string GameName = "Map Map - A Game About Maps";

    internal new static ManualLogSource Log;
    internal static ConnectionSettings Settings;
    internal static ApConnection Connection;

    public override void Load()
    {
        Log = base.Log;
        Settings = new ConnectionSettings(Config);
        Connection = new ApConnection();
        var harmony = new HarmonyLib.Harmony(Guid);
        harmony.PatchAll(typeof(SaveRedirect.Patches));
        harmony.PatchAll(typeof(Items.Patches));
        harmony.PatchAll(typeof(Travel.Patches));
        harmony.PatchAll(typeof(Checks.Patches));
        harmony.PatchAll(typeof(DeathLinkHandler.Patches));
        AddComponent<PluginRunner>();
        Log.LogInfo($"{Name} {Version} loaded.");
    }
}

// Connection settings saved in BepInEx\config\mapmap.archipelago.cfg.
// The password is only saved if the player ticks "Remember password" (the config file is plain text).
public class ConnectionSettings
{
    public readonly ConfigEntry<string> Server;
    public readonly ConfigEntry<string> Slot;
    public readonly ConfigEntry<bool> RememberPassword;
    public readonly ConfigEntry<string> Password;

    public ConnectionSettings(ConfigFile config)
    {
        Server = config.Bind("Connection", "Server", "archipelago.gg:38281", "Archipelago server address (host:port).");
        Slot = config.Bind("Connection", "SlotName", "", "Your slot (player) name from your YAML.");
        RememberPassword = config.Bind("Connection", "RememberPassword", false,
            "Save the room password in this file. It is stored as plain text.");
        Password = config.Bind("Connection", "Password", "", "Only used when RememberPassword is true.");
    }
}

// The plugin's one Unity component. Unity calls Update() every frame and OnGUI() whenever it draws UI.
// All work that touches the game happens here, on Unity's main thread.
public class PluginRunner : MonoBehaviour
{
    private ConnectionPanel panel;

    public PluginRunner(IntPtr ptr) : base(ptr) { }

    private void Awake() => panel = new ConnectionPanel(Plugin.Settings, Plugin.Connection);

    private void Update()
    {
        try { Plugin.Connection.RunMainThreadWork(); Goal.PollForEnding(); DeathLinkHandler.Update(); }
        catch (Exception e) { Plugin.Log.LogError($"Update: {e}"); }
    }

    private void OnGUI()
    {
        try { panel.Draw(); Notifications.Draw(); }
        catch (Exception e) { Plugin.Log.LogError($"OnGUI: {e}"); }
    }
}
