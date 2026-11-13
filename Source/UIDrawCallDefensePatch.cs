using HarmonyLib;
using UnityEngine;












[HarmonyPatch(typeof(UIDrawCall))]
public static class UIDrawCallDefensePatch
{
    private static Shader _fallbackShader;

    private static Shader GetFallbackShader()
    {
        if (_fallbackShader != null)
        {
            return _fallbackShader;
        }
        _fallbackShader = Shader.Find("Unlit/Text");
        if (_fallbackShader == null)
        {
            _fallbackShader = Shader.Find("GUI/Text Shader");
        }
        if (_fallbackShader == null)
        {
            _fallbackShader = Shader.Find("Unlit/Transparent Colored");
        }
        return _fallbackShader;
    }




    [HarmonyPrefix]
    [HarmonyPatch("CreateMaterial")]
    public static void CreateMaterialPrefix(UIDrawCall __instance)
    {
        try
        {
            Material baseMat = __instance.baseMaterial;
            if (baseMat == null)
            {
                return;
            }
            if (baseMat.shader != null)
            {
                return;
            }


            Shader fallback = GetFallbackShader();
            if (fallback == null)
            {
                return;
            }
            baseMat.shader = fallback;
            Debug.LogWarning("[CATUI] UIDrawCall baseMaterial had null shader, repaired to " + fallback.name);
        }
        catch (System.Exception e)
        {

            Debug.LogWarning("[CATUI] CreateMaterialPrefix error: " + e.Message);
        }
    }
}
