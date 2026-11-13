using System;
using System.Collections.Generic;




namespace CatuiQuantity.ServerFoundation
{
    public enum TransferDirection : byte { BackpackToLoot = 1, LootToBackpack = 2 }
    public enum TransferStatus : byte
    {
        Completed = 0, Invalid = 1, NotAuthorized = 2, Stale = 3,
        NoRoom = 4, Busy = 5, InternalError = 6
    }
    public sealed class TransferRequest
    {

        public ulong RequestId;
        public TransferDirection Direction;
        public int SourceSlot;
        public int Quantity;

        public long ContainerSessionId;
        public int ContainerVersion;
        public int BackpackVersion;
    }
    public sealed class TransferResponse
    {
        public ulong RequestId;
        public TransferStatus Status;
        public int Moved;
        public string Detail;
    }
    public sealed class SnapshotStack
    {
        public string Identity;
        public int Count;
        public int MaxCount;
        public bool Locked;
        public SnapshotStack Clone() { return (SnapshotStack)MemberwiseClone(); }
    }
    public sealed class SnapshotInventory
    {
        public string StorageId;
        public int Version;
        public List<SnapshotStack> Slots = new List<SnapshotStack>();
        public SnapshotInventory Clone()
        {
            var next = new SnapshotInventory { StorageId = StorageId, Version = Version };
            foreach (var s in Slots) next.Slots.Add(s == null ? null : s.Clone());
            return next;
        }
    }
    public sealed class ServerRequestContext
    {

        public string PlayerId;
        public object Connection;
    }
    public interface IAuthoritativeStorage
    {


        bool TryResolveOpenContainer(ServerRequestContext sender, long sessionId,
                                     out string containerId);

        bool TryRead(ServerRequestContext sender, string containerId,
                     out SnapshotInventory backpack, out SnapshotInventory loot);



        bool TryCommitBoth(ServerRequestContext sender, string containerId,
                           SnapshotInventory beforeBackpack, SnapshotInventory beforeLoot,
                           SnapshotInventory afterBackpack, SnapshotInventory afterLoot);
    }
    public sealed class TransferEngine
    {
        readonly IAuthoritativeStorage storage;
        readonly object mutex = new object();



        readonly Dictionary<string, Dictionary<ulong, TransferResponse>> replay =
            new Dictionary<string, Dictionary<ulong, TransferResponse>>();
        public TransferEngine(IAuthoritativeStorage storage)
        {
            if (storage == null) throw new ArgumentNullException("storage");
            this.storage = storage;
        }
        public TransferResponse Process(ServerRequestContext sender, TransferRequest req)
        {
            if (sender == null || String.IsNullOrEmpty(sender.PlayerId) || req == null)
                return Fail(req, TransferStatus.NotAuthorized, "Missing authenticated sender");
            lock (mutex)
            {
                Dictionary<ulong, TransferResponse> seen;
                if (!replay.TryGetValue(sender.PlayerId, out seen))
                {
                    seen = new Dictionary<ulong, TransferResponse>();
                    replay.Add(sender.PlayerId, seen);
                }
                TransferResponse already;
                if (seen.TryGetValue(req.RequestId, out already)) return already;
                var result = ProcessOnce(sender, req);



                if (result.Status == TransferStatus.Completed) seen[req.RequestId] = result;
                return result;
            }
        }
        TransferResponse ProcessOnce(ServerRequestContext sender, TransferRequest r)
        {
            if (r.Quantity <= 0 || r.Quantity > 1000000 || r.SourceSlot < 0 ||
                (r.Direction != TransferDirection.BackpackToLoot &&
                 r.Direction != TransferDirection.LootToBackpack))
                return Fail(r, TransferStatus.Invalid, "Invalid request");
            try
            {
                string container;
                if (!storage.TryResolveOpenContainer(sender, r.ContainerSessionId, out container) ||
                    String.IsNullOrEmpty(container))
                    return Fail(r, TransferStatus.NotAuthorized, "Container is not open/accessible");
                SnapshotInventory backpack, loot;
                if (!storage.TryRead(sender, container, out backpack, out loot) ||
                    backpack == null || loot == null)
                    return Fail(r, TransferStatus.Stale, "Inventory snapshot unavailable");
                if (backpack.Version != r.BackpackVersion || loot.Version != r.ContainerVersion)
                    return Fail(r, TransferStatus.Stale, "Inventory changed; refresh UI");
                var source = r.Direction == TransferDirection.BackpackToLoot ? backpack : loot;
                var target = r.Direction == TransferDirection.BackpackToLoot ? loot : backpack;
                if (r.SourceSlot >= source.Slots.Count)
                    return Fail(r, TransferStatus.Invalid, "Source slot out of range");
                SnapshotStack item = source.Slots[r.SourceSlot];
                if (item == null || item.Locked || item.Count < r.Quantity ||
                    item.Count <= 0 || item.MaxCount <= 0 ||
                    String.IsNullOrEmpty(item.Identity))
                    return Fail(r, TransferStatus.Invalid, "Source not available");


                SnapshotInventory beforeB = backpack.Clone(), beforeL = loot.Clone();
                SnapshotInventory newB = backpack.Clone(), newL = loot.Clone();
                var from = r.Direction == TransferDirection.BackpackToLoot ? newB : newL;
                var into = r.Direction == TransferDirection.BackpackToLoot ? newL : newB;
                int remaining = r.Quantity;

                for (int i = 0; i < into.Slots.Count && remaining > 0; i++)
                {
                    var s = into.Slots[i];
                    if (s == null || s.Locked || s.Count <= 0 ||
                        s.Identity != item.Identity || s.Count >= s.MaxCount ||
                        s.MaxCount != item.MaxCount) continue;
                    int add = Math.Min(remaining, s.MaxCount - s.Count);
                    s.Count += add;
                    remaining -= add;
                }

                for (int i = 0; i < into.Slots.Count && remaining > 0; i++)
                {
                    var s = into.Slots[i];
                    if (s != null && (s.Count != 0 || s.Locked)) continue;
                    int add = Math.Min(remaining, item.MaxCount);
                    into.Slots[i] = new SnapshotStack {
                        Identity = item.Identity, Count = add,
                        MaxCount = item.MaxCount, Locked = false
                    };
                    remaining -= add;
                }
                if (remaining != 0)
                    return Fail(r, TransferStatus.NoRoom, "Destination lacks capacity");
                var sourceNew = from.Slots[r.SourceSlot];
                sourceNew.Count -= r.Quantity;
                if (sourceNew.Count == 0) from.Slots[r.SourceSlot] = null;
                if (!storage.TryCommitBoth(sender, container, beforeB, beforeL, newB, newL))
                    return Fail(r, TransferStatus.Stale, "Atomic commit rejected");
                return new TransferResponse {
                    RequestId = r.RequestId, Status = TransferStatus.Completed,
                    Moved = r.Quantity, Detail = "Committed by authoritative server"
                };
            }
            catch (Exception)
            {

                return Fail(r, TransferStatus.InternalError, "Server operation failed");
            }
        }
        static TransferResponse Fail(TransferRequest r, TransferStatus status, string text)
        {
            return new TransferResponse { RequestId = r == null ? 0 : r.RequestId,
                                          Status = status, Moved = 0, Detail = text };
        }
    }
}
