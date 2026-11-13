using System.Reflection;
using HarmonyLib;
using UnityEngine;














[HarmonyPatch(typeof(XUiV_Panel), "updateClipping")]
public static class ScrollViewStabilizerPatch
{

	public struct ScrollState
	{

		public bool Active;


		public Vector2 ClipOffset;


		public Vector3 LocalPosition;
	}


	private static readonly FieldInfo PanelField = AccessTools.Field(typeof(XUiV_Panel), "panel");


	private static readonly FieldInfo UiTransformField = AccessTools.Field(typeof(XUiView), "uiTransform");





	[HarmonyPrefix]
	public static void Prefix(XUiV_Panel __instance, out ScrollState __state)
	{
		__state = default(ScrollState);
		if (UiTransformField == null || PanelField == null)
		{
			Log.Warning("[CATUI] ScrollViewStabilizerPatch: panel/uiTransform fields not found, patch disabled");
			return;
		}

		var uiTransform = (Transform)UiTransformField.GetValue(__instance);
		if (uiTransform == null || uiTransform.GetComponent<UIScrollView>() == null)
		{
			return;
		}

		var panel = (UIPanel)PanelField.GetValue(__instance);
		if (panel != null)
		{
			__state = new ScrollState
			{
				Active = true,
				ClipOffset = panel.clipOffset,
				LocalPosition = uiTransform.localPosition
			};
		}
	}





	[HarmonyPostfix]
	public static void Postfix(XUiV_Panel __instance, ScrollState __state)
	{
		if (!__state.Active || UiTransformField == null || PanelField == null)
		{
			return;
		}

		var uiTransform = (Transform)UiTransformField.GetValue(__instance);
		var panel = (UIPanel)PanelField.GetValue(__instance);
		if (uiTransform == null || panel == null)
		{
			return;
		}

		panel.clipOffset = __state.ClipOffset;
		uiTransform.localPosition = __state.LocalPosition;
	}
}