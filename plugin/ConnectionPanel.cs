using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapMapArchipelago;

// The "AP Connect" box drawn with Unity's simple IMGUI system.
//
// On the main menu it starts open. In-game it collapses to a small status button in the corner
// that can be clicked to open it again. Only controls proven to work in this game build are used
// (Box, Label, Toggle, Button, and our own TextInputField - GUI.TextField is stripped).
public class ConnectionPanel
{
    private const string MainMenuScene = "MainMenuScene";

    private readonly ConnectionSettings settings;
    private readonly ApConnection connection;
    private readonly TextInputField server;
    private readonly TextInputField slot;
    private readonly TextInputField password;

    private bool expanded = true;
    private bool wasOnMainMenu;

    public ConnectionPanel(ConnectionSettings settings, ApConnection connection)
    {
        this.settings = settings;
        this.connection = connection;
        server = new TextInputField { Text = settings.Server.Value };
        slot = new TextInputField { Text = settings.Slot.Value };
        password = new TextInputField { Masked = true, Text = settings.RememberPassword.Value ? settings.Password.Value : "" };
    }

    public void Draw()
    {
        // Open the panel automatically whenever the player returns to the main menu.
        bool onMainMenu = SceneManager.GetSceneByName(MainMenuScene).isLoaded;
        if (onMainMenu && !wasOnMainMenu) expanded = true;
        wasOnMainMenu = onMainMenu;

        // Designed at 1080p; scale so it looks the same size at any resolution (proven in probe v0.3).
        float scale = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        if (!expanded)
        {
            if (GUI.Button(new Rect(10, 10, 260, 26), "AP: " + connection.StatusText)) expanded = true;
            return;
        }

        GUI.Box(new Rect(10, 10, 380, 300), "Archipelago");
        bool editable = connection.Status == ApConnection.State.Disconnected;

        GUI.Label(new Rect(25, 38, 340, 22), "Server:");
        DrawField(server, new Rect(25, 60, 350, 24), editable);
        GUI.Label(new Rect(25, 88, 340, 22), "Slot name:");
        DrawField(slot, new Rect(25, 110, 350, 24), editable);
        GUI.Label(new Rect(25, 138, 340, 22), "Password (optional):");
        DrawField(password, new Rect(25, 160, 350, 24), editable);

        bool remember = GUI.Toggle(new Rect(25, 190, 340, 22), settings.RememberPassword.Value, "Remember password (saved as plain text)");
        if (remember != settings.RememberPassword.Value) settings.RememberPassword.Value = remember;

        GUI.Label(new Rect(25, 216, 350, 44), connection.StatusText);

        switch (connection.Status)
        {
            case ApConnection.State.Disconnected:
                if (GUI.Button(new Rect(25, 266, 120, 30), "Connect")) Connect();
                break;
            case ApConnection.State.Connected:
                if (GUI.Button(new Rect(25, 266, 120, 30), "Disconnect")) connection.Disconnect();
                break;
        }
        if (GUI.Button(new Rect(255, 266, 120, 30), "Hide")) expanded = false;
    }

    private static void DrawField(TextInputField field, Rect rect, bool editable)
    {
        if (editable) field.Draw(rect);
        else GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width, rect.height), field.Masked ? new string('*', field.Text.Length) : field.Text);
    }

    private void Connect()
    {
        // Save what was typed so it's pre-filled next launch (password only if the player opted in).
        settings.Server.Value = server.Text;
        settings.Slot.Value = slot.Text;
        settings.Password.Value = settings.RememberPassword.Value ? password.Text : "";
        connection.Connect(server.Text, slot.Text, password.Text);
    }
}
