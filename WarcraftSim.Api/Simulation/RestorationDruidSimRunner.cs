using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

public sealed class RestorationDruidSimRequest
{
    public int Seed { get; set; } = 144101;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; }
    public decimal AllyMaximumHealth { get; set; } = 11000m;
    public decimal AllyStartingHealthPercent { get; set; } = 65m;
    public int AdditionalAllies { get; set; } = 4;
    public decimal IncomingDamage { get; set; } = 750m;
    // Can be raised in focused tests; ordinary healer priority remains unchanged.
    public decimal SwiftmendHealthThresholdPercent { get; set; } = 55m;
    public decimal DamageEverySeconds { get; set; } = 3m;
    public ForeverRestorationDruidTalents Talents { get; set; } = new();
    public ForeverRestorationDruidTuning Tuning { get; set; } = new();
}
public sealed class RestorationDruidSimResponse
{
    public decimal HealingDone { get; init; }
    public decimal OverhealingDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public Dictionary<string,decimal> HealingByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
}
public sealed class RestorationDruidSimRunner
{
    public RestorationDruidSimResponse Run(RestorationDruidSimRequest request,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <=0m or >1200m || request.AdditionalAllies is <0 or >8 ||
            request.AllyMaximumHealth<=0m || request.AllyStartingHealthPercent is <1m or >100m ||
            request.IncomingDamage<0m || request.DamageEverySeconds<=0m ||
            request.SwiftmendHealthThresholdPercent is <0m or >100m)
            throw new ArgumentException("Invalid Restoration Druid scenario.");
        request.Talents.Validate();request.Tuning.Validate();
        var t=request.Tuning;
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=request.Seed,DurationSeconds=request.DurationSeconds,
            PrimaryActorKey="druid",CaptureTimeline=request.CaptureTimeline,
            MaximumProcessedEvents=150000,CancellationToken=token
        });
        SimulationActorState actor(string key,string team,decimal max,decimal hp)
        {
            var actor=new SimulationActorState {Key=key,Name=key,TeamKey=team,Level=60,MaximumHealth=max};
            actor.InitializeHealth(max,hp);ctx.AddActor(actor);return actor;
        }
        var druid=actor("druid","raid",t.MaximumHealth,t.MaximumHealth);
        druid.AddResource(new ResourceState("mana",t.MaximumMana,t.MaximumMana));
        var target=actor("tank","raid",request.AllyMaximumHealth,
            request.AllyMaximumHealth*request.AllyStartingHealthPercent/100m);
        var enemy=actor("boss","enemy",1000000m,1000000m);
        for(var i=1;i<=request.AdditionalAllies;i++)
            actor($"raid-{i}","raid",request.AllyMaximumHealth,
                request.AllyMaximumHealth*request.AllyStartingHealthPercent/100m);
        foreach(var ability in ForeverRestorationDruidSpellbook.Create(request.Talents,t))
            druid.AddAbility(ability);
        var auras=new AuraManager();
        var rolls=new DruidHealingRolls(t.CriticalChancePercent, request.Talents.Rank("improved-regrowth"));
        var mitigation=new NoMitigation();
        var absorbs=new AbsorbManager();
        var swiftness = new DruidNatureSwiftnessProcessor(druid.Key, auras);
        var executor=new AbilityExecutor(auras,rolls,mitigation,absorbManager:absorbs,
            abilityTimingProvider:swiftness);
        var extras=new RestorationDruidExtraHealingProcessor(druid.Key,t,request.Talents,auras);
        var livingSpiritBonus = t.BaseSpirit * .05m * request.Talents.Rank("living-spirit");
        var regen=new FiveSecondManaRegenerationProcessor(druid.Key,
            t.ManaRegenerationPerSecond + livingSpiritBonus * t.ManaPerSpiritPerSecond,0m,0m);
        regen.Initialize(ctx);
        var rules=new List<HealerPriorityRule>();
        void add(string key,decimal threshold,int injured=1,string? skip=null)
        {
            if(!druid.Abilities.ContainsKey(key))return;
            rules.Add(new HealerPriorityRule {Priority=rules.Count,AbilityKey=key,
                CastAtOrBelowHealthPercent=threshold,MinimumInjuredTeamMembers=injured,
                SkipWhenTargetHasAuraKey=skip,MinimumMissingHealth=1m});
        }
        if (druid.Abilities.ContainsKey("natures-swiftness"))
            rules.Add(new HealerPriorityRule { Priority=rules.Count,
                AbilityKey="natures-swiftness",CastAtOrBelowHealthPercent=100m,
                RequiredTargetActorKey=druid.Key,AllowUninjuredTargets=true,
                SkipWhenTargetHasAuraKey="druid-natures-swiftness-ready",
                MinimumMissingHealth=0m });
        add("swiftmend",request.SwiftmendHealthThresholdPercent);
        add("wild-growth",90m,3,"wild-growth");
        add("rejuvenation",94m,1,"rejuvenation");
        add("regrowth",65m,1,"regrowth");
        add("tranquility",45m,3);
        add("healing-touch",80m);
        var healer=new HealerPriorityProcessor(new HealerPriorityDefinition {
            HealerActorKey="druid",EligibleTeamKey="raid",
            DecisionIntervalSeconds=.25m,Rules=rules
        },executor);
        var engine=new SimulationEngine([executor,auras,absorbs,
            new ScriptedEncounterDamageProcessor(mitigation,absorbs),regen,extras,swiftness,
            new ThreatManager(new DruidHealingThreatResolver(.5m *
                (1m - request.Talents.Rank("subtlety") * .10m),
                request.Talents.Rank("improved-tranquility"))),healer]);
        var run=engine.Run(ctx,started=> {
            healer.ScheduleFirst(started);
            if(request.IncomingDamage<=0m)return;
            for(decimal time=2m;time<request.DurationSeconds;time+=request.DamageEverySeconds)
                foreach(var ally in started.Actors.Values.Where(a=>a.TeamKey=="raid" && a.Key!="druid"))
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds=time,Type=CombatEventType.ScriptedDamage,
                        SourceActorKey=enemy.Key,TargetActorKey=ally.Key,
                        EncounterEventKey="restoration-druid-training",SchoolKey="physical",
                        MitigationType=DamageMitigationTypes.None,RawAmount=request.IncomingDamage
                    });
        });
        druid.RefreshResources(request.DurationSeconds);
        run.CustomMetrics["druid-natures-swiftness-consumed"] = swiftness.Consumed;
        run.CustomMetrics["druid-healing-threat"] = enemy.ThreatTable.GetThreat(druid.Key);
        run.CustomMetrics["druid-spirit-after-living-spirit"] = t.BaseSpirit + livingSpiritBonus;
        run.CustomMetrics["druid-mana-regeneration-per-second"] = t.ManaRegenerationPerSecond +
            livingSpiritBonus * t.ManaPerSpiritPerSecond;
        var summary=run.Summary.ActorSummaries[druid.Key];
        return new RestorationDruidSimResponse {
            HealingDone=summary.HealingDone,OverhealingDone=summary.OverhealingDone,
            EndingMana=druid.Resources["mana"].Current,MaximumMana=t.MaximumMana,
            HealingByAbility=new Dictionary<string,decimal>(summary.HealingDoneByAbility,
                StringComparer.OrdinalIgnoreCase),Run=run
        };
    }
    public sealed class DruidHealingThreatResolver(decimal coefficient,
        int improvedTranquility) : IThreatGenerationResolver
    {
        private readonly HealingThreatResolver _base = new(coefficient);
        public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext ctx, CombatEvent ev)
        {
            var baseThreat = _base.Resolve(ctx, ev);
            if (ev.AbilityKey != "tranquility" || improvedTranquility == 0)
                return baseThreat;
            var multiplier = Math.Max(0m, 1m - .5m * improvedTranquility);
            return baseThreat.Select(x => new ThreatGenerationContribution {
                ThreatOwnerActorKey = x.ThreatOwnerActorKey,
                ThreatSourceActorKey = x.ThreatSourceActorKey,
                Amount = x.Amount * multiplier
            }).ToList();
        }
    }
    private sealed class DruidHealingRolls(decimal crit, int improvedRegrowth) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect)
        {
            // Swiftmend heals the full duration of one of this caster's active
            // Rejuvenation/Regrowth effects without consuming the HoT in Forever.
            // Derive the value from the currently equipped spell definitions,
            // including their talent and healing-power scaling.
            var scale = ability.Key == "swiftmend" && effect.MinimumValue > 0m
                ? DruidSwiftmendHealing.FullDurationAmount(source, target,
                    context.CurrentTimeSeconds) / effect.MinimumValue : 1m;
            if (!effect.CanCrit)
                return new CombatRollResult { Landed = true, ResultKey = CombatResultTypes.Hit,
                    AmountMultiplier = scale };
            var critical = (decimal)context.Random.NextDouble()*100m <
                crit + (ability.Key == "regrowth" ? 10m * improvedRegrowth : 0m);
            return new CombatRollResult { Landed = true, IsCritical = critical,
                ResultKey = critical ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                AmountMultiplier = scale * (critical ? 1.5m : 1m) };
        }
    }
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState src,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,
            decimal rawAmount)=>DamageMitigationResult.Unmitigated(rawAmount);
    }
}

