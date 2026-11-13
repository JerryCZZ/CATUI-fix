using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class CatuiQuantityProbe
{
    private const string Name = "CATUI_QuantityTransfer";
    private static bool enabled;
    private static readonly List<XUiC_ItemStack> liveSlots = new List<XUiC_ItemStack>();
    public static void Reload()
    {
        enabled = false;
        try
        {
            string value = FindSaved();
            if (value == null) value = FindDefault();
            enabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
        catch (Exception ex)
        {
            enabled = false;
            Log.Warning("[CATUI][QuantityProbe] Settings read failed: " + ex.Message);
        }
    }

    private static string FindSaved()
    {
        string path = Path.Combine(GameIO.GetUserGameDataDir(), "Gears", "ModSettings.xml");
        if (!File.Exists(path)) return null;
        XDocument doc = XDocument.Load(path);
        var mod = doc.Descendants("Mod").FirstOrDefault(x =>
            string.Equals((string)x.Attribute("name"), "CATUI", StringComparison.OrdinalIgnoreCase));
        return mod?.Descendants().FirstOrDefault(x => (string)x.Attribute("name") == Name)?.Attribute("value")?.Value;
    }

    private static string FindDefault()
    {
        string path = CatuiSettings.SettingsFilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        XDocument doc = XDocument.Load(path);
        var setting = doc.Descendants().FirstOrDefault(x => (string)x.Attribute("name") == Name);
        return setting?.Attribute("value")?.Value ?? setting?.Attribute("defaultValue")?.Value;
    }

    public static void OnItemStackInitialized(XUiC_ItemStack slot)
    {
        if (!enabled) return;
        if (!liveSlots.Contains(slot)) liveSlots.Add(slot);
    }


    public static bool Enabled { get { return enabled; } }

    public static void OnRightMouseOver(GameObject hovered)
    {
        if (!enabled || hovered == null) return;
        try
        {
            int matches = 0;
            XUiC_ItemStack selected = null;
            string selectedSource = null;
            int populated = 0;
            int inspected = 0;
            string hoverPath = Hierarchy(hovered.transform);
            for (int i = liveSlots.Count - 1; i >= 0; --i)
            {
                XUiC_ItemStack slot = liveSlots[i];
                if (slot == null) { liveSlots.RemoveAt(i); continue; }
                if (++inspected > 1200) break;
                if (slot.itemStack != null && !slot.itemStack.IsEmpty()) populated++;

                Transform slotTransform = ResolveTransform(slot);
                if (slotTransform == null) continue;
                if (!hovered.transform.IsChildOf(slotTransform) && !slotTransform.IsChildOf(hovered.transform))
                    continue;

                ++matches;
                string source = Hierarchy(slotTransform).ToLowerInvariant().Contains("loot") ? "Loot" :
                    Hierarchy(slotTransform).ToLowerInvariant().Contains("backpack") ? "Backpack" : "Unknown";
                if (selected == null) { selected = slot; selectedSource = source; }
            }
            if (matches == 1 && selected != null && selected.itemStack != null &&
                !selected.itemStack.IsEmpty() && (selectedSource == "Backpack" || selectedSource == "Loot"))
            {
                var lootWindow = selected.xui.GetChildByType<XUiC_LootWindow>();
                if (lootWindow != null && lootWindow.IsOpen)
                    CatuiQuantityDialog.Open(selected, selectedSource);
            }

        }
        catch (Exception ex) { Log.Warning("[CATUI][QuantitySlot] Inspect error: " + ex); }
    }

    private static Transform ResolveTransform(XUiC_ItemStack slot)
    {
        object view = GetMember(slot, "ViewComponent") ?? GetMember(slot, "viewComponent") ?? GetMember(slot, "View");
        return ToTransform(view) ?? ToTransform(GetMember(view, "UITransform")) ??
               ToTransform(GetMember(view, "uiTransform")) ?? ToTransform(GetMember(view, "GameObject")) ??
               ToTransform(GetMember(view, "gameObject")) ?? ToTransform(GetMember(view, "Transform")) ??
               ToTransform(GetMember(slot, "UITransform"));
    }

    private static object GetMember(object obj, string name)
    {
        if (obj == null) return null;
        try
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = obj.GetType();
            for (; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, f | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(obj, null);
                var field = t.GetField(name, f | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(obj);
            }
        }
        catch { }
        return null;
    }

    private static Transform ToTransform(object obj)
    {
        if (obj is Transform transform) return transform;
        if (obj is GameObject gameObject) return gameObject.transform;
        if (obj is Component component) return component.transform;
        return null;
    }

    private static string Hierarchy(Transform node)
    {
        var parts = new List<string>();
        for (int i = 0; node != null && i < 12; i++, node = node.parent)
            parts.Add(node.name);
        return string.Join(" <- ", parts.ToArray());
    }

    public static void OnItemPress(XUiC_ItemStack slot, XUiController sender, int mouseButton) { }
}

public static class CatuiQuantityTransferPlanner
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;

    public struct Placement
    {
        public int TargetSlot;
        public int Amount;
        public Placement(int slot, int amount) { TargetSlot = slot; Amount = amount; }
    }

    public sealed class Plan
    {
        public string Direction;
        public int SourceSlot;
        public int Requested;
        public int Available;
        public int Movable;
        public int Leftover;
        public int StackLimit;
        public bool Valid;
        public bool PrecommitReady;
        public string Reason;
        public readonly List<Placement> Placements = new List<Placement>();
    }

    public static Plan Build(XUiC_ItemStack source, string origin, int requested,
                              XUiC_LootWindow lootWindow)
    {
        Plan p = new Plan();
        p.Direction = origin == "Backpack" ? "Backpack->Loot" : "Loot->Backpack";
        p.SourceSlot = source == null ? -1 : source.SlotNumber;
        p.Requested = requested;
        p.Reason = "Blocked";
        if (source == null || source.xui == null || source.itemStack == null ||
            source.itemStack.IsEmpty() || lootWindow == null || !lootWindow.IsOpen ||
            (origin != "Backpack" && origin != "Loot") || requested <= 0)
        { p.Reason = "Invalid source or closed loot window"; return p; }

        object backpack = source.xui.GetChildByType<XUiC_Backpack>();
        object loot = null;
        FieldInfo containerField = typeof(XUiC_LootWindow).GetField("lootContainer",F);
        if (containerField != null) loot = containerField.GetValue(lootWindow);
        if (loot == null) loot = source.xui.GetChildByType<XUiC_LootContainer>();
        if (backpack == null || loot == null)
        { p.Reason = "Both inventory controllers are required"; return p; }

        object srcCtl = origin == "Backpack" ? backpack : loot;
        object dstCtl = origin == "Backpack" ? loot : backpack;
        ItemStack[] srcSlots = ReadSlots(srcCtl);
        ItemStack[] dstSlots = ReadSlots(dstCtl);
        if (srcSlots == null || dstSlots == null || p.SourceSlot < 0 ||
            p.SourceSlot >= srcSlots.Length)
        { p.Reason = "Cannot resolve live source/destination slots"; return p; }
        int storageSlotCount = GetPhysicalStorageSlotCount(loot);
        if (storageSlotCount <= 0)
        { p.Reason = "Physical storage slot count unavailable"; return p; }
        if (origin == "Loot" && p.SourceSlot >= storageSlotCount)
        { p.Reason = "Source is outside physical storage bounds"; return p; }
        int destinationCount = origin == "Backpack" ?
            Math.Min(dstSlots.Length, storageSlotCount) : dstSlots.Length;
        ItemStack current = srcSlots[p.SourceSlot];
        if (current == null || current.IsEmpty() ||
            !current.itemValue.Equals(source.itemStack.itemValue))
        { p.Reason = "Source slot changed during popup"; return p; }
        p.Available = current.count;
        if (requested > p.Available)
        { p.Reason = "Source count changed"; return p; }
        bool sourceLocked;
        if (!TryGetLock(srcCtl,p.SourceSlot,out sourceLocked) || sourceLocked)
        { p.Reason = "Source lock state unknown or source locked"; return p; }

        p.StackLimit = GetStackLimit(current.itemValue);
        if (p.StackLimit <= 0)
        { p.Reason = "Item-specific stack limit unavailable"; return p; }

        int left = requested;
        for (int i=0; i<destinationCount && left>0; i++)
        {
            ItemStack target=dstSlots[i];
            if (target==null || target.IsEmpty() || target.count<=0 ||
                target.count>=p.StackLimit ||
                !target.itemValue.Equals(current.itemValue)) continue;
            bool locked;
            if (!TryGetLock(dstCtl,i,out locked))
            {
                p.Reason="Destination lock state unknown at occupied slot "+i;
                p.Placements.Clear();p.Movable=0;p.Leftover=requested;
                return p;
            }
            if (locked) continue;
            int n=Math.Min(left,p.StackLimit-target.count);
            if(n>0) {p.Placements.Add(new Placement(i,n));left-=n;}
        }
        for (int i=0; i<destinationCount && left>0; i++)
        {
            if (dstSlots[i] != null && !dstSlots[i].IsEmpty()) continue;
            bool locked;
            if (!TryGetLock(dstCtl,i,out locked))
            {
                p.Reason = "Destination lock state unknown at slot " + i;
                p.Placements.Clear(); p.Movable=0; p.Leftover=requested;
                return p;
            }
            if (locked) continue;
            int n=Math.Min(left,p.StackLimit);
            p.Placements.Add(new Placement(i,n));
            left-=n;
        }
        p.Movable = requested-left;
        p.Leftover = left;
        p.Valid = p.Movable > 0 && p.Movable == Sum(p.Placements);
        p.PrecommitReady = p.Valid;
        p.Reason = !p.Valid ? "Not enough space or invalid plan" :
                   (left>0 ? "Partial destination capacity" : "Destination capacity verified");
        return p;
    }

    public static int GetPhysicalStorageSlotCount(object controller)
    {
        if (controller == null) return -1;
        try
        {
            object tile = FindMember(controller, "localTileEntity");
            if (tile == null) return -1;
            object grid = FindMember(tile, "ItemGrid");
            if (grid == null) return -1;
            int count = ToPositiveInt(FindMember(grid, "SlotCount"));
            if (count <= 0) count = ToPositiveInt(FindMember(grid, "Length"));
            if (count <= 0) count = ToPositiveInt(FindMember(grid, "Count"));
            int arrayCount = -1;
            for (Type type = grid.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(F | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType != typeof(ItemStack[])) continue;
                    ItemStack[] array = field.GetValue(grid) as ItemStack[];
                    if (array == null) continue;
                    if (arrayCount < 0 || array.Length < arrayCount) arrayCount = array.Length;
                }
            }
            if (count <= 0) count = arrayCount;
            else if (arrayCount >= 0) count = Math.Min(count,arrayCount);
            return count > 0 && count <= 512 ? count : -1;
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][Quantity] Storage bounds inspection failed: " + ex.GetBaseException().Message);
            return -1;
        }
    }

    private static int ToPositiveInt(object value)
    {
        if (value is int && (int)value > 0) return (int)value;
        return -1;
    }

    private static object FindMember(object instance, string name)
    {
        if (instance == null) return null;
        for (Type type = instance.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name,F | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name,F | BindingFlags.DeclaredOnly);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(instance,null);
        }
        return null;
    }

    private static ItemStack[] ReadSlots(object controller)
    {
        MethodInfo m=controller.GetType().GetMethod("GetSlots",F,null,Type.EmptyTypes,null);
        return m == null ? null : m.Invoke(controller,null) as ItemStack[];
    }

    private static int Sum(List<Placement> list)
    {
        long total=0;
        for (int i=0;i<list.Count;i++)
        {
            if (list[i].Amount<=0) return -1;
            total+=list[i].Amount;
            if(total>int.MaxValue) return -1;
        }
        return (int)total;
    }

    private static bool TryGetLock(object controller,int slot,out bool locked)
    {
        locked=true;
        try
        {
            MethodInfo getter=controller.GetType().GetMethod("GetItemStackControllers",F,
                null,Type.EmptyTypes,null);
            if(getter!=null)
            {
                XUiC_ItemStack[] widgets=getter.Invoke(controller,null) as XUiC_ItemStack[];
                if(widgets!=null)
                {
                    for(int i=0;i<widgets.Length;i++)
                        if(widgets[i]!=null && widgets[i].SlotNumber==slot)
                        { locked=widgets[i].UserLockedSlot; return true; }
                }
            }
            object tile=null;
            FieldInfo local=controller.GetType().GetField("localTileEntity",F);
            if(local!=null) tile=local.GetValue(controller);
            if(tile!=null)
            {
                PropertyInfo gridProp=tile.GetType().GetProperty("ItemGrid",F);
                object grid=gridProp==null?null:gridProp.GetValue(tile,null);
                if(grid!=null)
                {
                    MethodInfo check=grid.GetType().GetMethod("IsSlotLocked",F,null,
                        new Type[]{typeof(int)},null);
                    if(check!=null)
                    { locked=(bool)check.Invoke(grid,new object[]{slot}); return true; }
                }
            }
        }
        catch(Exception ex)
        {
            Log.Warning("[CATUI][QuantityPlan] Lock read failed slot="+slot+" : "+ex.Message);
        }
        return false;
    }

    private static int GetStackLimit(ItemValue value)
    {
        try
        {
            object itemClass=null;
            Type t=value.GetType();
            PropertyInfo prop=t.GetProperty("ItemClass",F);
            if (prop!=null) itemClass=prop.GetValue(value,null);
            if (itemClass==null)
            {
                FieldInfo field=t.GetField("ItemClass",F);
                if (field!=null) itemClass=field.GetValue(value);
            }
            if (itemClass==null) return 0;
            Type c=itemClass.GetType();
            object limit=null;
            PropertyInfo number=c.GetProperty("Stacknumber",F);
            if (number!=null) limit=number.GetValue(itemClass,null);
            if (limit==null)
            {
                FieldInfo field=c.GetField("Stacknumber",F);
                if (field!=null) limit=field.GetValue(itemClass);
            }
            if (limit==null) return 0;
            if (limit is int) return Math.Max(0,(int)limit);
            PropertyInfo v=limit.GetType().GetProperty("Value",F);
            if (v!=null) limit=v.GetValue(limit,null);
            int result;
            return int.TryParse(Convert.ToString(limit),out result) && result>0 ? result : 0;
        }
        catch { return 0; }
    }
}

