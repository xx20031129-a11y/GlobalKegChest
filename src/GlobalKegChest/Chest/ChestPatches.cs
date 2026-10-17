using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;
using GameChest = StardewValley.Objects.Chest;

namespace GlobalKegChest.Chest;

internal static class ChestPatches
{
    private static IMonitor monitor = null!;

    public static void Apply(Harmony harmony, IMonitor log)
    {
        monitor = log;
        harmony.Patch(AccessTools.Method(typeof(SObject), nameof(SObject.placementAction),
                new[] { typeof(GameLocation), typeof(int), typeof(int), typeof(Farmer) }),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(AfterPlacement)));
        harmony.Patch(AccessTools.Method(typeof(GameChest), nameof(GameChest.GetActualCapacity)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(AfterCapacity)));
        harmony.Patch(AccessTools.Method(typeof(ItemGrabMenu), nameof(ItemGrabMenu.CanHaveColorPicker)),
            postfix: new HarmonyMethod(typeof(ChestPatches), nameof(AfterColorPicker)));
    }

    internal static void AfterCapacity(GameChest __instance, ref int __result)
    {
        if (__instance.QualifiedItemId == ChestIdentity.QualifiedItemId)
            __result = ChestIdentity.Capacity;
    }

    internal static void AfterColorPicker(ItemGrabMenu __instance, ref bool __result)
    {
        if (__instance.sourceItem?.QualifiedItemId == ChestIdentity.QualifiedItemId)
            __result = false;
    }

    internal static void AfterPlacement(SObject __instance, GameLocation location, int x, int y, bool __result)
    {
        if (!__result || __instance.QualifiedItemId != ChestIdentity.QualifiedItemId)
            return;
        var tile = new Vector2(x / Game1.tileSize, y / Game1.tileSize);
        if (!location.Objects.TryGetValue(tile, out var placed)
            || placed.QualifiedItemId != ChestIdentity.QualifiedItemId || placed is GameChest)
            return;
        try
        {
            var chest = CreateChest(tile);
            chest.owner.Value = placed.owner.Value;
            foreach (var pair in placed.modData.Pairs)
                chest.modData[pair.Key] = pair.Value;
            chest.modData[ChestIdentity.SchemaKey] = "1";
            location.Objects[tile] = chest;
        }
        catch (Exception ex)
        {
            monitor.Log($"Couldn't initialize the placed keg chest; its item is still present. {ex}", LogLevel.Error);
        }
    }

    internal static GameChest CreateChest(Vector2 tile)
    {
        var chest = new GameChest(true, tile, ChestIdentity.ItemId);
        chest.modData[ChestIdentity.SchemaKey] = "1";
        chest.resetLidFrame();
        return chest;
    }
}