/// <summary>Periodic group healing from Tranquility and Wild Growth. Group membership
/// is approximated with first 5 injured raid actors; real party/range is deferred.</summary>
internal sealed class RestorationDruidExtraHealingProcessor : ICombatEventProcessor
{
    private readonly string _healer;
    private readonly ForeverRestorationDruidTuning _t;
    private readonly ForeverRestorationDruidTalents _talents;
    private readonly AuraManager _auras;
    private readonly Dictionary<Guid,(string Target,int Tick)> _wild = [];
    public RestorationDruidExtraHealingProcessor(string healer,ForeverRestorationDruidTuning t,
        ForeverRestorationDruidTalents talents,AuraManager auras)
    { _healer=healer;_t=t;_talents=talents;_auras=auras; }
    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(ev.SourceActorKey!=_healer)return;
        if(ev.Type==CombatEventType.AbilityCastCompleted && ev.AbilityKey=="wild-growth" &&
            ev.AbilityExecutionId is Guid exec && ev.TargetActorKey is { } key)
        {
            _wild[exec]=(key,0);
            for(var i=1;i<=7;i++)
                if(ctx.CurrentTimeSeconds+i<=ctx.Options.DurationSeconds)
                    ctx.ScheduleEvent(new CombatEvent {
                        TimeSeconds=ctx.CurrentTimeSeconds+i,Type=CombatEventType.RotationDecision,
                        SourceActorKey=_healer,TargetActorKey=key,AbilityExecutionId=exec,
                        EffectKey="wild-growth-group-pulse",IsInternal=true
                    });
        }
        if(ev.Type==CombatEventType.RotationDecision && ev.EffectKey=="wild-growth-group-pulse" &&
            ev.AbilityExecutionId is Guid id && _wild.TryGetValue(id,out var state))
        {
            _wild[id]=(state.Target,state.Tick+1);
            var amount=_t.WildGrowthInitialTick * (8m - (state.Tick+1)) / 7m;
            GroupHeal(ctx,"wild-growth",id,amount,CombatEffectDeliveryType.Periodic,5);
            if(state.Tick>=6)_wild.Remove(id);
        }
        if(ev.Type==CombatEventType.Healing && ev.AbilityKey=="tranquility" &&
            ev.EffectKey=="tranquility-primary" && ev.IsChannelTick)
            GroupHeal(ctx,"tranquility",ev.AbilityExecutionId,
                (_t.TranquilityTick+_t.SpellHealingPower*.15m)*
                (1m+_talents.Rank("gift-of-nature")*.02m),
                CombatEffectDeliveryType.ChannelTick,4,ev.TargetActorKey);
    }
    private void GroupHeal(SimulationContext ctx,string ability,Guid? exec,decimal raw,
        CombatEffectDeliveryType delivery,int max,string? exclude=null)
    {
        var caster=ctx.GetActor(_healer);
        if(caster is null)return;
        foreach(var target in ctx.Actors.Values.Where(a=>a.TeamKey==caster.TeamKey && a.IsAlive &&
            a.Key!=caster.Key && a.Key!=exclude).OrderBy(a=>a.CurrentHealth/a.MaximumHealth)
            .ThenBy(a=>a.Key,StringComparer.OrdinalIgnoreCase).Take(max))
        {
            var critical=(decimal)ctx.Random.NextDouble()*100m<_t.CriticalChancePercent;
            var amount=HealingReceivedModifiers.Apply(target,ctx.CurrentTimeSeconds,
                raw*(critical?1.5m:1m));
            var effective=target.Heal(amount);
            ctx.EmitEvent(new CombatEvent {
                TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.Healing,
                SourceActorKey=_healer,TargetActorKey=target.Key,AbilityKey=ability,
                AbilityExecutionId=exec,EffectKey=$"{ability}-group-tick",
                SchoolKey="nature",RawAmount=amount,Amount=effective,
                OverhealingAmount=amount-effective,
                EffectDeliveryType=delivery,IsCritical=critical,
                ResultKey=critical?CombatResultTypes.Critical:CombatResultTypes.Hit
            });
        }
    }
}

