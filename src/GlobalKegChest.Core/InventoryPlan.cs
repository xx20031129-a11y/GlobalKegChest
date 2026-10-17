namespace GlobalKegChest.Core;

public readonly record struct SlotKey(string Container, int Index);
public sealed record SlotSnapshot(SlotKey Slot, string? Kind, int Count, int MaxStack);
public sealed record SlotDelta(SlotKey Slot, string Kind, int ExpectedCount, int NewCount);
public sealed record OutputPlan(IReadOnlyList<SlotDelta> Changes);
public sealed record StackMove(SlotKey From, SlotKey To, int Count);
public sealed record InputPlan(SlotKey Target, int RequiredCount, IReadOnlyList<StackMove> Moves);
