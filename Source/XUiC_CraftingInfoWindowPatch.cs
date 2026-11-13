using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public class XUiC_CraftingInfoWindowPatch
{
    public const int TABTYPE_STAT = 3;


    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), "SetSelectedButtonByType")]
    [HarmonyPostfix]
    public static void SetSelectedButtonByType_Postfix(XUiC_CraftingInfoWindow __instance)
    {
        int currentTabType = (int)__instance.TabType;
        XUiController statButton = __instance.GetChildById("statButton");
        ((XUiV_Button)statButton.ViewComponent).Selected = currentTabType == TABTYPE_STAT;
    }


    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), "Init")]
    [HarmonyPostfix]
    public static void Init_Postfix(XUiC_CraftingInfoWindow __instance)
    {
        XUiController statButton = __instance.GetChildById("statButton");
        statButton.OnPress += (sender, mouseButton) =>
        {
            __instance.TabType = (XUiC_CraftingInfoWindow.TabTypes)TABTYPE_STAT;
            __instance.SetSelectedButtonByType(__instance.TabType);
            __instance.IsDirty = true;
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), "GetBindingValueInternal")]
    public static bool GetBindingValueInternalPrefix(string bindingName, ref string value, ref bool __result, XUiC_CraftingInfoWindow __instance)
    {
        if (__instance == null)
            return false;
        switch (bindingName)
        {

            case "showStatTab":
                value = "false";
                if (__instance.recipe != null)
                {
                    int itemTier = __instance.selectedCraftingTier;
                    int itemId = __instance.recipe.itemValueType;
                    ItemValue itemValue = new ItemValue(itemId, itemTier, itemTier);
                    ItemDisplayEntry itemDisplayEntry = UIDisplayInfoManager.Current.GetDisplayStatsForTag(itemValue.ItemClass.IsBlock() ? Block.list[itemValue.type].DisplayType : itemValue.ItemClass.DisplayType);
                    if (itemValue?.ItemClass != null && itemDisplayEntry != null)
                    {
                        value = (itemDisplayEntry.DisplayStats.Count > 0).ToString();
                    }
                }
                __result = true;
                return false;


            case "showstats":
                value = "false";
                var tabType = typeof(XUiC_CraftingInfoWindow).GetField("TabType", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
                value = ((int)tabType == TABTYPE_STAT).ToString();
                __result = true;
                return false;

            default:

                if (bindingName.StartsWith("itemstattitle"))
                {
					return GetStat(bindingName, "itemstattitle", ref value, ref __result, __instance, (entry, displayInfo) => {
						if (displayInfo.TitleOverride != null)
							return displayInfo.TitleOverride;
						return UIDisplayInfoManager.Current.GetLocalizedName(displayInfo.StatType);
					});
                }

                if (bindingName.StartsWith("itemstat"))
                {
                    return GetStat(bindingName, "itemstat", ref value, ref __result, __instance, (entry, displayInfo) => {
						return XUiM_ItemStack.GetStatItemValueTextWithCompareInfo(entry, ItemValue.None, __instance.xui.playerUI.entityPlayer, displayInfo);
					});
                }
                return true;
        }
    }

    private static bool GetStat(string bindingName, string prefix, ref string value, ref bool __result,
        XUiC_CraftingInfoWindow instance, Func<ItemValue, DisplayInfoEntry, string> valueGenerator)
    {
        try
        {
            if (instance == null || string.IsNullOrEmpty(bindingName) || instance.recipe == null)
            {
                value = string.Empty;
                __result = true;
                return false;
            }


            int index;
            if (bindingName.Length <= prefix.Length || !int.TryParse(bindingName.Substring(prefix.Length), out index))
            {
                value = string.Empty;
                __result = true;
                return false;
            }


            int itemTier = instance.selectedCraftingTier;
            int itemId = instance.recipe.itemValueType;
            ItemValue itemValue = new ItemValue(itemId, itemTier, itemTier);

            if (itemValue?.ItemClass == null)
            {
                value = string.Empty;
                __result = true;
                return false;
            }


            ItemDisplayEntry itemDisplayEntry = UIDisplayInfoManager.Current.GetDisplayStatsForTag(
                itemValue.ItemClass.IsBlock() ? Block.list[itemValue.type].DisplayType : itemValue.ItemClass.DisplayType);
            if (itemDisplayEntry == null || itemDisplayEntry.DisplayStats == null ||
                index < 0 || index >= itemDisplayEntry.DisplayStats.Count)
            {
                value = string.Empty;
                __result = true;
                return false;
            }


            DisplayInfoEntry displayInfoEntry = itemDisplayEntry.DisplayStats[index];
            value = valueGenerator(itemValue, displayInfoEntry);
            __result = true;
            return false;
        }
        catch (Exception ex)
        {

			Debug.LogError($"[CATUI] XUiC_CraftingInfoWindowPatch: Error GetStat '{bindingName}': {ex.Message}\n{ex.StackTrace}");
            value = string.Empty;
            __result = true;
            return false;
        }
    }
}
