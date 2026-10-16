using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>Shared class-neutral scaffolding for level-60 solo caster DPS previews.
/// Keeps new class runners on the same core event queue, player-profile stat pipeline,
/// combat-use items and reporting model as Mage/Warlock.</summary>
internal static class ForeverCasterSoloSupport
{
    public static (SimulationContext Context, SimulationActorState Actor, SimulationActorState Target,
        decimal SpellPower, decimal Mana) Create(CharacterProfile profile, string classKey,
        string specialization, int seed, decimal duration, bool capture, decimal health,
        decimal targetHealth, decimal startingMana, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Level != 60 || !string.Equals(profile.ClassKey,classKey,StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profile.SpecializationKey,specialization,StringComparison.OrdinalIgnoreCase) ||
            duration is <= 0m or > 1200m || health <= 0m || targetHealth <= 0m ||
            startingMana is < 0m or > 100m)
            throw new ArgumentException($"Invalid level-60 {classKey} {specialization} preview configuration.");
        var stats=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>health)
            .Calculate(profile).EffectiveStats;
        var intellect=Math.Max(0m,stats.Get("intellect"));
        var mana=1376m+Math.Max(0m,intellect-20m)*15m;
        var context=new SimulationContext(new SimulationRunOptions
        {
            Seed=seed,DurationSeconds=duration,PrimaryActorKey=classKey,
            CaptureTimeline=capture,MaximumProcessedEvents=60000,CancellationToken=token
        });
        var actor=new SimulationActorState
        {
            Key=classKey,Name=profile.Name,TeamKey="raid",Level=60,MaximumHealth=health,Stats=stats
        };
        actor.InitializeHealth(health);
        actor.AddResource(new ResourceState("mana",mana,mana*startingMana/100m));
        var boss=new SimulationActorState
        {
            Key="target",Name=$"Level 63 {classKey} training boss",TeamKey="enemy",
            Level=63,MaximumHealth=targetHealth
        };
        boss.InitializeHealth(targetHealth);
        context.AddActor(actor);context.AddActor(boss);
        return (context,actor,boss,Math.Max(0m,stats.Get("spell-power")),mana);
    }

    public static void ValidatePercent(decimal value,string name)
    {
        if(value is < 0m or > 100m)throw new ArgumentException($"{name} must be between 0 and 100.");
    }

    public sealed class SpellRolls(decimal hit,decimal crit,decimal criticalMultiplier) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect)=>
            Resolve(context,src,dst,ability,effect,CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if(effect.CanMiss && (decimal)context.Random.NextDouble()*100m>=
                Math.Min(100m,hit+src.Stats.Get("spell-hit-percent")))return CombatRollResult.Miss();
            if(effect.CanCrit && (decimal)context.Random.NextDouble()*100m<
                Math.Min(100m,crit+effect.BonusCriticalChancePercent))
                return CombatRollResult.Critical(criticalMultiplier);
            return CombatRollResult.Hit();
        }
    }
    public sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState dst,AbilityDefinition ability,AbilityEffectDefinition effect,
            decimal raw)=>DamageMitigationResult.Unmitigated(raw);
    }
}
