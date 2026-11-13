using HarmonyLib;
using System;
using UnityEngine;

[HarmonyPatch]
public class XUiC_PartyWindowPatch
{


	public static int ComputePositionY(XUiC_PartyWindow window)
	{
		int defaultPartyWindowPositionY = 74;
		int entryHeight = 56;
		ReadAttributeInt(window, "default_PartyWindowPositionY", ref defaultPartyWindowPositionY);
		ReadAttributeInt(window, "entry_height", ref entryHeight);
		EntityPlayer player = window?.xui?.playerUI?.entityPlayer;
		if (player == null)
		{
			return defaultPartyWindowPositionY;
		}
		int y = defaultPartyWindowPositionY;
		if (player.Party != null && player.Party.MemberList != null)
		{
			y += (player.Party.MemberList.Count - 1) * entryHeight;
		}
		if (player.Companions != null)
		{
			y += player.Companions.Count * entryHeight;
		}
		return y;
	}

	static void ReadAttributeInt(XUiC_PartyWindow window, string attributeName, ref int fallback)
	{
		if (window == null)
		{
			return;
		}

		foreach (var pair in window.CustomAttributes)
		{
			if (string.Equals(pair.Key, attributeName, StringComparison.OrdinalIgnoreCase) &&
				pair.Value != null && int.TryParse(pair.Value.ToString(), out int parsed))
			{
				fallback = parsed;
				return;
			}
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_PartyWindow), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(string _bindingName, ref string _value, ref bool __result, XUiC_PartyWindow __instance)
	{
		switch (_bindingName)
		{

			case "CATUI_PartyWindowPositionY":
				_value = ComputePositionY(__instance).ToString();
				__result = true;
				return false;
			default:
				return true;
		}
	}




	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_PartyEntryList), "RefreshPartyList")]
	public static void PartyEntryList_RefreshPartyListPostfix(XUiC_PartyEntryList __instance)
	{
		RefreshPartyWindowPosition(__instance);
	}


	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_CompanionEntryList), "RefreshPartyList")]
	public static void CompanionEntryList_RefreshPartyListPostfix(XUiC_CompanionEntryList __instance)
	{
		RefreshPartyWindowPosition(__instance);
	}

	static void RefreshPartyWindowPosition(XUiController source)
	{
		try
		{
			XUiC_PartyWindow window = source?.windowGroup?.Controller as XUiC_PartyWindow;
			if (window?.ViewComponent == null)
			{
				return;
			}

			window.RefreshBindings();

			Vector2i pos = window.ViewComponent.Position;
			window.ViewComponent.Position = new Vector2i(pos.x, ComputePositionY(window));
			window.ViewComponent.TryUpdatePosition();
		}
		catch
		{
		}
	}
}
