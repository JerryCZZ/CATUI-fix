using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch]
public class XUiC_SkillEntryPatch
{
	private const string TAG = "[CATUI]";

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_SkillEntry), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(string bindingName, ref string value, ref bool __result, XUiC_SkillEntry __instance)
	{
		try
		{
			return GetBindingValueInternalSafe(bindingName, ref value, ref __result, __instance);
		}
		catch (Exception ex)
		{



			Log.Error("{0} Binding '{1}' evaluation failed on skill entry. This usually means the server's progression.xml is missing or incomplete. Skill: '{2}', hierarchy: {3}",
				TAG, bindingName, GetSkillName(__instance), GetHierarchy(__instance));
			Log.Exception(ex);
			value = GetSafeDefault(bindingName);
			__result = true;
			return false;
		}
	}

	private static bool GetBindingValueInternalSafe(string bindingName, ref string value, ref bool __result, XUiC_SkillEntry __instance)
	{
		switch (bindingName)
		{

			case "rowstatecolor":
				value = (__instance.IsSelected ? "160,160,160,255" : (__instance.IsHovered ? __instance.hoverColor : __instance.rowColor));
				__result = true;
				return false;


			case "CATUI_SkillEntryDisabled":
				value = "false";
				if (__instance.currentSkill != null && GetMaxLevel(__instance) == 0)
				{
					value = "true";
				}
				__result = true;
				return false;


			case "CATUI_SkillEntryIsBuffed":
				value = "false";

                if (__instance.currentSkill != null && !IsBookGroup(__instance) && !IsCrafting(__instance))
                {

					int calculatedLevel = GetCalculatedLevel(__instance);

					int Level = __instance.currentSkill.Level;
					if (calculatedLevel > Level) {
						value = "true";
					}
				}

				__result = true;
				return false;


			case "CATUI_SkillEntryIsNerfed":
				value = "false";

				if (__instance.currentSkill != null && !IsBookGroup(__instance) && !IsCrafting(__instance))
				{

					int calculatedLevel = GetCalculatedLevel(__instance);

					int Level = __instance.currentSkill.Level;
					if (calculatedLevel < Level)
					{
						value = "true";
					}
				}
				__result = true;
				return false;	


			case "CATUI_GroupEntryCount":
				value = "0";
				if (__instance.Skill != null && __instance.Skill.ProgressionClass != null && __instance.Skill.ProgressionClass.Parent != null)
				{
					int count = 0;
					IEnumerable<ProgressionClass> children = __instance.Skill.ProgressionClass.Parent.Children;
					if (children != null)
					{
						foreach (ProgressionClass child2 in children)
						{
							if (child2 != null && !child2.IsSkill)
							{
								count++;
							}
						}
					}
					value = (count).ToString();
				}
				__result = true;
				return false;


			case "CATUI_GroupType":
				value = "skill";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null)
				{
					ProgressionClass entryClass = __instance.currentSkill.ProgressionClass;
					if (entryClass.IsCrafting) {
						value = "craft";
					}
					else if (entryClass.IsBookGroup)
					{
						value = "book";
					}
				}
				__result = true;
				return false;


			case "CATUI_GroupEntryColor":
				value = GetSkillGroupColor((__instance.currentSkill != null) ? __instance.currentSkill.ProgressionClass : null);
				__result = true;
				return false;


			case "CATUI_ParentEntryColor":
				value = "255,255,255,255";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null
					&& __instance.currentSkill.ProgressionClass.IsPerk
					&& __instance.currentSkill.ProgressionClass.Parent != null)
				{
					value = GetSkillGroupColor(__instance.currentSkill.ProgressionClass.Parent);
				}
				__result = true;
				return false;


			case "CATUI_ParentEntryIcon":
				value = "";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null
					&& __instance.currentSkill.ProgressionClass.IsPerk
					&& __instance.currentSkill.ProgressionClass.Parent != null)
				{
					value = __instance.currentSkill.ProgressionClass.Parent.Icon ?? "";
				}
				__result = true;
				return false;


			case "CATUI_ParentEntryName":
				value = "";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null
					&& __instance.currentSkill.ProgressionClass.IsPerk
					&& __instance.currentSkill.ProgressionClass.Parent != null)
				{
					value = Localization.Get(__instance.currentSkill.ProgressionClass.Parent.NameKey);
				}
				__result = true;
				return false;


			case "CATUI_GroupIcon":
				value = "";
				if (__instance.Skill != null && __instance.Skill.ProgressionClass != null && __instance.Skill.ProgressionClass.Parent != null)
				{
					value = __instance.Skill.ProgressionClass.Parent.Icon;
				}
				__result = true;
				return false;


			case "CATUI_GroupEntryType":
				value = "skill";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null)
				{
					ProgressionClass entryClass = __instance.currentSkill.ProgressionClass;
					if (entryClass.IsPerk)
					{
						value = "perk";
					}
					else if (entryClass.IsAttribute)
					{
						value = "attribute";
					}
				}
				__result = true;
				return false;


			case "CATUI_GroupEntryLevel":
				value = "0";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null)
				{

					if (__instance.currentSkill.ProgressionClass.IsBookGroup) {
						value = GetBookGroupLevel(__instance).ToString();
					}

					else
					{
						value = GetCalculatedLevel(__instance).ToString();
					}
				}
				__result = true;
				return false;


			case "CATUI_GroupEntryLevelMax":
				value = "0";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null)
				{

					if (__instance.currentSkill.ProgressionClass.IsBookGroup)
					{
						int num = CountChildren(__instance.currentSkill.ProgressionClass);
						value = Mathf.Max(0, num - 1).ToString();
					}

					else
					{
						value = GetMaxLevel(__instance).ToString();
					}
				}
				__result = true;
				return false;


			case "CATUI_GroupEntryLevelFill":
				value = "0";
				if (__instance.currentSkill != null && __instance.currentSkill.ProgressionClass != null)
				{

					if (__instance.currentSkill.ProgressionClass.IsBookGroup)
					{
						float num = CountChildren(__instance.currentSkill.ProgressionClass);
						float num2 = GetBookGroupLevel(__instance);
						num2 = Mathf.Min(num2, num - 1);
						float levelPercent = (num > 1) ? (num2 / (num - 1)) : 0f;
						value = levelPercent < 0.01f ? "0" : levelPercent.ToString("F2");
					}

					else
					{
						float Level = GetCalculatedLevel(__instance);
						float MaxLevel = GetMaxLevel(__instance);

						if (MaxLevel == 0) {
							value = "1";
						} else {
							float levelPercent = Level / MaxLevel;
							value = levelPercent < 0.01f ? "0" : levelPercent.ToString("F2");
						}
					}
				}
				__result = true;
				return false;

			default:
				return true;
		}
	}



	private static bool IsBookGroup(XUiC_SkillEntry instance)
	{
		return instance.currentSkill != null && instance.currentSkill.ProgressionClass != null && instance.currentSkill.ProgressionClass.IsBookGroup;
	}

	private static bool IsCrafting(XUiC_SkillEntry instance)
	{
		return instance.currentSkill != null && instance.currentSkill.ProgressionClass != null && instance.currentSkill.ProgressionClass.IsCrafting;
	}


	private static int GetCalculatedLevel(XUiC_SkillEntry instance)
	{
		EntityPlayerLocal entityPlayer = GetLocalPlayer(instance);
		if (instance.currentSkill == null || instance.currentSkill.ProgressionClass == null || entityPlayer == null)
		{
			return 0;
		}
		return instance.currentSkill.CalculatedLevel(entityPlayer);
	}

	private static int GetMaxLevel(XUiC_SkillEntry instance)
	{
		if (instance.currentSkill == null || instance.currentSkill.ProgressionClass == null)
		{
			return 0;
		}
		return instance.currentSkill.ProgressionClass.MaxLevel;
	}

	private static EntityPlayerLocal GetLocalPlayer(XUiC_SkillEntry instance)
	{
		try
		{
			if (instance == null || instance.xui == null || instance.xui.playerUI == null)
			{
				return null;
			}
			return instance.xui.playerUI.entityPlayer;
		}
		catch
		{
			return null;
		}
	}


	private static int GetBookGroupLevel(XUiC_SkillEntry instance)
	{
		if (instance.currentSkill == null || instance.currentSkill.ProgressionClass == null)
		{
			return 0;
		}
		EntityPlayerLocal entityPlayer = GetLocalPlayer(instance);
		if (entityPlayer == null || entityPlayer.Progression == null)
		{
			return 0;
		}
		IList<ProgressionClass> children = instance.currentSkill.ProgressionClass.Children;
		if (children == null || children.Count == 0)
		{
			return 0;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < children.Count; i++)
		{
			ProgressionClass child = children[i];
			if (child == null || string.IsNullOrEmpty(child.Name))
			{
				continue;
			}
			num++;
			ProgressionValue pv = entityPlayer.Progression.GetProgressionValue(child.Name);
			if (pv != null && pv.Level == 1)
			{
				num2++;
			}
		}
		return Mathf.Min(num2, num - 1);
	}

	private static int CountChildren(ProgressionClass progressionClass)
	{
		if (progressionClass == null || progressionClass.Children == null)
		{
			return 0;
		}
		int num = 0;
		foreach (ProgressionClass child in progressionClass.Children)
		{
			if (child != null)
			{
				num++;
			}
		}
		return num;
	}

	private static string GetSkillName(XUiC_SkillEntry instance)
	{
		try
		{
			if (instance != null && instance.currentSkill != null && instance.currentSkill.ProgressionClass != null)
			{
				return instance.currentSkill.Name;
			}
		}
		catch
		{
		}
		return "null";
	}

	private static string GetHierarchy(XUiC_SkillEntry instance)
	{
		try
		{
			if (instance != null)
			{
				return instance.GetXuiHierarchy();
			}
		}
		catch
		{
		}
		return "unknown";
	}


	private static string GetSafeDefault(string bindingName)
	{
		switch (bindingName)
		{
			case "CATUI_GroupEntryCount":
			case "CATUI_GroupEntryLevel":
			case "CATUI_GroupEntryLevelMax":
			case "CATUI_GroupEntryLevelFill":
				return "0";
			case "CATUI_SkillEntryDisabled":
			case "CATUI_SkillEntryIsBuffed":
			case "CATUI_SkillEntryIsNerfed":
				return "false";
			case "rowstatecolor":
			case "CATUI_GroupEntryColor":
			case "CATUI_ParentEntryColor":
				return "255,255,255,255";
			case "CATUI_GroupType":
			case "CATUI_GroupEntryType":
				return "skill";
			default:
				return "";
		}
	}


	private static string GetSkillGroupColor(ProgressionClass groupClass)
	{
		if (groupClass == null || !groupClass.IsSkill || groupClass.Children == null || groupClass.Children.Count == 0)
		{
			return "255,255,255,255";
		}
		ProgressionClass parent = groupClass.Parent;
		if (parent == null || parent.Children == null)
		{
			return "255,255,255,255";
		}
		int idx = 0;
		foreach (ProgressionClass child in parent.Children)
		{
			if (child == null || !child.IsSkill)
			{
				continue;
			}
			if (child == groupClass)
			{
				if (Constants.TrackedFriendColors.Length > 0)
				{
					Color32 color = Constants.TrackedFriendColors[idx % Constants.TrackedFriendColors.Length];
					return string.Format("{0},{1},{2},{3}", color.r, color.g, color.b, color.a);
				}
				return "255,255,255,255";
			}
			idx++;
		}
		return "255,255,255,255";
	}
}
