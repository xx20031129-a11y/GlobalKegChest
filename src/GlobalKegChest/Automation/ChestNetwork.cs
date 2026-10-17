using GlobalKegChest.Chest;
using GlobalKegChest.Core;
using StardewValley;
using StardewValley.Inventories;
using GameChest = StardewValley.Objects.Chest;

namespace GlobalKegChest.Automation;

/// <summary>A short-lived view of chests whose native mutexes the controller owns.</summary>
internal sealed class ChestNetwork
{
    private sealed record Entry(string Key, PlacedObject Placed, IInventory Inventory);
    private readonly Entry[] entries;
    private readonly Dictionary<string, Entry> byKey;

    public ChestNetwork(IEnumerable<PlacedObject> chests, Farmer actor)
    {
        entries = chests.Select((placed, i) => new Entry(i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            placed, ((GameChest)placed.Object).GetItemsForPlayer(actor.UniqueMultiplayerID))).ToArray();
        byKey = entries.ToDictionary(p => p.Key, StringComparer.Ordinal);
    }

    public bool IsUsable => entries.Length > 0 && entries.All(p => p.Placed.IsPresent
        && ((GameChest)p.Placed.Object).GetMutex().IsLockHeld());

    public IReadOnlyList<Item> InputCandidates()
    {
        var representatives = new List<Item>();
        foreach (var entry in entries)
        {
            foreach (var item in entry.Inventory)
            {
                if (item is null || item.Stack <= 0 || representatives.Any(p => Compatible(p, item)))
                    continue;
                representatives.Add(item);
            }
        }
        return representatives;
    }

    public InputPlan? PlanInput(Item prototype, int count) =>
        InventoryPlanner.PlanInput("match", count, Snapshot(prototype));

    public OutputPlan? PlanOutput(Item output) => output.Stack > 0
        ? InventoryPlanner.PlanOutput("match", output.Stack, output.maximumStackSize(), Snapshot(output))
        : null;

    private IReadOnlyList<SlotSnapshot> Snapshot(Item prototype)
    {
        var result = new List<SlotSnapshot>(entries.Length * ChestIdentity.Capacity);
        foreach (var entry in entries)
        {
            // Do not invent room when another mod has left more than 70 nonempty slots.
            int count = Math.Max(ChestIdentity.Capacity, entry.Inventory.Count);
            for (int i = 0; i < count; i++)
            {
                Item? item = i < entry.Inventory.Count ? entry.Inventory[i] : null;
                var key = new SlotKey(entry.Key, i);
                if (item is not null && item.Stack > 0)
                    result.Add(new(key, Compatible(prototype, item) ? "match" : $"other:{entry.Key}:{i}",
                        item.Stack, Math.Max(item.Stack, item.maximumStackSize())));
                else if (i < ChestIdentity.Capacity)
                    result.Add(new(key, null, 0, 0));
            }
        }
        return result;
    }

    internal static bool Compatible(Item a, Item b) => a.canStackWith(b) && b.canStackWith(a);

    public Item GetItem(SlotKey key) => GetOptionalItem(key)
        ?? throw new InvalidOperationException("A reserved input stack is no longer present.");

    public IInventory GetInventory(SlotKey key) => byKey[key.Container].Inventory;

    private Item? GetOptionalItem(SlotKey key)
    {
        var inventory = byKey[key.Container].Inventory;
        return key.Index < inventory.Count ? inventory[key.Index] : null;
    }

    private void SetItem(SlotKey key, Item? item)
    {
        var inventory = byKey[key.Container].Inventory;
        while (inventory.Count <= key.Index)
            inventory.Add(null!);
        inventory[key.Index] = item!;
    }

    public bool TryApply(InputPlan plan)
    {
        if (!IsUsable)
            return false;
        var target = GetItem(plan.Target);
        long available = target.Stack;
        foreach (var move in plan.Moves)
        {
            var source = GetItem(move.From);
            if (move.To != plan.Target || move.Count <= 0 || source.Stack < move.Count
                || !Compatible(target, source))
                return false;
            available += move.Count;
        }
        if (available < plan.RequiredCount || available > target.maximumStackSize())
            return false;
        foreach (var move in plan.Moves)
        {
            var source = GetItem(move.From);
            target.Stack += move.Count;
            source.Stack -= move.Count;
            if (source.Stack <= 0)
                SetItem(move.From, null);
        }
        return true;
    }

    public bool TryApply(OutputPlan plan, Item prototype)
    {
        if (!IsUsable)
            return false;
        long added = 0;
        foreach (var change in plan.Changes)
        {
            var old = GetOptionalItem(change.Slot);
            if ((old?.Stack ?? 0) != change.ExpectedCount || change.NewCount <= change.ExpectedCount
                || change.NewCount > prototype.maximumStackSize()
                || (old is not null && !Compatible(old, prototype)))
                return false;
            added += change.NewCount - change.ExpectedCount;
        }
        if (added != prototype.Stack)
            return false;
        foreach (var change in plan.Changes)
        {
            var old = GetOptionalItem(change.Slot);
            if (old is not null)
                old.Stack = change.NewCount;
            else
            {
                Item copy = prototype.getOne();
                copy.Stack = change.NewCount;
                SetItem(change.Slot, copy);
            }
        }
        return true;
    }

    public InventoryTransaction BeginTransaction() => new(entries.Select(p => p.Inventory));

    /// <summary>Restores native stack references/counts, including slots compacted by PlaceInMachine.</summary>
    internal sealed class InventoryTransaction : IDisposable
    {
        private readonly (IInventory Inventory, Item?[] Items, int[] Counts)[] snapshots;
        private bool committed;

        public InventoryTransaction(IEnumerable<IInventory> inventories)
        {
            snapshots = inventories.Distinct().Select(inventory =>
            {
                var items = inventory.ToArray();
                return (inventory, items, items.Select(p => p?.Stack ?? 0).ToArray());
            }).ToArray();
        }

        public void Commit() => committed = true;

        public void Dispose()
        {
            if (committed)
                return;
            foreach (var snapshot in snapshots)
            {
                snapshot.Inventory.Clear();
                for (int i = 0; i < snapshot.Items.Length; i++)
                {
                    var item = snapshot.Items[i];
                    if (item is not null)
                        item.Stack = snapshot.Counts[i];
                    snapshot.Inventory.Add(item!);
                }
            }
            committed = true;
        }
    }
}
