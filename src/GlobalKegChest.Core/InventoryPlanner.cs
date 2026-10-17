namespace GlobalKegChest.Core;

public static class InventoryPlanner
{
    public static OutputPlan? PlanOutput(string kind, int count, int maxStack, IReadOnlyList<SlotSnapshot> slots)
    {
        ValidateKind(kind);
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (maxStack <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxStack));
        ValidateSlots(slots);

        var changes = new List<SlotDelta>();
        int remaining = count;

        // GroupBy preserves the supplied container order and the slot order within each container.
        foreach (var chest in slots.GroupBy(slot => slot.Slot.Container, StringComparer.Ordinal))
        {
            foreach (var slot in chest)
            {
                if (!StringComparer.Ordinal.Equals(slot.Kind, kind))
                    continue;

                int space = Math.Min(slot.MaxStack, maxStack) - slot.Count;
                if (space <= 0)
                    continue;

                int moved = Math.Min(space, remaining);
                changes.Add(new SlotDelta(slot.Slot, kind, slot.Count, slot.Count + moved));
                remaining -= moved;
                if (remaining == 0)
                    return new OutputPlan(changes.AsReadOnly());
            }

            foreach (var slot in chest)
            {
                if (slot.Kind is not null)
                    continue;

                int moved = Math.Min(maxStack, remaining);
                changes.Add(new SlotDelta(slot.Slot, kind, 0, moved));
                remaining -= moved;
                if (remaining == 0)
                    return new OutputPlan(changes.AsReadOnly());
            }
        }

        return null;
    }

    public static InputPlan? PlanInput(string kind, int requiredCount, IReadOnlyList<SlotSnapshot> slots)
    {
        ValidateKind(kind);
        if (requiredCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredCount));
        ValidateSlots(slots);

        SlotSnapshot? target = null;
        var moves = new List<StackMove>();
        int remaining = requiredCount;

        foreach (var slot in slots)
        {
            if (!StringComparer.Ordinal.Equals(slot.Kind, kind))
                continue;

            if (target is null)
            {
                target = slot;
                if (target.MaxStack < requiredCount)
                    return null;
                remaining -= target.Count;
            }
            else
            {
                int moved = Math.Min(slot.Count, remaining);
                moves.Add(new StackMove(slot.Slot, target.Slot, moved));
                remaining -= moved;
            }

            if (remaining <= 0)
                return new InputPlan(target.Slot, requiredCount, moves.AsReadOnly());
        }

        return null;
    }

    private static void ValidateKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("A compatibility token is required.", nameof(kind));
    }

    private static void ValidateSlots(IReadOnlyList<SlotSnapshot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var keys = new HashSet<SlotKey>();
        foreach (var slot in slots)
        {
            if (slot is null || string.IsNullOrWhiteSpace(slot.Slot.Container) || slot.Slot.Index < 0)
                throw new ArgumentException("Each slot must have a container and a nonnegative index.", nameof(slots));
            if (!keys.Add(slot.Slot))
                throw new ArgumentException("Slot keys must be unique.", nameof(slots));

            bool validEmpty = slot.Kind is null && slot.Count == 0 && slot.MaxStack >= 0;
            bool validStack = !string.IsNullOrWhiteSpace(slot.Kind) && slot.Count > 0
                && slot.MaxStack > 0 && slot.Count <= slot.MaxStack;
            if (!validEmpty && !validStack)
                throw new ArgumentException("Slot quantities must describe an empty slot or a valid item stack.", nameof(slots));
        }
    }
}
