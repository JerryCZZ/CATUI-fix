


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

public static class CatuiDamageNumbers
{
    private static bool loaded;
    public static bool Enabled { get; private set; }
    public static Color NumberColor { get; private set; } = Color.white;
    public static bool Outline { get; private set; }
    public static Color OutlineColor { get; private set; } = Color.black;
    public static int OutlineThickness { get; private set; } = 1;
    public static int TextSizePercent { get; private set; } = 100;



    private sealed class OriginalFontSize { public float Size; public OriginalFontSize(float size) { Size = size; } }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Component, OriginalFontSize> OriginalSizes =
        new System.Runtime.CompilerServices.ConditionalWeakTable<Component, OriginalFontSize>();

    public static void Reload()
    {
        Enabled = false;
        NumberColor = Color.white;
        Outline = false;
        OutlineColor = Color.black;
        OutlineThickness = 1;
        TextSizePercent = 100;
        loaded = true;
        try
        {
            XElement fallback = ReadXml(CatuiSettings.SettingsFilePath);
            XElement save = ReadXml(Path.Combine(GameIO.GetUserGameDataDir(), "Gears", "ModSettings.xml"));
            XElement gears = save == null ? null : save.Elements("Mod").FirstOrDefault(e =>
                string.Equals((string)e.Attribute("name"), "CATUI", StringComparison.OrdinalIgnoreCase));
            Enabled = IsOn(Get(gears, fallback, "CATUI_DMGNumberColors", "false"));
            NumberColor = Rgb(Get(gears, fallback, "CATUI_DMGNumberColor", "255,255,255"), Color.white);
            Outline = IsOn(Get(gears, fallback, "CATUI_DMGOutline", "false"));
            OutlineColor = Rgb(Get(gears, fallback, "CATUI_DMGOutlineColor", "0,0,0"), Color.black);
            int thickness;
            if (int.TryParse(Get(gears, fallback, "CATUI_DMGOutlineThickness", "1"), out thickness))
                OutlineThickness = Mathf.Clamp(thickness, 1, 4);
            int sizePercent;
            if (int.TryParse(Get(gears, fallback, "CATUI_DMGTextSize", "100"), out sizePercent))
                TextSizePercent = Mathf.Clamp(sizePercent, 50, 200);
            Log.Out("[CATUI][DMG] Enabled={0}, Outline={1}, Thickness={2}, TextSize={3}%", Enabled, Outline, OutlineThickness, TextSizePercent);
        }
        catch (Exception ex) { Log.Warning("[CATUI][DMG] Settings read failed: " + ex.Message); }
    }

