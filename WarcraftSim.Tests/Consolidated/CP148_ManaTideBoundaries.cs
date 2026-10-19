using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp148ManaTideBoundaryTests
{
    [Fact]
    public void FourManaTidePulsesIncludeTheExpirationBoundary()
    {
        var result = RunScenario(replaceAtSeconds: null);
        Assert.Equal(400m, result.Restored);
        Assert.Equal(400m, result.Mana);
    }

    [Fact]
    public void ReplacingWaterTotemCancelsRemainingManaTidePulses()
    {
        var result = RunScenario(replaceAtSeconds: 7m);
        Assert.Equal(200m, result.Restored);
        Assert.Equal(200m, result.Mana);
    }

    private static (decimal Restored, decimal Mana) RunScenario(decimal? replaceAtSeconds)
    {
        var caster = new SimulationActorState {
            Key = "shaman", Name = "shaman", TeamKey = "raid", Level = 60,
            MaximumHealth = 4000m
        };
        caster.InitializeHealth(4000m);
        caster.AddResource(new ResourceState("mana", 1000m, 0m));
        var ctx = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 20m, PrimaryActorKey = caster.Key,
            CaptureTimeline = true
        });
        ctx.AddActor(caster);
        var auras = new AuraManager();
        var effects = new RestorationShamanCombatProcessor(caster.Key,
            new ForeverRestorationShamanTuning { ManaTideTickPercent = 10m },
            auras, new ForeverRestorationShamanTalents());
        var processors = new List<ICombatEventProcessor> { auras, effects };
        if (replaceAtSeconds.HasValue)
            processors.Add(new WaterSlotReplacement(auras, caster, replaceAtSeconds.Value));
        new SimulationEngine(processors).Run(ctx, started =>
            auras.ApplyAura(started, caster, new AuraDefinition {
                Key = "mana-tide-active", Name = "Mana Tide", DurationSeconds = 12m
            }, caster.Key, "mana-tide-totem", "mana-tide-aura"));
        return (effects.ManaTideRestored, caster.Resources["mana"].Current);
    }

    private sealed class WaterSlotReplacement(AuraManager auras, SimulationActorState shaman,
        decimal replaceAtSeconds) : ICombatEventProcessor
    {
        public void Process(SimulationContext ctx, CombatEvent ev)
        {
            if (ev.Type == CombatEventType.SimulationStarted)
                ctx.ScheduleEvent(new CombatEvent {
                    Type = CombatEventType.RotationDecision,
                    TimeSeconds = replaceAtSeconds, SourceActorKey = shaman.Key,
                    EffectKey = "cp148-replace-water", IsInternal = true
                });
            if (ev.Type == CombatEventType.RotationDecision &&
                ev.EffectKey == "cp148-replace-water")
                auras.RemoveAuras(ctx, shaman, "mana-tide-active", shaman.Key,
                    "water slot replaced");
        }
    }
}
