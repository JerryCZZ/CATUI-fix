using HarmonyLib;
using UnityEngine;
using System;
using System.Collections;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

[HarmonyPatch]
public class XUiC_HUDStatBarPatch
{
	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterXuiRgbaColor rgbaColorFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterInt playerStatCurrentHealthMaxFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterInt playerStatCurrentStaminaMaxFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterFloat playerEntityPenetrationCountFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterInt playerArmorRatingFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterFloat playerRunSpeedFormatter = new();

	[PublicizedFrom(EAccessModifier.Private)]
	public static CachedStringFormatterInt playerCurrencyAmountFormatter = new();


	private static int _cachedFrame;
	private static float _mobility;
	private static float _physicalDamageResist;
	private static float _runSpeed;
	private static float _barteringBuying;
	private static float _barteringSelling;
	private static float _entityPenetrationCount;
	private static bool _mobilityCached;
	private static bool _resistCached;
	private static bool _runSpeedCached;
	private static bool _buyingCached;
	private static bool _sellingCached;
	private static bool _penetrationCached;


	private static Vehicle _cachedVehicle;
	private static int _vehicleCachedFrame;


	private static int _cachedFactionTier = -1;
	private static int _cachedFactionPoints;
	private static int _cachedFactionMax;
	private static bool _questCached;


	private static World _cachedAirdropWorld;
	private static AIDirectorAirDropComponent _cachedAirdropComponent;

	private static FieldInfo _airDropNextField;
	private static FieldInfo _airDropLastCheckField;


	private const int DirtyThrottleFrames = 10;
	private const int VehicleDirtyThrottleFrames = 1;


	private const float TempBarLeft = 0f;
	private const float TempBarRight = 132f;
	private const float TempBarCenter = 66f;
	private static XUiV_Sprite _tempBarMark;

	private static XUiC_HUDStatBar _tempBarMarkOwner;


	private static readonly ConditionalWeakTable<XUiC_HUDStatBar, LastDirtyState> _dirtyStates = new ConditionalWeakTable<XUiC_HUDStatBar, LastDirtyState>();

	private sealed class LastDirtyState
	{

		public int lastDirtyFrame = -DirtyThrottleFrames;
		public int lastVehicleDirtyFrame = -VehicleDirtyThrottleFrames;
	}


	[PublicizedFrom(EAccessModifier.Private)]
	private static void MarkDirtyThrottled(XUiC_HUDStatBar instance, int intervalFrames)
	{
		if (instance == null)
		{
			return;
		}
		int frame = Time.frameCount;
		LastDirtyState state = _dirtyStates.GetOrCreateValue(instance);
		if (intervalFrames == VehicleDirtyThrottleFrames)
		{
			if (frame - state.lastVehicleDirtyFrame >= intervalFrames)
			{
				state.lastVehicleDirtyFrame = frame;
				instance.IsDirty = true;
			}
		}
		else if (frame - state.lastDirtyFrame >= intervalFrames)
		{
			state.lastDirtyFrame = frame;
			instance.IsDirty = true;
		}
	}


	[PublicizedFrom(EAccessModifier.Private)]
	private static void MarkDirtyThrottled(XUiC_HUDStatBar instance)
	{
		MarkDirtyThrottled(instance, DirtyThrottleFrames);
	}



	[HarmonyPostfix]
	[HarmonyPatch(typeof(XUiC_HUDStatBar), "Update")]
	[PublicizedFrom(EAccessModifier.Private)]
	private static void UpdatePostfix(XUiC_HUDStatBar __instance)
	{
		if (__instance == null)
		{
			return;
		}


		if (_tempBarMark == null || _tempBarMark.UiTransform == null)
		{
			_tempBarMark = null;
			_tempBarMarkOwner = null;
			XUiController markController = __instance.GetChildById("tempBarMark");
			if (markController != null)
			{
				_tempBarMark = markController.ViewComponent as XUiV_Sprite;
				_tempBarMarkOwner = __instance;
			}
		}

		if (ReferenceEquals(_tempBarMarkOwner, __instance) && _tempBarMark != null && __instance.localPlayer != null)
		{
			try
			{
				float coretemp = __instance.localPlayer.Buffs.GetCustomVar("_coretemp");
				int x = (int)Mathf.Clamp(coretemp, TempBarLeft, TempBarRight) - (int)TempBarCenter;
				_tempBarMark.position = new Vector2i(x, 0);
				_tempBarMark.positionDirty = true;
				_tempBarMark.TryUpdatePosition();
			}
			catch (Exception)
			{
				_tempBarMark = null;
				_tempBarMarkOwner = null;
			}
			return;
		}
		if (__instance.IsDirty)
		{
			return;
		}
		if (__instance.statGroup == HUDStatGroups.Vehicle)
		{
			__instance.IsDirty = true;
			return;
		}
		MarkDirtyThrottled(__instance);
	}



