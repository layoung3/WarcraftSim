using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP138: adaptive level-60 Moonkin DPS with Eclipse, Nature's Grace and
/// baseline Omen, using the common event-driven resource and item pipelines.</summary>
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
        ForeverCasterSoloSupport.ValidatePercent(request.OmenProcChancePercent,"Omen proc chance");
        var t=request.Tuning;
        if(new[]{t.WrathDamage,t.WrathMana,t.StarfireDamage,t.StarfireMana,
            t.MoonfireDirectDamage,t.MoonfireTickDamage,t.MoonfireMana,
            t.InsectSwarmTickDamage,t.InsectSwarmMana,t.HurricaneTickDamage,
            t.HurricaneMana,t.ManaRegenPerSecond,
            t.EclipseCastReductionPerRank}.Any(v=>v<0m))
            throw new ArgumentException("Balance damage, costs and timing must be nonnegative.");
        var (ctx,actor,boss,sp,maxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "druid","balance",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken);
        foreach(var spell in ForeverBalanceDruidSpellbook.Create(request.Talents,t,sp))actor.AddAbility(spell);
        var rotation=BalanceAdaptiveRotation.Build(request);
        var state=new BalanceState(request,rotation,actor,sp);
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(request.BaseSpellHitPercent+
            request.Talents.Rank("natures-reach")*2m+
            actor.Stats.Get("spell-hit-chance-percent"),
            request.SpellCritPercent+request.Talents.Rank("natures-majesty")*2m+
            (request.Talents.Rank("moonkin-form")>0?3m:0m)+
            actor.Stats.Get("spell-critical-chance-percent"),
            1.5m+request.Talents.Rank("vengeance")*.2m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated(),
            abilityTimingProvider:state);
        var potion=new CasterManaPotionProcessor(actor,executor,request.MajorManaPotionCharges,
            request.MajorManaPotionBelowManaPercent);
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","druid");
        var priority=new PriorityRotationExecutor(rotation,"druid","target",executor,true);
        var regen=new FiveSecondManaRegenerationProcessor("druid",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),request.Talents.Rank("reflection")*.5m/3m);
        var engine=new SimulationEngine([executor,aura,regen,state,items,potion,priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx);
        var report=run.Summary.ActorSummaries["druid"];
        return new BalanceDruidSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            ResolvedRotation=string.Join(" > ",rotation.Entries.Select(e=>e.AbilityKey)),
            EclipseChargesGenerated=state.ChargesGenerated,
            EclipseEmpoweredStarfires=state.EmpoweredStarfires,
            MaximumEclipseCharges=state.MaxCharges,
            OmenProcs=state.OmenProcs,OmenConsumed=state.OmenConsumed,
            NaturesGraceProcs=state.NaturesGraceProcs,
            MajorManaPotionUses=potion.Uses,
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                "CP138: Forever level-60 rank defaults: Wrath R8 91/120, Starfire R7 381/340, Moonfire R10 135+60/375, Insect Swarm R5 186/160.",
                "Improved Wrath and Moonglow reduce spell costs; Improved Starfire reduces cast time. Nature's Reach gives 2% hit/rank.",
                "Nature's Grace applies 10% faster spells/GCD for 3s after direct crit. Baseline Omen procs are approximated with a configurable chance and 10s cooldown; Wrath cannot consume them.",
                "Eclipse grants two charges on completed Wrath, capped at four, for 15s. Each empowered Starfire consumes one charge.",
                "Default priority maintains DoTs only when enough time remains and considers Eclipse, free-cast procs, expected throughput, Mana and encounter length. CustomPriority overrides it.",
                "Profiles represent provisional gear/enchants and assumed aggregate budgets. Trinket use, raid buffs, boss debuffs, moving targets and timed adds are not yet resolved by this solo runner.",
                "Hurricane is a ten-second channeled Nature spell with no cooldown (verified); its level-60 damage and Mana are provisional.",
                "Periodic hit/crit resolution is still based on the generic engine. The Omen proc rate and rank-60 spell-power scaling remain provisional."
            ]
        };
    }

    private sealed class BalanceState(BalanceDruidSimRequest r,RotationProfile rotation,
        SimulationActorState actor,decimal spellPower):ICombatEventProcessor,IAbilityTimingProvider
    {
        private readonly Dictionary<string,decimal> _baseCosts=actor.Abilities
            .Where(p=>p.Value.Definition.ResourceCosts.Count>0)
            .ToDictionary(p=>p.Key,p=>p.Value.Definition.ResourceCosts[0].Amount,StringComparer.OrdinalIgnoreCase);
        private int _charges;
        private decimal _expires;
        private decimal _graceExpires;
        private decimal _nextOmenEligible;
        private bool _omen;
        private string? _freeCast;
        public int ChargesGenerated{get;private set;}
        public int EmpoweredStarfires{get;private set;}
        public int MaxCharges{get;private set;}
        public int OmenProcs{get;private set;}
        public int OmenConsumed{get;private set;}
        public int NaturesGraceProcs{get;private set;}
        public AbilityTimingSnapshot Resolve(SimulationContext context,SimulationActorState source,
            AbilityDefinition ability)
        {
            var stacks=EffectiveCharges(context);
            var reduction=ability.Key=="starfire"&&r.UseEclipse&&r.Talents.Rank("eclipse")>0&&stacks>0
                ?r.Tuning.EclipseCastReductionPerRank*r.Talents.Rank("eclipse"):0m;
            var grace=(r.Talents.Rank("natures-grace")>0&&context.CurrentTimeSeconds<_graceExpires) ? 0.9m : 1m;
            return new AbilityTimingSnapshot
            {
                CastTimeSeconds=Math.Max(0m,Math.Max(1m,ability.CastTimeSeconds-reduction)*grace),
                GlobalCooldownSeconds=Math.Max(1m,ability.GlobalCooldownSeconds*grace),
                ChannelDurationSeconds=ability.ChannelDurationSeconds,
                ChannelTickIntervalSeconds=ability.ChannelTickIntervalSeconds
            };
        }
        private int EffectiveCharges(SimulationContext context)
        {
            if(_charges>0&&context.CurrentTimeSeconds>=_expires)_charges=0;
            return _charges;
        }
        private bool CanAffordStarfireOverRemainingFight(SimulationContext ctx)
        {
            if(!r.ConserveManaForEncounter||_omen)return true;
            if(!actor.Resources.TryGetValue("mana",out var mana))return true;
            var remaining=Math.Max(0m,r.DurationSeconds-ctx.CurrentTimeSeconds);
            if(remaining<=12m)return true;
            var wrathCost=_baseCosts.GetValueOrDefault("wrath");
            var starfireCost=_baseCosts.GetValueOrDefault("starfire");
            var wrathCast=Math.Max(1.5m,2m-r.Talents.Rank("improved-wrath")*.1m);
            var regen=r.Tuning.ManaRegenPerSecond+Math.Max(0m,actor.Stats.Get("mp5"))/5m;
            // Reserve a modest horizon of cheap Wrath casts; do not reserve the whole
            // 300s fight, which would incorrectly refuse all profitable Starfires.
            var horizon=Math.Min(35m,remaining);
            var expectedFillerCost=horizon/wrathCast*wrathCost;
            var projectedMana=mana.Current+horizon*regen;
            return projectedMana-expectedFillerCost>=Math.Max(0m,starfireCost-wrathCost);
        }
        private bool ShouldUseStarfire(SimulationContext ctx)
        {
            if(!r.UseStarfire)return false;
            if(r.CustomPriority.Count>0)return true;
            var stacks=EffectiveCharges(ctx);
            if(r.StarfireOnlyWithEclipse && (!r.UseEclipse||r.Talents.Rank("eclipse")==0||stacks==0))
                return false;
            if(r.RotationPolicy=="legacy")return CanAffordStarfireOverRemainingFight(ctx);
            if(!CanAffordStarfireOverRemainingFight(ctx))return false;
            var wrathDamage=(r.Tuning.WrathDamage+spellPower*.57m);
            var starfireDamage=(r.Tuning.StarfireDamage+spellPower);
            var wrathTime=Math.Max(1.5m,2m-r.Talents.Rank("improved-wrath")*.1m);
            var starfireTime=Math.Max(1.5m,3.5m-r.Talents.Rank("improved-starfire")*.1m-
                (stacks>0?r.Tuning.EclipseCastReductionPerRank*r.Talents.Rank("eclipse"):0m));
            // If the ability is not throughput-positive, use Wrath to save Mana;
            // still use a free Omen Starfire even if the difference is small.
            return _omen||starfireDamage/starfireTime>=wrathDamage/wrathTime*1.01m;
        }
        private void RefreshRotation(SimulationContext ctx)
        {
            if(r.CustomPriority.Count>0)return;
            foreach(var entry in rotation.Entries)
            {
                switch(entry.AbilityKey)
                {
                    case "starfire":entry.IsEnabled=ShouldUseStarfire(ctx);break;
                    case "moonfire":
                        entry.IsEnabled=r.UseMoonfire&&r.DurationSeconds-ctx.CurrentTimeSeconds>=3m;
                        break;
                    case "insect-swarm":
                        entry.IsEnabled=r.UseInsectSwarm&&r.DurationSeconds-ctx.CurrentTimeSeconds>=3m;
                        break;
                }
            }
        }
        private void DiscountOmen()
        {
            foreach(var (key,cost) in _baseCosts)
                if(key!="wrath"&&actor.Abilities.TryGetValue(key,out var ability)&&
                    ability.Definition.ResourceCosts.Count>0)
                    ability.Definition.ResourceCosts[0].Amount=0m;
        }
        private void RestoreCosts()
        {
            foreach(var (key,cost) in _baseCosts)
                if(actor.Abilities.TryGetValue(key,out var ability)&&
                    ability.Definition.ResourceCosts.Count>0)
                    ability.Definition.ResourceCosts[0].Amount=cost;
        }
        public void Process(SimulationContext ctx,CombatEvent ev)
        {
            EffectiveCharges(ctx);
            if(ev.SourceActorKey=="druid")
            {
                if(ev.Type==CombatEventType.AbilityCastStarted)
                {
                    if(ev.AbilityKey=="starfire"&&_charges>0)
                    {
                        _charges--;EmpoweredStarfires++;
                    }
                    if(_omen&&ev.AbilityKey is not null&&ev.AbilityKey!="wrath"&&
                        _baseCosts.ContainsKey(ev.AbilityKey))
                    {
                        _omen=false;_freeCast=ev.AbilityKey;OmenConsumed++;
                    }
                }
                if(ev.Type==CombatEventType.AbilityCastCompleted)
                {
                    if(ev.AbilityKey=="wrath"&&r.UseEclipse&&r.Talents.Rank("eclipse")>0)
                    {
                        var before=_charges;_charges=Math.Min(4,_charges+2);
                        ChargesGenerated+=_charges-before;MaxCharges=Math.Max(MaxCharges,_charges);
                        _expires=ctx.CurrentTimeSeconds+15m;
                    }
                    if(_freeCast==ev.AbilityKey)
                    {
                        _freeCast=null;RestoreCosts();
                    }
                }
                if(ev.Type==CombatEventType.Damage&&!ev.IsPeriodic&&!ev.IsChannelTick&&
                    (ev.ResultKey is "hit" or "critical"))
                {
                    if(ev.ResultKey=="critical"&&r.Talents.Rank("natures-grace")>0)
                    {
                        _graceExpires=ctx.CurrentTimeSeconds+3m;NaturesGraceProcs++;
                    }
                    if(r.UseOmenOfClarity&&!_omen&&ctx.CurrentTimeSeconds>=_nextOmenEligible&&
                        (ev.AbilityKey is "wrath" or "starfire" or "moonfire")&&
                        (decimal)ctx.Random.NextDouble()*100m<r.OmenProcChancePercent*
                        (r.Talents.Rank("moonkin-form")>0?2m:1m))
                    {
                        _omen=true;OmenProcs++;_nextOmenEligible=ctx.CurrentTimeSeconds+10m;
                        DiscountOmen();
                    }
                }
            }
            if(ev.Type==CombatEventType.RotationDecision||ev.Type is
                CombatEventType.AbilityCastStarted or CombatEventType.AbilityCastCompleted)
                RefreshRotation(ctx);
        }
    }
}
