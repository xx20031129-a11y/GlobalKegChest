using StardewValley;

namespace GlobalKegChest.Chest;

internal static class RecipeUnlocks
{
    public static void Update(Farmer farmer)
    {
        if (farmer.GetUnmodifiedSkillLevel(0) >= 8 && !farmer.craftingRecipes.ContainsKey(ChestIdentity.ItemId))
            farmer.craftingRecipes.Add(ChestIdentity.ItemId, 0);
    }
}
