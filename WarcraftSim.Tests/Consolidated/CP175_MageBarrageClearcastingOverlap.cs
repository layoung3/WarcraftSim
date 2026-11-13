using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Tests.Consolidated;

// Blizzard Forever beta development notes, updated November 2, 2026:
// https://us.forums.blizzard.com/en/wow/t/2360696/1
// Mage/Arcane: Barrage is consumed at channel start, and a Barrage-funded
// Arcane Missiles must not consume a simultaneously active Clearcasting.
public sealed class Cp175MageBarrageClearcastingOverlap
{
    private static MageSimRequest Setup(string spec = "arcane")
    {
        var request = MageSimRequest.CreatePreset(spec);
        request.Seed = 132001;
        request.DurationSeconds = 120m;
        request.TargetMaximumHealth = 1_000_000m;
        request.BaseSpellHitPercent = 100m;
        request.SpellCritPercent = 0m;
        // Five earlier Arcane points and two Barrage ranks form a legal
        // off-tree allocation, independent of the starting specialization.
        request.Talents = new ForeverMageTalentBuild {
            ArcanePointsSpent = 7,
            Ranks = new() { ["arcane-concentration"] = 5, ["missile-barrage"] = 2 }
        };
        request.UseArcanePower = false;
        request.UsePresenceOfMind = false;
        request.UseCombustion = false;
        request.UseEvocation = false;
        request.UseMajorManaPotion = false;
        request.CombatItems.ManaRubyCharges = 0;
        request.Character.BaseStats.Values["spell-power"] = 0m;
        request.Character.BaseStats.Values["spirit"] = 0m;
        request.Character.BaseStats.Values["mp5"] = 0m;
        request.Tuning.ManaRegenPerSecond = 0m;
        request.Tuning.FireballMana = 10m;
        request.Tuning.FireballDamage = 100m;
        request.Tuning.FireballDotTick = 0m;
        request.Tuning.MissileBarrageOtherChancePercent = 100m;
        // Only the generator can proc Clearcasting in these controlled
        // resource tests; channel damage has separate existing coverage.
        request.Tuning.ArcaneMissilesTick = 0m;
        request.Tuning.FireBlastDamage = 0m;
        request.CustomPriority = ["arcane-missiles", "fire-blast", "fireball"];
        request.PriorityConditions = [
            new() { AbilityKey = "arcane-missiles", Conditions = [
                Aura("mage-clearcasting"), Aura("mage-missile-barrage")] },
            new() { AbilityKey = "fire-blast", Conditions = [
                Aura("mage-clearcasting"), Aura("mage-missile-barrage", missing: true)] },
            new() { AbilityKey = "fireball", Conditions = [Aura("mage-clearcasting", missing: true)] }
        ];
        return request;
    }

    private static RotationConditionDefinition Aura(string key, bool missing = false) => new() {
        ConditionType = missing ? RotationConditionTypes.SourceAuraMissing : RotationConditionTypes.SourceAuraActive,
        Key = key
    };

    private static CombatEvent[] Events(MageSimResponse result, CombatEventType type, string key) =>
        result.Run.Timeline.Where(e => e.Type == type && e.AbilityKey == key).ToArray();

    private static CombatEvent ManaCost(MageSimResponse result, CombatEvent cast)
    {
        var completed = Assert.Single(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityExecutionId == cast.AbilityExecutionId);
        return Assert.Single(result.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == cast.AbilityKey && e.TimeSeconds == completed.TimeSeconds && e.ResourceKey == "mana");
    }

