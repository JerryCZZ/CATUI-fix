using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch]
public class XUiC_SkillPerkInfoWindowPatch
{
	// 各窗口当前展示的技能名，用于区分"购买刷新"与"切换技能"
	private static readonly Dictionary<XUiC_SkillPerkInfoWindow, string> LastSkill = new Dictionary<XUiC_SkillPerkInfoWindow, string>();

	// 技能为6的时候无法翻页的bug修复 + 购买技能点后保留当前页
	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillPerkInfoWindow), "SkillChanged")]
	public static bool SkillChangedPrefix(XUiC_SkillPerkInfoWindow __instance)
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
			// 同技能刷新（如购买技能点触发的整窗刷新）：保留当前页，仅越界时收尾
			int currentPage = pager.GetPage();
			pager.SetPage(Mathf.Min(currentPage, pager.GetLastPage()));
		}
		else
		{
			// 切换技能：回到第一页
			pager.Reset();
		}
		__instance.IsDirty = true;

		return false;
	}
}
