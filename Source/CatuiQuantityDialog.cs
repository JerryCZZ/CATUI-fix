using System;
using System.Reflection;
using UnityEngine;

public static class CatuiQuantityDialog
{
    private static XUiC_ItemStack selected;
    private static string origin;
    private static int openSlot;
    private static string amountText;
    private static bool visible;
    private static string feedbackText;
    private static int closedFrame = -10000;
    private static int lastShortcutFrame = -1;
    private static bool enterHeld;
    private static bool escapeHeld;
    private static bool newEscapeHeld;
    private static bool inputSystemResolved;
    private static PropertyInfo keyboardCurrentProperty;
    private static PropertyInfo escapeKeyProperty;
    private static PropertyInfo keyIsPressedProperty;
    private static PropertyInfo keyWasPressedProperty;


    public static bool BlocksPointerAtCurrentPosition()
    {
        if (!visible && Time.frameCount != closedFrame) return false;
        var mouse = Input.mousePosition;
        var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
        return rect.Contains(guiPoint);
    }
    private static bool clearInitialFocus;
    private static Rect rect = new Rect(0, 0, 366, 112);

    private static GUIStyle transparentStyle, titleStyle, labelStyle, fieldStyle, feedbackStyle;
    private static GUIStyle minusStyle, plusStyle, confirmStyle, cancelStyle;
    private static Texture2D panelTex, headerTex, fieldTex, fieldFocusedTex;
    private static Texture2D neutralTex, neutralHoverTex, greenTex, greenHoverTex, redTex, redHoverTex;
    private static Texture2D separatorTex;
    private static bool stylesReady;

    public static bool Visible { get { return visible; } }

