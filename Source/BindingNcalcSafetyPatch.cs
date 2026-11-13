using HarmonyLib;
using System;
using System.Collections.Generic;

[HarmonyPatch]
public class BindingNcalcSafetyPatch
{
	private const string TAG = "[CATUI]";

	private static readonly HashSet<string> loggedKeys = new HashSet<string>(StringComparer.Ordinal);










	[HarmonyPatch(typeof(BindingItemNcalc), "evaluateExpression")]
	[HarmonyPostfix]
	public static void EvaluateExpressionPostfix(ref string __result, BindingItemNcalc __instance)
	{
		if (!string.IsNullOrEmpty(__result))
		{
			return;
		}
		bool failed;
		try
		{
			__instance.expression.Evaluate();
			failed = false;
		}
		catch (Exception)
		{
			failed = true;
		}
		if (failed)
		{
			__result = "0";
			LogOnce(__instance);
		}
	}

	private static void LogOnce(BindingItemNcalc binding)
	{
		try
		{
			string hierarchy = "unknown";
			try
			{
				hierarchy = binding.Parent.View.GetXuiHierarchy();
			}
			catch
			{
			}
			string sourceText = binding.SourceText ?? "";
			if (loggedKeys.Add(sourceText + "|" + hierarchy))
			{
				Log.Warning("{0} NCalc binding '{{{1}}}' on '{2}' failed to evaluate (expression error, usually caused by missing or incomplete server XML data). Using '0' as a safe default.", TAG, sourceText, hierarchy);
			}
		}
		catch
		{
		}
	}
}
