using System;
using System.Reflection;




public static class CatuiGearsUiGate
{
    private static bool connected;
    private static object[] dependents;
    private static Delegate callback;

    public static void TryConnect()
    {
        if (connected) return;
        try
        {
            Type manager = Type.GetType("GearsAPI.Settings.GearsSettingsManager, GearsAPI", false);
            if (manager == null) return;
            MethodInfo getter = manager.GetMethod("GetGearsMod", BindingFlags.Public | BindingFlags.Static);
            object mod = getter?.Invoke(null, new object[] { "CATUI" });
            if (mod == null) return;
            object global = GetProp(mod, "GlobalSettings");
            object tab = InvokeNamed(global, "GetTab", "Toolbelt");
            object category = InvokeNamed(tab, "GetCategory", "Toolbelt");
            if (category == null) return;

            Assembly api = manager.Assembly;
            Type switchOpen = api.GetType("GearsAPI.Settings.Global.ISwitchGlobalSetting`1");
            Type sliderOpen = api.GetType("GearsAPI.Settings.Global.ISliderGlobalSetting`1");
            if (switchOpen == null || sliderOpen == null) return;

            object enable = GetSetting(category, switchOpen.MakeGenericType(typeof(bool)), "EnableToolbeltExtension");
            if (enable == null) return;
            dependents = new[]
            {
                GetSetting(category, sliderOpen.MakeGenericType(typeof(int)), "Pages"),
                GetSetting(category, sliderOpen.MakeGenericType(typeof(int)), "SlotsPerRow"),
                GetSetting(category, switchOpen.MakeGenericType(typeof(bool)), "HidePageArrows"),
                GetSetting(category, switchOpen.MakeGenericType(typeof(bool)), "HidePageNumbers")
            };
            foreach (object setting in dependents)
                if (setting == null) { dependents = null; return; }


            EventInfo changed = enable.GetType().GetEvent("OnSelectedChanged");
            if (changed == null)
            {

                foreach (Type iface in enable.GetType().GetInterfaces())
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == api.GetType("GearsAPI.Settings.IValueModSetting`1"))
                    { changed = iface.GetEvent("OnSelectedChanged"); break; }
            }
            if (changed == null) return;
            MethodInfo handler = typeof(CatuiGearsUiGate).GetMethod(nameof(EnabledSelected), BindingFlags.Static | BindingFlags.Public);
            callback = Delegate.CreateDelegate(changed.EventHandlerType, handler);
            changed.AddEventHandler(enable, callback);
            connected = true;
            bool selected = Convert.ToBoolean(GetProp(enable, "SelectedValue"));
            SetEnabled(selected);
            Log.Out("[CATUI][Gears] Toolbelt dependent settings connected.");
        }
        catch (Exception e)
        {
            Log.Warning("[CATUI][Gears] Couldn't link toolbelt controls: " + e.Message);
        }
    }


    public static void EnabledSelected(object setting, bool selected)
    {
        SetEnabled(selected);
    }

    private static void SetEnabled(bool active)
    {
        if (dependents == null) return;
        foreach (object s in dependents)
        {
            if (s == null) continue;
            PropertyInfo property = s.GetType().GetProperty("Enabled");
            if (property == null)
            {
                foreach (Type iface in s.GetType().GetInterfaces())
                    if (iface.FullName == "GearsAPI.Settings.IModSetting")
                    { property = iface.GetProperty("Enabled"); break; }
            }
            if (property != null) property.SetValue(s, active, null);
        }
    }

    private static object GetProp(object obj, string name) => obj?.GetType().GetProperty(name)?.GetValue(obj, null);
    private static object InvokeNamed(object obj, string method, string name)
    {
        return obj?.GetType().GetMethod(method, new[] { typeof(string) })?.Invoke(obj, new object[] { name });
    }
    private static object GetSetting(object category, Type settingType, string name)
    {
        foreach (MethodInfo method in category.GetType().GetMethods())
        {
            if (method.Name != "GetSetting" || !method.IsGenericMethodDefinition) continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                return method.MakeGenericMethod(settingType).Invoke(category, new object[] { name });
        }
        return null;
    }
}
