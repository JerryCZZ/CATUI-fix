using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public class XUiC_CompassWindowPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_CompassWindow), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(ref string value, string bindingName, ref bool __result, XUiC_CompassWindow __instance)
	{

		var localPlayer = __instance.localPlayer;
		bool hasLocalPlayer = localPlayer != null;
		BiomeDefinition.BiomeType? currentBiomeType = null;
		WeatherManager.BiomeWeather biomeWeather = null;
		int stormLevel = 0;
		int worldTime = 0;


		if (hasLocalPlayer)
		{
			worldTime = WeatherManager.worldTime;
			var biomeStandingOn = localPlayer.biomeStandingOn;
			if (biomeStandingOn != null)
			{
				currentBiomeType = biomeStandingOn.m_BiomeType;
				biomeWeather = WeatherManager.Instance.FindBiomeWeather(currentBiomeType.Value);
			}
			stormLevel = WeatherManager.currentWeather?.biomeDefinition?.currentWeatherGroup?.stormLevel ?? 0;
		}

		switch (bindingName)
		{



			case "CATUI_playerAlert":
				value = "false";
				if (__instance.localPlayer != null && __instance.localPlayer.Stealth.alertEnemy)
				{
					value = "true";
				}
				__instance.IsDirty = true;
				__result = true;
				return false;


			case "CATUI_nextBloodMoonDay":
				value = "7";
				if (hasLocalPlayer)
				{
					int currentDay = GameManager.Instance.World != null ? (int)(GameManager.Instance.World.worldTime / 24000) : 1;
					value = (GameStats.GetInt(EnumGameStats.BloodMoonDay) - currentDay).ToString();
				}
				__result = true;
				return false;



			case "CATUI_bloodMoonProgress":
				value = "0";
				if (hasLocalPlayer && GameManager.Instance.World != null)
				{
					var worldB = GameManager.Instance.World;
					var bmComp = worldB.aiDirector != null ? worldB.aiDirector.BloodMoonComponent : null;
					if (bmComp != null && bmComp.duskHour > 0)
					{
						ulong worldTimeC = worldB.worldTime;
						ulong day = worldTimeC / 24000;
						ulong hour = (worldTimeC % 24000) / 1000;
						int dusk = bmComp.duskHour;
						int dawn = bmComp.dawnHour;

						ulong baseDay = hour >= (ulong)dusk ? day : (day > 0 ? day - 1 : day);
						ulong windowStart = baseDay * 24000 + (ulong)dusk * 1000;
						ulong windowEnd = (baseDay + 1) * 24000 + (ulong)dawn * 1000;
						ulong total = windowEnd - windowStart;
						if (total > 0 && worldTimeC >= windowStart && worldTimeC <= windowEnd)
						{
							double p = (double)(worldTimeC - windowStart) / (double)total;
							value = p.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
						}
					}
				}
				__result = true;
				return false;


			case "CATUI_airDropFrequency":
				value = "3";
				if (hasLocalPlayer)
				{
					value = (GameStats.GetInt(EnumGameStats.AirDropFrequency) / 24f).ToString("F1");
				}
				__result = true;
				return false;


			case "CATUI_currentWeather":
				value = "None";
				if (hasLocalPlayer && currentBiomeType.HasValue && biomeWeather != null && biomeWeather.biomeDefinition != null)
				{
					bool isBloodMoon = false;
					var world = GameManager.Instance.World;
					if (world != null && world.aiDirector != null && world.aiDirector.BloodMoonComponent != null)
					{
						isBloodMoon = world.aiDirector.BloodMoonComponent.BloodMoonActive;
					}
					string spectrum = biomeWeather.biomeDefinition.weatherSpectrum.ToString();
					if (isBloodMoon)
					{
						value = "BloodMoon";
					}
					else if (spectrum == "Snowy" || spectrum == "Stormy" || spectrum == "Rainy")
					{
						value = spectrum;
					}


					else if (SkyManager.GetFogDensity() > 0.2f)
					{

						value = "Foggy";
					}
					else
					{
						value = spectrum;
					}
				}
				__result = true;
				return false;


			case "CATUI_isDaytime":
				value = "true";
				if (hasLocalPlayer)
				{
					value = GameManager.Instance.World.IsDaytime().ToString();
				}
				__result = true;
				return false;

			/* 风暴逻辑 ================================================================== */

			case "CATUI_stormLevel":
				value = stormLevel.ToString();
				__result = true;
				return false;


			case "CATUI_stormName":
				value = "";
				if (hasLocalPlayer && currentBiomeType.HasValue && stormLevel > 0)
				{
					if (currentBiomeType.Value == BiomeDefinition.BiomeType.burnt_forest)
					{
						value = "Burnt";
					}
					else if (currentBiomeType.Value == BiomeDefinition.BiomeType.Desert ||
							 currentBiomeType.Value == BiomeDefinition.BiomeType.Snow ||
							 currentBiomeType.Value == BiomeDefinition.BiomeType.Wasteland)
					{
						value = currentBiomeType.Value.ToString();
					}
				}
				__result = true;
				return false;


			case "CATUI_stormDurationTimeReal":
				value = "0";
				if (hasLocalPlayer && biomeWeather != null && stormLevel > 0)
				{
					int stormRemaining = biomeWeather.stormWorldTime + biomeWeather.stormDuration - worldTime;

					int stormDurationTimeSec = (stormRemaining / GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec));
					value = XUiM_PlayerBuffs.ConvertToTimeString(stormDurationTimeSec);
				}
				__result = true;
				return false;


			case "CATUI_stormDurationTime":
				value = "0";
				if (hasLocalPlayer && biomeWeather != null)
				{
					value = biomeWeather.stormDuration.ToString();
				}
				__result = true;
				return false;


			case "CATUI_stormRemainingTime":
				value = "0";
				if (hasLocalPlayer && biomeWeather != null && stormLevel > 0)
				{
					int stormRemaining = biomeWeather.stormWorldTime + biomeWeather.stormDuration - worldTime;
					value = stormRemaining.ToString();
				}
				__result = true;
				return false;


			case "CATUI_stormStartWorldTime":
				value = "0";
				if (hasLocalPlayer && biomeWeather != null)
				{
					value = biomeWeather.stormWorldTime.ToString();
				}
				__result = true;
				return false;


			case "CATUI_stormEndWorldTime":
				value = "0";
				if (hasLocalPlayer && biomeWeather != null)
				{
					value = (biomeWeather.stormWorldTime + biomeWeather.stormDuration).ToString();
				}
				__result = true;
				return false;


			case "CATUI_worldTime":
				value = "0";
				if (hasLocalPlayer)
				{
					value = worldTime.ToString();
				}
				__result = true;
				return false;


			case "CATUI_stormFill":
				value = "0.000";
				if (hasLocalPlayer && biomeWeather != null && stormLevel > 0)
				{
					int stormRemaining = biomeWeather.stormWorldTime + biomeWeather.stormDuration - worldTime;
					double fillPercentage = (double)stormRemaining / biomeWeather.stormDuration;
					value = fillPercentage.ToString("F3");
				}
				__result = true;
				return false;

			default:
				return true;
		}
	}



	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiController), "Update")]
	[PublicizedFrom(EAccessModifier.Private)]
	private static void UpdatePostfix(XUiController __instance)
	{
		if (__instance as XUiC_CompassWindow == null)
		{
			return;
		}
		var world = GameManager.Instance.World;
		if (world == null || world.aiDirector == null || world.aiDirector.BloodMoonComponent == null)
		{
			return;
		}
		if (!world.aiDirector.BloodMoonComponent.BloodMoonActive)
		{
			return;
		}
		__instance.IsDirty = true;
	}
}
