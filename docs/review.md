# Global Keg Chest review — 2026-10-17

Result: both identified P1 defects are resolved. No remaining concrete P1/P2 findings in the reviewed implementation and scoped mutex follow-up.

Reviewed the implementation under `src/GlobalKegChest`, its `GlobalKegChest.Core` integration, and the relevant native chest, inventory, machine, location and mutex behavior. Decompiled game sources were used only for local inspection and remain excluded from deliverables.

1. **Resolved P1: an unattended source chest stalled the global lock batch.** The original controller queued native requests without advancing remote chest mutexes. Native chest updates only poll those mutexes in occupied locations, so a source chest in an empty shed could leave automation waiting indefinitely and retain locks already acquired elsewhere. `AutomationController.RequestChestLocks` now advances the requested mutexes using online farmers, allowing the host to acquire and release the batch within the same update. Regression: `Remote unoccupied chest lease completes and releases in one host update`.

2. **Resolved P1: repeated batches stalled on stale native previous ownership.** In a batch containing two remote chests, the last acquisition callback released earlier mutexes after their own native `Update` calls had returned. Their internal previous-owner value therefore still identified the host. A subsequent request could reacquire the same owner without producing the ownership-change callback, leaving that batch pending. The helper now polls idle mutexes before constructing the request, normalizing the released ownership transition before reacquisition. Regression: `Multiple remote chest leases can repeat without stale native ownership`, covering two unattended chests and three consecutive leases.

The final helper was re-read at `AutomationController.cs:167-192`. Inspection of its synchronous constructor callbacks, post-request callbacks, failure path and controller cancellation found no additional release defect. The native request rejects an already locked mutex before requesting any locks; its release list contains only mutexes it acquired. The helper stops polling once the request completes, and the controller retains its epoch/cursor checks and `finally` release. No path was found that releases an existing lock owned by another online player.

Verification run during this review:

```powershell
dotnet test .\tests\GlobalKegChest.Tests\GlobalKegChest.Tests.csproj -c Release --no-restore --logger 'console;verbosity=minimal'
```

Result: 53 passed, 0 failed, 0 skipped.

```powershell
dotnet run --project .\tests\GlobalKegChest.RuntimeChecks\GlobalKegChest.RuntimeChecks.csproj -c Release --no-restore
```

Final result: 26 passed, 0 failed, using actual Stardew Valley 1.6.15 game classes and data. These checks run without a game window or rendering and do not access save files or establish a multiplayer session. Live game, save/reload and multiplayer acceptance remain separate from this review's test evidence.
