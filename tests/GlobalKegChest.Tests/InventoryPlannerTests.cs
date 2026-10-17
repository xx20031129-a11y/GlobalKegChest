using GlobalKegChest.Core;

namespace GlobalKegChest.Tests;

public sealed class InventoryPlannerTests
{
    [Fact]
    public void FullChestWithoutCompatibleSpaceProducesNoOutputPlan()
    {
        var slots = Enumerable.Range(0, 70)
            .Select(i => Stack("A", i, "wood", 999))
            .ToArray();

        Assert.Null(InventoryPlanner.PlanOutput("wine", 1, 999, slots));
        Assert.All(slots, slot => Assert.Equal(999, slot.Count));
    }

    [Fact]
    public void FullChestStillAcceptsOutputInCompatibleExistingStack()
    {
        var slots = Enumerable.Range(0, 70)
            .Select(i => Stack("A", i, i == 43 ? "wine" : "wood", i == 43 ? 990 : 999))
            .ToArray();

        var plan = InventoryPlanner.PlanOutput("wine", 9, 999, slots);

        Assert.NotNull(plan);
        Assert.Equal(new SlotDelta(new SlotKey("A", 43), "wine", 990, 999), Assert.Single(plan.Changes));
        Assert.Equal(990, slots[43].Count);
    }

    [Fact]
    public void OutputMergesBeforeUsingEmptySlotsWithinEachChest()
    {
        var slots = new[] { Empty("B", 0), Stack("B", 1, "wine", 8, 10), Empty("A", 0), Stack("A", 1, "wine", 9, 10) };

        var plan = InventoryPlanner.PlanOutput("wine", 14, 10, slots);

        Assert.NotNull(plan);
        Assert.Equal(new[]
        {
            new SlotDelta(new SlotKey("B", 1), "wine", 8, 10),
            new SlotDelta(new SlotKey("B", 0), "wine", 0, 10),
            new SlotDelta(new SlotKey("A", 1), "wine", 9, 10),
            new SlotDelta(new SlotKey("A", 0), "wine", 0, 1)
        }, plan.Changes);
    }

