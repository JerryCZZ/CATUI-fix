using System;
using System.IO;
using System.Xml.Linq;




public static class CatuiToolbeltSettings
{
    private const string Tag = "[CATUI][Toolbelt]";
    public static bool Enabled { get; private set; }
    public static bool HidePageArrows { get; private set; }
    public static bool HidePageNumbers { get; private set; }

    public static void Apply()
    {
        try
        {
            XElement fallback = null;
            string path = FindFallbackFile();
            if (path != null && File.Exists(path))
                fallback = XDocument.Load(path).Root;

            bool on = ReadBool(fallback, "Enabled", false);
            int pages = ReadInt(fallback, "PageCount", 1, 1, 10);
            int slots = ReadInt(fallback, "SlotsPerBar", 10, 10, 20);
            bool hideArrows = ReadBool(fallback, "HidePageArrows", false);
            bool hideNumbers = ReadBool(fallback, "HidePageNumbers", false);



            if (IsGearsInstalled())
            {
                XElement gearMod = FindGearsSavedMod();
                if (gearMod != null)
                {
                    on = ParseBool(ReadGearsValue(gearMod, "EnableToolbeltExtension"), on);
                    pages = ParseInt(ReadGearsValue(gearMod, "Pages"), pages, 1, 10);
                    slots = ParseInt(ReadGearsValue(gearMod, "SlotsPerRow"), slots, 10, 20);
                    hideArrows = ParseBool(ReadGearsValue(gearMod, "HidePageArrows"), hideArrows);
                    hideNumbers = ParseBool(ReadGearsValue(gearMod, "HidePageNumbers"), hideNumbers);
                }
            }

            Enabled = on;
            HidePageArrows = on && hideArrows;
            HidePageNumbers = on && hideNumbers;



            ToolbeltConfig config = ToolbeltConfig.GameConfig;
            config.SlotsPerBar = on ? slots : 10;
            config.BarsPerPage = 1;
            config.PageCount = on ? pages : 1;

            Log.Out("{0} Extension={1}, SlotsPerBar={2}, BarsPerPage={3}, Pages={4}, HideArrows={5}, HideNumbers={6}, Total={7}",
                Tag, on, config.SlotsPerBar, config.BarsPerPage, config.PageCount,
                HidePageArrows, HidePageNumbers, config.TotalSlots);
        }
        catch (Exception e)
        {

            Enabled = false;
            HidePageArrows = false;
            HidePageNumbers = false;
            ToolbeltConfig config = ToolbeltConfig.GameConfig;
            config.SlotsPerBar = 10;
            config.BarsPerPage = 1;
            config.PageCount = 1;
            Log.Error("{0} Settings failed; using vanilla toolbelt: {1}", Tag, e);
        }
    }

    private static bool IsGearsInstalled()
    {
        foreach (Mod mod in ModManager.GetLoadedMods())
            if (string.Equals(mod.Name, "Gears", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string FindFallbackFile()
    {
        foreach (Mod mod in ModManager.GetLoadedMods())
        {
            if (mod.Name != null && mod.Name.StartsWith("CATUI", StringComparison.OrdinalIgnoreCase)
                && mod.Name.IndexOf("_", StringComparison.Ordinal) < 0)
                return mod.Path + "/ToolbeltSettings.xml";
        }
        return null;
    }

    private static XElement FindGearsSavedMod()
    {
        string path = GameIO.GetUserGameDataDir() + "/Gears/ModSettings.xml";
        if (!File.Exists(path)) return null;
        XElement root = XDocument.Load(path).Root;
        if (root == null) return null;
        foreach (XElement mod in root.Elements("Mod"))
        {
            string name = (string)mod.Attribute("name");
            if (name != null && name.StartsWith("CATUI", StringComparison.OrdinalIgnoreCase))
                return mod;
        }
        return null;
    }

    private static string ReadGearsValue(XElement mod, string name)
    {
        foreach (XElement tab in mod.Elements("Tab"))
            foreach (XElement category in tab.Elements("Category"))
                foreach (XElement setting in category.Elements())
                    if ((string)setting.Attribute("name") == name)
                        return (string)setting.Attribute("value");
        return null;
    }

    private static bool ReadBool(XElement root, string name, bool fallback)
    {
        return ParseBool((string)root?.Element(name)?.Attribute("value"), fallback);
    }
    private static bool ParseBool(string text, bool fallback)
    {
        if (text == null) return fallback;
        if (string.Equals(text, "On", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "True", StringComparison.OrdinalIgnoreCase) || text == "1") return true;
        if (string.Equals(text, "Off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "False", StringComparison.OrdinalIgnoreCase) || text == "0") return false;
        return fallback;
    }
    private static int ReadInt(XElement root, string name, int fallback, int min, int max)
    {
        return ParseInt((string)root?.Element(name)?.Attribute("value"), fallback, min, max);
    }
    private static int ParseInt(string text, int fallback, int min, int max)
    {
        if (!int.TryParse(text, out int value)) return fallback;
        return Math.Max(min, Math.Min(max, value));
    }
}
