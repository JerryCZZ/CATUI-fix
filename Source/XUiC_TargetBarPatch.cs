using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using TargetBuffs;

[HarmonyPatch]
public class XUiC_TargetBarPatch
{

	private static readonly HashSet<string> NonBuffBindings = new HashSet<string>
	{
		"CATUI_fillCurrent",
		"CATUI_EntityType",
		"CATUI_EntityTags",
        "CATUI_EntityBuffsEnabled",
        "CATUI_EntityBackgroundColor",
        "CATUI_ShowNormalBackground",
	};

	    private static string GetEntityKind(EntityAlive target)
    {
        EntityClass entityClass = EntityClass.list[target.entityClass];
        bool boss = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("boss"));
        bool feral = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("feral"));
        bool radiated = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("radiated"));
        bool charged = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("charged"));
        bool infernal = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("infernal"));
        string name = entityClass.entityClassName;
        if (radiated) return "radiated";
        if (charged) return "charged";
        if (infernal) return "infernal";
        if (feral) return "feral";
        if (boss || name == "animalBear" || name == "animalZombieBear" || name == "animalDireWolf") return "boss";
        return "normal";
    }





	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_TargetBar), "Update")]
	public static void UpdatePostfix(XUiC_TargetBar __instance)
	{
		if (__instance == null)
		{
			return;
		}
		TargetBuffController.SetTarget(__instance.Target);
		TargetBuffController.Refresh();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_TargetBar), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(string bindingName, ref string value, ref bool __result, XUiC_TargetBar __instance)
	{
		EntityAlive Target = __instance.Target;


		if (NonBuffBindings.Contains(bindingName))
		{
			switch (bindingName)
			{

				case "CATUI_ShowNormalBackground":
                    value = CatuiHealthBarSettings.ShowNormalBackground ? "true" : "false";
                    __result = true;
                    return false;

                case "CATUI_EntityBackgroundColor":
                    value = "48,52,59,200";
                    if (Target != null)
                    {
                        string kind = GetEntityKind(Target);
                        value = CatuiHealthBarSettings.BackgroundColor(CatuiHealthBarSettings.VisibleEntityType(kind));
                    }
                    __result = true;
                    return false;

                case "CATUI_EntityBuffsEnabled":
                    value = CatuiHealthBarSettings.BuffsAndDebuffs ? "true" : "false";
                    __result = true;
                    return false;

                case "CATUI_fillCurrent":
					value = "0";
					if (Target != null && Target.IsAlive())
					{
						float healthPercent = (float)Target.Health / (float)Target.GetMaxHealth();
						value = (healthPercent * 1.02f).ToString("F7");
					}
					__result = true;
					return false;


				case "CATUI_EntityType":
					value = "normal";
					if (Target != null)
					{
						EntityClass entityClass = EntityClass.list[Target.entityClass];
						bool IsBoss = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("boss"));
						bool IsFeral = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("feral"));
						bool IsRadiated = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("radiated"));
						bool IsCharged = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("charged"));
						bool IsInfernal = entityClass.Tags.Test_Bit(FastTags<TagGroup.Global>.GetBit("infernal"));
						bool IsBear = entityClass.entityClassName == "animalBear";
						bool IsZombieBear = entityClass.entityClassName == "animalZombieBear";
						bool IsDireWolf = entityClass.entityClassName == "animalDireWolf";

						if (IsRadiated) {
							value = "radiated";
						}

						else if(IsCharged)
						{
							value = "charged";
						}

						else if(IsInfernal)
						{
							value = "infernal";
						}

						else if (IsFeral)
						{
							value = "feral";
						}

						else if (IsBoss || IsBear || IsZombieBear || IsDireWolf)
						{
							value = "boss";
						}
					}
					value = CatuiHealthBarSettings.VisibleEntityType(value);
                    __result = true;
					return false;


				case "CATUI_EntityTags":
					value = "";
					if (Target != null)
					{
						value = EntityClass.list[Target.entityClass].Tags.ToString();
					}
					__result = true;
					return false;
			}
		}


		if (TargetBuffController.HasBinding(bindingName))
		{
			value = TargetBuffController.GetBindingValue(bindingName);
			__result = true;
			return false;
		}


		return true;
	}
}