    private static void AssertChannel(MageSimResponse result, CombatEvent start, decimal duration, decimal cost)
    {
        Assert.Equal(-cost, ManaCost(result, start).Amount);
        var end = Assert.Single(result.Run.Timeline, e =>
            e.Type == CombatEventType.AbilityChannelCompleted && e.AbilityExecutionId == start.AbilityExecutionId);
        Assert.Equal(duration, end.TimeSeconds - start.TimeSeconds);
    }

    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void BarrageFundedChannelPreservesClearcastingForNextEligibleSpell(string spec)
    {
        var result = new MageSimRunner().Run(Setup(spec));
        var channel = Events(result, CombatEventType.AbilityChannelStarted, "arcane-missiles").First();
        AssertChannel(result, channel, 2.5m, 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "missile-barrage" && e.TimeSeconds == channel.TimeSeconds);
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "arcane-concentration" && e.TimeSeconds == channel.TimeSeconds);
        var next = result.Run.Timeline.First(e => e.Type == CombatEventType.AbilityCastStarted &&
            e.TimeSeconds > channel.TimeSeconds);
        Assert.Equal("fire-blast", next.AbilityKey);
        Assert.Equal(0m, ManaCost(result, next).Amount);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "arcane-concentration" && e.TimeSeconds == next.TimeSeconds);
        Assert.True(result.ClearcastingConsumed > 0);
    }

    [Fact]
    public void RemovingBarrageLeavesClearcastingOnlyMissilesFreeButAtNormalDuration()
    {
        var request = Setup();
        request.Talents.Ranks.Remove("missile-barrage");
        request.CustomPriority = ["arcane-missiles", "fireball"];
        request.PriorityConditions.RemoveAll(r => r.AbilityKey == "fire-blast");
        request.PriorityConditions[0].Conditions.RemoveAll(c => c.Key == "mage-missile-barrage");
        var result = new MageSimRunner().Run(request);
        var channel = Events(result, CombatEventType.AbilityChannelStarted, "arcane-missiles").First();
        AssertChannel(result, channel, 5m, 0m);
        Assert.Equal(0, result.MissileBarrageProcs);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "arcane-concentration" && e.TimeSeconds == channel.TimeSeconds);
        Assert.True(result.ClearcastingConsumed > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovingConcentrationLeavesOnlyOneBarrageFundedChannel(bool barrage)
    {
        var request = Setup();
        request.DurationSeconds = 11.6m;
        request.Talents.Ranks.Remove("arcane-concentration");
        if (!barrage) request.Talents.Ranks.Remove("missile-barrage");
        request.CustomPriority = ["fireball", "arcane-missiles"];
        request.PriorityConditions = [
            new() { AbilityKey = "fireball", Conditions = [new() {
                ConditionType = RotationConditionTypes.CurrentTimeSeconds,
                ComparisonOperator = RotationComparisonOperators.LessThan, Value = 1m }] },
            new() { AbilityKey = "arcane-missiles", Conditions = [new() {
                ConditionType = RotationConditionTypes.CurrentTimeSeconds,
                ComparisonOperator = RotationComparisonOperators.GreaterThanOrEqual, Value = 4m }] }
        ];
        if (!barrage) request.DurationSeconds = 14.1m;
        var result = new MageSimRunner().Run(request);
        var channels = Events(result, CombatEventType.AbilityChannelStarted, "arcane-missiles");
        Assert.True(channels.Length >= 2);
        AssertChannel(result, channels[0], barrage ? 2.5m : 5m, barrage ? 0m : request.Tuning.ArcaneMissilesMana);
        AssertChannel(result, channels[1], 5m, request.Tuning.ArcaneMissilesMana);
        Assert.Equal(0, result.ClearcastingProcs);
        Assert.Equal(0, result.ClearcastingConsumed);
        Assert.Equal(barrage ? 1 : 0, result.MissileBarrageProcs);
    }

    [Fact]
    public void PreservedClearcastingStillExpiresDuringBarrageChannel()
    {
        var request = Setup();
        request.Seed = 7; // First generator grants both buffs with matching expiry.
        request.PriorityConditions[0].Conditions.Add(new() {
            ConditionType = RotationConditionTypes.SourceAuraRemainingSeconds,
            Key = "mage-clearcasting", ComparisonOperator = RotationComparisonOperators.LessThanOrEqual,
            Value = 1m
        });
        var result = new MageSimRunner().Run(request);
        var channel = Events(result, CombatEventType.AbilityChannelStarted, "arcane-missiles").First();
        AssertChannel(result, channel, 2.5m, 0m);
        var proc = Events(result, CombatEventType.AuraApplied, "arcane-concentration")
            .Last(e => e.TimeSeconds <= channel.TimeSeconds);
        var removed = Events(result, CombatEventType.AuraRemoved, "arcane-concentration")
            .First(e => e.TimeSeconds >= channel.TimeSeconds);
        Assert.Equal(proc.TimeSeconds + 15m, removed.TimeSeconds);
        Assert.InRange(removed.TimeSeconds - channel.TimeSeconds, 0.01m, 1m);
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted &&
            e.AbilityKey == "fire-blast" && e.TimeSeconds >= channel.TimeSeconds &&
            e.TimeSeconds <= channel.TimeSeconds + 2.5m);
        var nextFireball = Events(result, CombatEventType.AbilityCastStarted, "fireball")
            .First(e => e.TimeSeconds >= channel.TimeSeconds + 2.5m);
        Assert.Equal(-request.Tuning.FireballMana, ManaCost(result, nextFireball).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutomaticAndCustomResourceAuraAndChannelReceiptsReplayDeterministically(bool automatic)
    {
        var request = Setup();
        request.Seed = 7;
        if (automatic) { request.CustomPriority.Clear(); request.PriorityConditions.Clear(); }
        var first = new MageSimRunner().Run(request);
        var replay = new MageSimRunner().Run(request);
        Assert.True(first.ClearcastingProcs > 0);
        Assert.True(first.MissileBarrageProcs > 0);
        Assert.True(first.ClearcastingConsumed > 0);
        var channel = Events(first, CombatEventType.AbilityChannelStarted, "arcane-missiles").First();
        AssertChannel(first, channel, 2.5m, 0m);
        Assert.DoesNotContain(first.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "arcane-concentration" && e.TimeSeconds == channel.TimeSeconds);
        var next = first.Run.Timeline.First(e => e.Type == CombatEventType.AbilityCastStarted &&
            e.TimeSeconds > channel.TimeSeconds);
        Assert.Equal(0m, ManaCost(first, next).Amount);
        static object[] Receipt(MageSimResponse r) => r.Run.Timeline
            .Where(e => e.Type is CombatEventType.ResourceChanged or CombatEventType.AuraApplied or
                CombatEventType.AuraRemoved or CombatEventType.AbilityChannelStarted or
                CombatEventType.AbilityChannelCompleted)
            .Select(e => (object)(e.TimeSeconds, e.Type, e.AbilityKey, e.EffectKey, e.Amount)).ToArray();
        Assert.Equal(Receipt(first), Receipt(replay));
        Assert.Equal(first.EndingMana, replay.EndingMana);
        Assert.Equal(first.ClearcastingConsumed, replay.ClearcastingConsumed);
    }
}
