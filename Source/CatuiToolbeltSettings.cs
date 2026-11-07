using System;
using System.IO;
using System.Xml.Linq;

// Reads ToolbeltSettings.xml from the CATUI mod folder and applies it to the game's own toolbelt config
// (ToolbeltConfig.GameConfig, 7DTD 3.4+). The game already implements:
//   - ALT + 1..0      -> switch to toolbar page 1..10 (only works when PageCount > 1)
//   - SHIFT + 1..0    -> use the second bar of the current page (only works when BarsPerPage > 1)
// so only the numbers need to be set. If the file is missing, the game defaults are left untouched.
public static class CatuiToolbeltSettings
{
	private const string TAG = "[CATUI][Toolbelt]";

	public static void Apply()
	{
		try
		{
			string path = FindFile();
			if (path == null || !File.Exists(path))
			{
				return;
			}
			XElement root = XDocument.Load(path).Root;
			if (root == null)
			{
				return;
			}
			ToolbeltConfig config = ToolbeltConfig.GameConfig;
			config.SlotsPerBar = ReadInt(root, "SlotsPerBar", config.SlotsPerBar, 10, 20);
			config.BarsPerPage = ReadInt(root, "BarsPerPage", config.BarsPerPage, 1, 2);
			config.PageCount = ReadInt(root, "PageCount", config.PageCount, 1, 10);
			Log.Out("{0} SlotsPerBar={1} BarsPerPage={2} PageCount={3} (total {4} slots)", TAG, config.SlotsPerBar, config.BarsPerPage, config.PageCount, config.TotalSlots);
		}
		catch (Exception e)
		{
			Log.Error("{0} Failed to apply ToolbeltSettings.xml: {1}", TAG, e);
		}
	}

	private static string FindFile()
	{
		foreach (Mod mod in ModManager.GetLoadedMods())
		{
			if (mod.Name != null && mod.Name.StartsWith("CATUI", StringComparison.OrdinalIgnoreCase) && mod.Name.IndexOf("_", StringComparison.Ordinal) < 0)
			{
				return mod.Path + "/ToolbeltSettings.xml";
			}
		}
		return null;
	}

	private static int ReadInt(XElement root, string name, int fallback, int min, int max)
	{
		XElement element = root.Element(name);
		string text = element?.Attribute("value")?.Value;
		if (!int.TryParse(text, out int value))
		{
			return fallback;
		}
		return Math.Max(min, Math.Min(max, value));
	}
}
