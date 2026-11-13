
using System.Reflection;
using HarmonyLib;
using UnityEngine;


public class ModStartup : IModApi
{

    public void InitMod(Mod modInstance)
    {

        Assembly executeAssembly = Assembly.GetExecutingAssembly();

        CatuiGearsPreviewResources.Restore(executeAssembly);
        CatuiSettings.Load();
        CatuiToolbeltSettings.Apply();
        CatuiHealthBarSettings.Load();
        CatuiQuantityProbe.Reload();


        Harmony harmony = new Harmony(executeAssembly.GetName().Name);


        harmony.PatchAll(executeAssembly);



        CatuiQuantityInputShield.Install(harmony);


        Debug.Log("<color=#00FF00>CATUI Applied.</color>");


        ModEvents.GameAwake.RegisterHandler((ref ModEvents.SGameAwakeData _data) =>
        {

            CatuiSettings.Load();
            CatuiToolbeltSettings.Apply();
        CatuiHealthBarSettings.Load();
            CatuiQuantityProbe.Reload();
            CatuiGearsUiGate.TryConnect();
            CatuiQuantityMouseWatcher.EnsureRunning();

            Constants.TrackedFriendColors = new Color[8]
            {
                new Color32(255, 173, 31, byte.MaxValue),
                new Color32(4, 254, 133, byte.MaxValue),
                new Color32(1, 239, 255, byte.MaxValue),
                new Color32(255, 82, 82, byte.MaxValue),
                new Color32(89, 167, 255, byte.MaxValue),
                new Color32(231, 92, 255, byte.MaxValue),
                new Color32(255, 235, 59, byte.MaxValue),
                new Color32(153, 110, 255, byte.MaxValue)
            };
        });
    }
}