    private static XElement ReadXml(string path) { return File.Exists(path) ? XDocument.Load(path).Root : null; }
    private static string Get(XElement save, XElement fallback, string name, string initial)
    {
        var found = Find(save, name, false) ?? Find(fallback?.Element("Global"), name, true);
        return found ?? initial;
    }
    private static string Find(XElement root, string name, bool defaultAllowed)
    {
        if (root == null) return null;
        foreach (var setting in root.Descendants().Where(e => (string)e.Attribute("name") == name))
            return (string)setting.Attribute("value") ?? (defaultAllowed ? (string)setting.Attribute("defaultValue") : null);
        return null;
    }
    private static bool IsOn(string value) { return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) || value == "1"; }
    private static Color Rgb(string text, Color backup)
    {
        if (string.IsNullOrWhiteSpace(text)) return backup;
        string[] parts = text.Trim().TrimStart('#').Split(',');
        if (parts.Length >= 3)
        {
            int r,g,b;
            if (int.TryParse(parts[0], out r) && int.TryParse(parts[1], out g) && int.TryParse(parts[2], out b))
                return new Color32((byte)Mathf.Clamp(r,0,255),(byte)Mathf.Clamp(g,0,255),(byte)Mathf.Clamp(b,0,255),255);
        }
        Color c;
        return ColorUtility.TryParseHtmlString(text, out c) ? c : backup;
    }

    private static void ApplySize(Component component, float current, Action<float> apply)
    {
        if (TextSizePercent == 100 || current <= 0f) return;
        OriginalFontSize original = OriginalSizes.GetValue(component, key => new OriginalFontSize(current));
        float desired = original.Size * TextSizePercent / 100f;
        if (!Mathf.Approximately(current, desired)) apply(desired);
    }

    internal static void Style(object instance)
    {
        if (!loaded) Reload();
        if (!Enabled) return;
        Component component = instance as Component;
        if (component == null || !component) return;

        foreach (TMP_Text tmp in component.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!tmp) continue;
            Color previous = tmp.color;
            tmp.color = new Color(NumberColor.r, NumberColor.g, NumberColor.b, previous.a);
            ApplySize(tmp, tmp.fontSize, s => tmp.fontSize = s);

            if (Outline)
            {
                tmp.outlineColor = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 1f);
                tmp.outlineWidth = Mathf.Clamp01(OutlineThickness * 0.10f);
            }
        }

        foreach (TextMesh mesh in component.GetComponentsInChildren<TextMesh>(true))
        {
            if (!mesh) continue;
            Color previous = mesh.color;
            mesh.color = new Color(NumberColor.r, NumberColor.g, NumberColor.b, previous.a);
            ApplySize(mesh, mesh.characterSize, s => mesh.characterSize = s);
        }

        foreach (Component child in component.GetComponentsInChildren<Component>(true))
        {
            if (child == null || child.GetType().Name != "UILabel") continue;
            try
            {
                Type t = child.GetType();
                PropertyInfo color = t.GetProperty("color");
                if (color != null && color.CanRead && color.CanWrite && color.PropertyType == typeof(Color))
                {
                    Color old = (Color)color.GetValue(child, null);
                    color.SetValue(child, new Color(NumberColor.r, NumberColor.g, NumberColor.b, old.a), null);
                }
                PropertyInfo fontSize = t.GetProperty("fontSize");
                if (fontSize != null && fontSize.CanRead && fontSize.CanWrite && fontSize.PropertyType == typeof(int))
                {
                    int currentSize = (int)fontSize.GetValue(child, null);
                    ApplySize(child, currentSize, value => fontSize.SetValue(child, Mathf.Max(1, Mathf.RoundToInt(value)), null));
                }
                if (Outline)
                {
                    PropertyInfo effectStyle = t.GetProperty("effectStyle");
                    PropertyInfo effectColor = t.GetProperty("effectColor");
                    if (effectStyle != null && effectStyle.CanWrite)
                        effectStyle.SetValue(child, Enum.Parse(effectStyle.PropertyType, "Outline"), null);
                    if (effectColor != null && effectColor.CanWrite)
                        effectColor.SetValue(child, OutlineColor, null);
                }
            }
            catch { /* Keep rendering even if this particular version exposes different label properties. */ }
        }
    }
}

[HarmonyPatch]
internal static class CatuiDamageNumbersPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type type = AccessTools.TypeByName("DamageText");
        if (type == null)
        {
            Log.Warning("[CATUI][DMG] DamageText class not found; leaving vanilla behavior unchanged.");
            return Enumerable.Empty<MethodBase>();
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "OnEnable", "Start", "Update", "LateUpdate", "SetText", "SetDamage", "ShowDamage", "Init", "Initialize" };
        MethodBase[] targets = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsAbstract && !m.IsGenericMethod && m.ReturnType == typeof(void) && names.Contains(m.Name))
            .Cast<MethodBase>().ToArray();
        Log.Out("[CATUI][DMG] DamageText patch targets: {0}", string.Join(", ", targets.Select(x => x.Name).ToArray()));
        return targets;
    }
    private static void Postfix(object __instance)
    {
        try { CatuiDamageNumbers.Style(__instance); }
        catch (Exception ex) { Log.Warning("[CATUI][DMG] Styling error: " + ex.Message); }
    }
}
