using HarmonyLib;
using System.Collections.Generic;

[HarmonyPatch]
public class XUiC_SkillCraftingInfoWindowPatch
{

	private static void ApplyScrollEventToChildren(XUiController _controller)
	{
		if (_controller == null || _controller.ViewComponent == null)
			return;
		
		_controller.ViewComponent.EventOnScroll = true;
		foreach (XUiController child in _controller.Children)
		{
			ApplyScrollEventToChildren(child);
		}
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoWindow), "Init")]
	public static void Init_Postfix(XUiC_SkillCraftingInfoWindow __instance)
	{
		foreach (XUiC_SkillCraftingInfoEntry levelEntry in __instance.levelEntries)
		{

			ApplyScrollEventToChildren(levelEntry);
			levelEntry.OnScroll += (XUiController _sender, float _delta) =>
			{
				if (_delta > 0f)
				{
					__instance.pager?.PageDown();
				}
				else
				{
					__instance.pager?.PageUp();
				}
			};
		}
	}


	private static List<ProgressionClass.DisplayData> RebuildDisplayDataList(ProgressionClass progressionClass)
	{
		List<ProgressionClass.DisplayData> newDisplayDataList = new List<ProgressionClass.DisplayData>();
		if (progressionClass != null && progressionClass.DisplayDataList != null)
		{
			for (int i = 0; i < progressionClass.DisplayDataList.Count; i++)
			{
				if (progressionClass.DisplayDataList[i].UnlockDataList != null)
				{
					for (int j = 0; j < progressionClass.DisplayDataList[i].UnlockDataList.Count; j++)
					{
						ProgressionClass.DisplayData originalDisplayData = progressionClass.DisplayDataList[i];
						ProgressionClass.DisplayData.UnlockData unlockData = originalDisplayData.UnlockDataList[j];
						ProgressionClass.DisplayData newDisplayData = new ProgressionClass.DisplayData();
						newDisplayData.CustomHasQuality = originalDisplayData.CustomHasQuality;
						newDisplayData.CustomIcon = originalDisplayData.CustomIcon;
						newDisplayData.CustomIconTint = originalDisplayData.CustomIconTint;
						newDisplayData.item = originalDisplayData.item;
						newDisplayData.Owner = originalDisplayData.Owner;
						newDisplayData.QualityStarts = originalDisplayData.QualityStarts;
						newDisplayData.UnlockDataList = new List<ProgressionClass.DisplayData.UnlockData>();
						newDisplayData.UnlockDataList.Add(unlockData);
						newDisplayData.ItemName = unlockData.ItemName;
						newDisplayDataList.Add(newDisplayData);
					}
				}
			}
		}
		return newDisplayDataList;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoWindow), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(ref string _value, string _bindingName, ref bool __result, XUiC_SkillCraftingInfoWindow __instance)
	{
		switch (_bindingName)
		{
			case "showPaging":
				_value = "false";
				if (__instance.CurrentSkill != null)
				{
					ProgressionClass progressionClass = __instance.CurrentSkill.ProgressionClass;
					if (progressionClass != null && progressionClass.DisplayDataList != null)
					{
						int elementCount = 0;
						for (int i = 0; i < progressionClass.DisplayDataList.Count; i++)
						{
							if (progressionClass.DisplayDataList[i].UnlockDataList != null)
							{
								elementCount += progressionClass.DisplayDataList[i].UnlockDataList.Count;
							}
						}
						int skillsPerPage = __instance.levelEntries.Count - __instance.hiddenEntriesWithPaging;
						_value = (elementCount > skillsPerPage).ToString();
					}
				}
				__result = true;
				return false;
			default:
				return true;
		}
	}


	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoWindow), "SkillChanged")]
	public static bool SkillChangedPrefix(XUiC_SkillCraftingInfoWindow __instance)
	{
		var levelEntries = __instance.levelEntries;
		int skillsPerPage = __instance.skillsPerPage;
		var pager = __instance.pager;

		if (pager == null || levelEntries == null)
			return true;

		if (__instance.CurrentSkill == null)
		{
			pager.SetLastPageByElementsAndPageLength(0, skillsPerPage);
			pager.Reset();
			__instance.IsDirty = true;
			__instance.SelectedData = null;
			__instance.SelectedEntry = null;
			return false;
		}


		ProgressionClass progressionClass = __instance.CurrentSkill.ProgressionClass;
		List<ProgressionClass.DisplayData> newDisplayDataList = RebuildDisplayDataList(progressionClass);
		int elementCount = newDisplayDataList.Count;

		pager.SetLastPageByElementsAndPageLength(elementCount, skillsPerPage);
		pager.Reset();
		__instance.IsDirty = true;
		__instance.SelectedData = null;
		__instance.SelectedEntry = null;

		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoWindow), "UpdateSkill")]
	public static bool UpdateSkill_Prefix(XUiC_SkillCraftingInfoWindow __instance)
	{

		ProgressionValue CurrentSkill = __instance.CurrentSkill;
		XUiC_ItemActionList actionItemList = __instance.actionItemList;
		int skillsPerPage = __instance.skillsPerPage;
		XUiC_Paging pager = __instance.pager;
		XUiWindowGroup windowGroup = __instance.windowGroup;
		List<XUiC_SkillCraftingInfoEntry> levelEntries = __instance.levelEntries;

		if (CurrentSkill != null && actionItemList != null)
		{
			actionItemList.SetCraftingActionList(XUiC_ItemActionList.ItemActionListTypes.Skill, __instance);
		}
		int num = (pager?.GetPage() ?? 0) * skillsPerPage;
		ProgressionClass progressionClass = ((CurrentSkill != null) ? CurrentSkill.ProgressionClass : null);
		if (progressionClass != null && progressionClass.DisplayDataList != null)
		{
			XUiC_SkillEntry entryForSkill = windowGroup.Controller.GetChildByType<XUiC_SkillList>().GetEntryForSkill(CurrentSkill);
			{

				List<ProgressionClass.DisplayData> newDisplayDataList = RebuildDisplayDataList(progressionClass);

				foreach (XUiC_SkillCraftingInfoEntry levelEntry in levelEntries)
				{

					ProgressionClass.DisplayData data = (newDisplayDataList.Count > num) ? newDisplayDataList[num] : null;
					levelEntry.Data = data;
					levelEntry.IsDirty = true;
					if (entryForSkill != null)
					{
						levelEntry.ViewComponent.NavLeftTarget = entryForSkill.ViewComponent;
					}
					num++;
				}
				return false;
			}
		}
		foreach (XUiC_SkillCraftingInfoEntry levelEntry2 in levelEntries)
		{
			levelEntry2.Data = null;
			levelEntry2.IsDirty = true;
		}

		return false;
	}


	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillCraftingInfoWindow), "Entry_OnPress")]
	public static bool Entry_OnPress_Prefix(XUiC_SkillCraftingInfoWindow __instance, XUiController _sender, int _mouseButton)
	{
		XUi xUi = _sender.xui;
		XUiC_SkillCraftingInfoEntry xUiC_SkillCraftingInfoEntry = _sender as XUiC_SkillCraftingInfoEntry;

		if (xUiC_SkillCraftingInfoEntry?.Data?.GetUnlockItem(0) == null)
		{
			return false;
		}

        xUi.playerUI.windowManager.Close("looting");
        List<XUiC_RecipeList> childrenByType = xUi.GetChildrenByType<XUiC_RecipeList>();
        XUiC_RecipeList xUiC_RecipeList = null;
        for (int i = 0; i < childrenByType.Count; i++)
        {
            if (childrenByType[i].WindowGroup != null && childrenByType[i].WindowGroup.isShowing)
            {
                xUiC_RecipeList = childrenByType[i];
                break;
            }
        }

        if (xUiC_RecipeList == null)
        {
            XUiC_WindowSelector.OpenSelectorAndWindow(xUi.playerUI.entityPlayer, "crafting");
            xUiC_RecipeList = xUi.GetChildByType<XUiC_RecipeList>();
        }

		xUiC_RecipeList?.SetRecipeDataByItem(xUiC_SkillCraftingInfoEntry.Data.GetUnlockItem(0).Id);
		return false;
	}

}
