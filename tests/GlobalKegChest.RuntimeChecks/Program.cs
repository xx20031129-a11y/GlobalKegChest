using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using GlobalKegChest;
using GlobalKegChest.Automation;
using GlobalKegChest.Chest;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.Inventories;
using StardewValley.ItemTypeDefinitions;
using GameChest = StardewValley.Objects.Chest;
using SObject = StardewValley.Object;

internal static class Program
{
    private static string gamePath = "";
    private static int passed;
    private static int failed;
    private static bool simulateHost = true;
    private static int simulateScreen;
    private static bool benchmark;

    public static int Main(string[] args)
    {
        benchmark = args.Contains("--benchmark");
        gamePath = Path.GetFullPath(args.FirstOrDefault(p => !p.StartsWith("--", StringComparison.Ordinal))
            ?? @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            foreach (string directory in new[] { gamePath, Path.Combine(gamePath, "smapi-internal") })
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path))
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
            return null;
        };
        return Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        Initialize();
        var harmony = new Harmony("Local.GlobalKegChest.RuntimeChecks");
        ChestPatches.Apply(harmony, DispatchProxy.Create<IMonitor, MonitorProxy>());

        Check("Native chest capacity stays 36; special chest is 70", () =>
        {
            Equal(36, new GameChest(true, Vector2.Zero).GetActualCapacity());
            Equal(70, ChestPatches.CreateChest(Vector2.Zero).GetActualCapacity());
        });
        Check("Fixed-art special chest hides unsupported recoloring control", () =>
        {
            var menu = (StardewValley.Menus.ItemGrabMenu)FormatterServices.GetUninitializedObject(typeof(StardewValley.Menus.ItemGrabMenu));
            menu.source = 1;
            menu.sourceItem = ChestPatches.CreateChest(Vector2.Zero);
            Assert(!menu.CanHaveColorPicker());
            menu.sourceItem = new GameChest(true, Vector2.Zero);
            Assert(menu.CanHaveColorPicker());
        });
        Check("Placement converts custom Object into native Chest", () =>
        {
            var location = Location("Placement");
            var item = ItemRegistry.Create<SObject>(ChestIdentity.QualifiedItemId);
            Place(location, new(3, 4), item);
            ChestPatches.AfterPlacement(item, location, 3 * 64, 4 * 64, true);
            Assert(location.Objects[new Vector2(3, 4)] is GameChest);
            Equal("1", location.Objects[new Vector2(3, 4)].modData[ChestIdentity.SchemaKey]);
        });
        Check("Placement failure never replaces an object", () =>
        {
            var location = Location("FailedPlacement");
            var item = ItemRegistry.Create<SObject>(ChestIdentity.QualifiedItemId);
            Place(location, new(1, 1), item);
            ChestPatches.AfterPlacement(item, location, 64, 64, false);
            Assert(ReferenceEquals(item, location.Objects[new Vector2(1, 1)]));
        });
        Check("Placed chest survives location save/load as a native Chest", () =>
        {
            var location = Location("LocationRoundTrip");
            var tile = new Vector2(3, 4);
            var item = ItemRegistry.Create<SObject>(ChestIdentity.QualifiedItemId);
            Place(location, tile, item);
            ChestPatches.AfterPlacement(item, location, 3 * 64, 4 * 64, true);
            Assert(location.Objects[tile] is GameChest);
            ((GameChest)location.Objects[tile]).Items.Add(ItemRegistry.Create("(O)388", 7));

            using var stream = new MemoryStream();
            SaveGame.locationSerializer.Serialize(stream, location);
            stream.Position = 0;
            var restored = (GameLocation)SaveGame.locationSerializer.Deserialize(stream)!;
            Assert(restored.Objects.TryGetValue(tile, out var restoredObject));
            Assert(restoredObject is GameChest);
            Equal(ChestIdentity.QualifiedItemId, restoredObject.QualifiedItemId);
            Equal(7, ((GameChest)restoredObject).Items[0].Stack);
        });
        Check("Placed chest survives a full location net-state clone as a native Chest", () =>
        {
            var location = Location("LocationNetClone");
            var tile = new Vector2(3, 4);
            var item = ItemRegistry.Create<SObject>(ChestIdentity.QualifiedItemId);
            Place(location, tile, item);
            ChestPatches.AfterPlacement(item, location, 3 * 64, 4 * 64, true);
            ((GameChest)location.Objects[tile]).Items.Add(ItemRegistry.Create("(O)388", 7));

            var clone = new Netcode.NetRoot<GameLocation>(location).Clone().Value;
            Assert(clone.Objects.TryGetValue(tile, out var clonedObject));
            Assert(clonedObject is GameChest);
            Equal(ChestIdentity.QualifiedItemId, clonedObject.QualifiedItemId);
            Equal(7, ((GameChest)clonedObject).Items[0].Stack);
        });
        Check("An already connected location receives placed Object-to-Chest replacement", () =>
        {
            var location = Location("LocationNetDelta");
            var tile = new Vector2(3, 4);
            var item = ItemRegistry.Create<SObject>(ChestIdentity.QualifiedItemId);
            Place(location, tile, item);
            var hostRoot = new Netcode.NetRoot<GameLocation>(location);
            var guestRoot = hostRoot.Clone();
            Assert(guestRoot.Value.Objects[tile] is not GameChest);

            item.Stack = 2;
            using (var controlStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(controlStream, System.Text.Encoding.UTF8, leaveOpen: true))
                    hostRoot.Write(writer);
                controlStream.Position = 0;
                using (var reader = new BinaryReader(controlStream, System.Text.Encoding.UTF8, leaveOpen: true))
                    guestRoot.Read(reader);
                for (int i = 0; i < 10; i++)
                    guestRoot.TickTree();
                Equal(2, guestRoot.Value.Objects[tile].Stack);
            }

            ChestPatches.AfterPlacement(item, location, 3 * 64, 4 * 64, true);
            ((GameChest)location.Objects[tile]).Items.Add(ItemRegistry.Create("(O)388", 7));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                hostRoot.Write(writer);
            stream.Position = 0;
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                guestRoot.Read(reader);
            for (int i = 0; i < 10; i++)
                guestRoot.TickTree();

            Assert(guestRoot.Value.Objects.TryGetValue(tile, out var guestObject));
            Assert(guestObject is GameChest);
            Equal(ChestIdentity.QualifiedItemId, guestObject.QualifiedItemId);
            Equal(7, ((GameChest)guestObject).Items[0].Stack);
        });
        Check("Farming 7/8 recipe gate and existing-save grant", () =>
        {
            Game1.player.craftingRecipes.Remove(ChestIdentity.ItemId);
            Game1.player.farmingLevel.Value = 7;
            RecipeUnlocks.Update(Game1.player);
            Assert(!Game1.player.craftingRecipes.ContainsKey(ChestIdentity.ItemId));
            Game1.player.farmingLevel.Value = 8;
            RecipeUnlocks.Update(Game1.player);
            RecipeUnlocks.Update(Game1.player);
            Equal(0, Game1.player.craftingRecipes[ChestIdentity.ItemId]);
        });
        Check("Native crafting parses exact material counts and custom output", () =>
        {
            var recipe = new CraftingRecipe(ChestIdentity.ItemId);
            Equal(ChestIdentity.QualifiedItemId, recipe.createItem().QualifiedItemId);
            var expected = new Dictionary<string, int>
                { ["388"] = 150, ["334"] = 3, ["335"] = 1, ["725"] = 1, ["787"] = 1, ["337"] = 1 };
            foreach (var entry in expected)
                Equal(entry.Value, recipe.recipeList[entry.Key]);
        });
        Check("70 native inventory stacks survive XML serialization", () =>
        {
            var chest = ChestPatches.CreateChest(Vector2.Zero);
            for (int i = 0; i < 70; i++)
                chest.Items.Add(ItemRegistry.Create("(O)388", i + 1));
            var serializer = new XmlSerializer(typeof(GameChest));
            using var stream = new MemoryStream();
            serializer.Serialize(stream, chest);
            stream.Position = 0;
            var restored = (GameChest)serializer.Deserialize(stream)!;
            Equal(70, restored.GetActualCapacity());
            Equal(70, restored.Items.Count);
            Equal(70, restored.Items[69].Stack);
            Equal("1", restored.modData[ChestIdentity.SchemaKey]);
        });

        foreach (var (input, amount, output) in new[]
        {
            ("613", 1, "348"), ("192", 1, "350"), ("304", 1, "303"), ("262", 1, "346"),
            ("340", 1, "459"), ("815", 1, "614"), ("423", 1, "419"), ("433", 5, "395")
        })
        {
            Check($"Native recipe {input} consumes exactly {amount} and outputs {output}", () =>
            {
                using var fixture = new Fixture();
                var raw = ItemRegistry.Create<SObject>("(O)" + input, amount + 2);
                fixture.Chest.Items.Add(raw);
                Game1.player.Items.Clear();
                var backpack = ItemRegistry.Create("(O)" + input, 10);
                Game1.player.Items.Add(backpack);
                Assert(KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
                Equal(2, raw.Stack);
                Equal(10, backpack.Stack);
                Equal("(O)" + output, fixture.Keg.heldObject.Value.QualifiedItemId);
                Assert(fixture.Keg.MinutesUntilReady > 0);
                Assert(!KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
                Equal(2, raw.Stack);
            });
        }
        Check("Split 3+2 coffee beans consolidate and consume once", () =>
        {
            using var fixture = new Fixture(secondChest: true);
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)433", 3));
            fixture.OtherChest!.Items.Add(ItemRegistry.Create("(O)433", 2));
            Assert(KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            Equal(0, Total(fixture, "(O)433"));
            Equal("(O)395", fixture.Keg.heldObject.Value.QualifiedItemId);
        });
        Check("Insufficient and different-quality beans remain untouched", () =>
        {
            using var fixture = new Fixture(secondChest: true);
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)433", 3));
            var other = ItemRegistry.Create<SObject>("(O)433", 2);
            other.Quality = 2;
            fixture.OtherChest!.Items.Add(other);
            Assert(!KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            Equal(5, Total(fixture, "(O)433"));
            Assert(fixture.Keg.heldObject.Value is null);
        });
        Check("One apple cannot start two kegs", () =>
        {
            using var fixture = new Fixture();
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", 1));
            var second = ItemRegistry.Create<SObject>("(BC)12");
            Place(fixture.Location, new(4, 4), second);
            Assert(KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            Assert(!KegAdapter.TryLoad(second, fixture.Network, Game1.player));
            Equal(0, Total(fixture, "(O)613"));
        });
        Check("Full chest keeps finished output; freeing one slot resumes", () =>
        {
            using var fixture = new Fixture();
            Ready(fixture.Keg, "348", 1);
            for (int i = 0; i < 70; i++)
                fixture.Chest.Items.Add(ItemRegistry.Create("(O)388", 999));
            var original = fixture.Keg.heldObject.Value;
            Assert(!KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
            Assert(ReferenceEquals(original, fixture.Keg.heldObject.Value));
            Assert(fixture.Keg.readyForHarvest.Value);
            fixture.Chest.Items.RemoveAt(69);
            Assert(KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
            Equal(1, Total(fixture, "(O)348"));
            Assert(fixture.Keg.heldObject.Value is null);
            Assert(!KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
        });
        Check("Full but compatible stack accepts output with flavor and quality", () =>
        {
            using var fixture = new Fixture();
            Ready(fixture.Keg, "348", 1);
            var output = fixture.Keg.heldObject.Value;
            output.Quality = 2;
            output.preservedParentSheetIndex.Value = "613";
            output.modData["test/flavor"] = "apple";
            var existing = output.getOne();
            existing.Stack = 2;
            fixture.Chest.Items.Add(existing);
            for (int i = 1; i < 70; i++)
                fixture.Chest.Items.Add(ItemRegistry.Create("(O)388", 999));
            Assert(KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
            Equal(3, existing.Stack);
            Equal(2, ((SObject)existing).Quality);
            Equal("613", ((SObject)existing).preservedParentSheetIndex.Value);
        });
        Check("Two-item output with only one space stays in keg", () =>
        {
            using var fixture = new Fixture();
            Ready(fixture.Keg, "419", 2);
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)419", 998));
            for (int i = 1; i < 70; i++)
                fixture.Chest.Items.Add(ItemRegistry.Create("(O)388", 999));
            Assert(!KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
            Equal(2, fixture.Keg.heldObject.Value.Stack);
            Equal(998, fixture.Chest.Items[0].Stack);
        });
        Check("Ready keg collects and reloads using remaining raw material", () =>
        {
            using var fixture = new Fixture();
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", 2));
            Assert(KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            fixture.Keg.readyForHarvest.Value = true;
            fixture.Keg.MinutesUntilReady = 0;
            Assert(KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player));
            Assert(KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            Equal(1, Total(fixture, "(O)348"));
            Equal(0, Total(fixture, "(O)613"));
            Assert(!fixture.Keg.readyForHarvest.Value);
        });
        Check("Failure after native consumption restores input, machine and static context", () =>
        {
            using var fixture = new Fixture();
            var faulty = new ThrowAfterConsumption("613", 2) { FailNextConsumption = true };
            fixture.Chest.Items.Add(faulty);
            var previous = new Inventory();
            SObject.autoLoadFrom = previous;
            try
            {
                bool threw = false;
                try { KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player); }
                catch (InvalidOperationException ex) when (ex.Message == "Injected consume failure") { threw = true; }
                Assert(threw);
                Equal(2, faulty.Stack);
                Assert(ReferenceEquals(faulty, fixture.Chest.Items[0]));
                Assert(fixture.Keg.heldObject.Value is null);
                Assert(ReferenceEquals(previous, SObject.autoLoadFrom));
            }
            finally { SObject.autoLoadFrom = null; }
        });
        Check("Unlocked chest is never modified", () =>
        {
            using var fixture = new Fixture();
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", 1));
            fixture.Chest.GetMutex().ReleaseLock();
            Assert(!KegAdapter.TryLoad(fixture.Keg, fixture.Network, Game1.player));
            Equal(1, fixture.Chest.Items[0].Stack);
        });
        Check("World traversal includes building interiors and remote roots", () =>
        {
            using var fixture = new Fixture();
            var remote = Location("IslandRemote");
            var keg = ItemRegistry.Create<SObject>("(BC)12");
            Place(remote, new(1, 1), keg);
            var interior = new GameLocation();
            interior.name.Value = "UnvisitedShed";
            var building = new StardewValley.Buildings.Building();
            building.indoors.Value = interior;
            fixture.Location.buildings.Add(building);
            Place(interior, new(2, 2), ItemRegistry.Create<SObject>("(BC)12"));
            var index = new WorldIndex();
            index.Rebuild();
            Assert(index.Kegs.Any(p => ReferenceEquals(p.Location, remote)));
            Assert(index.Kegs.Any(p => ReferenceEquals(p.Location, interior)));
        });
        Check("Remote unoccupied chest lease completes and releases in one host update", () =>
        {
            var remote = Location("UnoccupiedRemoteChest");
            var chest = ChestPatches.CreateChest(new(1, 1));
            Place(remote, new(1, 1), chest);
            Assert(!remote.farmers.Any());
            bool acquired = false;
            var request = AutomationController.RequestChestLocks(new[] { chest }, lease =>
            {
                acquired = true;
                Assert(chest.GetMutex().IsLockHeld());
                lease.ReleaseLocks();
            }, _ => throw new InvalidOperationException("Remote lease failed"));
            Assert(acquired);
            Assert(!chest.GetMutex().IsLocked());
        });
        Check("Multiple remote chest leases can repeat without stale native ownership", () =>
        {
            var remote = Location("RepeatRemoteLease");
            var chests = new[] { ChestPatches.CreateChest(new(1, 1)), ChestPatches.CreateChest(new(2, 1)) };
            foreach (var chest in chests)
                Place(remote, chest.TileLocation, chest);
            for (int i = 0; i < 3; i++)
            {
                bool acquired = false;
                AutomationController.RequestChestLocks(chests, lease =>
                {
                    acquired = true;
                    lease.ReleaseLocks();
                }, _ => throw new InvalidOperationException("Repeated lease failed"));
                Assert(acquired);
                Assert(chests.All(p => !p.GetMutex().IsLocked()));
            }
        });
        Check("Partial output-write exception restores both inventory and ready keg", () =>
        {
            using var fixture = new Fixture();
            var output = new ThrowOnClone("419", 2);
            fixture.Keg.heldObject.Value = output;
            fixture.Keg.readyForHarvest.Value = true;
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)419", 998));
            for (int i = 1; i < 69; i++)
                fixture.Chest.Items.Add(ItemRegistry.Create("(O)388", 999));
            bool threw = false;
            try { KegAdapter.TryCollect(fixture.Keg, fixture.Network, Game1.player); }
            catch (InvalidOperationException ex) when (ex.Message == "Injected output clone failure") { threw = true; }
            Assert(threw);
            Equal(998, fixture.Chest.Items[0].Stack);
            Equal(69, fixture.Chest.Items.Count);
            Assert(ReferenceEquals(output, fixture.Keg.heldObject.Value));
            Assert(fixture.Keg.readyForHarvest.Value);
        });

        // These exercise the real scheduler with controlled SMAPI context flags.
        // They are not a network session or a running game's clock/UI test.
        harmony.Patch(AccessTools.PropertyGetter(typeof(Context), nameof(Context.IsWorldReady)),
            prefix: new HarmonyMethod(typeof(Program), nameof(WorldReadyPrefix)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Context), nameof(Context.IsMainPlayer)),
            prefix: new HarmonyMethod(typeof(Program), nameof(MainPlayerPrefix)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Context), nameof(Context.ScreenId)),
            prefix: new HarmonyMethod(typeof(Program), nameof(ScreenPrefix)));

        Check("Controlled scheduler: farmhand and secondary screen cannot mutate world", () =>
        {
            using var fixture = new Fixture();
            fixture.Chest.GetMutex().ReleaseLock();
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", 1));
            Game1.timeOfDay = 600;
            var controller = new AutomationController(DispatchProxy.Create<IMonitor, MonitorProxy>(), new ModConfig());
            controller.OnDayStarted();
            simulateHost = false;
            controller.Update();
            Equal(1, fixture.Chest.Items[0].Stack);
            simulateHost = true;
            simulateScreen = 1;
            controller.Update();
            Equal(1, fixture.Chest.Items[0].Stack);
            simulateScreen = 0;
            controller.OnDayStarted();
            for (int i = 0; i < 10; i++) controller.Update();
            Assert(fixture.Keg.heldObject.Value is not null);
            Equal(0, Total(fixture, "(O)613"));
            controller.OnSaving();
            Ready(fixture.Keg, "348", 1);
            controller.RequestRescan();
            for (int i = 0; i < 3; i++) controller.Update();
            Assert(fixture.Keg.readyForHarvest.Value);
            controller.Reset();
            for (int i = 0; i < 3; i++) controller.Update();
            Assert(fixture.Keg.readyForHarvest.Value);
        });
        Check("Controlled scheduler: 30-minute interval anchors to day start", () =>
        {
            using var fixture = new Fixture();
            fixture.Chest.GetMutex().ReleaseLock();
            Game1.timeOfDay = 600;
            var controller = new AutomationController(DispatchProxy.Create<IMonitor, MonitorProxy>(),
                new ModConfig { ProcessIntervalMinutes = 30 });
            controller.OnDayStarted();
            for (int i = 0; i < 10; i++) controller.Update();
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", 1));
            controller.OnTimeChanged(610);
            for (int i = 0; i < 10; i++) controller.Update();
            Assert(fixture.Keg.heldObject.Value is null);
            controller.OnTimeChanged(630);
            for (int i = 0; i < 10; i++) controller.Update();
            Assert(fixture.Keg.heldObject.Value is not null);
            controller.Reset();
        });

        if (benchmark)
        {
            foreach (int count in new[] { 100, 1000, 10000 })
                Check($"Controlled scheduler stress: {count} kegs", () => Benchmark(count));
        }
        Console.WriteLine($"Runtime checks: {passed} passed, {failed} failed. Actual game: {Game1.version}. No window, rendering, save-file access, or multiplayer session.");
        harmony.UnpatchAll(harmony.Id);
        return failed == 0 ? 0 : 1;
    }

    private static void Initialize()
    {
        Game1.content = new HeadlessContent(new GameServiceContainer(), Path.Combine(gamePath, "Content"));
        Game1.objectData = DataLoader.Objects(Game1.content);
        Game1.bigCraftableData = DataLoader.BigCraftables(Game1.content);
        Game1.shirtData = DataLoader.Shirts(Game1.content);
        Game1.bigCraftableData[ChestIdentity.ItemId] = ChestContent.CreateData("Global Keg Chest", "Runtime check");
        CraftingRecipe.craftingRecipes = DataLoader.CraftingRecipes(Game1.content);
        CraftingRecipe.craftingRecipes[ChestIdentity.ItemId] = ChestContent.CreateRecipe("Global Keg Chest");
        CraftingRecipe.cookingRecipes = DataLoader.CookingRecipes(Game1.content);
        Game1.game1 = (Game1)FormatterServices.GetUninitializedObject(typeof(Game1));
        Game1.game1._locations = new List<GameLocation>();
        Game1.otherFarmers = new Netcode.NetRootDictionary<long, Farmer>();
        Game1.netWorldState = new Netcode.NetRoot<StardewValley.Network.NetWorldState>(new StardewValley.Network.NetWorldState());
        ItemRegistry.AddTypeDefinition(new ObjectDataDefinition());
        ItemRegistry.AddTypeDefinition(new BigCraftableDataDefinition());
        ItemRegistry.AddTypeDefinition(new ShirtDataDefinition());
        typeof(Game1).GetProperty(nameof(Game1.player))!.SetValue(null, new Farmer());
        Game1.player.UniqueMultiplayerID = 123456;
        Game1.player.currentLocation = Location("Initial");
    }

    private static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + "\n" + ex); }
    }

    private static bool WorldReadyPrefix(ref bool __result) { __result = true; return false; }
    private static bool MainPlayerPrefix(ref bool __result) { __result = simulateHost; return false; }
    private static bool ScreenPrefix(ref int __result) { __result = simulateScreen; return false; }

    private static void Benchmark(int count)
    {
        using var fixture = new Fixture();
        fixture.Chest.GetMutex().ReleaseLock();
        for (int remaining = count; remaining > 0; remaining -= Math.Min(remaining, 999))
            fixture.Chest.Items.Add(ItemRegistry.Create("(O)613", Math.Min(remaining, 999)));
        var kegs = new List<SObject> { fixture.Keg };
        for (int i = 1; i < count; i++)
        {
            var keg = ItemRegistry.Create<SObject>("(BC)12");
            Place(fixture.Location, new(10 + i % 100, 10 + i / 100), keg);
            kegs.Add(keg);
        }
        var controller = new AutomationController(DispatchProxy.Create<IMonitor, MonitorProxy>(), new ModConfig());
        controller.OnDayStarted();
        var total = System.Diagnostics.Stopwatch.StartNew();
        double largest = 0;
        int updates = 0;
        while (kegs.Any(p => p.heldObject.Value is null) && updates < count * 2)
        {
            var tick = System.Diagnostics.Stopwatch.StartNew();
            controller.Update();
            largest = Math.Max(largest, tick.Elapsed.TotalMilliseconds);
            updates++;
        }
        Assert(kegs.All(p => p.heldObject.Value?.QualifiedItemId == "(O)348"));
        Equal(0, Total(fixture, "(O)613"));
        controller.Reset();
        Console.WriteLine($"BENCH {count} kegs: {updates} updates, {total.Elapsed.TotalMilliseconds:F2}ms total, {largest:F2}ms largest update (headless; includes discovery).");
    }

    private static void Assert(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }

    private static GameLocation Location(string name)
    {
        var location = new GameLocation();
        location.name.Value = name;
        Game1.locations.Add(location);
        return location;
    }

    private static void Place(GameLocation location, Vector2 tile, SObject obj)
    {
        obj.Location = location;
        obj.TileLocation = tile;
        location.Objects[tile] = obj;
    }

    private static void Ready(SObject keg, string output, int amount)
    {
        keg.heldObject.Value = ItemRegistry.Create<SObject>("(O)" + output, amount);
        keg.readyForHarvest.Value = true;
        keg.MinutesUntilReady = 0;
    }

    private static int Total(Fixture fixture, string id) => fixture.Chest.Items
        .Concat(fixture.OtherChest?.Items ?? new Inventory()).Where(p => p is not null && p.QualifiedItemId == id).Sum(p => p.Stack);

    private sealed class Fixture : IDisposable
    {
        public GameLocation Location { get; }
        public GameChest Chest { get; }
        public GameChest? OtherChest { get; }
        public SObject Keg { get; }
        public ChestNetwork Network { get; }

        public Fixture(bool secondChest = false)
        {
            Location = Program.Location("Check" + passed + "_" + failed);
            Game1.player.currentLocation = Location;
            Chest = ChestPatches.CreateChest(new(1, 1));
            Place(Location, new(1, 1), Chest);
            Chest.GetMutex().RequestLock();
            Chest.GetMutex().Update(Game1.getOnlineFarmers());
            if (secondChest)
            {
                OtherChest = ChestPatches.CreateChest(new(2, 1));
                Place(Location, new(2, 1), OtherChest);
                OtherChest.GetMutex().RequestLock();
                OtherChest.GetMutex().Update(Game1.getOnlineFarmers());
            }
            Keg = ItemRegistry.Create<SObject>("(BC)12");
            Place(Location, new(3, 1), Keg);
            var chests = new List<PlacedObject> { new(Location, Chest.TileLocation, Chest) };
            if (OtherChest is not null)
                chests.Add(new(Location, OtherChest.TileLocation, OtherChest));
            Network = new(chests, Game1.player);
            Assert(Network.IsUsable);
        }

        public void Dispose()
        {
            Chest.GetMutex().ReleaseLock();
            OtherChest?.GetMutex().ReleaseLock();
            Game1.locations.Remove(Location);
        }
    }

    private sealed class ThrowAfterConsumption : SObject
    {
        public bool FailNextConsumption { get; set; }
        public ThrowAfterConsumption(string id, int count) : base(id, count) { }
        public override int Stack
        {
            get => base.Stack;
            set
            {
                bool fail = FailNextConsumption && value < base.Stack;
                base.Stack = value;
                if (fail)
                {
                    FailNextConsumption = false;
                    throw new InvalidOperationException("Injected consume failure");
                }
            }
        }
    }

    private sealed class ThrowOnClone : SObject
    {
        public ThrowOnClone(string id, int count) : base(id, count) { }
        protected override Item GetOneNew() => throw new InvalidOperationException("Injected output clone failure");
    }
}

public class MonitorProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name is "Log" or "LogOnce")
            Console.WriteLine("LOG " + args?[0]);
        return targetMethod?.ReturnType == typeof(bool) ? false : null;
    }
}

internal sealed class HeadlessContent : LocalizedContentManager
{
    private readonly Texture2D placeholder = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
    public HeadlessContent(IServiceProvider services, string root) : base(services, root) { }
    public override T Load<T>(string name) => typeof(T) == typeof(Texture2D)
        ? (T)(object)placeholder : base.Load<T>(name);
    public override LocalizedContentManager CreateTemporary() => new HeadlessContent(ServiceProvider, RootDirectory);
}
