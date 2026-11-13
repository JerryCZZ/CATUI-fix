/*Copyright 2022 Christopher Beda

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.*/

using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TargetBuffs;





[HarmonyPatch(typeof(XUi))]
public static class XUiFontPatch
{
    private const string TAG = "Error Reverse Patching XUiController method: ";




    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch("GetUIFontByName")]
    public static bool GetUIFontByName(ref NGUIFont __result, string _name, bool _showWarning = true)
    {
        NGUIFont catuiFont = CATUIFontManager.GetNGUIFontByName(_name);


        if (CATUIFontManager.IsFontUsable(catuiFont))
        {
            __result = catuiFont;
            return false;
        }


        if (_showWarning)
        {
            Log.Warning("CATUI font not found or unusable: " + _name + ", from: " + StackTraceUtility.ExtractStackTrace());
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch("loadAsync")]
    public static IEnumerator LoadAsync(IEnumerator __result, XUi __instance)
    {

        TargetBuffController.Initialize();

        Dictionary<string, XUiFromXml.StyleData> styles = XUiFromXml.styles;
        yield return CATUIFontManager.LoadFonts(__instance);
        yield return __result;
        yield break;
    }
}