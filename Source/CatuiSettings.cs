using System.IO;
using System.Xml.Linq;
using UnityEngine;















public static class CatuiSettings
{
    private const string TAG = "[CATUI]";
    private const string SwitchName = "CATUI_LocalLoadEnabled";


    public static string SettingsFilePath
    {
        get
        {
            string path = null;
            foreach (Mod mod in ModManager.GetLoadedMods())
            {
                if (mod.Name != null && mod.Name.StartsWith("CATUI", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = mod.Path + "/ModSettings.xml";
                    break;
                }
            }
            return path;
        }
    }


    public static string GearsSavedSettingsFilePath
    {
        get { return GameIO.GetUserGameDataDir() + "/Gears/ModSettings.xml"; }
    }





    public static void Load()
    {
        string value = ReadFromGearsSave();
        string source = "Gears save";

        if (value == null)
        {
            value = ReadFromModFile();
            source = "ModSettings.xml";
        }

        if (value == null)
        {
            Log.Out("{0} 'LocalLoadEnabled' not found, using default (On).", TAG);
            return;
        }

        bool enabled = ParseOnOff(value);
        LocalLoadPatch.EnableLocalLoad = enabled;
        Log.Out("{0} ModSettings: LocalLoadEnabled = {1} (from {2})", TAG, enabled ? "On" : "Off", source);
    }


    private static string ReadFromGearsSave()
    {
        string path = GearsSavedSettingsFilePath;
        if (path == null || !SdFile.Exists(path))
        {
            return null;
        }
        try
        {
            XElement root = XElement.Load(path);
            foreach (XElement mod in root.Elements("Mod"))
            {
                string modName = mod.Attribute("name")?.Value;
                if (modName != null && modName.StartsWith("CATUI", System.StringComparison.OrdinalIgnoreCase))
                {
                    return FindSettingValue(mod, SwitchName);
                }
            }
        }
        catch (System.Exception e)
        {
            Log.Warning("{0} Failed to parse Gears saved settings at '{1}': {2}", TAG, path, e.Message);
        }
        return null;
    }


    private static string ReadFromModFile()
    {
        string path = SettingsFilePath;
        if (path == null || !SdFile.Exists(path))
        {
            return null;
        }
        try
        {
            XElement root = XElement.Load(path);
            XElement global = root.Element("Global");
            if (global == null)
            {
                return null;
            }
            foreach (XElement tab in global.Elements("Tab"))
            {
                foreach (XElement category in tab.Elements("Category"))
                {
                    foreach (XElement setting in category.Elements())
                    {
                        string name = setting.Attribute("name")?.Value;
                        if (name == SwitchName)
                        {
                            return setting.Attribute("value")?.Value ?? setting.Attribute("defaultValue")?.Value;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Log.Warning("{0} Failed to parse ModSettings.xml at '{1}': {2}", TAG, path, e.Message);
        }
        return null;
    }


    private static string FindSettingValue(XElement modRoot, string settingName)
    {
        foreach (XElement tab in modRoot.Elements("Tab"))
        {
            foreach (XElement category in tab.Elements("Category"))
            {
                foreach (XElement setting in category.Elements())
                {
                    string name = setting.Attribute("name")?.Value;
                    if (name == settingName)
                    {
                        return setting.Attribute("value")?.Value;
                    }
                }
            }
        }
        return null;
    }


    private static bool ParseOnOff(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }
        switch (value.Trim().ToLowerInvariant())
        {
            case "off":
            case "low":
            case "false":
            case "0":
            case "disabled":
            case "disable":
                return false;
            default:
                return true;
        }
    }
}
