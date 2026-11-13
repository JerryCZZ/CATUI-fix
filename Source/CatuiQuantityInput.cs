using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

public sealed class CatuiQuantityMouseWatcher : MonoBehaviour
{
    private static CatuiQuantityMouseWatcher instance;
    private static Type uiCameraType;
    private static PropertyInfo hoveredProperty;
    private static FieldInfo hoveredField;
    private static bool reflected;
    private static bool inputErrorReported;

    public static void EnsureRunning()
    {
        if (instance != null) return;
        try
        {
            var go = new GameObject("CATUI_Quantity_Mouse_Input");
            UnityEngine.Object.DontDestroyOnLoad(go);
            instance = go.AddComponent<CatuiQuantityMouseWatcher>();
            
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantityMouse] Failed to attach watcher: " + ex);
        }
    }

    private void Update()
    {
        try
        {
            CatuiQuantityDialog.PollShortcuts();
            CatuiQuantityInputShield.UpdateShortcutCaptureState();
            if (!CatuiQuantityInputShield.IsQuantityShortcutDown()) return;
            if (!CatuiQuantityDialog.Visible) CatuiQuantityProbe.OnRightMouseOver(GetHoveredGameObject());
        }
        catch (Exception ex)
        {
            if (inputErrorReported) return;
            inputErrorReported = true;
            Log.Warning("[CATUI][QuantityMouse] Input polling unavailable: " + ex.Message);
        }
    }

    private void OnGUI()
    {
        if (CatuiQuantityProbe.Enabled) CatuiQuantityDialog.Render();
    }

    private static void EnsureHoveredReflection()
    {
        if (reflected) return;
        reflected = true;
        uiCameraType = AccessTools.TypeByName("UICamera");
        if (uiCameraType != null)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            hoveredProperty = uiCameraType.GetProperty("hoveredObject", flags);
            hoveredField = uiCameraType.GetField("hoveredObject", flags);
        }
    }

    private static GameObject GetHoveredGameObject()
    {
        try
        {
            EnsureHoveredReflection();
            object hovered = hoveredProperty != null ? hoveredProperty.GetValue(null, null) :
                hoveredField != null ? hoveredField.GetValue(null) : null;
            if (hovered is GameObject gameObject) return gameObject;
            return (hovered as Component)?.gameObject;
        }
        catch { return null; }
    }

    private static string DescribeHovered()
    {
        try
        {
            EnsureHoveredReflection();
            object hovered = hoveredProperty != null ? hoveredProperty.GetValue(null, null)
                : hoveredField != null ? hoveredField.GetValue(null) : null;
            GameObject obj = hovered as GameObject;
            if (obj == null && hovered is Component component) obj = component.gameObject;
            if (obj == null) return hovered == null ? "null" : hovered.GetType().Name;

            var result = new StringBuilder();
            Transform node = obj.transform;
            for (int i = 0; node != null && i < 7; i++, node = node.parent)
            {
                if (i != 0) result.Append(" <- ");
                result.Append(node.name);
            }
            return result.ToString();
        }
        catch (Exception ex)
        {
            return "hover-inspection-error:" + ex.GetType().Name;
        }
    }
}

public static class CatuiQuantityInputShield
{
    private static bool installed;
    private static bool suppressShortcutClick;
    private static int releaseFrame = -1;

    public static void UpdateShortcutCaptureState()
    {
        if (!suppressShortcutClick) return;
        if (Input.GetMouseButton(1))
        {
            releaseFrame = -1;
            return;
        }
        if (releaseFrame < 0) releaseFrame = Time.frameCount;
        if (Time.frameCount > releaseFrame + 1)
        {
            suppressShortcutClick = false;
            releaseFrame = -1;
        }
    }

    public static bool IsQuantityShortcutDown()
    {
        return CatuiQuantityProbe.Enabled &&
               (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
               !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) &&
               !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) &&
               Input.GetMouseButtonDown(1);
    }

    public static void Install(Harmony harmony)
    {
        if (installed) return;
        try
        {
            Type camera = AccessTools.TypeByName("UICamera");
            if (camera == null)
            {
                Log.Warning("[CATUI][QuantityShield] UICamera type not found; click-through NOT blocked.");
                return;
            }
            MethodInfo target = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;
            foreach (MethodInfo method in camera.GetMethods(flags))
            {
                if (method.Name != "Notify") continue;
                ParameterInfo[] args = method.GetParameters();
                bool hasTarget = false;
                bool hasName = false;
                foreach (ParameterInfo arg in args)
                {
                    if (arg.ParameterType == typeof(GameObject)) hasTarget = true;
                    if (arg.ParameterType == typeof(string)) hasName = true;
                }
                if (hasTarget && hasName)
                {
                    target = method;
                    break;
                }
            }
            if (target == null)
            {
                Log.Warning("[CATUI][QuantityShield] UICamera.Notify(GameObject,string,...) not found; click-through NOT blocked.");
                return;
            }
            MethodInfo handler = typeof(CatuiQuantityInputShield).GetMethod(
                "BeforeNguiNotify", BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(target, prefix: new HarmonyMethod(handler));
            installed = true;
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantityShield] Patch failed; click-through NOT blocked: " + ex);
        }
    }

    public static bool BeforeNguiNotify(object[] __args)
    {
        if (__args == null) return true;
        string eventName = null;
        GameObject target = null;
        for (int i = 0; i < __args.Length; i++)
        {
            if (__args[i] is string) eventName = (string)__args[i];
            if (__args[i] is GameObject) target = (GameObject)__args[i];
        }
        if (string.IsNullOrEmpty(eventName)) return true;

        if (IsQuantityShortcutDown() && eventName == "OnPress")
        {
            suppressShortcutClick = true;
            releaseFrame = -1;
            if (!CatuiQuantityDialog.Visible)
                CatuiQuantityProbe.OnRightMouseOver(target);
        }

        if (suppressShortcutClick)
        {
            switch (eventName)
            {
                case "OnPress":
                case "OnClick":
                case "OnDoubleClick":
                case "OnDrag":
                case "OnDragStart":
                case "OnDragEnd":
                case "OnDragOver":
                case "OnDragOut":
                case "OnDrop":
                    return false;
            }
        }

        if (!CatuiQuantityDialog.BlocksPointerAtCurrentPosition()) return true;
        switch (eventName)
        {
            case "OnPress":
            case "OnClick":
            case "OnDoubleClick":
            case "OnDrag":
            case "OnDragStart":
            case "OnDragEnd":
            case "OnDragOver":
            case "OnDragOut":
            case "OnDrop":
            case "OnScroll":
            case "OnHover":
            case "OnTooltip":
                return false;
            default:
                return true;
        }
    }
}
