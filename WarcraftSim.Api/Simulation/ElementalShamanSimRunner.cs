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
        var prepared=Prepare(request,cancellationToken);
        request=prepared.Request;
        var (ctx,actor,boss,sp,maxMana,baseline,preparationNotes,rotation)=
            (prepared.Context,prepared.Actor,prepared.Target,prepared.SpellPower,
             prepared.MaximumMana,prepared.Baseline,prepared.Notes,prepared.Rotation);
        var t=request.Tuning;
        var aura=new AuraManager();
        var roll=new ForeverCasterSoloSupport.SpellRolls(
            prepared.SpellHitPercent,prepared.SpellCritPercent,
            1.5m+request.Talents.Rank("elemental-fury")*.1m);
        var executor=new AbilityExecutor(aura,roll,new ForeverCasterSoloSupport.Unmitigated(),
            abilityTimingProvider:baseline is null ? null :
                new ForeverEquipmentCastHasteTimingProvider(BaseAbilityTimingProvider.Instance));
        var state=new ElementalState(request,actor,executor);
        var potion=baseline is null ? new CasterManaPotionProcessor(actor,executor,
            request.MajorManaPotionCharges,request.MajorManaPotionBelowManaPercent) : null;
        var items=new CombatUseItemProcessor(request.CombatItems,executor,actor,"target","shaman");
        var priority=new PriorityRotationExecutor(rotation,"shaman","target",executor,true);
        var fireTotem=new ShamanFireTotemProcessor(actor,aura);
        var totemSlots=new ShamanTotemLifecycleProcessor(actor,aura);
        var shield=new ShamanLightningShieldProcessor(actor,aura,executor,
            request.LightningShieldInternalCooldownSeconds);
        var devastation=new ShamanElementalDevastationProcessor(actor,aura,
            request.Talents.Rank("elemental-devastation"));
        var regen=new FiveSecondManaRegenerationProcessor("shaman",t.ManaRegenPerSecond,
            Math.Max(0m,actor.Stats.Get("mp5")),
            // Historical source uses exact ranks, not linear thirds of 50%.
            request.Talents.Rank("mindfulness") switch {1=>.17m,2=>.33m,3=>.5m,_=>0m});
        // Lightning Overload triggered copies cause damage but no threat.
        // Natural Grace reduces the threat of Shaman spells by 5% per rank.
        var threat=new ThreatManager(new SoloDamageThreatResolver("shaman",
            ev => 1m-.05m*request.Talents.Rank("natural-grace"),
            ev => ev.AbilityKey is not
                ("lightning-overload-bolt" or "lightning-overload-chain")));
        var engine=new SimulationEngine([executor,aura,fireTotem,totemSlots,shield,
            new ScriptedEncounterDamageProcessor(new ForeverCasterSoloSupport.Unmitigated(),
                modifyRawDamage:(context,defender,ev,raw)=>
                    defender.Key==actor.Key && ev.SchoolKey=="physical" &&
                    defender.ActiveAuras.Any(a=>a.Definition.Key=="shaman-stoneskin-totem" &&
                        a.IsActiveAt(context.CurrentTimeSeconds)) ? Math.Max(0m,raw-30m):raw),
            regen,state,devastation,items,..(potion is null ? Array.Empty<ICombatEventProcessor>() : [potion]),threat,priority]);
        regen.Initialize(ctx);
        var run=engine.Run(ctx,started=> {
            if(request.IncomingBossHitDamage>0m)
                for(decimal at=2m;at<request.DurationSeconds;at+=request.IncomingBossSwingSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds=at,Type=CombatEventType.ScriptedDamage,
                        SourceActorKey=boss.Key,TargetActorKey=actor.Key,
                        SchoolKey="physical",MitigationType=DamageMitigationTypes.None,
                        RawAmount=request.IncomingBossHitDamage,
                        EncounterEventKey="elemental-shield-test"
                    });
        });
        run.CustomMetrics["shaman-lightning-shield-triggers"]=shield.Triggers;
        run.CustomMetrics["shaman-fire-totem-placements"]=fireTotem.Placements;
        run.CustomMetrics["shaman-totem-replacements"]=totemSlots.Replacements;
        run.CustomMetrics["shaman-totem-recall-mana"]=totemSlots.RecallManaGained;
        if(request.Talents.Rank("elemental-devastation")>0)
            run.CustomMetrics["shaman-elemental-devastation-procs"]=devastation.Procs;
        var report=run.Summary.ActorSummaries["shaman"];
        return new ElementalShamanSimResponse
        {
            Dps=report.DamageDone/request.DurationSeconds,DamageDone=report.DamageDone,
            EndingMana=actor.Resources["mana"].Current,MaximumMana=maxMana,SpellPower=sp,
            MaximumHealth=actor.MaximumHealth,EndingHealth=actor.CurrentHealth,
            ResolvedRotation=string.Join(" > ",rotation.Entries.Select(e=>e.AbilityKey)),
            ClearcastingProcs=state.ClearcastingProcs,
            ClearcastingConsumed=state.ClearcastingConsumed,
            ElementalMasteryCasts=state.ElementalMasteryCasts,
            LightningOverloadProcs=state.LightningOverloadProcs,
            LavaBurstFlameShockBuffed=state.BuffedLavaBursts,
            MajorManaPotionUses=potion?.Uses??items.Uses.GetValueOrDefault("major-mana-potion"),
            CombatItemUses=new Dictionary<string,int>(items.Uses,StringComparer.OrdinalIgnoreCase),
            DamageByAbility=new Dictionary<string,decimal>(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),
            Run=run,
            ModelingNotes=[
                ..ForeverCasterSoloSupport.BaselineModelingNotes(baseline,"shaman"),
                ..preparationNotes,
                "CP137: Forever Oct 16 (build 70425) calibration: Lightning Bolt R10 196/220, Chain Lightning R4 123/315, Flame Shock R6 166+176/12 sec; Lava Burst R4 numbers remain provisional.",
                "Elemental Focus clearcasting, Tidal Focus hit, Mindfulness five-second-rule regen, Thundering Strikes crit, Elemental Mastery timing and Searing Totem now modeled.",
                "Historical-provisional Trial by Fire adds 10% Fire Nova damage and reduces its cooldown by 2 seconds per rank, separately from Call of Flame. Nova still requires an owned active Fire totem; the talent's Magma Totem duration effect remains unavailable because this runner has no Magma Totem. Mindfulness retains exactly 0/17/33/50% of Spirit regeneration within the existing continuous five-second-rule model; MP5 is independent. These source-backed values are not current-client certification.",
                "CP137.1: Ancestral Knowledge increases Intellect and Mana by rank; Shamanistic Focus reduces Flame Shock Mana cost by 45%.",
                "Adaptive priority derives from talent availability and encounter length; customPriority overrides it, and legacy preserves CP135 behavior.",
                "Lightning Overload produces separate half-damage casts. 2-3 target Chain Lightning uses the generic multi-target engine but 30% jump decay is not yet modeled.",
                "DoT ticks are currently allowed to crit without independent miss rolls; application-level hit and per-target spell tables need a general engine pass.",
                baseline is null
                    ? "Gear stats require verification for individual item slots, trinket use effects, enchants, racials and raid buffs. Profile sheet budgets are planning assumptions, not measured Forever gear."
                    : "Prepared catalog gear, static enchants and selected self buffs carry source labels above. Dynamic equipment effects, selected target debuffs and racial effects remain unsupported. Authored sheet budgets are planning assumptions, not measured Forever gear.",
                "Historical CP135 exports cannot directly validate post-Oct-16 damage balance. Rebenchmark multiple seeds on updated profiles."
            ]
        };
    }
    // The simulation and read-only previews share this exact isolated precombat path.
    // No events execute here, and no prepared mutable state survives a request.
    internal static ElementalPrecombatPreparation Prepare(ElementalShamanSimRequest request,
        CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request=CasterSimulationRequestSnapshot.Create(request);
        if(request.Talents is null||request.Tuning is null||request.CombatItems is null||request.Character is null||request.ExternalModifiers is null)
            throw new ArgumentException("Elemental requires talents, tuning, items and a character.");
        request.Talents.Validate();request.CombatItems.Validate("shaman");
        if(request.MajorManaPotionCharges is <0 or >20 ||
            request.MajorManaPotionBelowManaPercent is <0m or >100m)
            throw new ArgumentException("Invalid Mana Potion charges or threshold.");
        var baseline=ForeverLevel60Baseline.Resolve(request.Level60Baseline,
            request.UseReviewedLevel60Baseline,request.Character.Race??"","shaman",
            request.UseProvisionalLevel60Baseline);
        if(baseline is not null)
            ForeverCasterSoloSupport.PrepareCombatItems(request.CombatItems,
                request.MajorManaPotionCharges,request.MajorManaPotionBelowManaPercent);
        ForeverCasterSoloSupport.ValidatePercent(request.BaseSpellHitPercent,"Spell hit");
        ForeverCasterSoloSupport.ValidatePercent(request.SpellCritPercent,"Spell crit");
        var t=request.Tuning;
        if(request.IncomingBossHitDamage<0m || request.IncomingBossSwingSeconds<=0m ||
            request.LightningShieldInternalCooldownSeconds<0m ||
            new[]{t.LightningBoltDamage,t.LightningBoltMana,t.ChainLightningDamage,t.ChainLightningMana,
            t.FlameShockDirectDamage,t.FlameShockTickDamage,t.FlameShockMana,
            t.EarthShockDamage,t.EarthShockMana,t.LavaBurstDamage,
            t.LavaBurstMana,t.ManaRegenPerSecond,t.LavaBurstFlameShockBonusPercent,
            t.LightningOverloadChancePercentRank1,t.LightningOverloadChancePercentRank2,
            t.LightningOverloadChancePercentRank3}.Any(v=>v<0m)||
            t.LightningOverloadChancePercentRank3>100m||t.LightningOverloadChancePercentRank2>100m||
            t.LightningOverloadChancePercentRank1>100m)
            throw new ArgumentException("Elemental damage, costs, regen and proc chance must be nonnegative.");
        var preparationNotes=new List<string>();
        var (ctx,actor,boss,sp,baseMaxMana)=ForeverCasterSoloSupport.Create(request.Character,
            "shaman","elemental",request.Seed,request.DurationSeconds,request.CaptureTimeline,
            request.MaximumHealth,request.TargetMaximumHealth,request.StartingManaPercent,cancellationToken,baseline,
            1m+request.Talents.Rank("ancestral-knowledge")*.02m,
            request.ExternalModifiers,preparationNotes);
        // CP137.1: Ancestral Knowledge increases total Intellect by 2% per rank.
        // Apply it to the already-computed equipment/buff stats, before the fight starts,
        // then recalculate maximum and starting Mana. Do not mutate the request's
        // CharacterProfile or change the shared caster factory used by other classes.
        var maxMana=baseMaxMana;
        var ancestralKnowledgeRank=request.Talents.Rank("ancestral-knowledge");
        if (baseline is null && ancestralKnowledgeRank>0)
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
        var totemDiscount=1m-.05m*request.Talents.Rank("totemic-focus");
        foreach(var key in new[] {"strength-of-earth-totem","stoneskin-totem",
            "windfury-totem","mana-spring-totem"})
            actor.Abilities[key].Definition.ResourceCosts[0].Amount*=totemDiscount;
        if (request.TargetCount is < 1 or > 3 || request.CustomPriority is null ||
            request.CallOfElementsTotems is null ||
            request.CallOfAncestorsTotems is null ||
            request.CallOfSpiritsTotems is null)
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
        actor.AddAbility(ForeverShamanTotemCalls.Call("call-of-elements",
            actor.Abilities.Values.Select(x=>x.Definition).ToArray(),request.CallOfElementsTotems));
        actor.AddAbility(ForeverShamanTotemCalls.Call("call-of-ancestors",
            actor.Abilities.Values.Select(x=>x.Definition).ToArray(),request.CallOfAncestorsTotems));
        actor.AddAbility(ForeverShamanTotemCalls.Call("call-of-spirits",
            actor.Abilities.Values.Select(x=>x.Definition).ToArray(),request.CallOfSpiritsTotems));
        actor.AddAbility(ForeverShamanTotemCalls.Recall());
        var rotation=ElementalAdaptiveRotation.Build(request,sp);
        return new(request,ctx,actor,boss,sp,maxMana,baseline,preparationNotes,rotation);
    }

    public static ElementalPrecombatPreview Preview(ElementalShamanSimRequest request)
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
            [..ForeverCasterSoloSupport.BaselineModelingNotes(prepared.Baseline,"shaman"),..prepared.Notes,
                "Display-only exact precombat snapshot. Hit and critical chance include manual, derived and passive talent inputs; ability-specific bonuses and combat procs are excluded. Hardcast haste does not alter GCDs, channels or periodic ticks. Historical model parity is not current-client certification."],
            prepared.Rotation,prepared.Baseline is null ? 0m : Math.Max(0m,
                prepared.Actor.Stats.Get("haste-percent")+prepared.Actor.Stats.Get("cast-haste-percent")));
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
                ev.AbilityKey is ("lightning-bolt" or "lightning-bolt-rank7" or "chain-lightning" or "lava-burst" or "flame-shock" or "earth-shock") &&
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

internal sealed record ElementalPrecombatPreparation(ElementalShamanSimRequest Request,
    SimulationContext Context,SimulationActorState Actor,SimulationActorState Target,
    decimal SpellPower,decimal MaximumMana,ForeverLevel60BaselineObservation? Baseline,
    List<string> Notes,RotationProfile Rotation)
{
    public decimal SpellHitPercent=>Request.BaseSpellHitPercent+
        Actor.Stats.Get("spell-hit-chance-percent")+Request.Talents.Rank("tidal-focus");
    public decimal SpellCritPercent=>Request.SpellCritPercent+
        Actor.Stats.Get("spell-critical-chance-percent")+Request.Talents.Rank("thundering-strikes");
}

public sealed record ElementalPrecombatPreview(WarcraftSim.Core.Stats.StatCollection EffectiveStats,
    decimal SpellPower,decimal MaximumMana,decimal MaximumHealth,object? Baseline,
    string SourceStatus,IReadOnlyList<string> ModelingNotes,RotationProfile Rotation,
    decimal HardcastHastePercent)
{
    public string StatPreviewScope=>"elemental-exact-precombat-display-only-not-current-client-certified";
}