    public static void Open(XUiC_ItemStack slot, string source)
    {
        if (slot == null || slot.itemStack == null || slot.itemStack.IsEmpty()) return;
        if (source != "Backpack" && source != "Loot") return;
        try
        {
            XUiC_LootWindow loot = slot.xui.GetChildByType<XUiC_LootWindow>();
            if (loot == null || !loot.IsOpen) return;

            selected = slot;
            origin = source;
            openSlot = slot.SlotNumber;
            amountText = slot.itemStack.count.ToString();
            feedbackText = null;
            rect.width = 366f;
            rect.height = 112f;
            float guiY = Screen.height - Input.mousePosition.y;
            rect.x = Mathf.Clamp(Input.mousePosition.x - rect.width * .5f, 8f,
                                 Mathf.Max(8f, Screen.width - rect.width - 8f));
            rect.y = Mathf.Clamp(guiY - rect.height - 12f, 8f,
                                 Mathf.Max(8f, Screen.height - rect.height - 8f));
            visible = true;
            clearInitialFocus = true;
            enterHeld = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
            escapeHeld = Input.GetKey(KeyCode.Escape);
            newEscapeHeld = IsInputSystemEscapePressed();
            Log.Out("[CATUI][QuantityDialog] OPEN source={0} slot={1} available={2}",
                    origin, openSlot, slot.itemStack.count);
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantityDialog] OPEN failed: " + ex);
            Close();
        }
    }

    private static void Close()
    {
        if (visible) Log.Out("[CATUI][QuantityDialog] CLOSED");
        if (visible) closedFrame = Time.frameCount;
        visible = false;
        feedbackText = null;
        selected = null;
        clearInitialFocus = false;
    }

    private static bool StillValid()
    {
        try
        {
            if (selected == null || selected.xui == null ||
                selected.itemStack == null || selected.itemStack.IsEmpty())
                return false;
            var loot = selected.xui.GetChildByType<XUiC_LootWindow>();
            return loot != null && loot.IsOpen &&
                   selected.SlotNumber == openSlot && selected.itemStack.count > 0;
        }
        catch { return false; }
    }

    public static void Render()
    {
        if (!visible) return;
        if (!StillValid()) { Close(); return; }
        try
        {
            EnsureStyles();
            Event e = Event.current;
            if (e != null && (e.type == EventType.KeyDown || e.rawType == EventType.KeyDown))
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    TryShortcut(false, "IMGUI");
                    if (e.type != EventType.Used) e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter ||
                    e.character == '\n' || e.character == '\r')
                {
                    TryShortcut(true, "IMGUI");
                    if (e.type != EventType.Used) e.Use();
                    return;
                }
            }
            rect = GUI.Window(82416, rect, DrawWindow, GUIContent.none, transparentStyle);
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantityDialog] Render failed: " + ex.Message);
            Close();
        }
    }

    public static void PollShortcuts()
    {
        if (!visible) return;
        try
        {
            bool escape = Input.GetKey(KeyCode.Escape);
            bool enter = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
            bool newEscape = IsInputSystemEscapePressed();
            bool escapePressed = Input.GetKeyDown(KeyCode.Escape) ||
                                 (escape && !escapeHeld) ||
                                 (newEscape && !newEscapeHeld) ||
                                 WasInputSystemEscapePressedThisFrame();
            bool enterPressed = Input.GetKeyDown(KeyCode.Return) ||
                                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                (enter && !enterHeld);
            escapeHeld = escape;
            newEscapeHeld = newEscape;
            enterHeld = enter;
            if (escapePressed) TryShortcut(false, "Update");
            else if (enterPressed) TryShortcut(true, "Update");
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantityDialog] Keyboard polling failed: " + ex.Message);
        }
    }

    private static object InputSystemEscapeKey()
    {
        if (!inputSystemResolved)
        {
            inputSystemResolved = true;
            try
            {
                Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem", false);
                if (keyboardType == null) return null;
                const BindingFlags f = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
                keyboardCurrentProperty = keyboardType.GetProperty("current", f);
                escapeKeyProperty = keyboardType.GetProperty("escapeKey", f);
                if (escapeKeyProperty != null)
                {
                    Type keyType = escapeKeyProperty.PropertyType;
                    keyIsPressedProperty = keyType.GetProperty("isPressed", f);
                    keyWasPressedProperty = keyType.GetProperty("wasPressedThisFrame", f);
                }
                Log.Out("[CATUI][QuantityDialog] InputSystem Escape fallback available={0}",
                    keyboardCurrentProperty != null && escapeKeyProperty != null);
            }
            catch (Exception ex)
            {
                Log.Warning("[CATUI][QuantityDialog] InputSystem Escape discovery: " + ex.Message);
            }
        }
        try
        {
            object keyboard = keyboardCurrentProperty == null ? null : keyboardCurrentProperty.GetValue(null, null);
            return keyboard == null || escapeKeyProperty == null ? null : escapeKeyProperty.GetValue(keyboard, null);
        }
        catch { return null; }
    }

    private static bool IsInputSystemEscapePressed()
    {
        try
        {
            object key = InputSystemEscapeKey();
            return key != null && keyIsPressedProperty != null &&
                   (bool)keyIsPressedProperty.GetValue(key, null);
        }
        catch { return false; }
    }

    private static bool WasInputSystemEscapePressedThisFrame()
    {
        try
        {
            object key = InputSystemEscapeKey();
            return key != null && keyWasPressedProperty != null &&
                   (bool)keyWasPressedProperty.GetValue(key, null);
        }
        catch { return false; }
    }

    private static void TryShortcut(bool confirm, string source)
    {
        if (!visible || lastShortcutFrame == Time.frameCount) return;
        lastShortcutFrame = Time.frameCount;
        Log.Out("[CATUI][QuantityDialog] KEY {0} via {1}",
            confirm ? "Enter" : "Escape", source);
        if (confirm) ConfirmPreview();
        else Close();
    }

    private static void DrawWindow(int id)
    {
        Event keyEvent = Event.current;
        if (keyEvent != null && keyEvent.type == EventType.KeyDown &&
            keyEvent.keyCode == KeyCode.Escape)
        {
            TryShortcut(false, "Field");
            keyEvent.Use();
            return;
        }
        int available = selected.itemStack.count;
        float w = rect.width;

        GUI.DrawTexture(new Rect(0, 0, w, rect.height), panelTex, ScaleMode.StretchToFill);
        GUI.DrawTexture(new Rect(0, 0, w, 30), headerTex, ScaleMode.StretchToFill);
        GUI.Label(new Rect(10, 2, w - 20, 26), "QUANTITY TRANSFER", titleStyle);

        if (GUI.Button(new Rect(13, 40, 43, 43), "−", minusStyle)) Change(-1);
        GUI.SetNextControlName("CATUI_QTY_AMOUNT");
        string edited = GUI.TextField(new Rect(65, 42, 108, 39), amountText ?? "1", 12, fieldStyle);
        if (edited != amountText)
        {
            amountText = DigitsOnly(edited);
            feedbackText = null;
            rect.height = 112f;
        }
        if (GUI.Button(new Rect(181, 40, 43, 43), "+", plusStyle)) Change(+1);

        GUI.DrawTexture(new Rect(237, 41, 1, 41), separatorTex);
        if (GUI.Button(new Rect(251, 40, 43, 43), "✓", confirmStyle)) ConfirmPreview();
        if (GUI.Button(new Rect(307, 40, 43, 43), "×", cancelStyle)) Close();

        GUI.Label(new Rect(9, 87, 53, 20), "Decrease", labelStyle);
        GUI.Label(new Rect(65, 87, 108, 20), "Amount", labelStyle);
        GUI.Label(new Rect(176, 87, 53, 20), "Increase", labelStyle);
        GUI.Label(new Rect(246, 87, 53, 20), "Confirm", labelStyle);
        GUI.Label(new Rect(301, 87, 55, 20), "Cancel", labelStyle);
        if (!string.IsNullOrEmpty(feedbackText))
            GUI.Label(new Rect(13, 110, w - 26, 24), feedbackText, feedbackStyle);

        if (clearInitialFocus && Event.current.type == EventType.Layout)
        {
            GUI.FocusControl(null);
            clearInitialFocus = false;
        }
        GUI.DragWindow(new Rect(0, 0, w, 28));
    }

    private static string DigitsOnly(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        char[] result = new char[raw.Length];
        int n = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] >= '0' && raw[i] <= '9')
                result[n++] = raw[i];
        }
        return new string(result, 0, n);
    }

    private static void Change(int delta)
    {
        if (!StillValid()) { Close(); return; }
        int available = selected.itemStack.count;
        int current = CatuiQuantityTransferRules.NormalizeAmount(amountText, available);
        long next = (long)current + delta;
        feedbackText = null;
        rect.height = 112f;
        amountText = Math.Max(1L, Math.Min((long)available, next)).ToString();
        Log.Out("[CATUI][QuantityDialog] EDIT amount={0} available={1}", amountText, available);
    }

    private static void ConfirmPreview()
    {
        if (!StillValid()) { Close(); return; }
        int available = selected.itemStack.count;
        int requested = CatuiQuantityTransferRules.NormalizeAmount(amountText, available);
        amountText = requested.ToString();

        CatuiQuantityBackendResult result = CatuiQuantityBackend.TryTransfer(selected, origin, requested);
        Log.Out("[CATUI][QuantityDialog] CONFIRM backend source={0} slot={1} requested={2} status={3} moved={4}",
                origin, openSlot, requested, result.Status, result.Moved);
        if (result.Status == CatuiQuantityBackendStatus.Completed ||
            result.Status == CatuiQuantityBackendStatus.Partial)
        {
            Close();
        }
        else
        {
            if (result.Status == CatuiQuantityBackendStatus.NotEnoughSpace)
                PlayNotEnoughSpaceSound();
            feedbackText = result.Status == CatuiQuantityBackendStatus.NotEnoughSpace ?
                "Not enough space" : "Transfer unavailable";
            rect.height = 140f;
            rect.y = Mathf.Clamp(rect.y, 8f, Mathf.Max(8f, Screen.height - rect.height - 8f));
            Log.Warning("[CATUI][QuantityDialog] Transfer not performed: " + result.Message);
        }
    }

    private static void PlayNotEnoughSpaceSound()
    {
        try
        {
            if (selected == null || selected.xui == null) return;
            MethodInfo playBack = selected.xui.GetType().GetMethod(
                "PlayMenuBackSound", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (playBack != null)
                playBack.Invoke(selected.xui, null);
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantitySound] Not enough space sound failed: " + ex.GetBaseException().Message);
        }
    }

    private static void EnsureStyles()
    {
        if (stylesReady) return;
        stylesReady = true;
        panelTex = Rounded(366, 112, 13, new Color32(42,42,42,247));
        headerTex = Rounded(366, 30, 12, new Color32(53,53,53,255));
        fieldTex = Rounded(108, 39, 8, new Color32(26,26,26,255));
        fieldFocusedTex = Rounded(108, 39, 8, new Color32(35,35,35,255));
        neutralTex = Rounded(43, 43, 21, new Color32(71,71,71,255));
        neutralHoverTex = Rounded(43, 43, 21, new Color32(99,74,41,255));
        greenTex = Rounded(43, 43, 21, new Color32(40,137,75,255));
        greenHoverTex = Rounded(43, 43, 21, new Color32(55,170,89,255));
        redTex = Rounded(43, 43, 21, new Color32(164,50,52,255));
        redHoverTex = Rounded(43, 43, 21, new Color32(206,65,67,255));
        separatorTex = Solid(new Color32(200,142,52,165));

        transparentStyle = new GUIStyle(GUI.skin.window);
        transparentStyle.normal.background = null;
        transparentStyle.onNormal.background = null;
        transparentStyle.border = new RectOffset(0,0,0,0);
        transparentStyle.padding = new RectOffset(0,0,0,0);
        transparentStyle.margin = new RectOffset(0,0,0,0);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color32(255,187,71,255);

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontSize = 11;
        labelStyle.normal.textColor = new Color32(213,213,213,255);

        feedbackStyle = new GUIStyle(GUI.skin.label);
        feedbackStyle.alignment = TextAnchor.MiddleCenter;
        feedbackStyle.fontSize = 12;
        feedbackStyle.fontStyle = FontStyle.Bold;
        feedbackStyle.normal.textColor = new Color32(255,115,115,255);

        fieldStyle = new GUIStyle(GUI.skin.textField);
        fieldStyle.alignment = TextAnchor.MiddleCenter;
        fieldStyle.fontSize = 20;
        fieldStyle.fontStyle = FontStyle.Bold;
        fieldStyle.normal.background = fieldTex;
        fieldStyle.focused.background = fieldFocusedTex;
        fieldStyle.hover.background = fieldFocusedTex;
        fieldStyle.normal.textColor = Color.white;
        fieldStyle.focused.textColor = Color.white;
        fieldStyle.border = new RectOffset(0,0,0,0);
        fieldStyle.padding = new RectOffset(5,5,3,3);

        minusStyle = CircleStyle(neutralTex, neutralHoverTex, new Color32(255,255,255,255), 28);
        plusStyle = CircleStyle(neutralTex, neutralHoverTex, new Color32(255,255,255,255), 28);
        confirmStyle = CircleStyle(greenTex, greenHoverTex, Color.white, 29);
        cancelStyle = CircleStyle(redTex, redHoverTex, Color.white, 28);
    }

    private static GUIStyle CircleStyle(Texture2D normal, Texture2D hover, Color textColor, int fontSize)
    {
        var style = new GUIStyle(GUI.skin.button);
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = hover;
        style.focused.background = normal;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = fontSize;
        style.fontStyle = FontStyle.Bold;
        style.border = new RectOffset(0,0,0,0);
        style.padding = new RectOffset(0,0,0,0);
        return style;
    }

    private static Texture2D Solid(Color32 color)
    {
        var tex = new Texture2D(1,1,TextureFormat.ARGB32,false);
        tex.SetPixel(0,0,color);
        tex.Apply();
        return tex;
    }

    private static Texture2D Rounded(int width, int height, int radius, Color32 color)
    {
        var tex = new Texture2D(width,height,TextureFormat.ARGB32,false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32 clear = new Color32(0,0,0,0);
        float r = radius - 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x < radius ? r : (x >= width-radius ? width-r-1f : x);
                float cy = y < radius ? r : (y >= height-radius ? height-r-1f : y);
                float dx = x-cx, dy = y-cy;
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                float alpha = Mathf.Clamp01(radius - dist);
                Color32 c = color;
                c.a = (byte)Mathf.RoundToInt(color.a * alpha);
                tex.SetPixel(x,y,alpha <= 0f ? clear : c);
            }
        }
        tex.Apply();
        return tex;
    }
}