	private static bool IsLocalPlayerMounted(EntityPlayer player)
	{
		if (player == null) return false;
		try
		{
			Type t = player.GetType();
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			foreach (string name in new[] { "IsAttachedToVehicle", "IsInVehicle", "IsMounted" })
			{
				PropertyInfo p = t.GetProperty(name, flags);
				if (p != null && p.PropertyType == typeof(bool)) return (bool)p.GetValue(player);
				MethodInfo m = t.GetMethod(name, flags, null, Type.EmptyTypes, null);
				if (m != null && m.ReturnType == typeof(bool)) return (bool)m.Invoke(player, null);
			}
			foreach (string name in new[] { "AttachedToEntity", "attachedToEntity", "attachedToEntityId", "_attachedToEntityId" })
			{
				PropertyInfo p = t.GetProperty(name, flags);
				object v = p != null ? p.GetValue(player) : t.GetField(name, flags)?.GetValue(player);
				if (v is int id) return id >= 0;
				if (v is Entity entity) return entity is EntityVehicle;
			}
		}
		catch (Exception) { }
		return false;
	}

	private static void EnsureCacheFrame()
	{
		int frame = Time.frameCount;
		if (frame != _cachedFrame)
		{
			_cachedFrame = frame;
			_mobilityCached = false;
			_resistCached = false;
			_runSpeedCached = false;
			_buyingCached = false;
			_sellingCached = false;
			_penetrationCached = false;
			_vehicleCachedFrame = -1;
			_questCached = false;
		}
	}

	private static float GetMobility(EntityPlayerLocal player)
	{
		if (!_mobilityCached)
		{
			_mobility = EffectManager.GetValue(PassiveEffects.Mobility, null, 0f, player, null, XUiM_Player.GetPlayer().generalTags, calcEquipment: true, calcHoldingItem: true, calcProgression: true, calcBuffs: true, calcChallenges: true, 1, useMods: true, _useDurability: true);
			_mobilityCached = true;
		}
		return _mobility;
	}

	private static float GetPhysicalDamageResist(EntityPlayerLocal player)
	{
		if (!_resistCached)
		{
			_physicalDamageResist = EffectManager.GetValue(PassiveEffects.PhysicalDamageResist, null, 0f, player);
			_resistCached = true;
		}
		return _physicalDamageResist;
	}

	private static float GetRunSpeed(EntityPlayerLocal player)
	{
		if (!_runSpeedCached)
		{
			_runSpeed = EffectManager.GetValue(PassiveEffects.RunSpeed, null, 0f, player);
			_runSpeedCached = true;
		}
		return _runSpeed;
	}

	private static float GetBarteringBuying(EntityPlayerLocal player)
	{
		if (!_buyingCached)
		{
			_barteringBuying = EffectManager.GetValue(PassiveEffects.BarteringBuying, null, 0f, player);
			_buyingCached = true;
		}
		return _barteringBuying;
	}

	private static float GetBarteringSelling(EntityPlayerLocal player)
	{
		if (!_sellingCached)
		{
			_barteringSelling = EffectManager.GetValue(PassiveEffects.BarteringSelling, null, 0f, player);
			_sellingCached = true;
		}
		return _barteringSelling;
	}

	private static float GetEntityPenetrationCount(EntityPlayerLocal player)
	{
		if (!_penetrationCached)
		{
			_entityPenetrationCount = EffectManager.GetValue(PassiveEffects.EntityPenetrationCount, null, 0f, player);
			_penetrationCached = true;
		}
		return _entityPenetrationCount;
	}

	private static Vehicle GetCachedVehicle(XUiC_HUDStatBar instance)
	{
		if (_vehicleCachedFrame != _cachedFrame)
		{
			_cachedVehicle = instance.vehicle?.GetVehicle();
			_vehicleCachedFrame = _cachedFrame;
		}
		return _cachedVehicle;
	}



