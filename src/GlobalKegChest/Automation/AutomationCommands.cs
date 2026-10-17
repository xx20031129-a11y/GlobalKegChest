using GlobalKegChest.Chest;
using StardewModdingAPI;
using StardewValley;

namespace GlobalKegChest.Automation;

internal sealed class AutomationCommands
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly AutomationController controller;
    private readonly ModConfig config;

    public AutomationCommands(IModHelper helper, IMonitor monitor, AutomationController controller, ModConfig config)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.controller = controller;
        this.config = config;
    }

    public void Run(string command, string[] args)
    {
        string action = args.FirstOrDefault()?.ToLowerInvariant() ?? "status";
        if (!Context.IsWorldReady)
        {
            monitor.Log(helper.Translation.Get("command.no-save"), LogLevel.Info);
            return;
        }
        if (action == "give")
        {
            Item item = ItemRegistry.Create(ChestIdentity.QualifiedItemId);
            bool added = Game1.player.addItemToInventoryBool(item, makeActiveObject: false);
            monitor.Log(helper.Translation.Get(added ? "command.given" : "command.full"), LogLevel.Info);
            return;
        }
        if (!Context.IsMainPlayer || Context.ScreenId != 0)
        {
            monitor.Log(helper.Translation.Get("command.host-only"), LogLevel.Warn);
            return;
        }
        if (action == "rescan")
        {
            controller.RequestRescan();
            monitor.Log(helper.Translation.Get("command.rescan"), LogLevel.Info);
            return;
        }
        if (action != "status")
        {
            monitor.Log("Usage: gkc status | gkc rescan | gkc give", LogLevel.Info);
            return;
        }
        controller.Index.Rebuild();
        var kegs = controller.Index.Kegs;
        monitor.Log(helper.Translation.Get("command.status", new
        {
            chests = controller.Index.Chests.Count,
            kegs = kegs.Count,
            empty = kegs.Count(p => p.Object.heldObject.Value is null),
            working = kegs.Count(p => p.Object.heldObject.Value is not null && !p.Object.readyForHarvest.Value),
            ready = kegs.Count(p => p.Object.readyForHarvest.Value),
            errors = controller.ErrorCount,
            enabled = config.Enabled
        }), LogLevel.Info);
    }
}
