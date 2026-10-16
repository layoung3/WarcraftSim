using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP133 Elemental Shaman single-target, full event-driven caster DPS preview.</summary>
public sealed class ElementalShamanSimRunner
{
    public ElementalShamanSimResponse Run(ElementalShamanSimRequest request,
        CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.Talents is null||request.Tuning is null||request.CombatItems is null||request.Character is null)
            throw new ArgumentException("Elemental requires talents, tuning, items and a character.");
        request.Talents.Validate();request.CombatItems.Validate("shaman");
        ForeverCasterSoloSupport.ValidatePercent(request.BaseSpellHitPercent,"Spell hit");
        ForeverCasterSoloSupport.ValidatePercent(request.SpellCritPercent,"Spell crit");
        var t=request.Tuning;
        if(new[]{t.LightningBoltDamage,t.LightningBoltMana,t.ChainLightningDamage,t.ChainLightningMana,
            t.FlameShockDirectDamage,t.FlameShockTickDamage,t.FlameShockMana,t.LavaBurstDamage,
            t.LavaBurstMana,t.ManaRegenPerSecond,t.LavaBurstFlameShockBonusPercent,
            t.LightningOverloadChancePercentRank1,t.LightningOverloadChancePercentRank2,
            t.LightningOverloadChancePercentRank3}.Any(v=>v<0m)||
            t.LightningOverloadChancePercentRank3>100m||t.LightningOverloadChancePercentRank2>100m||
            t.LightningOverloadChancePercentRank1>100m)
            throw new ArgumentException("Elemental damage, costs, regen and proc chance must be nonnegative.");
        var (ctx,actor,boss,sp,maxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "shaman","elemental",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken);
        foreach(var ability in ForeverElementalShamanSpellbook.Create(request.Talents,t,sp))actor.AddAbility(ability);
        var rotation=new RotationProfile{Name="CP133 Elemental",ClassKey="shaman",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        var p=0;
        if(request.UseFlameShock)rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="flame-shock",
            Conditions=[new RotationConditionDefinition{ConditionType=RotationConditionTypes.TargetAuraMissing,
                Key="flame-shock"}]});
        if(request.UseLavaBurst&&request.Talents.Rank("lava-burst")>0)
            rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="lava-burst"});
        if(request.UseChainLightning)rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="chain-lightning"});
        rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="lightning-bolt"});
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(request.BaseSpellHitPercent,
            request.SpellCritPercent+request.Talents.Rank("call-of-thunder"),
            1.5m+request.Talents.Rank("elemental-fury")*.1m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated());
        var state=new ElementalState(request,actor,executor);
        var potion=new CasterManaPotionProcessor(actor,executor,request.MajorManaPotionCharges,
            request.MajorManaPotionBelowManaPercent);
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","shaman");
        var priority=new PriorityRotationExecutor(rotation,"shaman","target",executor,true);
        var regen=new FiveSecondManaRegenerationProcessor("shaman",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),0m);
        var engine=new SimulationEngine([executor,aura,regen,state,items,potion,priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx);
        var report=run.Summary.ActorSummaries["shaman"];
        return new ElementalShamanSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            LightningOverloadProcs=state.LightningOverloadProcs,
            LavaBurstFlameShockBuffed=state.BuffedLavaBursts,
            MajorManaPotionUses=potion.Uses,
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                "CP133: Flame Shock is maintained; Lava Burst is a 2.5-second, 10-second-cooldown spell with +20% damage when Flame Shock is active.",
                "Lightning Overload procs produce separate half-damage zero-Mana casts. Single-target Chain Lightning only; overload threat is irrelevant in this target dummy.",
                "Elemental Mastery is intentionally not present: removed from Forever Elemental talents.",
                "Selected talent effects are modeled. Elemental Focus, totems, mana returns and full rank 60 spell coefficients require later calibration.",
                "One seeded run is not a fair spec comparison. Exact target resistances, spell ranks and raid buffs remain configurable assumptions."]
        };
    }
    private sealed class ElementalState(ElementalShamanSimRequest request,
        SimulationActorState actor,AbilityExecutor executor):ICombatEventProcessor
    {
        private readonly decimal _lavaBase=actor.Abilities.TryGetValue("lava-burst",out var lava)
            ? lava.Definition.Effects[0].DamageMultiplier:1m;
        public int LightningOverloadProcs{get;private set;}
        public int BuffedLavaBursts{get;private set;}
        public void Process(SimulationContext ctx,CombatEvent ev)
        {
            if(!string.Equals(ev.SourceActorKey,"shaman",StringComparison.OrdinalIgnoreCase))return;
            if(ev.Type==CombatEventType.AbilityCastStarted&&ev.AbilityKey=="lava-burst"&&
                actor.Abilities.TryGetValue("lava-burst",out var lava))
            {
                var target=ctx.GetActor("target");
                var flame=target?.ActiveAuras.Any(a=>a.Definition.Key=="flame-shock"&&
                    a.SourceActorKey=="shaman"&&a.ExpiresAtSeconds>ctx.CurrentTimeSeconds)??false;
                lava.Definition.Effects[0].DamageMultiplier=_lavaBase*(flame?
                    1m+request.Tuning.LavaBurstFlameShockBonusPercent/100m:1m);
                if(flame)BuffedLavaBursts++;
            }
            if(ev.Type!=CombatEventType.Damage||ev.ResultKey is not ("hit" or "critical")||
                ev.IsPeriodic||ev.IsChannelTick||!request.UseLightningOverload||
                request.Talents.Rank("lightning-overload")==0||
                ev.AbilityKey is not ("lightning-bolt" or "chain-lightning"))return;
            var chance=request.Talents.Rank("lightning-overload") switch
            {
                1=>request.Tuning.LightningOverloadChancePercentRank1,
                2=>request.Tuning.LightningOverloadChancePercentRank2,
                _=>request.Tuning.LightningOverloadChancePercentRank3
            };
            if((decimal)ctx.Random.NextDouble()*100m>=chance)return;
            var key=ev.AbilityKey=="lightning-bolt"?"lightning-overload-bolt":"lightning-overload-chain";
            if(executor.TryStartAbility(ctx,"shaman","target",key).Success)LightningOverloadProcs++;
        }
    }
}
