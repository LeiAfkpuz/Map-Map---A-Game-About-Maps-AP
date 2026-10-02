using System.Collections.Generic;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using UnityEngine;

namespace MapMapArchipelago;

// Small "Sent / Received" messages in the top-right corner, drawn with the same IMGUI controls as the
// connection panel. Each one stays a few seconds; at most a handful are shown at once.
public static class Notifications
{
    private const float ShowSeconds = 6f;
    private const int MaxShown = 5;

    private struct Note { public string Text; public float Until; }
    private static readonly List<Note> notes = new();

    public static void Show(string text)
    {
        notes.Add(new Note { Text = text, Until = Time.realtimeSinceStartup + ShowSeconds });
        if (notes.Count > MaxShown) notes.RemoveAt(0);
        Plugin.Log.LogInfo($"Notification: {text}");
    }

    // Called (on the main thread) for every message the server sends. Only item messages that involve
    // this player become notifications; chat, hints and other players' items are ignored.
    public static void OnServerMessage(LogMessage message)
    {
        if (message is not ItemSendLogMessage send || !send.IsRelatedToActivePlayer) return;
        string item = send.Item.ItemName;
        if (send.IsSenderTheActivePlayer && send.IsReceiverTheActivePlayer) Show($"Found your {item}");
        else if (send.IsReceiverTheActivePlayer) Show($"Received {item} from {send.Sender.Name}");
        else Show($"Sent {item} to {send.Receiver.Name}");
    }

    // Called from PluginRunner.OnGUI every frame.
    public static void Draw()
    {
        float now = Time.realtimeSinceStartup;
        notes.RemoveAll(n => n.Until < now);
        if (notes.Count == 0) return;

        // Same 1080p-relative scaling as the connection panel.
        float scale = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float width = 420, x = Screen.width / scale - width - 10, y = 10;
        foreach (var note in notes)
        {
            GUI.Box(new Rect(x, y, width, 28), "");
            GUI.Label(new Rect(x + 8, y + 4, width - 16, 22), note.Text);
            y += 32;
        }
    }
}