	private static float GetEffectiveMaxSpeed(EntityVehicle vehicle)
	{
		if (vehicle != null)
		{
			float vm = vehicle.velocityMax;
			if (vm > 0f)
			{
				return vm;
			}
			Vehicle v = vehicle.GetVehicle();
			if (v != null)
			{
				return v.VelocityMaxTurboForward * v.EffectVelocityMaxPer;
			}
		}
		return 0f;
	}

	private static void EnsureQuestCache(EntityPlayerLocal player)
	{
		if (!_questCached && player != null)
		{
			_cachedFactionTier = player.QuestJournal.GetCurrentFactionTier(1);
			_cachedFactionPoints = player.QuestJournal.GetQuestFactionPoints(1);
			_cachedFactionMax = player.QuestJournal.GetQuestFactionMax(1, _cachedFactionTier);
			_questCached = true;
		}
	}


	private static AIDirectorAirDropComponent GetAirdropComponent()
	{
		if (ConnectionManager.Instance == null || !ConnectionManager.Instance.IsServer)
		{
			return null;
		}
		World world = GameManager.Instance?.World;
		if (world == null)
		{
			return null;
		}
		if (_cachedAirdropComponent == null || !ReferenceEquals(_cachedAirdropWorld, world))
		{
			_cachedAirdropWorld = world;
			_cachedAirdropComponent = world.aiDirector?.GetComponent<AIDirectorAirDropComponent>();
		}
		return _cachedAirdropComponent;
	}


