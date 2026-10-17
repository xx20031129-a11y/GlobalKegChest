using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace GlobalKegChest.Automation;

internal static class KegAdapter
{
    public static bool TryLoad(SObject keg, ChestNetwork network, Farmer actor)
    {
        if (keg.QualifiedItemId != "(BC)12" || keg.heldObject.Value is not null
            || keg.readyForHarvest.Value || !network.IsUsable)
            return false;
        var data = keg.GetMachineData();
        if (data is null)
            return false;
        foreach (var candidate in network.InputCandidates())
        {
            bool matches = MachineDataUtility.TryGetMachineOutputRule(keg, data,
                MachineOutputTrigger.ItemPlacedInMachine, candidate, actor, keg.Location,
                out _, out var trigger, out _, out var triggerIgnoringCount);
            int? required = (matches ? trigger : triggerIgnoringCount)?.RequiredCount;
            if (required is null || required <= 0)
                continue;
            var plan = network.PlanInput(candidate, required.Value);
            if (plan is null)
                continue;

            var machineBefore = new MachineSnapshot(keg);
            using var transaction = network.BeginTransaction();
            try
            {
                if (!network.TryApply(plan))
                    continue;
                var input = network.GetItem(plan.Target);
                var inventory = network.GetInventory(plan.Target);
                var previous = SObject.autoLoadFrom;
                bool started;
                try
                {
                    SObject.autoLoadFrom = inventory;
                    started = keg.PlaceInMachine(data, input, probe: false, who: actor,
                        showMessages: false, playSounds: false);
                }
                finally
                {
                    SObject.autoLoadFrom = previous;
                }
                if (started)
                {
                    if (keg.heldObject.Value is null)
                        throw new InvalidOperationException("Machine reported success without producing a processing item.");
                    transaction.Commit();
                    return true;
                }
                // No recipe started. Undo consolidation, then examine the next candidate.
                machineBefore.Restore(keg);
            }
            catch
            {
                machineBefore.Restore(keg);
                throw;
            }
        }
        return false;
    }

    public static bool TryCollect(SObject keg, ChestNetwork network, Farmer actor)
    {
        if (keg.QualifiedItemId != "(BC)12" || !keg.readyForHarvest.Value
            || keg.heldObject.Value is null || !network.IsUsable)
            return false;
        var machineBefore = new MachineSnapshot(keg);
        var data = keg.GetMachineData();
        bool committed = false;
        using var transaction = network.BeginTransaction();
        try
        {
            var output = keg.heldObject.Value;
            var rule = data?.OutputRules?.FirstOrDefault(p => p.Id == keg.lastOutputRuleId.Value);
            if (rule?.RecalculateOnCollect == true)
            {
                keg.heldObject.Value = null;
                keg.OutputMachine(data, rule, keg.lastInputItem.Value, actor, keg.Location,
                    probe: false, heldObjectOnly: true);
                keg.heldObject.Value ??= output;
                output = keg.heldObject.Value;
            }
            var plan = network.PlanOutput(output);
            if (plan is null || !network.TryApply(plan, output))
            {
                machineBefore.Restore(keg);
                return false;
            }

            // After this commit, an exception in an external output-collected callback
            // must never put the already delivered output back into the keg.
            keg.heldObject.Value = null;
            keg.readyForHarvest.Value = false;
            keg.showNextIndex.Value = false;
            keg.MinutesUntilReady = 0;
            keg.ResetParentSheetIndex();
            transaction.Commit();
            committed = true;

            MachineDataUtility.UpdateStats(data?.StatsToIncrementWhenHarvested, output, output.Stack);
            if (data is not null && MachineDataUtility.TryGetMachineOutputRule(keg, data,
                MachineOutputTrigger.OutputCollected, output.getOne(), actor, keg.Location,
                out var next, out _, out _, out _))
                keg.OutputMachine(data, next, keg.lastInputItem.Value, actor, keg.Location, probe: false);
            AwardExperience(data?.ExperienceGainOnHarvest, actor);
            return true;
        }
        catch
        {
            if (!committed)
                machineBefore.Restore(keg);
            throw;
        }
    }

    private static void AwardExperience(string? definition, Farmer actor)
    {
        if (string.IsNullOrWhiteSpace(definition))
            return;
        var parts = definition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            int skill = Farmer.getSkillNumberFromName(parts[i]);
            if (skill >= 0 && int.TryParse(parts[i + 1], out int amount) && amount > 0)
                actor.gainExperience(skill, amount);
        }
    }

    private sealed class MachineSnapshot
    {
        private readonly SObject? output;
        private readonly Item? input;
        private readonly string? rule;
        private readonly bool ready;
        private readonly bool nextIndex;
        private readonly int minutes;
        private readonly int sprite;
        private readonly Microsoft.Xna.Framework.Vector2 scale;

        public MachineSnapshot(SObject keg)
        {
            output = keg.heldObject.Value;
            input = keg.lastInputItem.Value;
            rule = keg.lastOutputRuleId.Value;
            ready = keg.readyForHarvest.Value;
            nextIndex = keg.showNextIndex.Value;
            minutes = keg.MinutesUntilReady;
            sprite = keg.ParentSheetIndex;
            scale = keg.scale;
        }

        public void Restore(SObject keg)
        {
            keg.heldObject.Value = output;
            keg.lastInputItem.Value = input;
            keg.lastOutputRuleId.Value = rule;
            keg.readyForHarvest.Value = ready;
            keg.showNextIndex.Value = nextIndex;
            keg.MinutesUntilReady = minutes;
            keg.ParentSheetIndex = sprite;
            keg.scale = scale;
        }
    }
}
