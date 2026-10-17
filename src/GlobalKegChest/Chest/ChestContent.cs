using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.BigCraftables;
using StardewValley;

namespace GlobalKegChest.Chest;

internal static class ChestIdentity
{
    // 1.0.1 used this item ID in saves. Keep it stable when changing the public manifest ID.
    public const string ModId = "Local.GlobalKegChest";
    public const string ItemId = ModId + "_Chest";
    public const string QualifiedItemId = "(BC)" + ItemId;
    public const string SchemaKey = ModId + "/schema";
    public const string TextureName = ModId + "/Chest";
    public const int Capacity = 70;
}

internal sealed class ChestContent
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    public ChestContent(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public void OnLocaleChanged(object? sender, LocaleChangedEventArgs e)
    {
        // The data assets may have been loaded in English before SMAPI restores the saved language.
        // Invalidating them also refreshes Game1.bigCraftableData, ItemRegistry, and crafting recipes.
        helper.GameContent.InvalidateCache("Data/BigCraftables");
        helper.GameContent.InvalidateCache("Data/CraftingRecipes");

        if (Game1.bigCraftableData is not null
            && Game1.bigCraftableData.TryGetValue(ChestIdentity.ItemId, out var chest)
            && CraftingRecipe.craftingRecipes is not null
            && CraftingRecipe.craftingRecipes.TryGetValue(ChestIdentity.ItemId, out var recipe))
        {
            string expected = helper.Translation.Get("chest.name");
            if (chest.DisplayName != expected || !recipe.EndsWith("/" + expected, StringComparison.Ordinal))
                monitor.Log($"Localized chest data did not refresh for {e.NewLocale}.", LogLevel.Warn);
            else
                monitor.Log($"Localized chest data refreshed for {e.NewLocale}: {expected}.", LogLevel.Debug);
        }
    }

    public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(ChestIdentity.TextureName))
            e.LoadFromModFile<Texture2D>("assets/chest.png", AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
        {
            e.Edit(asset => asset.AsDictionary<string, BigCraftableData>().Data[ChestIdentity.ItemId] =
                CreateData(helper.Translation.Get("chest.name"), helper.Translation.Get("chest.description")));
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(asset => asset.AsDictionary<string, string>().Data[ChestIdentity.ItemId] =
                CreateRecipe(helper.Translation.Get("chest.name")));
        }
    }

    internal static BigCraftableData CreateData(string displayName, string description) => new()
    {
        Name = "Global Keg Chest",
        DisplayName = displayName,
        Description = description,
        Price = 0,
        Fragility = 0,
        CanBePlacedIndoors = true,
        CanBePlacedOutdoors = true,
        Texture = ChestIdentity.TextureName,
        SpriteIndex = 0
    };

    internal static string CreateRecipe(string displayName) =>
        $"388 150 334 3 335 1 725 1 787 1 337 1/Home/{ChestIdentity.ItemId} 1/true/s Farming 8/{displayName}";

    public void CheckAssets(IMonitor monitor)
    {
        var data = ItemRegistry.GetData(ChestIdentity.QualifiedItemId)
            ?? throw new InvalidOperationException("Global Keg Chest data wasn't registered.");
        var texture = data.GetTexture();
        if (texture.Width / 16 * (texture.Height / 32) < 6)
            throw new InvalidOperationException("Global Keg Chest texture needs six 16x32 frames.");
        monitor.Log($"Chest assets initialized: {texture.Width}x{texture.Height}, 70 slots, recipe registered.", LogLevel.Debug);
    }
}
