using System;
using System.IO;
using System.Xml.Linq;



public static class CatuiHealthBarSettings
{
    private static readonly string[] Types = { "normal", "feral", "radiated", "infernal", "charged", "boss" };
    private static readonly string[] Labels = { "Normal", "Feral", "Radiated", "Infernal", "Charged", "Boss" };
    private static readonly string[] DefaultColors = { "48,52,59", "124,90,47", "69,113,53", "209,112,39", "60,114,174", "112,69,158" };
    private static readonly string[] Modes = { "ON", "ON", "ON", "ON", "ON", "ON" };
    private static readonly string[] CustomColors = (string[])DefaultColors.Clone();
    public static bool BuffsAndDebuffs { get; private set; } = true;

    private static int TypeIndex(string type)
    {
        for (int i = 0; i < Types.Length; i++)
            if (string.Equals(type, Types[i], StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    public static string VisibleEntityType(string type)
    {
        int index = TypeIndex(type);
        return Modes[index] == "OFF" ? "normal" : type;
    }

    public static bool ShowNormalBackground
    {
        get { return Modes[0] == "CUSTOM"; }
    }


    public static string BackgroundColor(string type)
    {
        int index = TypeIndex(type);
        string rgb = Modes[index] == "CUSTOM" ? CustomColors[index] : DefaultColors[index];
        if (Modes[index] == "OFF") rgb = DefaultColors[0];
        return rgb + ",200";
    }

    public static void Load()
    {
        try
        {
            XElement fallback = LoadXml(CatuiSettings.SettingsFilePath);
            XElement gears = IsGearsInstalled() ? FindGearsMod(LoadXml(GameIO.GetUserGameDataDir() + "/Gears/ModSettings.xml")) : null;
            for (int i = 0; i < Types.Length; i++)
            {
                string prefix = "CATUI_" + Labels[i] + "Zombie";

                string mode = ReadString(fallback, gears, prefix + "HPMode", null);
                if (string.IsNullOrWhiteSpace(mode))
                    mode = ReadString(fallback, gears, prefix + "HPBar", "ON");
                mode = mode.Trim().ToUpperInvariant();

                if (mode == "TRUE") mode = "ON";
                if (mode == "FALSE") mode = "OFF";
                Modes[i] = mode == "OFF" || mode == "CUSTOM" ? mode : "ON";
                CustomColors[i] = NormalizeRgb(ReadString(fallback, gears, prefix + "CustomColor", DefaultColors[i]), DefaultColors[i]);
            }
            BuffsAndDebuffs = Read(fallback, gears, "CATUI_EntityBuffsDebuffs");
            Log.Out("[CATUI][HealthBars] Modes: normal={0} feral={1} radiated={2} infernal={3} charged={4} boss={5}",
                Modes[0], Modes[1], Modes[2], Modes[3], Modes[4], Modes[5]);
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][HealthBars] Could not load settings; defaulting to enabled: " + ex.Message);
        }
    }

    private static string ReadString(XElement fallback, XElement gears, string name, string defaultValue)
    {
        return FindValue(gears, name, false) ?? FindValue(fallback?.Element("Global"), name, true) ?? defaultValue;
    }

    private static string NormalizeRgb(string rgb, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rgb)) return fallback;
        string[] parts = rgb.Split(',');
        if (parts.Length < 3) return fallback;
        int[] values = new int[3];
        for (int i = 0; i < 3; i++)
        {
            int value;
            if (!int.TryParse(parts[i].Trim(), out value)) return fallback;
            values[i] = Math.Max(0, Math.Min(255, value));
        }
        return string.Join(",", values[0].ToString(), values[1].ToString(), values[2].ToString());
    }

    private static XElement LoadXml(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        return XDocument.Load(path).Root;
    }

    private static bool IsGearsInstalled()
    {
        foreach (Mod mod in ModManager.GetLoadedMods())
            if (string.Equals(mod.Name, "Gears", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static XElement FindGearsMod(XElement root)
    {
        if (root == null) return null;
        foreach (XElement node in root.Elements("Mod"))
            if (string.Equals((string)node.Attribute("name"), "CATUI", StringComparison.OrdinalIgnoreCase)) return node;
        return null;
    }

    private static bool Read(XElement fallback, XElement gears, string name)
    {

        string value = FindValue(gears, name, false) ?? FindValue(fallback?.Element("Global"), name, true);
        if (string.IsNullOrEmpty(value)) return true;
        value = value.Trim();
        return !(value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                 value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0");
    }

    private static string FindValue(XElement root, string name, bool includeDefault)
    {
        if (root == null) return null;
        foreach (XElement tab in root.Elements("Tab"))
            foreach (XElement category in tab.Elements("Category"))
                foreach (XElement setting in category.Elements())
                    if ((string)setting.Attribute("name") == name)
                        return (string)setting.Attribute("value") ??
                            (includeDefault ? (string)setting.Attribute("defaultValue") : null);
        return null;
    }
}
