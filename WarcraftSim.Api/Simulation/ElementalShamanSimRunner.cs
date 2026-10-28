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
        var (ctx,actor,boss,sp,baseMaxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "shaman","elemental",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken);
        // CP137.1: Ancestral Knowledge increases total Intellect by 2% per rank.
        // Apply it to the already-computed equipment/buff stats, before the fight starts,
        // then recalculate maximum and starting Mana. Do not mutate the request's
        // CharacterProfile or change the shared caster factory used by other classes.
        var maxMana=baseMaxMana;
        var ancestralKnowledgeRank=request.Talents.Rank("ancestral-knowledge");
        if (ancestralKnowledgeRank>0)
        {
            var intellect=Math.Max(0m,actor.Stats.Get("intellect")) *
                (1m+ancestralKnowledgeRank*.02m);
            actor.Stats.Set("intellect",intellect);
            maxMana=1376m+Math.Max(0m,intellect-20m)*15m;
            var manaResource=actor.Resources["mana"];
            manaResource.Maximum=maxMana;
            manaResource.SetCurrent(maxMana*request.StartingManaPercent/100m);
            // The context snapshots starting resources when it adds an actor,
            // before this Elemental-specific talent adjustment. Keep the report
            // consistent with the corrected precombat starting resource.
            ctx.Summary.ActorSummaries["shaman"].StartingResources["mana"]=manaResource.Current;
        }
        foreach(var ability in ForeverElementalShamanSpellbook.Create(request.Talents,t,sp))actor.AddAbility(ability);
        if (request.TargetCount is < 1 or > 3 || request.CustomPriority is null)
            throw new ArgumentException("Elemental requires 1-3 targets and a non-null custom priority.");
        if (request.Talents.Rank("lightning-overload")>0 &&
            request.Talents.Rank("elemental-alacrity")<3)
            throw new ArgumentException("The Oct 16 Lightning Overload requires Elemental Alacrity 3/3.");
        for (var i=2;i<=request.TargetCount;i++)
        {
            var add=new SimulationActorState{Key=$"target-{i}",Name=$"Level 63 additional target {i}",
                TeamKey="enemy",Level=63,MaximumHealth=request.TargetMaximumHealth};
            add.InitializeHealth(request.TargetMaximumHealth);
            ctx.AddActor(add);
        }
        var rotation=ElementalAdaptiveRotation.Build(request,sp);
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(
            request.BaseSpellHitPercent+actor.Stats.Get("spell-hit-chance-percent")+
                request.Talents.Rank("tidal-focus"),
            request.SpellCritPercent+actor.Stats.Get("spell-critical-chance-percent")+
                request.Talents.Rank("thundering-strikes"),
            1.5m+request.Talents.Rank("elemental-fury")*.1m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated());
        var state=new ElementalState(request,actor,executor);
        var potion=new CasterManaPotionProcessor(actor,executor,request.MajorManaPotionCharges,
            request.MajorManaPotionBelowManaPercent);
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","shaman");
        var priority=new PriorityRotationExecutor(rotation,"shaman","target",executor,true);
        var regen=new FiveSecondManaRegenerationProcessor("shaman",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),
            request.Talents.Rank("mindfulness")*.5m/3m);
        // Lightning Overload triggered copies cause damage but no threat.
        // Natural Grace reduces the threat of Shaman spells by 5% per rank.
        var threat=new ThreatManager(new SoloDamageThreatResolver("shaman",
            ev => 1m-.05m*request.Talents.Rank("natural-grace"),
            ev => ev.AbilityKey is not
                ("lightning-overload-bolt" or "lightning-overload-chain")));
        var engine=new SimulationEngine([executor,aura,regen,state,items,potion,threat,priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx);
        var report=run.Summary.ActorSummaries["shaman"];
        return new ElementalShamanSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            ResolvedRotation=string.Join(" > ",rotation.Entries.Select(e=>e.AbilityKey)),
            ClearcastingProcs=state.ClearcastingProcs,
            ClearcastingConsumed=state.ClearcastingConsumed,
            ElementalMasteryCasts=state.ElementalMasteryCasts,
            LightningOverloadProcs=state.LightningOverloadProcs,
            LavaBurstFlameShockBuffed=state.BuffedLavaBursts,
            MajorManaPotionUses=potion.Uses,
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                "CP137: Forever Oct 16 (build 70425) calibration: Lightning Bolt R10 196/220, Chain Lightning R4 123/315, Flame Shock R6 166+176/12 sec; Lava Burst R4 numbers remain provisional.",
                "Elemental Focus clearcasting, Tidal Focus hit, Mindfulness five-second-rule regen, Thundering Strikes crit, Elemental Mastery timing and Searing Totem now modeled.",
                "CP137.1: Ancestral Knowledge increases Intellect and Mana by rank; Shamanistic Focus reduces Flame Shock Mana cost by 45%.",
                "Adaptive priority derives from talent availability and encounter length; customPriority overrides it, and legacy preserves CP135 behavior.",
                "Lightning Overload produces separate half-damage casts. 2-3 target Chain Lightning uses the generic multi-target engine but 30% jump decay is not yet modeled.",
                "DoT ticks are currently allowed to crit without independent miss rolls; application-level hit and per-target spell tables need a general engine pass.",
                "Gear stats require verification for individual item slots, trinket use effects, enchants, racials and raid buffs. Profile sheet budgets are planning assumptions, not measured Forever gear.",
                "Historical CP135 exports cannot directly validate post-Oct-16 damage balance. Rebenchmark multiple seeds on updated profiles."
            ]
        };
    }
    private sealed class ElementalState(ElementalShamanSimRequest request,
        SimulationActorState actor, AbilityExecutor executor) : ICombatEventProcessor
    {
        private readonly Dictionary<string,decimal> _baseCosts=actor.Abilities
            .Where(p=>p.Value.Definition.ResourceCosts.Count>0)
            .ToDictionary(p=>p.Key,p=>p.Value.Definition.ResourceCosts[0].Amount,
                StringComparer.OrdinalIgnoreCase);
        private readonly decimal _lavaBase=actor.Abilities.TryGetValue("lava-burst",out var lava)
            ? lava.Definition.Effects[0].DamageMultiplier:1m;
        private bool _clearcast;
        private string? _discountedCast;
        private bool _mastery;
        public int LightningOverloadProcs{get;private set;}
        public int BuffedLavaBursts{get;private set;}
        public int ClearcastingProcs{get;private set;}
        public int ClearcastingConsumed{get;private set;}
        public int ElementalMasteryCasts{get;private set;}

        public void Process(SimulationContext ctx,CombatEvent ev)
        {
            if (!string.Equals(ev.SourceActorKey,"shaman",StringComparison.OrdinalIgnoreCase)) return;
            if (ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="elemental-mastery")
                SetMastery(true);
            if (ev.Type==CombatEventType.AuraRemoved && ev.AbilityKey=="elemental-mastery")
                SetMastery(false);
            if (ev.Type==CombatEventType.AbilityCastStarted)
            {
                if (ev.AbilityKey=="elemental-mastery") ElementalMasteryCasts++;
                if (ev.AbilityKey=="lava-burst" && actor.Abilities.TryGetValue("lava-burst",out var lava))
                {
                    var hasFlame=ctx.GetActor("target")?.ActiveAuras.Any(a=>a.Definition.Key=="flame-shock"&&
                        a.SourceActorKey=="shaman"&&a.ExpiresAtSeconds>ctx.CurrentTimeSeconds)??false;
                    lava.Definition.Effects[0].DamageMultiplier=_lavaBase*(hasFlame?
                        1m+request.Tuning.LavaBurstFlameShockBonusPercent/100m:1m);
                    if (hasFlame) BuffedLavaBursts++;
                }
                if (_clearcast && ev.AbilityKey is not null && _baseCosts.ContainsKey(ev.AbilityKey) &&
                    ev.AbilityKey is not ("mana-spring-totem" or "searing-totem" or "elemental-mastery"
                        or "lightning-overload-bolt" or "lightning-overload-chain"))
                {
                    _clearcast=false;
                    _discountedCast=ev.AbilityKey;
                    ClearcastingConsumed++;
                }
            }
            if (ev.Type==CombatEventType.AbilityCastCompleted && ev.AbilityKey==_discountedCast)
            {
                // The executor has already paid the discounted cost at completion.
                _discountedCast=null;
                RestoreCosts();
            }
            if (ev.Type!=CombatEventType.Damage || ev.ResultKey is not ("hit" or "critical") ||
                ev.IsPeriodic || ev.IsChannelTick || ev.AbilityKey is null) return;
            // Only one proc roll per landed direct damage effect; triggered overloads never retrigger.
            if (request.Talents.Rank("elemental-focus")>0 && !_clearcast &&
                ev.AbilityKey is ("lightning-bolt" or "lightning-bolt-rank7" or "chain-lightning" or "lava-burst" or "flame-shock") &&
                (decimal)ctx.Random.NextDouble()*100m<10m)
            {
                _clearcast=true;
                ClearcastingProcs++;
                DiscountCosts();
            }
            if (!request.UseLightningOverload || request.Talents.Rank("lightning-overload")==0 ||
                ev.AbilityKey is not ("lightning-bolt" or "chain-lightning")) return;
            var chance=request.Talents.Rank("lightning-overload") switch
            {
                1=>request.Tuning.LightningOverloadChancePercentRank1,
                2=>request.Tuning.LightningOverloadChancePercentRank2,
                _=>request.Tuning.LightningOverloadChancePercentRank3
            };
            if ((decimal)ctx.Random.NextDouble()*100m>=chance) return;
            var key=ev.AbilityKey=="lightning-bolt"?"lightning-overload-bolt":"lightning-overload-chain";
            if (executor.TryStartAbility(ctx,"shaman","target",key).Success) LightningOverloadProcs++;
        }
        private void DiscountCosts()
        {
            foreach (var key in _baseCosts.Keys)
                if (actor.Abilities.TryGetValue(key,out var ability) && ability.Definition.ResourceCosts.Count>0 &&
                    key is not ("mana-spring-totem" or "searing-totem" or "elemental-mastery"))
                    ability.Definition.ResourceCosts[0].Amount=0m;
        }
        private void RestoreCosts()
        {
            foreach (var (key,cost) in _baseCosts)
                if (actor.Abilities.TryGetValue(key,out var ability) && ability.Definition.ResourceCosts.Count>0)
                    ability.Definition.ResourceCosts[0].Amount=cost;
        }
        private void SetMastery(bool enabled)
        {
            if (_mastery==enabled) return;
            _mastery=enabled;
            foreach (var key in new[]{"lightning-bolt","lightning-bolt-rank7","lava-burst"})
                if (actor.Abilities.TryGetValue(key,out var a))
                    a.Definition.CastTimeSeconds+=enabled?-.5m:.5m;
            if (actor.Abilities.TryGetValue("chain-lightning",out var chain))
                chain.Definition.CooldownSeconds=enabled?0m:6m;
        }
    }
}
