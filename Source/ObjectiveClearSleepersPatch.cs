using HarmonyLib;
using PrefabVolumes;
using UnityEngine;

















[HarmonyPatch]
public class ObjectiveClearSleepersPatch
{

	public static int clearedSleepersCount;


	public static int totalSleepersCount;


	private static Rect activePoiRect = Rect.zero;


	private static bool hasActiveQuest;


	public static void SetupSleepersCount(Quest quest)
	{
		clearedSleepersCount = 0;
		totalSleepersCount = 0;
		hasActiveQuest = false;
		activePoiRect = Rect.zero;

		Vector3 poiPos = quest.GetLocation();
		if (poiPos == Vector3.zero)
		{
			return;
		}


		Vector3 poiSize;
		quest.GetPositionData(out poiSize, Quest.PositionDataTypes.POISize);
		activePoiRect = new Rect(poiPos.x, poiPos.z, poiSize.x, poiSize.z);
		hasActiveQuest = true;


		SleeperEventData sleeperData;
		if (QuestEventManager.Current.SleeperVolumeUpdateDictionary.TryGetValue(poiPos, out sleeperData))
		{
			totalSleepersCount = sleeperData.SleeperVolumes.Count;
			return;
		}



		PrefabInstance prefabFromWorldPos = GameManager.Instance.GetDynamicPrefabDecorator().GetPrefabFromWorldPos((int)poiPos.x, (int)poiPos.z);
		if (prefabFromWorldPos == null)
		{
			return;
		}
		for (int i = 0; i < prefabFromWorldPos.prefab.SleeperVolumeList.Count; i++)
		{
			PrefabSleeperVolume prefabSleeperVolume = prefabFromWorldPos.prefab.SleeperVolumeList[i];
			if (prefabSleeperVolume.Used && !prefabSleeperVolume.isQuestExclude)
			{
				totalSleepersCount++;
			}
		}
	}




	[HarmonyPostfix]
	[HarmonyPatch(typeof(QuestEventManager), "SleeperVolumePositionRemoved")]
	public static void SleeperVolumePositionRemovedPostfix(Vector3 pos)
	{
		if (hasActiveQuest && activePoiRect.Contains(new Vector2(pos.x, pos.z)))
		{
			clearedSleepersCount++;
		}
	}




	[HarmonyPrefix]
	[HarmonyPatch(typeof(ObjectiveClearSleepers), "get_ObjectiveValueType")]
	public static bool ObjectiveValueTypePrefix(ref BaseObjective.ObjectiveValueTypes __result)
	{
		__result = BaseObjective.ObjectiveValueTypes.Number;
		return false;
	}


	[HarmonyPrefix]
	[HarmonyPatch(typeof(BaseObjective), "get_StatusText")]
	public static bool StatusTextPrefix(ref string __result, BaseObjective __instance)
	{
		if (__instance is ObjectiveClearSleepers)
		{
			__result = "";

			if (__instance.OwnerQuest.CurrentState == Quest.QuestState.InProgress && __instance.ObjectiveState != BaseObjective.ObjectiveStates.Complete)
			{
				__result = string.Concat(clearedSleepersCount);
				if (totalSleepersCount > 0)
				{
					__result = __result + "/" + totalSleepersCount;
				}
			}
			return false;
		}
		return true;
	}




	[HarmonyPostfix]
	[HarmonyPatch(typeof(NetPackagePartyQuestChange), "HandlePlayer")]
	public static void HandlePlayerPostfix(EntityPlayerLocal localPlayer, int ___senderEntityID, int ___questCode, byte ___objectiveIndex)
	{
		Quest sharedQuest = localPlayer.QuestJournal.GetSharedQuest(___questCode);
		if (sharedQuest != null && ___objectiveIndex < sharedQuest.Objectives.Count && sharedQuest.Objectives[___objectiveIndex] is ObjectiveClearSleepers)
		{
			SetupSleepersCount(sharedQuest);
		}
	}




	[HarmonyPostfix]
	[HarmonyPatch(typeof(ObjectiveRallyPoint), "RallyPointActivate")]
	public static void RallyPointActivatePostfix(ObjectiveRallyPoint __instance, bool activate)
	{
		if (activate)
		{
			SetupSleepersCount(__instance.OwnerQuest);
		}
	}
}