    [Fact]
    public void OutputUsesEarlierChestEmptySlotBeforeLaterChestCompatibleStack()
    {
        var slots = new[] { Empty("A", 0), Stack("B", 0, "wine", 2, 10) };

        var plan = InventoryPlanner.PlanOutput("wine", 3, 10, slots);

        Assert.NotNull(plan);
        Assert.Equal(new SlotDelta(new SlotKey("A", 0), "wine", 0, 3), Assert.Single(plan.Changes));
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void PartialCapacityAcrossMultipleChestsMustAcceptEntireOutput(int count, bool expectedSuccess)
    {
        var slots = new[] { Stack("A", 0, "wine", 7, 10), Stack("B", 0, "wine", 4, 10) };
        var original = slots.ToArray();

        var plan = InventoryPlanner.PlanOutput("wine", count, 10, slots);

        Assert.Equal(expectedSuccess, plan is not null);
        Assert.Equal(original, slots);
        if (plan is not null)
            Assert.Equal(count, plan.Changes.Sum(change => change.NewCount - change.ExpectedCount));
    }

    [Fact]
    public void OutputHonorsExistingStackLimitAndOutputStackLimit()
    {
        var slots = new[] { Stack("A", 0, "wine", 2, 4), Empty("A", 1) };

        var plan = InventoryPlanner.PlanOutput("wine", 7, 6, slots);

        Assert.NotNull(plan);
        Assert.Equal(new[]
        {
            new SlotDelta(new SlotKey("A", 0), "wine", 2, 4),
            new SlotDelta(new SlotKey("A", 1), "wine", 0, 5)
        }, plan.Changes);
    }

    [Fact]
    public void OutputNeverOverflowsWhenCountsAreIntMaxValue()
    {
        var slots = new[] { Stack("A", 0, "wine", int.MaxValue - 1, int.MaxValue), Empty("B", 0) };

        var plan = InventoryPlanner.PlanOutput("wine", int.MaxValue, int.MaxValue, slots);

        Assert.NotNull(plan);
        Assert.Equal(new[]
        {
            new SlotDelta(new SlotKey("A", 0), "wine", int.MaxValue - 1, int.MaxValue),
            new SlotDelta(new SlotKey("B", 0), "wine", 0, int.MaxValue - 1)
        }, plan.Changes);
    }

    [Fact]
    public void OutputKeepsMetadataAndQualityCompatibilityTokensSeparate()
    {
        var slots = new[] { Stack("A", 0, "wine.apple.gold", 1), Stack("A", 1, "Wine.apple.normal", 1) };

        Assert.Null(InventoryPlanner.PlanOutput("wine.apple.normal", 1, 999, slots));
    }

    [Fact]
    public void FiveCompatibleBeansAcrossChestsCanBeConsolidated()
    {
        var slots = new[] { Stack("A", 0, "bean.normal", 3), Stack("B", 0, "bean.normal", 2) };

        var plan = InventoryPlanner.PlanInput("bean.normal", 5, slots);

        Assert.NotNull(plan);
        Assert.Equal(new SlotKey("A", 0), plan.Target);
        Assert.Equal(5, plan.RequiredCount);
        Assert.Equal(new StackMove(new SlotKey("B", 0), new SlotKey("A", 0), 2), Assert.Single(plan.Moves));
        Assert.Equal(5, slots.Sum(slot => slot.Count));
    }

    [Fact]
    public void FourBeansProduceNoInputPlan()
    {
        var slots = new[] { Stack("A", 0, "bean.normal", 3), Stack("B", 0, "bean.normal", 1) };

        Assert.Null(InventoryPlanner.PlanInput("bean.normal", 5, slots));
    }

    [Theory]
    [InlineData("bean.gold")]
    [InlineData("bean.normal.other-mod-data")]
    [InlineData("Bean.normal")]
    public void InputNeverCombinesDifferentCompatibilityTokens(string otherKind)
    {
        var slots = new[] { Stack("A", 0, "bean.normal", 3), Stack("B", 0, otherKind, 2) };

        Assert.Null(InventoryPlanner.PlanInput("bean.normal", 5, slots));
    }

    [Fact]
    public void InputTakesOnlyNeededItemsFromMultipleDonorsInOrder()
    {
        var slots = new[]
        {
            Empty("A", 0), Stack("A", 1, "bean", 1), Stack("A", 2, "wood", 99),
            Stack("B", 0, "bean", 2), Stack("B", 1, "bean", 9), Stack("C", 0, "bean", 7)
        };

        var plan = InventoryPlanner.PlanInput("bean", 5, slots);

        Assert.NotNull(plan);
        Assert.Equal(new SlotKey("A", 1), plan.Target);
        Assert.Equal(new[]
        {
            new StackMove(new SlotKey("B", 0), new SlotKey("A", 1), 2),
            new StackMove(new SlotKey("B", 1), new SlotKey("A", 1), 2)
        }, plan.Moves);
        AssertConservedInput(slots, plan);
    }

    [Fact]
    public void InputAlreadySufficientTargetRequiresNoMovesOrConsumption()
    {
        var slots = new[] { Stack("B", 0, "bean", 12), Stack("A", 0, "bean", 3) };

        var plan = InventoryPlanner.PlanInput("bean", 5, slots);

        Assert.NotNull(plan);
        Assert.Equal(new SlotKey("B", 0), plan.Target);
        Assert.Equal(5, plan.RequiredCount);
        Assert.Empty(plan.Moves);
        Assert.Equal(12, slots[0].Count);
    }

    [Fact]
    public void InputCannotSkipFirstTargetWhoseMaximumStackIsTooSmall()
    {
        var slots = new[] { Stack("A", 0, "bean", 3, 4), Stack("B", 0, "bean", 5) };

        Assert.Null(InventoryPlanner.PlanInput("bean", 5, slots));
    }

    [Fact]
    public void InputCanUseLargeDonorsWithoutOverflowingTotal()
    {
        var slots = new[]
        {
            Stack("A", 0, "bean", int.MaxValue - 2, int.MaxValue),
            Stack("B", 0, "bean", int.MaxValue, int.MaxValue),
            Stack("C", 0, "bean", int.MaxValue, int.MaxValue)
        };

        var plan = InventoryPlanner.PlanInput("bean", int.MaxValue, slots);

        Assert.NotNull(plan);
        Assert.Equal(new StackMove(new SlotKey("B", 0), new SlotKey("A", 0), 2), Assert.Single(plan.Moves));
        AssertConservedInput(slots, plan);
    }

    [Theory]
    [InlineData(1, 1, 1, 1, 1)]
    [InlineData(1, 1, 1, 1, 4)]
    [InlineData(2, 3, 7, 0, 9)]
    [InlineData(4, 1, 1, 9, 15)]
    [InlineData(10, 0, 0, 0, 10)]
    public void InputMovesConserveQuantityAtEveryStep(int first, int second, int third, int fourth, int required)
    {
        var counts = new[] { first, second, third, fourth };
        var slots = counts.Select((count, i) => count == 0 ? Empty("A", i) : Stack("A", i, "bean", count, 20)).ToArray();
        var original = slots.ToArray();

        var plan = InventoryPlanner.PlanInput("bean", required, slots);

        Assert.NotNull(plan);
        AssertConservedInput(slots, plan);
        Assert.Equal(original, slots);
    }

    [Fact]
    public void EmptySnapshotProducesNoPlan()
    {
        Assert.Null(InventoryPlanner.PlanOutput("wine", 1, 999, Array.Empty<SlotSnapshot>()));
        Assert.Null(InventoryPlanner.PlanInput("bean", 5, Array.Empty<SlotSnapshot>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidKindsAreRejected(string? kind)
    {
        Assert.ThrowsAny<ArgumentException>(() => InventoryPlanner.PlanOutput(kind!, 1, 999, Array.Empty<SlotSnapshot>()));
        Assert.ThrowsAny<ArgumentException>(() => InventoryPlanner.PlanInput(kind!, 1, Array.Empty<SlotSnapshot>()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonpositiveRequestedAmountsAreRejected(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryPlanner.PlanOutput("wine", count, 999, Array.Empty<SlotSnapshot>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryPlanner.PlanInput("bean", count, Array.Empty<SlotSnapshot>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryPlanner.PlanOutput("wine", 1, count, Array.Empty<SlotSnapshot>()));
    }

    [Fact]
    public void NullSnapshotsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryPlanner.PlanOutput("wine", 1, 999, null!));
        Assert.Throws<ArgumentNullException>(() => InventoryPlanner.PlanInput("bean", 1, null!));
    }

    public static IEnumerable<object[]> MalformedSnapshots()
    {
        yield return new object[] { new SlotSnapshot[] { null! } };
        yield return new object[] { new[] { Stack("", 0, "bean", 1) } };
        yield return new object[] { new[] { Stack("A", -1, "bean", 1) } };
        yield return new object[] { new[] { Stack("A", 0, "bean", -1) } };
        yield return new object[] { new[] { Stack("A", 0, "bean", 0) } };
        yield return new object[] { new[] { Stack("A", 0, "", 1) } };
        yield return new object[] { new[] { Stack("A", 0, "bean", 5, 4) } };
        yield return new object[] { new[] { Stack("A", 0, "bean", 1, 0) } };
        yield return new object[] { new[] { new SlotSnapshot(new SlotKey("A", 0), null, 1, 999) } };
        yield return new object[] { new[] { new SlotSnapshot(new SlotKey("A", 0), null, 0, -1) } };
        yield return new object[] { new[] { Empty("A", 0), Empty("A", 0) } };
    }

    [Theory]
    [MemberData(nameof(MalformedSnapshots))]
    public void MalformedSnapshotsAreRejectedBeforeAnyPlanIsReturned(SlotSnapshot[] slots)
    {
        Assert.ThrowsAny<ArgumentException>(() => InventoryPlanner.PlanOutput("wine", 1, 999, slots));
        Assert.ThrowsAny<ArgumentException>(() => InventoryPlanner.PlanInput("bean", 1, slots));
    }

    private static SlotSnapshot Empty(string chest, int index) => new(new SlotKey(chest, index), null, 0, 0);

    private static SlotSnapshot Stack(string chest, int index, string kind, int count, int max = 999) =>
        new(new SlotKey(chest, index), kind, count, max);

    private static void AssertConservedInput(IReadOnlyList<SlotSnapshot> slots, InputPlan plan)
    {
        var counts = slots.ToDictionary(slot => slot.Slot, slot => slot.Count);
        var snapshots = slots.ToDictionary(slot => slot.Slot);
        long total = counts.Values.Sum(count => (long)count);

        foreach (var move in plan.Moves)
        {
            Assert.True(move.Count > 0);
            Assert.NotEqual(move.From, move.To);
            Assert.Equal(snapshots[move.From].Kind, snapshots[move.To].Kind);
            Assert.InRange(move.Count, 1, counts[move.From]);
            counts[move.From] -= move.Count;
            counts[move.To] += move.Count;
            Assert.InRange(counts[move.To], 1, snapshots[move.To].MaxStack);
            Assert.Equal(total, counts.Values.Sum(count => (long)count));
        }

        Assert.True(counts[plan.Target] >= plan.RequiredCount);
        Assert.Equal(total, counts.Values.Sum(count => (long)count));
    }
}
