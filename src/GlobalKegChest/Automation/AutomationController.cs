using System.Diagnostics;
using GlobalKegChest.Core;
using StardewModdingAPI;
using StardewValley;
using GameChest = StardewValley.Objects.Chest;

namespace GlobalKegChest.Automation;

internal sealed class AutomationController
{
    private readonly IMonitor monitor;
    private readonly ModConfig config;
    private readonly WorldIndex index = new();
    private readonly HashSet<StardewValley.Object> quarantined = new(ReferenceEqualityComparer.Instance);
    private CycleCursor<PlacedObject>? cursor;
    private MultipleMutexRequest? pendingLocks;
    private bool waitingForLocks;
    private bool requested;
    private bool saving;
    private long epoch;
    private int lastCheckMinutes = -1;
    private int collected;
    private int loaded;
    private readonly Stopwatch cycleTime = new();

    public AutomationController(IMonitor monitor, ModConfig config)
    {
        this.monitor = monitor;
        this.config = config;
    }

    public int ErrorCount => quarantined.Count;
    public WorldIndex Index => index;

    public void OnDayStarted()
    {
        Cancel();
        saving = false;
        quarantined.Clear();
        lastCheckMinutes = Game1.timeOfDay / 100 * 60 + Game1.timeOfDay % 100;
        requested = true;
    }

    public void OnTimeChanged(int time)
    {
        int minutes = time / 100 * 60 + time % 100;
        if (lastCheckMinutes < 0 || minutes < lastCheckMinutes
            || minutes - lastCheckMinutes >= config.ProcessIntervalMinutes)
        {
            requested = true;
            lastCheckMinutes = minutes;
        }
    }

    public void RequestRescan()
    {
        Cancel();
        quarantined.Clear();
        index.Rebuild();
        requested = true;
    }

    private static bool HasAuthority => Context.IsWorldReady && Context.IsMainPlayer && Context.ScreenId == 0;

    private bool CanRun => HasAuthority && config.Enabled && !saving && !Game1.eventUp
        && Game1.activeClickableMenu is null && Game1.shouldTimePass();

    public void Update()
    {
        if (!HasAuthority || !config.Enabled)
        {
            if (cursor is not null || waitingForLocks)
                Cancel();
            return;
        }
        if (!CanRun || waitingForLocks)
            return;
        if (cursor is null)
        {
            if (!requested)
                return;
            requested = false;
            index.Rebuild();
            if (index.Chests.Count == 0 || index.Kegs.Count == 0)
                return;
            cursor = new(index.Kegs);
            collected = loaded = 0;
            cycleTime.Restart();
        }

        var available = index.Chests.Where(p => p.IsPresent && p.Object is GameChest chest
            && !chest.GetMutex().IsLocked()).ToArray();
        if (available.Length == 0)
        {
            if (!index.Chests.Any(p => p.IsPresent))
            {
                cursor = null;
                requested = true;
            }
            return;
        }

        var expectedCursor = cursor;
        long expectedEpoch = epoch;
        waitingForLocks = true;
        var request = RequestChestLocks(available.Select(p => (GameChest)p.Object).ToArray(),
            lease =>
            {
                try
                {
                    if (expectedEpoch != epoch || !ReferenceEquals(expectedCursor, cursor) || !CanRun)
                        return;
                    var network = new ChestNetwork(available, Game1.MasterPlayer);
                    var timer = Stopwatch.StartNew();
                    int examined = 0;
                    while (examined < 100 && (examined == 0 || timer.Elapsed.TotalMilliseconds < 4))
                    {
                        if (!expectedCursor.TryTake(1, out var batch))
                        {
                            cursor = null;
                            if (config.VerboseLogging)
                                monitor.Log($"Cycle: collected {collected}, loaded {loaded}; {cycleTime.Elapsed.TotalMilliseconds:F1} ms across frames.", LogLevel.Debug);
                            break;
                        }
                        examined++;
                        var placed = batch[0];
                        if (!placed.IsPresent || quarantined.Contains(placed.Object) || !network.IsUsable)
                            continue;
                        try
                        {
                            if (KegAdapter.TryCollect(placed.Object, network, Game1.MasterPlayer))
                                collected++;
                            if (KegAdapter.TryLoad(placed.Object, network, Game1.MasterPlayer))
                                loaded++;
                        }
                        catch (Exception ex)
                        {
                            quarantined.Add(placed.Object);
                            monitor.Log($"Paused keg at {placed.Label} until tomorrow or 'gkc rescan': {ex}", LogLevel.Error);
                        }
                    }
                }
                finally
                {
                    lease.ReleaseLocks();
                    if (expectedEpoch == epoch)
                    {
                        waitingForLocks = false;
                        pendingLocks = null;
                    }
                }
            },
            failed =>
            {
                failed.ReleaseLocks();
                if (expectedEpoch == epoch)
                {
                    waitingForLocks = false;
                    pendingLocks = null;
                }
            });
        // Host lock acquisition can invoke the callback synchronously in the constructor.
        if (waitingForLocks && expectedEpoch == epoch)
            pendingLocks = request;
    }

    internal static MultipleMutexRequest RequestChestLocks(IReadOnlyList<GameChest> chests,
        Action<MultipleMutexRequest> success, Action<MultipleMutexRequest> failure)
    {
        var mutexes = chests.Select(p => p.GetMutex()).Distinct().ToArray();
        // Earlier members of a multi-lock batch may have been released by the last
        // member's callback, after their own Update already returned. Normalize that
        // released owner transition before requesting the same mutex again.
        foreach (var mutex in mutexes)
        {
            if (!mutex.IsLocked())
                mutex.Update(Game1.getOnlineFarmers());
        }
        bool finished = false;
        var request = new MultipleMutexRequest(mutexes,
            lease => { finished = true; success(lease); },
            lease => { finished = true; failure(lease); });
        // The game only polls chest mutexes in occupied locations. We own the host
        // request and must poll it ourselves even when its shed/island is unattended.
        // Use online farmers, not the remote location's (possibly empty) farmers list.
        foreach (var mutex in mutexes)
        {
            if (finished)
                break;
            mutex.Update(Game1.getOnlineFarmers());
        }
        return request;
    }

    public void OnSaving()
    {
        saving = true;
        Cancel();
    }

    public void OnSaved() => saving = false;

    public void Reset()
    {
        Cancel();
        saving = false;
        requested = false;
        quarantined.Clear();
        index.Clear();
        lastCheckMinutes = -1;
    }

    private void Cancel()
    {
        epoch++;
        cursor?.Cancel();
        cursor = null;
        pendingLocks?.ReleaseLocks();
        pendingLocks = null;
        waitingForLocks = false;
    }
}
