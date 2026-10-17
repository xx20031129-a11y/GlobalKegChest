namespace GlobalKegChest.Core;

public sealed class CycleCursor<T>
{
    private T[]? snapshot;
    private int next;

    public CycleCursor(IReadOnlyList<T> ordered)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        this.snapshot = ordered.ToArray();
    }

    public bool TryTake(int maximum, out IReadOnlyList<T> batch)
    {
        if (maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        if (this.snapshot is null)
        {
            batch = Array.Empty<T>();
            return false;
        }

        int count = Math.Min(maximum, this.snapshot.Length - this.next);
        if (count == 0)
        {
            this.Cancel();
            batch = Array.Empty<T>();
            return false;
        }

        var items = new T[count];
        Array.Copy(this.snapshot, this.next, items, 0, count);
        this.next += count;
        batch = Array.AsReadOnly(items);

        if (this.next == this.snapshot.Length)
            this.Cancel();
        return true;
    }

    public void Cancel() => this.snapshot = null;
}
