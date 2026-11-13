using HarmonyLib;
using UnityEngine;
using System;
using UnityEngine.Scripting;
using System.Collections.Generic;

[HarmonyPatch]
public class XUiUpdaterPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiUpdater), "Update")]
    public static void UpdatePostfix()
    {

        if (InputUtils.AltKeyPressed && Input.GetKeyDown(KeyCode.B))
        {

            EntityPlayerLocal localPlayer = GameManager.Instance.World.GetPrimaryPlayer() as EntityPlayerLocal;
            if (localPlayer == null)
                return;


            EntityDrone playerDrone = FindPlayerDrone(localPlayer);
            if (playerDrone != null)
            {

                playerDrone.openStorageWindow(localPlayer.playerUI);
            }
        }




        if (Input.GetKeyDown(KeyCode.Mouse3))
        {

            EntityPlayerLocal localPlayer = GameManager.Instance.World.GetPrimaryPlayer() as EntityPlayerLocal;
            if (localPlayer == null)
                return;


            XUi xui = localPlayer.playerUI.xui;
            if (xui == null)
                return;


            XUiC_ContainerStandardControls containerControls = null;
            XUiC_BackpackWindow backpackWindow = xui.GetChildByType<XUiC_BackpackWindow>();
            XUiC_LootWindow lootWindow = xui.GetChildByType<XUiC_LootWindow>();
            XUiC_BagContainer vehicleContainer = xui.GetChildByType<XUiC_BagContainer>();
            if (backpackWindow != null)
            {
                containerControls = backpackWindow.GetChildByType<XUiC_ContainerStandardControls>();
            }


            if (containerControls != null && containerControls?.MoveAllowed != null && (lootWindow.IsOpen || vehicleContainer.IsOpen))
            {


                containerControls.MoveFillAndSmart();
                xui.PlayMenuClickSound();
            }
        }

    }


    private static EntityDrone FindPlayerDrone(EntityPlayerLocal player)
    {

        List<Entity> entities = GameManager.Instance.World.Entities.list;
        foreach (Entity entity in entities)
        {
            if (entity is EntityDrone drone)
            {

                if (drone.Owner?.entityId == player.entityId)
                {
                    return drone;
                }
            }
        }
        return null;
    }
}