/// <summary>
/// Makes the next eligible Nature heal instant through the existing timing
/// snapshot pipeline. Aura is consumed on cast start, not by unrelated HoTs.
/// </summary>
internal sealed class DruidNatureSwiftnessProcessor : ICombatEventProcessor, IAbilityTimingProvider
{
    private readonly string _druid;
    private readonly AuraManager _auras;
    public int Consumed { get; private set; }
    public DruidNatureSwiftnessProcessor(string druid,AuraManager auras)
    { _druid=druid;_auras=auras; }
    public AbilityTimingSnapshot Resolve(SimulationContext ctx,SimulationActorState src,
        AbilityDefinition ability)
    {
        var instant = src.Key == _druid && (ability.Key is "healing-touch" or "regrowth") &&
            src.ActiveAuras.Any(a => a.Definition.Key == "druid-natures-swiftness-ready" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds));
        return new AbilityTimingSnapshot {
            CastTimeSeconds=instant?0m:ability.CastTimeSeconds,
            GlobalCooldownSeconds=ability.GlobalCooldownSeconds,
            ChannelDurationSeconds=ability.ChannelDurationSeconds,
            ChannelTickIntervalSeconds=ability.ChannelTickIntervalSeconds
        };
    }
    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if (ev.SourceActorKey != _druid || ev.Type != CombatEventType.AbilityCastStarted ||
            ev.AbilityKey is not ("healing-touch" or "regrowth"))return;
        var actor=ctx.GetActor(_druid);
        if (actor is null || !actor.ActiveAuras.Any(a=>
            a.Definition.Key=="druid-natures-swiftness-ready" &&
            a.IsActiveAt(ctx.CurrentTimeSeconds)))return;
        _auras.RemoveAuras(ctx,actor,"druid-natures-swiftness-ready",_druid,
            "consumed by next Nature cast");
        Consumed++;
    }
}
