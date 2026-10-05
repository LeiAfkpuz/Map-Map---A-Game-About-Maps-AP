using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using UnityEngine;

namespace MapMapArchipelago;

// Owns the connection to the Archipelago server and the per-seed state.
//
// Threading: the network library talks to the server on background threads, but Unity (and the game)
// may only be touched from the main thread. So anything that comes back from the network is put on
// `mainThreadWork` and run later from PluginRunner.Update(), which is on the main thread.
//
// Seed binding rules (see SaveRedirect):
//   * Connecting on the main menu picks the seed: its own game save and ap_state file are used.
//   * Connecting while already playing is only allowed for the SAME seed (a reconnect).
//   * Connecting while playing the normal (non-AP) save is refused, so AP progress never leaks into it.
public class ApConnection
{
    public enum State { Disconnected, Connecting, Connected }

    public State Status { get; private set; } = State.Disconnected;
    public string StatusText { get; private set; } = "Not connected";
    public ArchipelagoSession Session { get; private set; }
    public Dictionary<string, object> SlotData { get; private set; }

    // The state for the active seed. Stays loaded after a disconnect (checks keep being recorded
    // offline) until the player returns to the main menu.
    public ApState ApState { get; private set; }

    private readonly ConcurrentQueue<Action> mainThreadWork = new();

    // Remembered for automatic reconnects (the password only in memory, never written to the log).
    private string lastServer, lastSlot, lastPassword;
    private float nextReconnectTime = -1f;
    private float nextHealthCheck;
    private int reconnectAttempts;
    private static readonly float[] ReconnectDelays = { 5f, 10f, 20f, 30f };

    // ------------------------------------------------------------------ main thread

    // Called every frame from PluginRunner.Update().
    public void RunMainThreadWork()
    {
        while (mainThreadWork.TryDequeue(out var work))
            work();

        if (itemsChanged)
        {
            itemsChanged = false;
            UpdateItems();
        }

        // Connection health check every 2 seconds. The library's "closed" event doesn't fire if the
        // server dies abruptly (tested 2026-10-01), so also look at the socket's own connected flag.
        if (Status == State.Connected && Time.realtimeSinceStartup >= nextHealthCheck)
        {
            nextHealthCheck = Time.realtimeSinceStartup + 2f;
            if (Session != null && !Session.Socket.Connected)
                HandleClosed("socket is no longer connected");
        }

        if (nextReconnectTime > 0 && Time.realtimeSinceStartup >= nextReconnectTime && Status == State.Disconnected)
        {
            nextReconnectTime = -1f;
            reconnectAttempts++;
            Plugin.Log.LogInfo($"Reconnect attempt {reconnectAttempts}");
            StartConnect(lastServer, lastSlot, lastPassword);
        }

        // Back on the main menu with no connection: release the seed, so the normal save is used again.
        if (Status == State.Disconnected && nextReconnectTime < 0 && ApState != null && SaveRedirect.OnMainMenu)
        {
            ApState = null;
            SaveRedirect.Deactivate();
        }
    }

    private void OnMainThread(Action work) => mainThreadWork.Enqueue(work);

    // Run something on the next frame, after the game has finished whatever it's doing right now.
    public void RunNextFrame(Action work) => mainThreadWork.Enqueue(work);

    // ------------------------------------------------------------------ connecting

