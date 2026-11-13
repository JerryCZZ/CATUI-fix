using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch]
public class XUiC_SkillAttributeInfoWindowPatch
{

	private static readonly Dictionary<XUiC_SkillAttributeInfoWindow, string> LastSkill = new Dictionary<XUiC_SkillAttributeInfoWindow, string>();


	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillAttributeInfoWindow), "SkillChanged")]
	public static bool SkillChangedPrefix(XUiC_SkillAttributeInfoWindow __instance)
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
			LastSkill[__instance] = null;
			return false;
		}

		string curName = __instance.CurrentSkill.Name;
		bool sameSkill = LastSkill.TryGetValue(__instance, out var prev) && prev == curName;
		LastSkill[__instance] = curName;

		int maxLevel = __instance.CurrentSkill.ProgressionClass.MaxLevel;
		int elementCount = (maxLevel > levelEntries.Count) ? maxLevel : 0;

		pager.SetLastPageByElementsAndPageLength(elementCount, skillsPerPage);

		if (sameSkill)
		{

			int currentPage = pager.GetPage();
			pager.SetPage(Mathf.Min(currentPage, pager.GetLastPage()));
		}
		else
		{

			pager.Reset();
		}
		__instance.IsDirty = true;

		return false;
	}
}