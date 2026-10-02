using System;
using UnityEngine;

namespace MapMapArchipelago;

// A minimal text box for IMGUI. Unity's own GUI.TextField is stripped from this
// game build, so this draws a box and types into it by hand.
//
// Privacy: keys are read ONLY from Unity's GUI events (Event.current), which Unity
// delivers only while the game window is focused, and ONLY while this field is
// focused (after the player clicks into it). Nothing is logged or stored here.
public class TextInputField
{
    public string Text = "";
    public bool Masked;          // show '*' instead of characters (passwords)
    public int MaxLength = 128;
    public bool Focused { get; private set; }

    // Call once per OnGUI pass. Returns true if Enter was pressed this frame.
    public bool Draw(Rect rect)
    {
        var e = Event.current;
        bool submitted = false;

        // Click inside -> focus; click anywhere else -> unfocus.
        if (e.type == EventType.MouseDown)
        {
            Focused = rect.Contains(e.mousePosition);
            if (Focused) e.Use();
        }

        if (Focused && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Backspace)
            {
                if (Text.Length > 0) Text = Text.Substring(0, Text.Length - 1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                Focused = false;
                submitted = true;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                Focused = false;
                e.Use();
            }
            else if (e.keyCode == KeyCode.V && (e.control || e.command))
            {
                Append(ReadClipboard());
                e.Use();
            }
            else if (e.character != '\0' && !char.IsControl(e.character))
            {
                Append(e.character.ToString());
                e.Use();
            }
        }

        // Draw: the box, then the text (or stars) with a blinking caret while focused.
        string shown = Masked ? new string('*', Text.Length) : Text;
        bool caretOn = Focused && (int)(Time.unscaledTime * 2) % 2 == 0;
        GUI.Box(rect, "");
        GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4), shown + (caretOn ? "|" : ""));
        return submitted;
    }

    private void Append(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        s = s.Replace("\r", "").Replace("\n", "").Trim();
        Text = (Text + s).Length > MaxLength ? (Text + s).Substring(0, MaxLength) : Text + s;
    }

    // Clipboard access may also be stripped; if so, pasting just does nothing.
    private static string ReadClipboard()
    {
        try { return GUIUtility.systemCopyBuffer; }
        catch (Exception e) { Plugin.Log.LogWarning($"[gui] clipboard unavailable: {e.GetType().Name}"); return ""; }
    }
}
