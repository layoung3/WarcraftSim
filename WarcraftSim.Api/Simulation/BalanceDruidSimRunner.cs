using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP134 Balance Druid single-target Moonkin caster with Forever Eclipse.</summary>
public sealed class BalanceDruidSimRunner
{
    public BalanceDruidSimResponse Run(BalanceDruidSimRequest request,
        CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.Talents is null||request.Tuning is null||request.CombatItems is null||request.Character is null)
            throw new ArgumentException("Balance requires talents, tuning, items and a character.");
        request.Talents.Validate();request.CombatItems.Validate("druid");
        ForeverCasterSoloSupport.ValidatePercent(request.BaseSpellHitPercent,"Spell hit");
        ForeverCasterSoloSupport.ValidatePercent(request.SpellCritPercent,"Spell crit");
        var t=request.Tuning;
        if(new[]{t.WrathDamage,t.WrathMana,t.StarfireDamage,t.StarfireMana,
            t.MoonfireDirectDamage,t.MoonfireTickDamage,t.MoonfireMana,
            t.InsectSwarmTickDamage,t.InsectSwarmMana,t.ManaRegenPerSecond,
            t.EclipseCastReductionPerRank}.Any(v=>v<0m))
            throw new ArgumentException("Balance damage, costs and timing must be nonnegative.");
        var (ctx,actor,boss,sp,maxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "druid","balance",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken);
        foreach(var spell in ForeverBalanceDruidSpellbook.Create(request.Talents,t,sp))actor.AddAbility(spell);
        var rotation=new RotationProfile{Name="CP134 Balance",ClassKey="druid",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        var p=0;
        if(request.UseMoonfire)rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="moonfire",
            Conditions=[new RotationConditionDefinition{ConditionType=RotationConditionTypes.TargetAuraMissing,Key="moonfire"}]});
        if(request.UseInsectSwarm&&request.Talents.Rank("insect-swarm")>0)
            rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="insect-swarm",
                Conditions=[new RotationConditionDefinition{ConditionType=RotationConditionTypes.TargetAuraMissing,Key="insect-swarm"}]});
        if(request.UseStarfire)rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="starfire"});
        rotation.Entries.Add(new RotationEntry{Priority=p++,AbilityKey="wrath"});
        var state=new BalanceState(request,rotation);
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(request.BaseSpellHitPercent+
            request.Talents.Rank("natures-reach"),request.SpellCritPercent+
            request.Talents.Rank("natures-majesty")*2m+
            (request.Talents.Rank("moonkin-form")>0?3m:0m),
            1.5m+request.Talents.Rank("vengeance")*.1m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated(),
            abilityTimingProvider:state);
        var potion=new CasterManaPotionProcessor(actor,executor,request.MajorManaPotionCharges,
            request.MajorManaPotionBelowManaPercent);
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","druid");
        var priority=new PriorityRotationExecutor(rotation,"druid","target",executor,true);
        var regen=new FiveSecondManaRegenerationProcessor("druid",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),0m);
        var engine=new SimulationEngine([executor,aura,regen,state,items,potion,priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx);
        var report=run.Summary.ActorSummaries["druid"];
        return new BalanceDruidSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            EclipseChargesGenerated=state.ChargesGenerated,
            EclipseEmpoweredStarfires=state.EmpoweredStarfires,
            MaximumEclipseCharges=state.MaxCharges,
            MajorManaPotionUses=potion.Uses,
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                "CP134: Wrath adds two Eclipse charges (maximum four); each Starfire consumes one and gets the talented cast reduction. Charges expire after 15 seconds.",
                "Moonfire and Insect Swarm are maintained through real periodic auras. Wrath has Forever's cheaper Mana cost; Starfire trades higher resource cost for damage.",
                "Moonkin Form's personal critical chance is modeled. Party aura application, Omen of Clarity and Nature's Grace require a later raid/proc integration.",
                "Selected talent effects are functional. Spell ranks, spell power coefficients, and precise Nature's Splendor duration are provisional.",
                "This is a single-target level-60 preview; AoE, shapeshift combat items and real healer interactions are deferred."
            ]
        };
    }
    private sealed class BalanceState(BalanceDruidSimRequest r,RotationProfile rotation):
        ICombatEventProcessor,IAbilityTimingProvider
    {
        private int _charges;
        private decimal _expires;
        public int ChargesGenerated{get;private set;}
        public int EmpoweredStarfires{get;private set;}
        public int MaxCharges{get;private set;}
        public AbilityTimingSnapshot Resolve(SimulationContext context,SimulationActorState source,AbilityDefinition ability)
        {
            var stacks=EffectiveCharges(context);
            var reduction=ability.Key=="starfire"&&r.UseEclipse&&r.Talents.Rank("eclipse")>0&&stacks>0
                ? r.Tuning.EclipseCastReductionPerRank*r.Talents.Rank("eclipse"):0m;
            return new AbilityTimingSnapshot
            {
                CastTimeSeconds=Math.Max(1.5m,ability.CastTimeSeconds-reduction),
                GlobalCooldownSeconds=ability.GlobalCooldownSeconds,
                ChannelDurationSeconds=ability.ChannelDurationSeconds,
                ChannelTickIntervalSeconds=ability.ChannelTickIntervalSeconds
            };
        }
        private int EffectiveCharges(SimulationContext context)
        {
            if(_charges>0&&context.CurrentTimeSeconds>=_expires)_charges=0;
            return _charges;
        }
        public void Process(SimulationContext context,CombatEvent ev)
        {
            EffectiveCharges(context);
            if(ev.SourceActorKey=="druid")
            {
                if(ev.Type==CombatEventType.AbilityCastCompleted&&ev.AbilityKey=="wrath"&&
                    r.UseEclipse&&r.Talents.Rank("eclipse")>0)
                {
                    var before=_charges;_charges=Math.Min(4,_charges+2);
                    ChargesGenerated+=_charges-before;
                    MaxCharges=Math.Max(MaxCharges,_charges);
                    _expires=context.CurrentTimeSeconds+15m;
                }
                else if(ev.Type==CombatEventType.AbilityCastStarted&&ev.AbilityKey=="starfire"&&_charges>0)
                {
                    _charges--;EmpoweredStarfires++;
                }
            }
            if(ev.Type==CombatEventType.RotationDecision||ev.Type is
                CombatEventType.AbilityCastStarted or CombatEventType.AbilityCastCompleted)
            {
                foreach(var entry in rotation.Entries)
                    if(entry.AbilityKey=="starfire")
                        entry.IsEnabled=r.UseStarfire&&(!r.StarfireOnlyWithEclipse||
                            r.UseEclipse&&r.Talents.Rank("eclipse")>0&&_charges>0);
            }
        }
    }
}
