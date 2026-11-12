using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Items;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP138: adaptive level-60 Moonkin DPS with Eclipse, Nature's Grace and
/// baseline Omen, using the common event-driven resource and item pipelines.</summary>
public sealed class BalanceDruidSimRunner
{
    public BalanceDruidSimResponse Run(BalanceDruidSimRequest request,
        CancellationToken cancellationToken=default)
    {
        var prepared=Prepare(request,cancellationToken);
        request=prepared.Request;
        var (ctx,actor,boss,sp,maxMana,baseline,preparationNotes,rotation)=
            (prepared.Context,prepared.Actor,prepared.Target,prepared.SpellPower,
             prepared.MaximumMana,prepared.Baseline,prepared.Notes,prepared.Rotation);
        var t=request.Tuning;
        var state=new BalanceState(request,rotation,actor,sp,baseline is not null);
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(
            prepared.SpellHitPercent,prepared.SpellCritPercent,
            1.5m+request.Talents.Rank("vengeance")*.1m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated(),
            abilityTimingProvider:baseline is null ? state : new ForeverEquipmentCastHasteTimingProvider(state));
        var potion=baseline is null ? new CasterManaPotionProcessor(actor,executor,
            request.MajorManaPotionCharges,request.MajorManaPotionBelowManaPercent) : null;
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","druid");
        var priority=new PriorityRotationExecutor(rotation,"druid","target",executor,true);
        var hurricane=new BalanceHurricaneCleaveProcessor("druid",executor,
            request.HurricaneSecondaryTargetLimit);
        var thornsDamage=t.ThornsBaseDamage + sp*t.ThornsSpellPowerCoefficient;
        // Balance-only Naturalist wiring; shared Feral/Restoration Thorns is unchanged.
        // Existing Thorns base tuning and missing Moonfury scaling remain provisional.
        if(request.Talents.Rank("naturalist")!=0)
            thornsDamage*=1m+request.Talents.Rank("naturalist")*.01m;
        var thorns=new FeralThornsProcessor("druid",request.UseThorns,thornsDamage);
        var absorbs=new AbsorbManager();
        var regen=new FiveSecondManaRegenerationProcessor("druid",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),request.Talents.Rank("reflection")*.5m/3m);
        var engine=new SimulationEngine([executor,aura,absorbs,
            new ScriptedEncounterDamageProcessor(new ForeverCasterSoloSupport.Unmitigated(),absorbs,
                modifyRawDamage: (context, defender, ev, raw) =>
                    ev.SchoolKey == "physical" && defender.ActiveAuras.Any(a =>
                        a.Definition.Key == "druid-barkskin" &&
                        a.IsActiveAt(context.CurrentTimeSeconds)) ? raw * .8m : raw),
            regen,state,items,..(potion is null ? Array.Empty<ICombatEventProcessor>() : [potion]),hurricane,thorns,
            new ThreatManager(new SoloDamageThreatResolver("druid",
                ev => ev.SchoolKey is "nature" or "arcane"
                    ? 1m-.10m*request.Talents.Rank("subtlety")
                    : 1m)),priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx,started => {
            if(request.UseThorns)
                aura.ApplyAura(started,actor,new WarcraftSim.Core.Auras.AuraDefinition {
                    Key="druid-thorns",Name="Thorns",DurationSeconds=request.DurationSeconds+1m
                },actor.Key,"thorns","balance-thorns");
            if(request.IncomingBossHitDamage>0m)
                for(decimal at=2m;at<request.DurationSeconds;at+=request.BossSwingIntervalSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds=at,Type=CombatEventType.ScriptedDamage,
                        SourceActorKey=boss.Key,TargetActorKey=actor.Key,
                        EncounterEventKey="balance-incoming-test",SchoolKey="physical",
                        MitigationType=DamageMitigationTypes.None,RawAmount=request.IncomingBossHitDamage
                    });
        });
        run.CustomMetrics["balance-hurricane-secondary-hits"]=hurricane.SecondaryHits;
        run.CustomMetrics["balance-thorns-hits"]=thorns.Hits;
        var report=run.Summary.ActorSummaries["druid"];
        return new BalanceDruidSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            MaximumHealth=actor.MaximumHealth,EndingHealth=actor.CurrentHealth,
            ResolvedRotation=string.Join(" > ",rotation.Entries.Select(e=>e.AbilityKey)),
            EclipseChargesGenerated=state.ChargesGenerated,
            EclipseEmpoweredStarfires=state.EmpoweredStarfires,
            MaximumEclipseCharges=state.MaxCharges,
            OmenProcs=state.OmenProcs,OmenConsumed=state.OmenConsumed,
            NaturesGraceProcs=state.NaturesGraceProcs,
            MajorManaPotionUses=potion?.Uses??items.Uses.GetValueOrDefault("major-mana-potion"),
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                ..ForeverCasterSoloSupport.BaselineModelingNotes(baseline,"druid"),
                ..preparationNotes,
                "CP138: Forever level-60 rank defaults: Wrath R8 91/120, Starfire R7 381/340, Moonfire R10 135+60/375, Insect Swarm R5 186/160.",
                "Improved Wrath and Moonglow reduce spell costs; Improved Starfire reduces cast time. Nature's Reach gives 2% hit/rank.",
                "Nature's Grace applies 10% faster spells/GCD for 3s after direct crit. Baseline Omen procs are approximated with a configurable chance and 10s cooldown; Wrath cannot consume them.",
                "Eclipse grants two charges on completed Wrath, capped at four, for 15s. Each empowered Starfire consumes one charge.",
                "Default priority maintains DoTs only when enough time remains and considers Eclipse, free-cast procs, expected throughput, Mana and encounter length. CustomPriority overrides it.",
                baseline is null
                    ? "Profiles represent provisional gear/enchants and assumed aggregate budgets. Trinket use, raid buffs, boss debuffs, moving targets and timed adds are not yet resolved by this solo runner."
                    : "Prepared catalog gear, static enchants and selected self buffs carry source labels above. Dynamic trinket effects, selected target debuffs, moving targets and timed adds remain unsupported.",
                "Hurricane is a ten-second channeled Nature spell with no cooldown (verified); its level-60 damage and Mana are provisional.",
                "Periodic hit/crit resolution is still based on the generic engine. The Omen proc rate and rank-60 spell-power scaling remain provisional.",
                "CP152: Faerie Fire applies a target-owned 40s armor debuff, with rank 4 armor/mana verified; Balance deals no physical damage without external melee attackers.",
                "CP152: Hurricane hits configured additional enemies with independent spell rolls; raid geometry is not modeled. Optional Thorns + scripted incoming attacks are off by default."
            ]
        };
    }

    // Exact isolated precombat path, shared with read-only previews. No events run.
    internal static BalancePrecombatPreparation Prepare(BalanceDruidSimRequest request,
        CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request=CasterSimulationRequestSnapshot.Create(request);
        if(request.Talents is null||request.Tuning is null||request.CombatItems is null||request.Character is null||request.ExternalModifiers is null)
            throw new ArgumentException("Balance requires talents, tuning, items and a character.");
        request.Talents.Validate();request.CombatItems.Validate("druid");
        if(request.MajorManaPotionCharges is <0 or >20 ||
            request.MajorManaPotionBelowManaPercent is <0m or >100m)
            throw new ArgumentException("Invalid Mana Potion charges or threshold.");
        var baseline=ForeverLevel60Baseline.Resolve(request.Level60Baseline,
            request.UseReviewedLevel60Baseline,request.Character.Race??"","druid",
            request.UseProvisionalLevel60Baseline);
        if(baseline is not null)
            ForeverCasterSoloSupport.PrepareCombatItems(request.CombatItems,
                request.MajorManaPotionCharges,request.MajorManaPotionBelowManaPercent);
        if (request.AdditionalTargets is <0 or >8 ||
            request.HurricaneSecondaryTargetLimit is <0 or >8 ||
            request.IncomingBossHitDamage<0m || request.BossSwingIntervalSeconds<=0m)
            throw new ArgumentException("Invalid Balance Druid multi-target scenario.");
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
        var preparationNotes=new List<string>();
        var (ctx,actor,boss,sp,maxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "druid","balance",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken,baseline,
            externalModifiers:request.ExternalModifiers,preparationNotes:preparationNotes);
        for (var i=1;i<=request.AdditionalTargets;i++)
        {
            var add=new SimulationActorState {Key=$"target-{i}",TeamKey="enemy",
                Level=63,Name="Hurricane target",MaximumHealth=request.TargetMaximumHealth};
            add.InitializeHealth(add.MaximumHealth);ctx.AddActor(add);
        }
        foreach(var spell in ForeverBalanceDruidSpellbook.Create(request.Talents,t,sp))actor.AddAbility(spell);
        actor.AddAbility(ForeverDruidSharedCooldowns.CreateInnervate());
        actor.AddAbility(ForeverDruidSharedCooldowns.CreateBarkskin());
        var rotation=BalanceAdaptiveRotation.Build(request);
        return new(request,ctx,actor,boss,sp,maxMana,baseline,preparationNotes,rotation);
    }

    public static BalancePrecombatPreview Preview(BalanceDruidSimRequest request)
    {
        var prepared=Prepare(request);
        var stats=new WarcraftSim.Core.Stats.StatCollection {
            Values=new(prepared.Actor.Stats.Values,prepared.Actor.Stats.Values.Comparer)
        };
        stats.Set("spell-power",prepared.SpellPower);
        stats.Set("health",prepared.Actor.MaximumHealth);
        stats.Set("mana",prepared.MaximumMana);
        stats.Set("spell-hit-chance-percent",Math.Min(100m,prepared.SpellHitPercent+prepared.Actor.Stats.Get("spell-hit-percent")));
        stats.Set("spell-critical-chance-percent",Math.Min(100m,prepared.SpellCritPercent));
        return new(stats,prepared.SpellPower,prepared.MaximumMana,prepared.Actor.MaximumHealth,
            prepared.Baseline is null ? null : ForeverLevel60Baseline.Summary(prepared.Baseline),
            prepared.Baseline?.VerificationStatus??"explicit-user-profile-not-client-verified",
            [..ForeverCasterSoloSupport.BaselineModelingNotes(prepared.Baseline,"druid"),..prepared.Notes,
                "Display-only exact precombat snapshot. Hit and critical chance include manual, derived and passive talent inputs, excluding ability-specific bonuses and combat procs. Rotation entries are initial priority only: BalanceState reorders adaptive priorities during combat using Eclipse, procs, Mana and remaining time. Hardcast haste excludes dynamic Nature's Grace and Eclipse, GCDs, channels and periodic ticks. Historical model parity is not current-client certification."],
            prepared.Rotation,prepared.Baseline is null ? 0m : Math.Max(0m,
                prepared.Actor.Stats.Get("haste-percent")+prepared.Actor.Stats.Get("cast-haste-percent")));
    }

    private sealed class BalanceState(BalanceDruidSimRequest r,RotationProfile rotation,
        SimulationActorState actor,decimal spellPower,bool preparedCombatItems):ICombatEventProcessor,IAbilityTimingProvider
    {
        private static readonly HashSet<string> CombatItemKeys=ForeverCombatItemCatalog.CreateAll()
            .Select(item=>item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            // Prepared catalog items retain their own timing, including instant
            // off-GCD recovery. Eclipse and Nature's Grace affect class spells,
            // not item casts, GCDs or channels. Preserve the legacy preview path.
            if(preparedCombatItems && CombatItemKeys.Contains(ability.Key))
                return AbilityTimingSnapshot.FromAbility(ability);
            var stacks=EffectiveCharges(context);
            var reduction=ability.Key=="starfire"&&r.UseEclipse&&r.Talents.Rank("eclipse")>0&&stacks>0
                ?r.Tuning.EclipseCastReductionPerRank*r.Talents.Rank("eclipse"):0m;
            var grace=(r.Talents.Rank("natures-grace")>0&&context.CurrentTimeSeconds<_graceExpires) ? 0.9m : 1m;
            return new AbilityTimingSnapshot
            {
                CastTimeSeconds=preparedCombatItems && ability.CastTimeSeconds<=0m ? 0m :
                    Math.Max(0m,Math.Max(1m,ability.CastTimeSeconds-reduction)*grace),
                GlobalCooldownSeconds=preparedCombatItems && ability.GlobalCooldownSeconds<=0m ? 0m :
                    Math.Max(1m,ability.GlobalCooldownSeconds*grace),
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

internal sealed record BalancePrecombatPreparation(BalanceDruidSimRequest Request,
    SimulationContext Context,SimulationActorState Actor,SimulationActorState Target,
    decimal SpellPower,decimal MaximumMana,ForeverLevel60BaselineObservation? Baseline,
    List<string> Notes,RotationProfile Rotation)
{
    public decimal SpellHitPercent=>Request.BaseSpellHitPercent+
        Request.Talents.Rank("natures-reach")*2m+Actor.Stats.Get("spell-hit-chance-percent");
    public decimal SpellCritPercent=>Request.SpellCritPercent+
        Request.Talents.Rank("natures-majesty")*2m+(Request.Talents.Rank("moonkin-form")>0?3m:0m)+
        Actor.Stats.Get("spell-critical-chance-percent");
}
public sealed record BalancePrecombatPreview(WarcraftSim.Core.Stats.StatCollection EffectiveStats,
    decimal SpellPower,decimal MaximumMana,decimal MaximumHealth,object? Baseline,
    string SourceStatus,IReadOnlyList<string> ModelingNotes,RotationProfile Rotation,
    decimal HardcastHastePercent)
{
    public string StatPreviewScope=>"balance-exact-precombat-display-only-not-current-client-certified";
    public string RotationPreviewScope=>"initial-priority-adaptive-order-changes-during-combat";
}
