using HarmonyLib;



[HarmonyPatch(typeof(XUiC_Toolbelt), "GetBindingValueInternal")]
public static class CatuiToolbeltVisibilityPatch
{
    [HarmonyPrefix]
    public static bool Prefix(string _bindingName, ref string _value, ref bool __result)
    {
        if (_bindingName == "catui_show_page_arrows")
        {
            _value = (!CatuiToolbeltSettings.HidePageArrows).ToString();
            __result = true;
            return false;
        }
        if (_bindingName == "catui_show_page_numbers")
        {
            _value = (!CatuiToolbeltSettings.HidePageNumbers).ToString();
            __result = true;
            return false;
        }
        return true;
    }
}
