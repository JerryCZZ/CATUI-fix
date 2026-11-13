using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

[HarmonyPatch]
public class XUiC_BuffPopoutListPatch
{

    private static readonly Dictionary<EntityUINotification, UILabel> cachedLabels = new();
    

    private static readonly Dictionary<EntityUINotification, string> cachedTexts = new();
    

    private static float lastUpdateTime = 0f;
    

    private const float UpdateInterval = 0.1f;


    private static UIScrollView cachedScrollView;


    private static float lastScrollbarUpdateTime = 0f;


    private const float ScrollbarUpdateInterval = 1f;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiC_BuffPopoutList), "OnOpen")]
    public static void OnOpenPostfix(XUiC_BuffPopoutList __instance)
    {

        cachedLabels.Clear();
        cachedTexts.Clear();
        cachedScrollView = null;
        lastScrollbarUpdateTime = 0f;
        
        if (__instance.items != null && __instance.items.Count > 0)
        {
            UpdateBuffPositions(__instance);
            PreCacheLabels(__instance);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiC_BuffPopoutList), "AddNotification")]
    public static void AddNotificationPostfix(XUiC_BuffPopoutList __instance, EntityUINotification _notification)
    {
        if (__instance.items != null && __instance.items.Count > 0)
        {
            var lastItem = __instance.items[__instance.items.Count - 1];
            if (lastItem != null && lastItem.Notification == _notification)
            {

                UILabel textName = CacheLabel(lastItem.Item, _notification);
                if (textName != null && _notification.Buff != null)
                {

                    string newText = BindingsManager.ReplaceCVars(_notification.Buff.BuffClass.LocalizedName);
                    UpdateTextIfChanged(_notification, textName, newText);
                }
                
                UISprite decoration = lastItem.Item.transform.Find("Decoration").GetComponent<UISprite>();
                if (decoration != null)
                {
                    decoration.color = _notification.GetColor();
                }
            }
            UpdateBuffPositions(__instance);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(XUiC_BuffPopoutList), "Update")]
    public static void UpdatePostfix(XUiC_BuffPopoutList __instance)
    {
        float currentTime = Time.time;


        if (currentTime - lastScrollbarUpdateTime >= ScrollbarUpdateInterval)
        {
            lastScrollbarUpdateTime = currentTime;
            RefreshScrollbars(__instance);
        }


        if (currentTime - lastUpdateTime < UpdateInterval)
        {
            return;
        }
        lastUpdateTime = currentTime;
        
        if (__instance.items != null && __instance.items.Count > 0)
        {
            foreach (var data in __instance.items)
            {
                if (data.Notification != null && data.Notification.Buff != null && !data.Notification.Buff.Paused)
                {

                    UILabel textName = GetOrCacheLabel(data.Item, data.Notification);
                    if (textName != null)
                    {
                        string newText = BindingsManager.ReplaceCVars(data.Notification.Buff.BuffClass.LocalizedName);
                        UpdateTextIfChanged(data.Notification, textName, newText);
                    }
                }
            }
            

            CleanupRemovedNotifications(__instance);
        }
    }
    





    private static void RefreshScrollbars(XUiC_BuffPopoutList __instance)
    {
        if (__instance == null || __instance.Parent == null || __instance.Parent.ViewComponent == null)
        {
            return;
        }

        if (cachedScrollView == null)
        {
            cachedScrollView = __instance.Parent.ViewComponent.UiTransform.GetComponent<UIScrollView>();
        }

        if (cachedScrollView != null)
        {
            cachedScrollView.UpdateScrollbars();
        }
    }




    private static void CleanupRemovedNotifications(XUiC_BuffPopoutList __instance)
    {
        var activeNotifications = new HashSet<EntityUINotification>();
        foreach (var data in __instance.items)
        {
            if (data.Notification != null)
            {
                activeNotifications.Add(data.Notification);
            }
        }
        

        var labelsToRemove = new List<EntityUINotification>();
        var textsToRemove = new List<EntityUINotification>();
        
        foreach (var key in cachedLabels.Keys)
        {
            if (!activeNotifications.Contains(key))
            {
                labelsToRemove.Add(key);
            }
        }
        
        foreach (var key in cachedTexts.Keys)
        {
            if (!activeNotifications.Contains(key))
            {
                textsToRemove.Add(key);
            }
        }
        
        foreach (var key in labelsToRemove)
        {
            cachedLabels.Remove(key);
        }
        
        foreach (var key in textsToRemove)
        {
            cachedTexts.Remove(key);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(XUiC_BuffPopoutList), "updateEntries")]
    public static bool updateEntriesPrefix(XUiC_BuffPopoutList __instance)
    {
        UpdateBuffPositions(__instance);
        return false;
    }
    



    private static void PreCacheLabels(XUiC_BuffPopoutList __instance)
    {
        foreach (var data in __instance.items)
        {
            if (data.Notification != null)
            {
                CacheLabel(data.Item, data.Notification);
            }
        }
    }
    



    private static UILabel CacheLabel(GameObject item, EntityUINotification notification)
    {
        if (!cachedLabels.TryGetValue(notification, out UILabel label))
        {
            label = item.transform.Find("TextName")?.GetComponent<UILabel>();
            if (label != null)
            {
                cachedLabels[notification] = label;
            }
        }
        return label;
    }
    



    private static UILabel GetOrCacheLabel(GameObject item, EntityUINotification notification)
    {
        if (!cachedLabels.TryGetValue(notification, out UILabel label) || label == null)
        {
            label = item.transform.Find("TextName")?.GetComponent<UILabel>();
            if (label != null)
            {
                cachedLabels[notification] = label;
            }
        }
        return label;
    }
    



    private static void UpdateTextIfChanged(EntityUINotification notification, UILabel label, string newText)
    {
        if (!cachedTexts.TryGetValue(notification, out string oldText) || oldText != newText)
        {
            label.text = newText;
            cachedTexts[notification] = newText;
        }
    }
    



    private static void UpdateBuffPositions(XUiC_BuffPopoutList __instance)
    {
        int visibleCount = 0;
        for (int i = 0; i < __instance.items.Count; i++)
        {
            var data = __instance.items[i];
            if (!data.Notification.Buff.Paused)
            {
                TweenPosition component = data.Item.GetComponent<TweenPosition>();
                if ((bool)component)
                {
                    Object.Destroy(component);
                }
                Vector3 currentPos = data.Item.transform.localPosition;
                float targetY = (float)visibleCount * __instance.height + __instance.yOffset;
                data.Item.transform.localPosition = new Vector3(currentPos.x, -targetY, currentPos.z);
                visibleCount++;
            }
        }
    }
}
