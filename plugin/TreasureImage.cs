using System.IO;
using UnityEngine;

namespace MapMapArchipelago;

// Optional custom picture for the treasure popup. If "ap_treasure.png" sits next to the plugin DLL
// (BepInEx\plugins\MapMapArchipelago\), the popup shows it instead of the vanilla sticker, since in an AP
// save the treasure is a check and the sticker isn't given (DESIGN.md decision 18). If the file is missing
// or unreadable, the vanilla picture is used - nothing else depends on it.
public static class TreasureImage
{
    private const string FileName = "ap_treasure.png";
    private static Sprite sprite;
    private static bool tried;

    public static Sprite Get()
    {
        if (tried) return sprite;
        tried = true;
        string path = Path.Combine(Path.GetDirectoryName(typeof(TreasureImage).Assembly.Location), FileName);
        if (!File.Exists(path)) return null;
        try
        {
            var texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new IOException("not a valid image");
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;  // keep Unity from unloading it between scenes
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Plugin.Log.LogInfo($"Loaded custom treasure image {FileName} ({texture.width}x{texture.height})");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Couldn't load {FileName}: {e.Message} - using the vanilla picture");
            sprite = null;
        }
        return sprite;
    }
}