public static class CatuiQuantityNativeCommit
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public static bool Try(XUiC_ItemStack selected, string origin, int quantity,
                           CatuiQuantityTransferPlanner.Plan plan, out string reason)
    {
        reason = "Experimental multi-slot commit refused";
        if (selected == null || selected.xui == null || plan == null ||
            !plan.PrecommitReady || !plan.Valid || plan.Movable != quantity ||
            quantity <= 0 || plan.Placements.Count < 1 ||
            plan.Placements.Count > 32 ||
            (origin != "Backpack" && origin != "Loot"))
        { reason = "Invalid or unsupported precommit plan"; return false; }

        try
        {
            XUiC_LootWindow window = selected.xui.GetChildByType<XUiC_LootWindow>();
            if (window == null || !window.IsOpen) { reason = "Loot window closed"; return false; }
            object backpack = selected.xui.GetChildByType<XUiC_Backpack>();
            FieldInfo lf = typeof(XUiC_LootWindow).GetField("lootContainer", F);
            object loot = lf == null ? null : lf.GetValue(window);
            if (loot == null) loot = selected.xui.GetChildByType<XUiC_LootContainer>();
            if (backpack == null || loot == null) { reason="Inventory controller missing"; return false; }
            object src = origin == "Backpack" ? backpack : loot;
            object dst = origin == "Backpack" ? loot : backpack;
            string destSide = origin == "Backpack" ? "Loot" : "Backpack";

            CatuiQuantityTransferPlanner.Plan live =
                CatuiQuantityTransferPlanner.Build(selected, origin, plan.Requested, window);
            if (!live.PrecommitReady || live.Movable != quantity ||
                live.Leftover != plan.Leftover || live.SourceSlot != plan.SourceSlot ||
                live.Available != plan.Available || live.StackLimit != plan.StackLimit ||
                live.Placements.Count != plan.Placements.Count)
            { reason = "Inventory plan changed"; return false; }
            int n = live.Placements.Count;
            for (int i=0;i<n;i++)
                if (live.Placements[i].TargetSlot != plan.Placements[i].TargetSlot ||
                    live.Placements[i].Amount != plan.Placements[i].Amount)
                { reason = "Destination plan changed"; return false; }

            ItemStack[] ss=Slots(src), ds=Slots(dst);
            int si=plan.SourceSlot;
            int storageSlots = CatuiQuantityTransferPlanner.GetPhysicalStorageSlotCount(loot);
            if (ss==null || ds==null || storageSlots <= 0 ||
                (origin == "Loot" && si >= storageSlots) ||
                si<0 || si>=ss.Length ||
                ss[si]==null || ss[si].IsEmpty() ||
                ss[si].count != plan.Available ||
                !ss[si].itemValue.Equals(selected.itemStack.itemValue))
            { reason="Source changed before commit"; return false; }

            int[] indices=new int[n], expected=new int[n], delta=new int[n];
            ItemStack[] original=new ItemStack[n], updated=new ItemStack[n];
            bool[] used=new bool[ds.Length];
            int sum=0;
            for (int i=0;i<n;i++)
            {
                int ix=plan.Placements[i].TargetSlot;
                int add=plan.Placements[i].Amount;
                if(ix<0 || ix>=ds.Length ||
                   (destSide == "Loot" && ix>=storageSlots) ||
                   used[ix] || add<=0 ||
                   add>plan.StackLimit || add>quantity-sum)
                { reason="Invalid target placement"; return false; }
                used[ix]=true;
                int prev=Count(ds[ix]);
                if(prev>0 && (!ds[ix].itemValue.Equals(ss[si].itemValue) ||
                              prev>plan.StackLimit-add))
                { reason="Target changed or full"; return false; }
                indices[i]=ix;
                delta[i]=add;
                expected[i]=prev+add;
                original[i]=CloneCount(ds[ix],prev);
                updated[i]=prev==0 ? CloneCount(ss[si],add) :
                                         CloneCount(ds[ix],prev+add);
                sum+=add;
            }
            if(sum!=quantity) { reason="Plan quantity mismatch"; return false; }

            ItemStack originalSource=CloneCount(ss[si],ss[si].count);
            ItemStack updatedSource=CloneCount(ss[si],ss[si].count-quantity);
            int touched=0;
            bool touchedSource=false;
            try
            {
                for(int i=0;i<n;i++)
                {
                    touched=i+1;
                    Apply(dst,destSide,indices[i],updated[i]);
                    ItemStack[] read=Slots(dst);
                    if(SlotCount(read,indices[i])!=expected[i] ||
                       read==null || read[indices[i]]==null ||
                       !read[indices[i]].itemValue.Equals(updated[i].itemValue))
                        throw new InvalidOperationException("Target slot readback mismatch "+indices[i]);
                }
                touchedSource=true;
                Apply(src,origin,si,updatedSource);
                ItemStack[] finalSrc=Slots(src),finalDst=Slots(dst);
                if(SlotCount(finalSrc,si)!=Count(updatedSource))
                    throw new InvalidOperationException("Source readback mismatch: "+SlotCount(finalSrc,si));
                for(int i=0;i<n;i++)
                    if(SlotCount(finalDst,indices[i])!=expected[i] ||
                       finalDst==null || finalDst[indices[i]]==null ||
                       !finalDst[indices[i]].itemValue.Equals(updated[i].itemValue))
                        throw new InvalidOperationException("Final target mismatch "+indices[i]);
                reason="Native inventory transfer verified by local readback";
                return true;
            }
            catch(Exception e)
            {
                Log.Error("[CATUI][QuantityCommit] MULTI WRITE FAILED: "+e);
                bool rollbackOk=true;
                try { if(touchedSource) Apply(src,origin,si,originalSource); }
                catch(Exception r) { rollbackOk=false;Log.Error("[CATUI][QuantityCommit] SOURCE ROLLBACK FAILED: "+r); }
                for(int i=touched-1;i>=0;i--)
                {
                    try { Apply(dst,destSide,indices[i],original[i]); }
                    catch(Exception r) { rollbackOk=false;Log.Error("[CATUI][QuantityCommit] TARGET ROLLBACK FAILED: "+r); }
                }
                ItemStack[] rs=Slots(src),rd=Slots(dst);
                if(SlotCount(rs,si)!=Count(originalSource)) rollbackOk=false;
                for(int i=0;i<n;i++)
                    if(SlotCount(rd,indices[i])!=Count(original[i])) rollbackOk=false;
                reason="Commit failed; rollback "+(rollbackOk?"readback verified":"UNVERIFIED")+
                    ". Stop and inspect inventories.";
                return false;
            }
        }
        catch(Exception e)
        {
            Log.Warning("[CATUI][QuantityCommit] PREP FAILED: "+e);
            reason="Commit preparation failed: "+e.Message;
            return false;
        }
    }
    static int SlotCount(ItemStack[] stacks, int index)
    { return stacks == null || index < 0 || index >= stacks.Length ? -1 : Count(stacks[index]); }
    static ItemStack EmptyStack()
    {
        return (ItemStack)Activator.CreateInstance(typeof(ItemStack), true);
    }
    static int Count(ItemStack stack) {return stack==null||stack.IsEmpty()?0:stack.count;}
    static ItemStack[] Slots(object ctl)
    {
        MethodInfo m=ctl.GetType().GetMethod("GetSlots",F,null,Type.EmptyTypes,null);
        return m==null?null:m.Invoke(ctl,null) as ItemStack[];
    }
    static ItemStack CloneCount(ItemStack stack,int count)
    {
        if(stack==null||stack.IsEmpty()) return EmptyStack();
        MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        ItemStack clone=(ItemStack)copy.Invoke(stack,null);
        FieldInfo f=typeof(ItemStack).GetField("count",F);
        if(f!=null) f.SetValue(clone,count);
        else
        {
            PropertyInfo p=typeof(ItemStack).GetProperty("count",F);
            if(p==null||!p.CanWrite) throw new MissingMemberException("ItemStack.count");
            p.SetValue(clone,count,null);
        }
        return clone;
    }
    static void Apply(object ctl,string side,int slot,ItemStack value)
    {
        if (side == "Backpack")
        {
            ItemStack[] before = Slots(ctl);
            if (before == null || slot < 0 || slot >= before.Length)
                throw new InvalidOperationException("Backpack slots unavailable");
            ItemStack[] changed = (ItemStack[])before.Clone();
            changed[slot] = value;
            MethodInfo update = ctl.GetType().GetMethod("UpdateBackend", F, null,
                new Type[]{typeof(ItemStack[])}, null);
            if (update == null)
                throw new MissingMethodException(ctl.GetType().Name,"UpdateBackend(ItemStack[])");
            update.Invoke(ctl,new object[]{changed});
            ItemStack[] readback = Slots(ctl);
            return;
        }
        MethodInfo handler=ctl.GetType().GetMethod("HandleLootSlotChangedEvent",F,null,
            new Type[]{typeof(int),typeof(ItemStack)},null);
        if(handler==null) throw new MissingMethodException(ctl.GetType().Name,"HandleLootSlotChangedEvent");
        handler.Invoke(ctl,new object[]{slot,value});
    }
}