	private static ulong GetAirdropField(AIDirectorAirDropComponent comp, ref FieldInfo field, string fieldName)
	{
		if (comp == null)
		{
			return 0;
		}
		if (field == null)
		{
			field = typeof(AIDirectorAirDropComponent).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		object raw = field?.GetValue(comp);
		return raw is ulong value ? value : 0;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(XUiC_HUDStatBar), "GetBindingValueInternal")]
	public static bool GetBindingValueInternalPrefix(string _bindingName, ref string _value, ref bool __result, XUiC_HUDStatBar __instance)
	{
		EnsureCacheFrame();
		MarkDirtyThrottled(__instance);

		switch (_bindingName)
		{

			case "CATUI_playerName":
				_value = " ";
				if (__instance.localPlayer != null)
				{
					_value = __instance.localPlayer.PlayerDisplayName;
				}
				__result = true;
				return false;


			case "CATUI_AmmoMax":
				_value = "";
				if (__instance.localPlayer != null)
				{
					_value = __instance.currentAmmoCount.ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerCurrentLife":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = XUiM_Player.GetCurrentLife(__instance.localPlayer).ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerArmorRating":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					float resist = GetPhysicalDamageResist(__instance.localPlayer);
					_value = playerArmorRatingFormatter.Format((int)resist);
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerArmorLevel":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					int armor = (int)GetPhysicalDamageResist(__instance.localPlayer);
					_value = armor switch
					{
						0 => "0",
						> 0 and < 20 => "1",
						>= 20 and < 40 => "2",
						>= 40 and < 60 => "3",
						>= 60 and < 80 => "4",
						>= 80 and < 100 => "5",
						_ => "6"
					};
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerGameStage":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = __instance.localPlayer.gameStage.ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerLootStage":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = __instance.localPlayer.GetLootStage(0f, 0f).ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerTraderStage":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					EnsureQuestCache(__instance.localPlayer);
					_value = _cachedFactionTier.ToString();
				}
				__result = true;
				return false;


			case "CATUI_isServer":
				_value = (ConnectionManager.Instance != null && ConnectionManager.Instance.IsServer).ToString().ToLower();
				__result = true;
				return false;


			case "CATUI_airDropTimeLeft":
				_value = "";
				{
					AIDirectorAirDropComponent airDrop = GetAirdropComponent();
					if (airDrop != null && AIDirectorAirDropComponent.MaxDayCount > 0)
					{
						ulong next = GetAirdropField(airDrop, ref _airDropNextField, "nextAirDropTime");
						World world = GameManager.Instance?.World;
						if (next > 0 && world != null)
						{
							ulong now = world.worldTime;
							ulong remaining = next > now ? next - now : 0;

							ulong hours = remaining / 1000;
							_value = hours > 0 ? $"{hours}{Localization.Get("timeAbbreviationHours")}" : $"<1{Localization.Get("timeAbbreviationHours")}";
							MarkDirtyThrottled(__instance);
						}
					}
				}
				__result = true;
				return false;


			case "CATUI_airDropProgress":
				_value = "0";
				{
					AIDirectorAirDropComponent airDrop = GetAirdropComponent();
					if (airDrop != null && AIDirectorAirDropComponent.MaxDayCount > 0)
					{
						ulong next = GetAirdropField(airDrop, ref _airDropNextField, "nextAirDropTime");
						ulong last = GetAirdropField(airDrop, ref _airDropLastCheckField, "lastAirdropCheckTime");
						World world = GameManager.Instance?.World;
						if (next > 0 && world != null)
						{
							ulong now = world.worldTime;
							ulong remaining = next > now ? next - now : 0;

							ulong total;
							if (last != 0 && next > last)
							{
								total = next - last;
							}
							else
							{
								total = (ulong)Math.Max(1, AIDirectorAirDropComponent.MaxDayCount) * 24000uL;
							}
							float progress = total > 0 ? 1f - Mathf.Clamp01((float)remaining / (float)total) : 0f;
							_value = progress.ToString("F3");
							MarkDirtyThrottled(__instance);
						}
					}
				}
				__result = true;
				return false;


			case "CATUI_playerTraderStageProgressCurrent":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					EnsureQuestCache(__instance.localPlayer);
					_value = _cachedFactionPoints.ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerTraderStageProgressMax":
				_value = "10";
				if (__instance.localPlayer != null)
				{
					EnsureQuestCache(__instance.localPlayer);
					_value = _cachedFactionMax.ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerTraveled":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = XUiM_Player.GetKMTraveled(__instance.localPlayer).ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerZombieKills":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = XUiM_Player.GetZombieKills(__instance.localPlayer).ToString();
				}
				__result = true;
				return false;


			case "CATUI_coretemp":
				_value = "";
				if (__instance.localPlayer != null)
				{
					_value = XUiM_Player.GetCoreTemp(__instance.localPlayer).ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_coretempcolor":
				_value = "255,255,255";
				if (__instance.localPlayer != null)
				{
					float coretemp = __instance.localPlayer.Buffs.GetCustomVar("_coretemp");
					_value = coretemp switch
					{
						<= 32f => "0,153,255",
						> 32f and <= 50f => "0,255,255",
						>= 85f and < 100f => "255,128,0",
						>= 100f => "255,0,0",
						_ => "255,255,255"
					};
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_outsidetemp":
				_value = "";
				if (__instance.localPlayer != null)
				{
					_value = XUiM_Player.GetOutsideTemp(__instance.localPlayer).ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_outsidetempcolor":
				_value = "255,255,255";
				if (__instance.localPlayer != null)
				{
					float outsidetemp = __instance.localPlayer.Buffs.GetCustomVar("_outsidetemp");
					_value = outsidetemp switch
					{
						<= 32f => "0,153,255",
						> 32f and <= 50f => "0,255,255",
						>= 85f and < 100f => "255,128,0",
						>= 100f => "255,0,0",
						_ => "255,255,255"
					};
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerPing":
				_value = "-1";
				if (__instance.localPlayer != null)
				{
					int _ping = __instance.localPlayer.pingToServer;
					if (_ping > 0)
					{
						_value = _ping > 1000 ? ">1000" : _ping.ToString();
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerPingColor":
				_value = "0,0,0";
				if (__instance.localPlayer != null)
				{
					int _ping = __instance.localPlayer.pingToServer;
					const string GoodColor = "67, 207, 124";
					const string MediumColor = "255, 195, 0";
					const string PoorColor = "255, 0, 0";
					if (_ping > 0)
					{
						if (_ping <= 150)
						{
							_value = GoodColor;
						}
						else if (_ping <= 500)
						{
							_value = MediumColor;
						}
						else
						{
							_value = PoorColor;
						}
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerPingVisible":
				_value = "false";
				if (__instance.localPlayer != null)
				{
					int _ping = __instance.localPlayer.pingToServer;
					if (_ping > 0)
					{
						_value = "true";
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerMoveSpeed":
				_value = "100";
				if (__instance.localPlayer != null)
				{
					float num = GetMobility(__instance.localPlayer) * 100f;
					_value = ((int)num).ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerMoveSpeedLevel":
				_value = "4";
				if (__instance.localPlayer != null)
				{
					float speed = GetMobility(__instance.localPlayer) * 100f;
					_value = speed switch
					{
						>= 0 and < 50 => "0",
						>= 50 and < 70 => "1",
						>= 70 and < 80 => "2",
						>= 80 and < 90 => "3",
						>= 100 and < 110 => "4",
						>= 110 and < 120 => "5",
						_ => "6"
					};
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerRunSpeed":
				_value = "110";
				if (__instance.localPlayer != null)
				{
					float num = GetRunSpeed(__instance.localPlayer) * 100f;
					_value = ((int)num).ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerBarteringBuying":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					float num = GetBarteringBuying(__instance.localPlayer) * 100f;
					_value = ((int)num).ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerBarteringSelling":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					float num = GetBarteringSelling(__instance.localPlayer) * 100f;
					_value = ((int)num).ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerActiveItemIcon":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					EntityPlayer localPlayer = __instance.localPlayer;
					Inventory inventory = localPlayer.inventory;
					ItemValue itemValue = inventory.GetItem(__instance.currentSlotIndex).itemValue;
					ItemClass itemClass = itemValue.ItemClass;
					if (itemClass != null)
					{
						_value = itemClass.GetIconName();
					}
				}
				__result = true;
				return false;



			case "CATUI_playerMounted":
				_value = IsLocalPlayerMounted(__instance.localPlayer) ? "true" : "false";
				__result = true;
				return false;

			case "CATUI_playerActiveItemName":
				_value = "";
				if (__instance.localPlayer != null)
				{
					EntityPlayer localPlayer = __instance.localPlayer;
					ItemActionAttack attackAction = __instance.attackAction;

					if (attackAction != null && attackAction.IsEditingTool())
					{
                        ItemInventoryData slotData = localPlayer.inventory.GetItemDataInSlot(__instance.currentSlotIndex);
                        ItemActionData itemActionDataInSlot = (slotData?.actionData != null && slotData.actionData.Count > 1) ? slotData.actionData[1] : null;
                        _value = attackAction.GetStat(itemActionDataInSlot);
					}
					else
					{
						Inventory inventory = localPlayer.inventory;
						ItemValue itemValue = inventory.GetItem(__instance.currentSlotIndex).itemValue;
						ItemClass itemClass = itemValue.ItemClass;
						if (itemClass != null)
						{
							_value = itemClass.GetLocalizedItemName();
						}
					}
				}
				__result = true;
				return false;


			case "CATUI_playerActiveItemDurabilityColor":
				_value = "255,255,255";
				if (__instance.localPlayer != null)
				{
					EntityPlayer localPlayer = __instance.localPlayer;
					Inventory inventory = localPlayer.inventory;
					ItemValue itemValue = inventory.GetItem(__instance.currentSlotIndex).itemValue;
					if (itemValue != null)
					{
						Color32 color = QualityInfo.GetQualityColor(itemValue.Quality);
						_value = rgbaColorFormatter.Format(color);
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerActiveItemUseTimesResidue":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					EntityPlayer localPlayer = __instance.localPlayer;
					Inventory inventory = localPlayer.inventory;
					ItemStack itemStack = inventory.GetItem(__instance.currentSlotIndex);
					if (itemStack.IsEmpty())
					{
						_value = "0";
					}
					else
					{
						if (itemStack.itemValue.MaxUseTimes == 0)
						{
							_value = "1";
						}
						else
						{
							_value = (itemStack.itemValue.MaxUseTimes - itemStack.itemValue.UseTimes).ToString("F0");
						}
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerActiveItemUseTimesMax":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					EntityPlayer localPlayer = __instance.localPlayer;
					Inventory inventory = localPlayer.inventory;
					ItemStack itemStack = inventory.GetItem(__instance.currentSlotIndex);
					if (itemStack.IsEmpty())
					{
						_value = "0";
					}
					else
					{
						if (itemStack.itemValue.MaxUseTimes == 0)
						{
							_value = "1";
						}
						else
						{
							_value = itemStack.itemValue.MaxUseTimes.ToString("F0");
						}
					}
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_playerSkillPointsAvailable":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = __instance.localPlayer.Progression.SkillPoints.ToString();
				}
				__result = true;
				return false;


			case "CATUI_playerEntityPenetrationCount":
				_value = "1";
				if (__instance.localPlayer != null)
				{
					_value = playerEntityPenetrationCountFormatter.Format(GetEntityPenetrationCount(__instance.localPlayer));
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_VehicleIcon":
				_value = "";
				if (__instance.vehicle != null)
				{
					_value = __instance.vehicle.GetMapIcon();
				}
				__result = true;
				return false;


			case "CATUI_VehicleCurrentSpeed":
				_value = "0";
				Vehicle v = GetCachedVehicle(__instance);
				if (v != null)
				{
					float currentSpeed = Mathf.Abs(v.CurrentForwardVelocity + 0.001f);
					_value = currentSpeed < 0.01f ? "0" : currentSpeed.ToString("F2");
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_VehicleCurrentSpeedFill":
				_value = "0";
				if (__instance.vehicle != null)
				{
					Vehicle vf = __instance.vehicle.GetVehicle();
					if (vf != null)
					{
						float maxSpeed = vf.VelocityMaxTurboForward * vf.EffectVelocityMaxPer;
						float currentSpeed = Mathf.Abs(vf.CurrentForwardVelocity);
						float SpeedPercent = maxSpeed > 0f ? currentSpeed / maxSpeed : 0f;
						SpeedPercent = Mathf.Clamp01(SpeedPercent);
						_value = SpeedPercent < 0.01f ? "0" : SpeedPercent.ToString("F3");
						MarkDirtyThrottled(__instance, VehicleDirtyThrottleFrames);
					}
				}
				__result = true;
				return false;


			case "CATUI_VehicleTurboRatio":
				_value = "0";
				if (__instance.vehicle != null)
				{
					Vehicle vr = __instance.vehicle.GetVehicle();
					if (vr != null)
					{
						float turboMax = vr.VelocityMaxTurboForward * vr.EffectVelocityMaxPer;
						float baseMax = vr.VelocityMaxForward * vr.EffectVelocityMaxPer;
						float ratio = turboMax > 0f ? baseMax / turboMax : 0f;
						ratio = Mathf.Clamp01(ratio);
						_value = ratio.ToString("F3");
						MarkDirtyThrottled(__instance, VehicleDirtyThrottleFrames);
					}
				}
				__result = true;
				return false;


			case "CATUI_VehicleCurrentSpeedKPH":
				_value = "0";
				Vehicle vk = GetCachedVehicle(__instance);
				if (vk != null)
				{
					float currentSpeed = Mathf.Abs(vk.CurrentForwardVelocity + 0.001f);
					_value = currentSpeed < 0.01f ? "0" : (currentSpeed * 3.6f).ToString("F1");
					MarkDirtyThrottled(__instance, VehicleDirtyThrottleFrames);
				}
				__result = true;
				return false;


			case "CATUI_VehicleMaxSpeedNotTurbo":
				_value = "0";
				if (__instance.vehicle != null)
				{
					_value = GetEffectiveMaxSpeed(__instance.vehicle).ToString("0.00");
				}
				__result = true;
				return false;


			case "CATUI_VehicleMaxSpeed":
				_value = "0";
				if (__instance.vehicle != null)
				{
					_value = GetEffectiveMaxSpeed(__instance.vehicle).ToString("0.00");
				}
				__result = true;
				return false;


			case "CATUI_VehicleIsBrake":
				_value = "false";
				Vehicle vb = GetCachedVehicle(__instance);
				if (vb != null)
				{
					_value = vb.CurrentIsBreak.ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_VehicleInventorySlotCount":
				_value = "false";
				if (__instance.vehicle != null && __instance.vehicle.GetVehicle().HasStorage())
				{
					_value = __instance.vehicle.bag.SlotCount.ToString();
				}
				__result = true;
				return false;


			case "CATUI_VehicleInventoryItemCount":
				_value = "false";
				if (__instance.vehicle != null && __instance.vehicle.GetVehicle().HasStorage())
				{
					_value = __instance.vehicle.bag.GetUsedSlotCount().ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_VehicleCanTurbo":
				_value = "false";
				Vehicle vct = GetCachedVehicle(__instance);
				if (vct != null)
				{
					_value = vct.CanTurbo.ToString();
				}
				__result = true;
				return false;


			case "CATUI_VehicleIsTurbo":
				_value = "false";
				Vehicle vt = GetCachedVehicle(__instance);
				if (vt != null)
				{
					_value = vt.IsTurbo.ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;


			case "CATUI_VehicleHasHorn":
				_value = "false";
				Vehicle vh = GetCachedVehicle(__instance);
				if (vh != null)
				{
					_value = vh.HasHorn().ToString();
				}
				__result = true;
				return false;


			case "CATUI_VehicleHasLight":
				_value = "false";
				if (__instance.vehicle != null)
				{
					_value = __instance.vehicle.HasHeadlight().ToString();
				}
				__result = true;
				return false;


			case "CATUI_VehicleIsLight":
				_value = "false";
				if (__instance.vehicle != null)
				{
					_value = __instance.vehicle.IsHeadlightOn.ToString();
					MarkDirtyThrottled(__instance);
				}
				__result = true;
				return false;

			default:
				return true;
		}
	}
}