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
        decimal targetHealth, decimal startingMana, CancellationToken token,
        ForeverLevel60BaselineObservation? baseline=null,decimal intellectMultiplier=1m)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Level != 60 || !string.Equals(profile.ClassKey,classKey,StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profile.SpecializationKey,specialization,StringComparison.OrdinalIgnoreCase) ||
            duration is <= 0m or > 1200m || health <= 0m || targetHealth <= 0m ||
            startingMana is < 0m or > 100m)
            throw new ArgumentException($"Invalid level-60 {classKey} {specialization} preview configuration.");
        if(baseline is not null)
            profile=CasterSimulationRequestSnapshot.GenerateBaseline(profile,baseline);
        var stats=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>health)
            .Calculate(profile).EffectiveStats;
        var intellect=Math.Max(0m,stats.Get("intellect"));
        var mana=1376m+Math.Max(0m,intellect-20m)*15m;
        if(baseline is not null)
        {
            // Apply the class multiplier exactly once, after raw contributions,
            // before both resource initialization and the context's report snapshot.
            intellect*=intellectMultiplier;
            stats.Set("intellect",intellect);
            mana=(baseline.ObservedUnbuffedMaximumMana is { } observedMana
                ? observedMana+15m*(intellect-baseline.Intellect)
                : ForeverClientBaseManaCatalog.GetBaseMana(classKey,60)+
                    Math.Max(0m,intellect-20m)*15m)+stats.Get("mana");
            // Without observed Health, health remains the manual scenario cap,
            // not a sourced naked HP value. Only post-baseline deltas are added.
            health=(baseline.ObservedUnbuffedMaximumHealth??health)+
                10m*(stats.Get("stamina")-baseline.Stamina)+stats.Get("health");
            if(mana<=0m || health<=0m)
                throw new ArgumentException("Prepared caster maximum Mana and Health must be positive.");
        }
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

    // Normalize only the isolated prepared request. Never sum duplicate inventory
    // aliases, and never instantiate the legacy potion processor on this path.
    // Positive shared inventory keeps its threshold, including equal aliases.
    // Copy the legacy threshold only when adapting legacy-only inventory.
    public static void PrepareCombatItems(CombatUseItemsOptions items,int legacyCharges,
        decimal legacyThreshold)
    {
        if(legacyCharges is <0 or >20 || legacyThreshold is <0m or >100m)
            throw new ArgumentException("Invalid Mana Potion charges or threshold.");
        if(legacyCharges<=0)return;
        if(items.MajorManaPotionCharges>0 && items.MajorManaPotionCharges!=legacyCharges)
            throw new ArgumentException("Conflicting Major Mana Potion inventories.");
        if(items.MajorManaPotionCharges==0)
        {
            items.MajorManaPotionCharges=legacyCharges;
            items.MajorManaPotionBelowManaPercent=legacyThreshold;
        }
    }

    public static IEnumerable<string> BaselineModelingNotes(
        ForeverLevel60BaselineObservation? baseline,string classKey)
    {
        if(baseline is null)return [];
        return [
            $"Prepared level-60 baseline: {baseline.VerificationStatus}; {baseline.SourceKind}; build {baseline.ClientBuild}; source {baseline.SourceReference}. Metadata does not independently certify an observation.",
            baseline.ObservedUnbuffedMaximumMana is not null
                ? "Maximum Mana uses the supplied naked observation plus post-baseline Intellect and flat Mana contributions."
                : $"Base Mana {ForeverClientBaseManaCatalog.GetBaseMana(classKey,60)} is directly sourced from PlayerExpectedStat ContentSetID=0, build {ForeverClientBaseManaCatalog.SourceBuild}, SHA-256 {ForeverClientBaseManaCatalog.SourceSha256}; it is not maximum Mana.",
            baseline.ObservedUnbuffedMaximumHealth is not null
                ? "Maximum Health uses the supplied naked observation plus post-baseline Stamina and flat Health contributions."
                : "No naked maximum Health observation: MaximumHealth is a manual scenario cap, not observed or source-verified Health; post-baseline Stamina and flat Health contributions are added.",
            "The existing 15 Mana per post-baseline Intellect and 10 Health per post-baseline Stamina conversions remain provisional. Historical-provisional primary attributes are not Forever-verified. Racial passives and equipment-ID resolution are not added by this preparation step.",
            "Prepared combat items use one shared inventory and scheduler; conflicting positive legacy/shared potion quantities are rejected. A selected shared inventory keeps its threshold; legacy-only inventory copies the legacy threshold."
        ];
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