public enum CatuiQuantityBackendStatus { Blocked, NotEnoughSpace, Completed, Partial }
public struct CatuiQuantityBackendResult
{
    public CatuiQuantityBackendStatus Status;
    public int Requested;
    public int Moved;
    public string Message;
}

public static class CatuiQuantityBackend
{
    private static readonly object gate = new object();
    private static bool active;
    private static readonly MethodInfo itemMoveSound = typeof(XUiC_ItemStack).GetMethod(
        "PlayPlaceSound", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
        null, new Type[] { typeof(ItemStack) }, null);

    private static void PlayTransferSound(XUiC_ItemStack source, ItemStack originalStack)
    {
        if (itemMoveSound == null || source == null || originalStack == null || originalStack.IsEmpty())
            return;
        try
        {
            itemMoveSound.Invoke(source, new object[] { originalStack });
        }
        catch (Exception ex)
        {
            Log.Warning("[CATUI][QuantitySound] Vanilla item move sound failed: " + ex.GetBaseException().Message);
        }
    }

    public static CatuiQuantityBackendResult TryTransfer(XUiC_ItemStack source, string origin, int requested)
    {
        var result = new CatuiQuantityBackendResult {
            Status = CatuiQuantityBackendStatus.Blocked,
            Requested = requested,
            Message = "Transfer unavailable"
        };
        lock (gate) {
            if (active) { result.Message = "Another transfer is in progress"; return result; }
            active = true;
        }
        try {
            if (source == null || source.xui == null || source.itemStack == null ||
                source.itemStack.IsEmpty() || requested < 1 || requested > source.itemStack.count ||
                (origin != "Backpack" && origin != "Loot")) {
                result.Message = "Invalid item or amount";
                return result;
            }
            var loot = source.xui.GetChildByType<XUiC_LootWindow>();
            if (loot == null || !loot.IsOpen) {
                result.Message = "Storage is not open";
                return result;
            }
            if (source.xui.playerUI == null || source.xui.playerUI.entityPlayer == null) {
                result.Message = "Local player inventory unavailable";
                return result;
            }
            var plan = CatuiQuantityTransferPlanner.Build(source, origin, requested, loot);
            if (!plan.Valid || !plan.PrecommitReady || plan.Movable <= 0) {
                result.Status = CatuiQuantityBackendStatus.NotEnoughSpace;
                result.Message = plan.Reason;
                return result;
            }
            ItemStack soundStack = source.itemStack;
            string reason;
            if (!CatuiQuantityNativeCommit.Try(source, origin, plan.Movable, plan, out reason)) {
                result.Message = reason;
                return result;
            }
            PlayTransferSound(source, soundStack);
            result.Status = plan.Leftover > 0 ?
                CatuiQuantityBackendStatus.Partial : CatuiQuantityBackendStatus.Completed;
            result.Moved = plan.Movable;
            result.Message = plan.Leftover > 0 ?
                "Transferred " + plan.Movable + " of " + requested + " items" :
                "Transfer submitted through native inventory controllers";
            return result;
        }
        catch (Exception ex) {
            result.Message = "Transfer failed: " + ex.Message;
            Log.Warning("[CATUI][Quantity] " + ex);
            return result;
        }
        finally { lock (gate) active = false; }
    }
}
