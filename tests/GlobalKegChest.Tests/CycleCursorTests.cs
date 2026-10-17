using GlobalKegChest.Core;

namespace GlobalKegChest.Tests;

public sealed class CycleCursorTests
{
    [Fact]
    public void EachKegAppearsOnceAcrossBatches()
    {
        var ordered = Enumerable.Range(0, 205).ToArray();
        var cursor = new CycleCursor<int>(ordered);
        var observed = new List<int>();
        var sizes = new List<int>();

        while (cursor.TryTake(100, out var batch))
        {
            sizes.Add(batch.Count);
            observed.AddRange(batch);
        }

        Assert.Equal(new[] { 100, 100, 5 }, sizes);
        Assert.Equal(ordered, observed);
        Assert.False(cursor.TryTake(100, out var exhausted));
        Assert.Empty(exhausted);
    }

    [Fact]
    public void CancelDiscardsRemainingObjectsAndIsIdempotent()
    {
        var cursor = new CycleCursor<int>(new[] { 1, 2, 3 });
        Assert.True(cursor.TryTake(1, out var first));

        cursor.Cancel();
        cursor.Cancel();

        Assert.False(cursor.TryTake(2, out var remaining));
        Assert.Empty(remaining);
        Assert.Equal(new[] { 1 }, first);
    }

    [Fact]
    public void CancelBeforeFirstBatchReturnsNothing()
    {
        var cursor = new CycleCursor<int>(new[] { 1, 2, 3 });
        cursor.Cancel();

        Assert.False(cursor.TryTake(100, out var batch));
        Assert.Empty(batch);
    }

    [Fact]
    public void CursorUsesItsOriginalOrderedSnapshot()
    {
        var ordered = new List<int> { 8, 3, 9 };
        var cursor = new CycleCursor<int>(ordered);
        ordered[0] = 88;
        ordered.Add(100);

        Assert.True(cursor.TryTake(int.MaxValue, out var batch));

        Assert.Equal(new[] { 8, 3, 9 }, batch);
        Assert.False(cursor.TryTake(1, out _));
    }

    [Fact]
    public void ReturnedBatchCannotChangeFutureBatches()
    {
        var cursor = new CycleCursor<int>(new[] { 1, 2, 3 });
        Assert.True(cursor.TryTake(2, out var first));
        if (first is IList<int> mutable)
            Assert.Throws<NotSupportedException>(() => mutable[0] = 100);

        Assert.True(cursor.TryTake(2, out var second));
        Assert.Equal(new[] { 3 }, second);
        Assert.Equal(new[] { 1, 2 }, first);
    }

    [Fact]
    public void EmptyCursorIsImmediatelyExhausted()
    {
        var cursor = new CycleCursor<int>(Array.Empty<int>());

        Assert.False(cursor.TryTake(10, out var batch));
        Assert.Empty(batch);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonpositiveBatchLimitIsRejectedWithoutAdvancing(int maximum)
    {
        var cursor = new CycleCursor<int>(new[] { 1, 2 });

        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.TryTake(maximum, out _));
        Assert.True(cursor.TryTake(2, out var batch));
        Assert.Equal(new[] { 1, 2 }, batch);
    }

    [Fact]
    public void NullOrderedSnapshotIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CycleCursor<int>(null!));
    }
}
