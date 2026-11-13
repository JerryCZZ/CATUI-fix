using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

[HarmonyPatch]
public class XUiC_SkillCraftingInfoEntryPatch
{

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<XUiC_SkillCraftingInfoEntry, bool> hoveredStates = new System.Collections.Concurrent.ConcurrentDictionary<XUiC_SkillCraftingInfoEntry, bool>();


    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiController), "OnHovered")]
    public static void OnHovered_Postfix(XUiController __instance, bool _isOver)
    {
        XUiC_SkillCraftingInfoEntry entry = __instance as XUiC_SkillCraftingInfoEntry;
        if (entry != null)
        {
            hoveredStates[entry] = _isOver;
            entry.IsDirty = true;
        }
    }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiController), "OnClose")]
    public static void OnClosePostfix(XUiController __instance)
    {
        if (__instance is XUiC_SkillCraftingInfoEntry entry)
        {
            hoveredStates.TryRemove(entry, out _);
        }
    }

    [HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoEntry), "GetBindingValueInternal")]
	public static bool Prefix(string _bindingName, ref string _value, ref bool __result, XUiC_SkillCraftingInfoEntry __instance)
	{
		bool flag = __instance.data != null;
		EntityPlayerLocal entityPlayer = __instance.xui.playerUI.entityPlayer;
		ProgressionClass.DisplayData data = __instance.data;
        switch (_bindingName)
		{

            case "showlock":
                _value = "false";
                if (flag)
                {
                    _value = data.GetUnlockItemLocked(entityPlayer, 0).ToString();
                }
                __result = true;
                return false;


            case "iconatlas":
                _value = "ItemIconAtlas";
                if (flag)
                {
                    _value = data.GetUnlockItemIconAtlas(entityPlayer, 0).ToString();
                }
                __result = true;
                return false;


            case "CATUI_SkillLevel":
                _value = "";
                if (flag)
                {
                    _value = entityPlayer.Progression.GetProgressionValue(data.Owner.Name).Level.ToString();
                }
                __result = true;
                return false;


            case "CATUI_ItemUnlockLevel":
                _value = "";
                if (flag)
                {
                    ProgressionClass.DisplayData.UnlockData unlockData = data.GetUnlockData(0);
                    if (unlockData != null)
                    {
                        _value = data.QualityStarts[unlockData.UnlockTier].ToString();
                    }
                }
                __result = true;
                return false;


            case "CATUI_QualityNextLevelFill":
                _value = "0";
                if (flag)
                {
                    float SkillLevel = entityPlayer.Progression.GetProgressionValue(data.Owner.Name).Level;
                    float QualityNextLevel = data.GetNextPoints(entityPlayer.Progression.GetProgressionValue(data.Owner.Name).Level);

                    if (QualityNextLevel > 0)
                    {
                        float percent = SkillLevel / QualityNextLevel;
                        _value = percent < 0.01f ? "0" : percent.ToString("F3");
                    }
                }
                __result = true;
                return false;


            case "CATUI_isHovered":
                _value = hoveredStates.TryGetValue(__instance, out bool isHovered) ? isHovered.ToString() : "false";
                __result = true;
                return false;

            default:
				return true;
		}
	}
}
