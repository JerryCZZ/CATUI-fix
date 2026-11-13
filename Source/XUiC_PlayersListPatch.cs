using HarmonyLib;

[HarmonyPatch]
public class XUiC_PlayersListPatch
{




	[HarmonyPatch(typeof(XUiC_PlayersList), "updatePlayersList")]
	[HarmonyPrefix]
	public static bool UpdatePlayersListPrefix()
	{
		if (GameManager.Instance == null || GameManager.Instance.World == null || GameManager.Instance.persistentPlayers == null)
		{
			return false;
		}
		return true;
	}
}