    public void Connect(string server, string slot, string password)
    {
        if (Status != State.Disconnected) return;
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(slot))
        {
            StatusText = "Enter a server and slot name";
            return;
        }
        if (SaveRedirect.ActiveTag == null && !SaveRedirect.OnMainMenu)
        {
            StatusText = "Connect from the main menu, before loading a save";
            return;
        }
        lastServer = server.Trim();
        lastSlot = slot.Trim();
        lastPassword = string.IsNullOrEmpty(password) ? null : password;
        reconnectAttempts = 0;
        StartConnect(lastServer, lastSlot, lastPassword);
    }

    private void StartConnect(string server, string slot, string password)
    {
        Status = State.Connecting;
        StatusText = $"Connecting to {server}...";
        Plugin.Log.LogInfo($"Connecting to {server} as '{slot}'");

        // Logging in waits on the network, so it runs on a background thread to keep the game responsive.
        Task.Run(() =>
        {
            ArchipelagoSession session = null;
            LoginResult result;
            try
            {
                session = ArchipelagoSessionFactory.CreateSession(server);
                result = session.TryConnectAndLogin(
                    Plugin.GameName,
                    slot,
                    ItemsHandlingFlags.AllItems,   // the server sends us every item we receive, including our own
                    version: new Version(0, 6, 7),   // the Archipelago version the apworld targets
                    password: password,
                    requestSlotData: true);
            }
            catch (Exception e)
            {
                result = new LoginFailure(e.GetBaseException().Message);
            }

            OnMainThread(() => FinishConnect(session, slot, result));
        });
    }

    private void FinishConnect(ArchipelagoSession session, string slot, LoginResult result)
    {
        if (result is not LoginSuccessful success)
        {
            var failure = (LoginFailure)result;
            Status = State.Disconnected;
            StatusText = "Failed: " + string.Join("; ", failure.Errors);
            Plugin.Log.LogWarning($"Connection failed: {string.Join("; ", failure.Errors)}");
            TryClose(session);
            if (reconnectAttempts > 0) ScheduleReconnect();  // keep retrying if this was an automatic reconnect
            return;
        }

        string seed = session.RoomState.Seed;
        string tag = ApState.MakeTag(seed, slot);

        // Mid-game, only the seed whose save is loaded may connect.
        if (SaveRedirect.ActiveTag != null && SaveRedirect.ActiveTag != tag)
        {
            Status = State.Disconnected;
            StatusText = "This is a different seed than the save you're playing. Return to the main menu first.";
            Plugin.Log.LogWarning($"Refused connection to {tag}: active save belongs to {SaveRedirect.ActiveTag}");
            TryClose(session);
            return;
        }

        Session = session;
        SlotData = success.SlotData;
        Status = State.Connected;
        StatusText = $"Connected as {slot}";
        reconnectAttempts = 0;
        Plugin.Log.LogInfo($"Connected as '{slot}' (slot {success.Slot}, team {success.Team}). Seed {seed}. " +
                           $"Slot data: {string.Join(", ", SlotData.Select(kv => $"{kv.Key}={kv.Value}"))}");

        if (ApState == null || ApState.FilePath == null || SaveRedirect.ActiveTag != tag)
        {
            ApState = ApState.LoadOrCreate(seed, slot);
            Items.Recompute(ApState.ReceivedItems);  // last known items, so the gates are right before the server's list arrives
        }
        if (SaveRedirect.ActiveTag != tag)
            SaveRedirect.Activate(tag);

        SyncChecks();

        // Items: the server sends the full received list on connect, then one at a time as they arrive.
        // Each arrival re-applies the whole list (every item is an unlock, so that's always safe).
        lastItemCount = 0;
        UpdateItems();
        // Arrivals only mark the list as changed; it's applied once per frame in RunMainThreadWork. When a
        // seed is released (e.g. after a solo goal) ~200 items arrive at once, and applying the whole list
        // once per item froze the game in testing (2026-10-02).
        session.Items.ItemReceived += _ => itemsChanged = true;

        // Death Link, if this seed turned it on (DeathLinkHandler.cs).
        bool deathLink = SlotData.TryGetValue("death_link", out var dl) && Convert.ToInt32(dl) == 1;
        DeathLinkHandler.OnConnected(session, slot, deathLink);

        // "Sent X to Y" / "Received X from Y" popups (Notifications.cs).
        session.MessageLog.OnMessageReceived += message => OnMainThread(() => Notifications.OnServerMessage(message));

        // A goal reached while offline is reported now; otherwise see if one is already met (e.g. all 3 stars).
        if (ApState.GoalSent) SendGoal();
        else Goal.Check();

        // Fire on a background thread if the connection drops or errors; hop back to the main thread.
        // (Plus the 2-second health check in RunMainThreadWork, which catches abrupt server deaths.)
        session.Socket.SocketClosed += reason => OnMainThread(() => HandleClosed(reason));
        session.Socket.ErrorReceived += (e, message) => OnMainThread(() => HandleClosed("error: " + message));
    }

    public void Disconnect()
    {
        if (Session == null) return;
        Plugin.Log.LogInfo("Disconnecting (player request)");
        nextReconnectTime = -1f;  // a deliberate disconnect stops auto-reconnect
        DeathLinkHandler.OnDisconnected();
        var old = Session;
        Session = null;
        SlotData = null;
        Status = State.Disconnected;
        StatusText = "Disconnected";
        TryClose(old);
    }

    private void HandleClosed(string reason)
    {
        if (Status != State.Connected) return;  // already handled (e.g. the player clicked Disconnect)
        Plugin.Log.LogWarning($"Connection lost: {reason}");
        DeathLinkHandler.OnDisconnected();
        Session = null;
        SlotData = null;
        Status = State.Disconnected;
        ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        float delay = ReconnectDelays[Math.Min(reconnectAttempts, ReconnectDelays.Length - 1)];
        nextReconnectTime = Time.realtimeSinceStartup + delay;
        StatusText = $"Connection lost - retrying in {delay:0}s (checks are saved meanwhile)";
    }

    private static void TryClose(ArchipelagoSession session)
    {
        try { session?.Socket.DisconnectAsync(); }
        catch (Exception e) { Plugin.Log.LogDebug($"Ignoring error while closing socket: {e.Message}"); }
    }

    // Tell the server this slot has finished its goal. Safe to repeat (the server ignores duplicates).
    public void SendGoal()
    {
        if (Status != State.Connected || Session == null) return;  // sent on the next connect instead
        var session = Session;
        Task.Run(() =>
        {
            try { session.SetGoalAchieved(); Plugin.Log.LogInfo("Goal reported to the server"); }
            catch (Exception e) { Plugin.Log.LogWarning($"Reporting the goal failed (will retry on reconnect): {e.Message}"); }
        });
    }

    // ------------------------------------------------------------------ items

    private int lastItemCount;
    private volatile bool itemsChanged;  // set from the network thread, read on the main thread

    private void UpdateItems()
    {
        if (Session == null || ApState == null) return;
        var all = Session.Items.AllItemsReceived;
        for (int i = lastItemCount; i < all.Count; i++)
            Plugin.Log.LogInfo($"Received: {all[i].ItemName} (from {all[i].Player.Name})");
        lastItemCount = all.Count;

        var ids = all.Select(item => item.ItemId).ToList();
        if (!ids.SequenceEqual(ApState.ReceivedItems))
        {
            ApState.ReceivedItems = ids;
            ApState.Save();
        }
        Items.Recompute(ids);
    }

    // ------------------------------------------------------------------ checks

    // Record a completed check. Saved to the state file immediately; sent now if connected,
    // otherwise on the next connect (see SyncChecks).
    public void SendCheck(long locationId)
    {
        if (ApState == null) return;  // no seed active (normal save)
        if (!ApState.CheckedLocations.Add(locationId)) return;  // already recorded
        ApState.Save();
        Plugin.Log.LogInfo($"Check {locationId} recorded{(Status == State.Connected ? ", sending" : " (offline, will send on reconnect)")}");
        if (Status == State.Connected) SendToServer(new[] { locationId });
        Goal.Check();  // a 3-star check can complete the all_three_stars goal
    }

    // On connect: send every recorded check the server doesn't have yet, and adopt any the server has
    // that we don't (e.g. if the state file was deleted), so the two always agree.
    private void SyncChecks()
    {
        var serverChecked = new HashSet<long>(Session.Locations.AllLocationsChecked);
        long[] missing = ApState.CheckedLocations.Where(id => !serverChecked.Contains(id)).ToArray();
        int adopted = serverChecked.Count(id => ApState.CheckedLocations.Add(id));
        if (adopted > 0) ApState.Save();
        Plugin.Log.LogInfo($"Check sync: {missing.Length} to send, {adopted} adopted from server");
        if (missing.Length > 0) SendToServer(missing);
    }

    private void SendToServer(long[] ids)
    {
        var session = Session;
        Task.Run(() =>
        {
            try { session.Locations.CompleteLocationChecks(ids); }
            catch (Exception e) { Plugin.Log.LogWarning($"Sending checks failed (will retry on reconnect): {e.Message}"); }
        });
    }
}
