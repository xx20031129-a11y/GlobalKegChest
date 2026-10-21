using GlobalKegChest.Automation;
using GlobalKegChest.Chest;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace GlobalKegChest;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var config = helper.ReadConfig<ModConfig>();
        if (config.Normalize())
        {
            Monitor.Log("Invalid processing interval; using 10 in-game minutes.", LogLevel.Warn);
            helper.WriteConfig(config);
        }
        var content = new ChestContent(helper, Monitor);
        var controller = new AutomationController(Monitor, config);
        var commands = new AutomationCommands(helper, Monitor, controller, config);
        ChestPatches.Apply(new Harmony(ModManifest.UniqueID), Monitor);
        helper.Events.Multiplayer.ModMessageReceived += (_, e) =>
        {
            if (e.FromModID != ModManifest.UniqueID || e.Type != AutomationCommands.RescanMessageType)
                return;
            if (Context.IsMainPlayer && Context.ScreenId == 0)
                controller.RequestRescan();
        };
        helper.Events.Content.AssetRequested += content.OnAssetRequested;
        helper.Events.Content.LocaleChanged += content.OnLocaleChanged;
        helper.Events.GameLoop.GameLaunched += (_, _) => content.CheckAssets(Monitor);
        helper.Events.GameLoop.SaveLoaded += (_, _) =>
        {
            controller.Reset();
            RecipeUnlocks.Update(Game1.player);
        };
        helper.Events.GameLoop.DayStarted += (_, _) =>
        {
            RecipeUnlocks.Update(Game1.player);
            if (Context.IsMainPlayer && Context.ScreenId == 0)
                controller.OnDayStarted();
        };
        helper.Events.Player.LevelChanged += (_, e) =>
        {
            if (e.IsLocalPlayer)
                RecipeUnlocks.Update(Game1.player);
        };
        helper.Events.GameLoop.TimeChanged += (_, e) =>
        {
            if (Context.IsMainPlayer && Context.ScreenId == 0)
                controller.OnTimeChanged(e.NewTime);
        };
        helper.Events.GameLoop.UpdateTicked += (_, _) =>
        {
            if (Context.ScreenId == 0)
                controller.Update();
        };
        helper.Events.GameLoop.Saving += (_, _) =>
        {
            if (Context.ScreenId == 0)
                controller.OnSaving();
        };
        helper.Events.GameLoop.Saved += (_, _) =>
        {
            if (Context.ScreenId == 0)
                controller.OnSaved();
        };
        helper.Events.GameLoop.ReturnedToTitle += (_, _) =>
        {
            if (Context.ScreenId == 0)
                controller.Reset();
        };
        helper.ConsoleCommands.Add("gkc", "Manage keg automation: status, rescan, give.", commands.Run);
        Monitor.Log("Global Keg Chest loaded: 70 slots; all placed kegs across locations.", LogLevel.Info);
    }
}
