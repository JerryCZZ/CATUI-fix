using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public class XUiC_CollectedItemListPatch
{


	private static void SetLabels(GameObject item, string nameText, string countText)
	{
		if (item == null)
		{
			return;
		}
		Transform t = item.transform.Find("ItemName");
		if (t != null)
		{
			UILabel label = t.GetComponent<UILabel>();
			if (label != null)
			{
				label.text = nameText;
			}
		}
		t = item.transform.Find("TextContent");
		if (t != null)
		{
			UILabel label = t.GetComponent<UILabel>();
			if (label != null)
			{
				label.text = countText;
			}
		}
	}



	private static void SetDurabilityColor(GameObject item, ItemValue itemValue)
	{
		if (item == null)
		{
			return;
		}
		Transform t = item.transform.Find("ItemDurabilityColor");
		if (t == null)
		{
			return;
		}
		UISprite sprite = t.GetComponent<UISprite>();
		if (sprite == null)
		{
			return;
		}
		if (itemValue != null && itemValue.ItemClass != null && itemValue.ItemClass.ShowQualityBar)
		{
			sprite.color = QualityInfo.GetQualityColor(itemValue.Quality);
		}
		else
		{
			sprite.color = new Color(1f, 1f, 1f, 0f);
		}
	}




	private static void FillItemText(XUiC_CollectedItemList instance, ItemStack _is, bool negative)
	{
		if (_is == null || _is.itemValue == null)
		{
			return;
		}

		for (int i = instance.items.Count - 1; i >= 0; i--)
		{
			var data = instance.items[i];
			if (data.ItemStack == null || data.ItemStack.itemValue.type != _is.itemValue.type || data.isNegative != negative)
			{
				continue;
			}

			SetDurabilityColor(data.Item, data.ItemStack.itemValue);
			if (data.isMissingNotifier)
			{
				return;
			}
			int count = data.ItemStack.count;
			string countText = negative
				? ((count >= 0) ? ("-" + count) : count.ToString())
				: ((count >= 1) ? ("+" + count) : count.ToString());
			SetLabels(data.Item, data.ItemStack.itemValue.ItemClass.GetLocalizedItemName(), countText);
			return;
		}
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_CollectedItemList), "AddItemStack")]
	public static void AddItemStackPostfix(XUiC_CollectedItemList __instance, ItemStack _is)
	{
		FillItemText(__instance, _is, _is != null && _is.count < 0);
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_CollectedItemList), "RemoveItemStack")]
	public static void RemoveItemStackPostfix(XUiC_CollectedItemList __instance, ItemStack _is)
	{
		FillItemText(__instance, _is, true);
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_CollectedItemList), "AddIconNotification")]
	public static void AddIconNotificationPostfix(XUiC_CollectedItemList __instance, string iconNotifier)
	{
		for (int i = __instance.items.Count - 1; i >= 0; i--)
		{
			var data = __instance.items[i];
			if (data.ItemStack != null || data.uiAtlasIcon == null || !data.uiAtlasIcon.Equals(iconNotifier, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int c = data.count;
			SetDurabilityColor(data.Item, null);
			SetLabels(data.Item, "", (c > 0) ? ("+" + c) : c.ToString());
			return;
		}
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_CollectedItemList), "AddCraftingSkillNotification")]
	public static void AddCraftingSkillNotificationPostfix(XUiC_CollectedItemList __instance, ProgressionValue craftingSkill)
	{
		for (int i = __instance.items.Count - 1; i >= 0; i--)
		{
			var data = __instance.items[i];
			if (data.CraftingSkill == null || data.CraftingSkill != craftingSkill)
			{
				continue;
			}
			SetDurabilityColor(data.Item, null);
			SetLabels(data.Item, "", $"{craftingSkill.Level}/{craftingSkill.ProgressionClass.MaxLevel}");
			return;
		}
	}
}