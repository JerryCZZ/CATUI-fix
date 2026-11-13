using HarmonyLib;





[HarmonyPatch(typeof(Hand), nameof(Hand.BindToolbelt))]
public static class CatuiToolbeltHandPatch
{
	[HarmonyPrefix]
	public static void Prefix(Hand __instance, Inventory _toolbelt)
	{
		if (__instance == null || _toolbelt == null)
		{
			return;
		}
		ItemInventoryData[] old = __instance.slotDatas;
		int wanted = _toolbelt.Length;
		if (old == null || old.Length >= wanted)
		{
			return;
		}
		ItemInventoryData[] grown = new ItemInventoryData[wanted];
		for (int i = 0; i < grown.Length; i++)
		{
			grown[i] = (i < old.Length) ? old[i] : ItemInventoryData.Create(__instance.entity, null, i);
		}
		__instance.slotDatas = grown;
		_toolbelt.ItemGrid.SlotChanged -= __instance.OnSlotChanged;
		_toolbelt.ItemGrid.SlotChanged += __instance.OnSlotChanged;
		Log.Out("[CATUI][Toolbelt] Hand slot data grown {0} -> {1}", old.Length, wanted);
	}
}
