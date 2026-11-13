using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ShamanFireAndRegenerationTests
{
    // Literal historical source curves, independent of the production helpers.
    private static readonly decimal[] NovaMultipliers = [1m, 1.1m, 1.2m];
    private static readonly decimal[] NovaCooldowns = [10m, 8m, 6m];
    private static readonly decimal[] CastingFractions = [0m, .17m, .33m, .50m];

    private static ElementalShamanSimRequest Request()
    {
        var request = new ElementalShamanSimRequest
        {
            DurationSeconds = 24m, CaptureTimeline = true, Seed = 175130,
            TargetMaximumHealth = 1_000_000_000m, BaseSpellHitPercent = 100m,
            SpellCritPercent = 0m, MajorManaPotionCharges = 0,
            UseLightningOverload = false, UseElementalMastery = false,
            UseFlameShock = false, UseLavaBurst = false, UseManaSpringTotem = false,
            UseChainLightning = false, UseFireNova = true,
            CustomPriority = ["searing-totem", "fire-nova"],
            Talents = new() { ElementalPointsSpent = 0, EnhancementPointsSpent = 0,
                RestorationPointsSpent = 0, Ranks = new(StringComparer.OrdinalIgnoreCase) },
            Tuning = new() { ManaRegenPerSecond = 0m }
        };
        request.Character.Equipment = new();
        request.Character.Buffs = new();
        request.Character.Talents = new();
        request.Character.BaseStats.Values = new() { ["intellect"] = 1000m, ["spell-power"] = 200m };
        return request;
    }

    private static void SetTrial(ElementalShamanSimRequest request, int rank, int callOfFlame = 0)
    {
        request.Talents.Ranks["trial-by-fire"] = rank;
        request.Talents.Ranks["call-of-flame"] = callOfFlame;
        request.Talents.ElementalPointsSpent = rank + callOfFlame;
    }

    private static CombatEvent[] Events(ElementalShamanSimResponse run, CombatEventType type, string key) =>
        run.Run.Timeline.Where(e => e.Type == type && e.AbilityKey == key).ToArray();

    public static IEnumerable<object[]> FireCases()
    {
        for (var rank = 0; rank <= 2; rank++)
        for (var callOfFlame = 0; callOfFlame <= 3; callOfFlame++)
        foreach (var power in new[] { 0m, 200m })
            yield return [rank, callOfFlame, power];
    }

    [Theory]
    [MemberData(nameof(FireCases))]
    public void TrialHasIndependentDamageAndCooldownWithoutChangingOtherSpells(
        int rank, int callOfFlame, decimal power)
    {
        var request = Request();
        SetTrial(request, rank, callOfFlame);
        var spells = ForeverElementalShamanSpellbook.Create(request.Talents, request.Tuning, power);
        var nova = Assert.Single(spells, s => s.Key == "fire-nova");
        var expected = (power == 0m ? 239m : 267.6m) *
            (1m + .05m * callOfFlame) * NovaMultipliers[rank];
        Assert.Equal(expected, Assert.Single(nova.Effects).MinimumValue);
        Assert.Equal(expected, nova.Effects[0].MaximumValue);
        Assert.Equal(NovaCooldowns[rank], nova.CooldownSeconds);
        Assert.Equal(375m, Assert.Single(nova.ResourceCosts).Amount);
        Assert.Equal(1.5m, nova.GlobalCooldownSeconds);
        Assert.Equal(9, nova.Effects[0].MaxTargets);
        Assert.Equal(["shaman-fire-totem-active"], nova.RequiredSourceAuraKeys);

        SetTrial(request, 0, callOfFlame);
        var baseline = ForeverElementalShamanSpellbook.Create(request.Talents, request.Tuning, power);
        Assert.Equal(JsonSerializer.Serialize(baseline.Where(s => s.Key != "fire-nova")),
            JsonSerializer.Serialize(spells.Where(s => s.Key != "fire-nova")));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(2, 1)]
    [InlineData(0, 3)] [InlineData(1, 3)] [InlineData(2, 3)]
    public void ActualNovaCadenceDamageTargetsAndManaFollowEveryRank(int rank, int targets)
    {
        var request = Request();
        SetTrial(request, rank, 3);
        request.TargetCount = targets;
        var before = JsonSerializer.Serialize(request);
        var run = new ElementalShamanSimRunner().Run(request);
        var casts = Events(run, CombatEventType.AbilityCastCompleted, "fire-nova");
        var expectedTimes = rank switch
        {
            0 => new[] { 1.5m, 11.5m, 21.5m },
            1 => new[] { 1.5m, 9.5m, 17.5m },
            _ => new[] { 1.5m, 7.5m, 13.5m, 19.5m }
        };
        Assert.Equal(expectedTimes, casts.Select(e => e.TimeSeconds));
        var hits = Events(run, CombatEventType.Damage, "fire-nova");
        Assert.Equal(casts.Length * targets, hits.Length);
        var expectedDamage = 267.6m * 1.15m * NovaMultipliers[rank];
        Assert.All(hits, hit => Assert.Equal(expectedDamage * (hit.IsCritical ? 1.5m : 1m), hit.Amount));
        Assert.Equal(hits.Sum(hit => expectedDamage * (hit.IsCritical ? 1.5m : 1m)),
            run.DamageByAbility["fire-nova"]);
        var payments = Events(run, CombatEventType.ResourceChanged, "fire-nova");
        Assert.Equal(casts.Length, payments.Length);
        Assert.All(payments, e => Assert.Equal(-375m, e.Amount));
        Assert.Equal(run.DamageByAbility.Values.Sum(), run.DamageDone);
        Assert.Equal(before, JsonSerializer.Serialize(request));
        var replay = new ElementalShamanSimRunner().Run(JsonSerializer.Deserialize<ElementalShamanSimRequest>(before)!);
        Assert.Equal(Outcomes(run), Outcomes(replay));
        SetTrial(request, 0, 3);
        var removed = new ElementalShamanSimRunner().Run(request);
        Assert.All(Events(removed, CombatEventType.Damage, "fire-nova"),
            e => Assert.Equal(267.6m * 1.15m * (e.IsCritical ? 1.5m : 1m), e.Amount));
        Assert.Equal(new[] { 1.5m, 11.5m, 21.5m },
            Events(removed, CombatEventType.AbilityCastCompleted, "fire-nova").Select(e => e.TimeSeconds));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AutomaticRotationUsesRankedNovaWithItsOwnedTotem(int rank)
    {
        var request = Request();
        SetTrial(request, rank, 3);
        request.CustomPriority.Clear();
        var preview = ElementalShamanSimRunner.Preview(request);
        Assert.Contains(preview.Rotation.Entries, e => e.AbilityKey == "fire-nova");
        var run = new ElementalShamanSimRunner().Run(request);
        var casts = Events(run, CombatEventType.AbilityCastCompleted, "fire-nova");
        Assert.True(casts.Length >= 2);
        for (var index = 1; index < casts.Length; index++)
            Assert.True(casts[index].TimeSeconds - casts[index - 1].TimeSeconds >= NovaCooldowns[rank]);
        Assert.All(Events(run, CombatEventType.Damage, "fire-nova"),
            e => Assert.Equal(267.6m * 1.15m * NovaMultipliers[rank] * (e.IsCritical ? 1.5m : 1m), e.Amount));
        request.UseFireNova = false;
        var disabled = new ElementalShamanSimRunner().Run(request);
        Assert.Empty(Events(disabled, CombatEventType.AbilityCastCompleted, "fire-nova"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TrialNeverBypassesFireTotemAvailability(int rank)
    {
        var request = Request();
        SetTrial(request, rank);
        request.CustomPriority = ["fire-nova"];
        var run = new ElementalShamanSimRunner().Run(request);
        Assert.Empty(Events(run, CombatEventType.AbilityCastCompleted, "fire-nova"));
        Assert.Equal(0m, run.DamageDone);
        Assert.Equal(run.MaximumMana, run.EndingMana);

        var context = new SimulationContext(new() { DurationSeconds = 60m });
        var owner = new SimulationActorState { Key = "shaman", Name = "Shaman", MaximumHealth = 1000m };
        owner.InitializeHealth(1000m);
        context.AddActor(owner);
        var auras = new AuraManager();
        var totem = new ShamanFireTotemProcessor(owner, auras);
        totem.Process(context, new() { Type = CombatEventType.AuraApplied,
            SourceActorKey = "other-shaman", TargetActorKey = "target", EffectKey = "searing-totem-tick" });
        Assert.Empty(owner.ActiveAuras);
        totem.Process(context, new() { Type = CombatEventType.AuraApplied,
            SourceActorKey = owner.Key, TargetActorKey = "target", EffectKey = "searing-totem-tick" });
        var marker = Assert.Single(owner.ActiveAuras);
        Assert.Equal(owner.Key, marker.SourceActorKey);
        Assert.True(marker.IsActiveAt(54.999m));
        Assert.False(marker.IsActiveAt(55m));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MindfulnessKeepsExactSpiritFractionWhileMp5AndFiveSecondRecoveryStayIndependent(int rank)
    {
        var request = Request();
        request.DurationSeconds = 12m;
        request.StartingManaPercent = 40m;
        request.CustomPriority = ["earth-shock"];
        request.Tuning.ManaRegenPerSecond = 10m;
        request.Character.BaseStats.Set("mp5", 25m);
        request.Talents.Ranks["mindfulness"] = rank;
        request.Talents.RestorationPointsSpent = rank;
        var before = JsonSerializer.Serialize(request);
        var run = new ElementalShamanSimRunner().Run(request);
        Assert.Equal(new[] { 0m, 6m }, Events(run, CombatEventType.AbilityCastCompleted, "earth-shock")
            .Select(e => e.TimeSeconds));
        // Two 5-second suppression windows; two unsuppressed seconds. Neither
        // resource cap nor starvation can hide the per-rank difference here.
        var expected = run.MaximumMana * .4m - 900m +
            10m * 10m * CastingFractions[rank] + 2m * 10m + 12m * 5m;
        Assert.Equal(expected, run.EndingMana);
        Assert.Equal(expected, run.Run.Summary.ActorSummaries["shaman"].EndingResources["mana"]);
        Assert.Equal(before, JsonSerializer.Serialize(request));
        Assert.Equal(Outcomes(run), Outcomes(new ElementalShamanSimRunner().Run(request)));
        request.Tuning.ManaRegenPerSecond = 0m;
        var mp5Only = new ElementalShamanSimRunner().Run(request);
        Assert.Equal(mp5Only.MaximumMana * .4m - 900m + 60m, mp5Only.EndingMana);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(2, 3)]
    public async Task LegalCrossTreeSelectionsSurviveStrictDispatchWithoutRequestMutation(int trial, int mindfulness)
    {
        var request = Request();
        request.Character.Race = "tauren";
        request.UseProvisionalLevel60Baseline = true;
        request.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "manual-shaman-budget",
            Stats = new() { Values = new() { ["intellect"] = 1000m, ["spell-power"] = 200m } } });
        request.Talents = new()
        {
            ElementalPointsSpent = 15 + trial, RestorationPointsSpent = 5 + mindfulness,
            EnhancementPointsSpent = 0,
            Ranks = new() { ["convection"] = 5, ["concussion"] = 5, ["reverberation"] = 5,
                ["trial-by-fire"] = trial, ["tidal-focus"] = 5, ["mindfulness"] = mindfulness }
        };
        var envelope = new ForeverLevel60SimulationRequest { ClassKey = "shaman", Specialization = "elemental",
            ItemizedTalentMode = true, ItemizedTalentRanks = new(request.Talents.Ranks), Elemental = request };
        var original = JsonSerializer.Serialize(envelope);
        var controller = new ForeverLevel60SimulationController();
        Assert.IsType<OkObjectResult>(await controller.Simulate(envelope, default));
        Assert.IsType<OkObjectResult>(await controller.Simulate(
            JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(original)!, default));
        Assert.Equal(original, JsonSerializer.Serialize(envelope));
        envelope.ItemizedTalentRanks["trial-by-fire"] = 3;
        request.Talents.Ranks["trial-by-fire"] = 3;
        request.Talents.ElementalPointsSpent = 18;
        Assert.IsType<BadRequestObjectResult>(await controller.Simulate(envelope, default));
    }

    private static string Outcomes(ElementalShamanSimResponse response) =>
        JsonSerializer.Serialize(new
        {
            response.DamageDone, response.EndingMana, response.DamageByAbility,
            Events = response.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.SourceActorKey,
                e.TargetActorKey, e.AbilityKey, e.EffectKey, e.Amount, e.RawAmount, e.ResultKey,
                e.EffectDeliveryType, e.ResourceKey })
        });
}
