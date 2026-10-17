using GlobalKegChest.Chest;
using Microsoft.Xna.Framework;
using StardewValley;
using SObject = StardewValley.Object;
using GameChest = StardewValley.Objects.Chest;

namespace GlobalKegChest.Automation;

internal sealed record PlacedObject(GameLocation Location, Vector2 Tile, SObject Object)
{
    public bool IsPresent => Location.Objects.TryGetValue(Tile, out var current) && ReferenceEquals(current, Object)
        && Object.TileLocation == Tile;
    public string Label => $"{Location.NameOrUniqueName} ({Tile.X}, {Tile.Y})";
}

internal sealed class WorldIndex
{
    public IReadOnlyList<PlacedObject> Chests { get; private set; } = Array.Empty<PlacedObject>();
    public IReadOnlyList<PlacedObject> Kegs { get; private set; } = Array.Empty<PlacedObject>();

    public void Rebuild()
    {
        var chests = new List<PlacedObject>();
        var kegs = new List<PlacedObject>();
        var seen = new HashSet<GameLocation>(ReferenceEqualityComparer.Instance);
        Utility.ForEachLocation(location =>
        {
            if (!seen.Add(location))
                return true;
            foreach (var entry in location.Objects.Pairs)
            {
                if (entry.Value.QualifiedItemId == "(BC)12")
                    kegs.Add(new(location, entry.Key, entry.Value));
                else if (entry.Value is GameChest && entry.Value.QualifiedItemId == ChestIdentity.QualifiedItemId)
                    chests.Add(new(location, entry.Key, entry.Value));
            }
            return true;
        }, includeInteriors: true, includeGenerated: true);
        Chests = Sort(chests);
        Kegs = Sort(kegs);
    }

    private static PlacedObject[] Sort(IEnumerable<PlacedObject> objects) => objects
        .OrderBy(p => p.Location.NameOrUniqueName, StringComparer.Ordinal)
        .ThenBy(p => p.Tile.Y).ThenBy(p => p.Tile.X).ToArray();

    public void Clear()
    {
        Chests = Array.Empty<PlacedObject>();
        Kegs = Array.Empty<PlacedObject>();
    }
}
