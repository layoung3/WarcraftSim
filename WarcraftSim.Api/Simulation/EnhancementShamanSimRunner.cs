using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP130 single-actor Enhancement DPS using the shared auto-attack,
/// triggered-swing, ability, aura, proc, rotation and reporting engine.</summary>
public sealed class EnhancementShamanSimRunner
{
    public EnhancementShamanSimResponse Run(EnhancementShamanSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.CombatItems is null ||
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "shaman", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, "enhancement", StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m || request.MaximumHealth <= 0m ||
            request.TargetMaximumHealth <= 0m || request.TargetArmor < 0m ||
            request.WeaponMinDamage < 0m || request.WeaponMaxDamage < request.WeaponMinDamage ||
            request.WeaponSpeedSeconds <= 0m ||
            (request.DualWield && (request.OffHandWeaponSpeedSeconds <= 0m ||
                request.OffHandWeaponMinDamage < 0m ||
                request.OffHandWeaponMaxDamage < request.OffHandWeaponMinDamage)) ||
            request.BaseMeleeHitPercent is < 0m or > 100m ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.CriticalChancePercent is < 0m or > 100m ||
            request.ProvisionalGlancingChancePercent is < 0m or > 100m ||
            request.Tuning.ManaRegenPerSecond < 0m ||
            request.Tuning.MaelstromChancePerMeleePercentPerRank < 0m ||
            request.Tuning.WindfuryWeaponProcChancePercent is < 0m or > 100m)
            throw new ArgumentException("Invalid Enhancement Shaman setup or level-60 specialization.");
        request.CombatItems.Validate("shaman");
        var mainImbue=request.MainHandImbue ??
            (request.UseWindfuryWeapon ? "windfury" : "none");
        var offImbue=request.OffHandImbue;
        if(mainImbue is not ("none" or "windfury" or "flametongue" or "rockbiter" or "frostbrand") ||
            offImbue is not ("none" or "flametongue" or "rockbiter" or "frostbrand") ||
            !request.DualWield && offImbue!="none" ||
            request.FrostbrandProcChancePercent is <0m or >100m ||
            request.WindfuryTotemProcChancePercent is <0m or >100m ||
            request.IncomingBossHitDamage<0m || request.IncomingBossSwingSeconds<=0m ||
            request.LightningShieldInternalCooldownSeconds<0m ||
            request.RockbiterExtraThreatMultiplier<0m ||
            request.CallOfElementsTotems is null)
            throw new ArgumentException("Invalid Shaman hand-specific imbue configuration.");
        request.Talents.Validate(60);
        var stats = CharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.MaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var intellect = stats.Get("intellect");
        var attackPower = Math.Max(0m, stats.Get("attack-power") +
            intellect * request.Talents.Rank("mental-dexterity") / 3m);
        var spellPower = Math.Max(0m, stats.Get("spell-power") +
            intellect * .15m * request.Talents.Rank("mental-quickness"));
        stats.Set(ForeverCombatStatKeys.AttackPower, attackPower);
        stats.Set(ForeverCombatStatKeys.HastePercent, Math.Max(0m, stats.Get(ForeverCombatStatKeys.HastePercent)));
        stats.Set("main-hand-weapon-skill", 300m);
        if (request.DualWield) stats.Set("off-hand-weapon-skill", 300m);
        var mana = 1376m + Math.Max(0m, intellect - 20m) * 15m;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "shaman", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 40000, CancellationToken = cancellationToken
        });
        var shaman = new SimulationActorState
        {
            Key = "shaman", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.MaximumHealth, Stats = stats
        };
        shaman.InitializeHealth(shaman.MaximumHealth);
        shaman.AddResource(new ResourceState("mana", mana, mana));
        var target = new SimulationActorState
        {
            Key = "target", Name = "Level 63 training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        target.InitializeHealth(target.MaximumHealth);
        context.AddActor(shaman);
        context.AddActor(target);
        foreach (var ability in ForeverEnhancementShamanSpellbook.Create(request.Tuning,
            request.Talents, spellPower, request.WeaponSpeedSeconds,
            request.WeaponMinDamage, request.WeaponMaxDamage,
            dualWield: request.DualWield)) shaman.AddAbility(ability);
        shaman.AddAbility(ForeverShamanTotemCalls.Call("call-of-elements",
            shaman.Abilities.Values.Select(x=>x.Definition).ToArray(),request.CallOfElementsTotems));
        shaman.AddAbility(ForeverShamanTotemCalls.Recall());
        var auras = new AuraManager();
        var rolls = new EnhancementRollResolver(request);
        ForeverEnhancementShamanReactiveProcessor? maelstromState = null;
        var timing=new EnhancementMaelstromTimingProvider(
            () => maelstromState?.CurrentMaelstromStacks ?? 0,
            request.Talents.Rank("maelstrom-weapon"));
        var executor = new AbilityExecutor(auras, rolls,
            new EnhancementArmorMitigation(request.TargetArmor),
            abilityTimingProvider:timing);
        var auto = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mainHand = ForeverAutoAttackFactory.CreatePlayerMelee("main-hand-auto",
            request.DualWield ? "One-handed Shaman main-hand" : "Two-handed Shaman main-hand",
            request.WeaponMinDamage, request.WeaponMaxDamage,
            request.WeaponSpeedSeconds, "main-hand-weapon-skill",
            usesDualWieldHitTable: request.DualWield);
        var offHand = request.DualWield
            ? ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand(
                "off-hand-auto", "Enhancement off-hand", request.OffHandWeaponMinDamage,
                request.OffHandWeaponMaxDamage, request.OffHandWeaponSpeedSeconds,
                "off-hand-weapon-skill")
            : null;
        // Hand-local AP prevents off-hand Rockbiter from buffing main-hand
        // attacks (and vice versa) through one shared actor-wide stat.
        mainHand.DamageEffect.ScalingStatKey="shaman-main-hand-attack-power";
        if(offHand is not null)offHand.DamageEffect.ScalingStatKey="shaman-off-hand-attack-power";
        if(shaman.Abilities.TryGetValue("stormstrike",out var stormstrike))
            foreach(var effect in stormstrike.Definition.Effects.Where(x=>x.WeaponHandKey=="main-hand"))
                effect.ScalingStatKey="shaman-main-hand-attack-power";
        var shield=new ShamanLightningShieldProcessor(shaman,auras,executor,
            request.LightningShieldInternalCooldownSeconds);
        var imbues=new ShamanWeaponImbueProcessor(shaman,auras,executor,attackPower,
            request.WeaponSpeedSeconds,request.OffHandWeaponSpeedSeconds,
            request.Talents.Rank("elemental-weapons"),request.FrostbrandProcChancePercent);
        var totemSlots=new ShamanTotemLifecycleProcessor(shaman,auras,
            imbues.RefreshHandAttackPower);
        var windfuryWeapon = mainImbue=="windfury"
            ? new TriggeredMeleeExtraAttackProcessor(executor, "shaman", mainHand,
                "windfury-weapon-extra", request.Tuning.WindfuryWeaponProcChancePercent,
                request.Tuning.WindfuryWeaponBonusAttackPower *
                    (1m + .40m * request.Talents.Rank("elemental-weapons") / 3m),
                2, request.Tuning.WindfuryWeaponInternalCooldownSeconds)
            : null;
        var windfuryTotem = (request.UseWindfuryTotem || request.UseCallOfElements) && mainImbue!="windfury"
            ? new TriggeredMeleeExtraAttackProcessor(executor, "shaman", mainHand,
                "windfury-totem-extra", request.WindfuryTotemProcChancePercent, 246m, 1,
                isEnabled: request.DeployWindfuryTotem || request.UseCallOfElements
                    ? context => shaman.ActiveAuras.Any(a=>a.Definition.Key=="shaman-windfury-totem" &&
                        a.IsActiveAt(context.CurrentTimeSeconds)) : null)
            : null;
        var reactive = new ForeverEnhancementShamanReactiveProcessor("shaman", auto,
            auras, request.Talents, request.Tuning);
        maelstromState=reactive;
        var items = new CombatUseItemProcessor(request.CombatItems, executor, shaman, "target", "shaman");
        var rotation = BuildRotation(request);
        var priority = new PriorityRotationExecutor(rotation, "shaman", "target",
            executor, autoAttackProcessor: auto);
        var regen = new FiveSecondManaRegenerationProcessor("shaman",
            request.Tuning.ManaRegenPerSecond, stats.Get("mp5"),
            0m, () => reactive.ImprovedStormstrikeCastingSpiritFraction);
        reactive.AttachManaRegeneration(regen);
        var engine = new SimulationEngine([
            executor, auras, imbues, totemSlots, shield, auto,
            new ScriptedEncounterDamageProcessor(new EnhancementArmorMitigation(0m),
                modifyRawDamage:(ctx,defender,ev,raw)=>
                    defender.Key==shaman.Key && ev.SchoolKey=="physical" &&
                    defender.ActiveAuras.Any(a=>a.Definition.Key=="shaman-stoneskin-totem" &&
                        a.IsActiveAt(ctx.CurrentTimeSeconds)) ? Math.Max(0m,raw-30m):raw),
            ..(windfuryWeapon is null ? Array.Empty<ICombatEventProcessor>() : new ICombatEventProcessor[] { windfuryWeapon }),
            ..(windfuryTotem is null ? Array.Empty<ICombatEventProcessor>() : new ICombatEventProcessor[] { windfuryTotem }),
            reactive, regen, items,
            new ThreatManager(new SoloDamageThreatResolver("shaman",
                ev=>ev.WeaponHandKey is "main-hand" or "off-hand" &&
                    ev.AbilityKey is ("main-hand-auto" or "off-hand-auto" or "stormstrike") &&
                    (ev.WeaponHandKey=="main-hand"?imbues.MainHand:imbues.OffHand)=="rockbiter"
                        ? 1m+request.RockbiterExtraThreatMultiplier : 1m)), priority
        ]);
        regen.Initialize(context);
        var run = engine.Run(context, started =>
        {
            if (!auto.Start(started, "shaman", "target", mainHand, 0m))
                throw new InvalidOperationException("Could not start Shaman main-hand auto attacks.");
            if (offHand is not null &&
                !auto.Start(started, "shaman", "target", offHand, 0m))
                throw new InvalidOperationException("Could not start Shaman off-hand auto attacks.");
            if (request.IncomingBossHitDamage>0m)
                for(decimal at=2m;at<request.DurationSeconds;at+=request.IncomingBossSwingSeconds)
                    started.ScheduleEvent(new CombatEvent {
                        TimeSeconds=at,Type=CombatEventType.ScriptedDamage,
                        SourceActorKey=target.Key,TargetActorKey=shaman.Key,
                        SchoolKey="physical",MitigationType=DamageMitigationTypes.None,
                        RawAmount=request.IncomingBossHitDamage,
                        EncounterEventKey="enhancement-shield-test"
                    });
        });
        var report = run.Summary.ActorSummaries["shaman"];
        run.CustomMetrics["shaman-lightning-shield-triggers"]=shield.Triggers;
        run.CustomMetrics["shaman-totem-replacements"]=totemSlots.Replacements;
        run.CustomMetrics["shaman-totem-recalls"]=totemSlots.Recalls;
        run.CustomMetrics["shaman-totemic-recall-mana"]=totemSlots.RecallManaGained;
        return new EnhancementShamanSimResponse
        {
            Dps = report.DamageDone / request.DurationSeconds,
            DamageDone = report.DamageDone, EndingMana = shaman.Resources["mana"].Current,
            MaximumMana = mana, AttackPower = attackPower, SpellPower = spellPower,
            WindfuryWeaponProcs = windfuryWeapon?.ProcCount ?? 0,
            WindfuryWeaponExtraAttacks = windfuryWeapon?.ExtraAttackCount ?? 0,
            DualWieldActive = request.DualWield,
            ActiveMainHandImbue=imbues.MainHand=="none" && mainImbue=="windfury" ? "windfury" : imbues.MainHand,
            ActiveOffHandImbue=imbues.OffHand,
            OffHandAutoDamage = report.DamageDoneByAbility.GetValueOrDefault("off-hand-auto"),
            WindfuryTotemProcs = windfuryTotem?.ProcCount ?? 0,
            MaelstromInstantBolts = reactive.MaelstromInstantBolts,
            MaxMaelstromStacks = reactive.MaxMaelstromStacks,
            ImprovedStormstrikeRegenProcs = reactive.ImprovedStormstrikeRegenProcs,
            ImprovedStormstrikeCooldownResets = reactive.ImprovedStormstrikeCooldownResets,
            CombatItemUses = items.Uses.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase),
            Run = run,
            ModelingNotes = [
                "CP130: Enhancement uses the shared combat queue, real seeded Windfury weapon attacks, melee procs, Flurry and Maelstrom five-stack instant Lightning Bolt.",
                "CP166 Stage 4D: optional real dual-wield main/off-hand streams use the shared dual-white penalty and 50% off-hand damage. The default remains a two-hander for backward-compatible requests.",
                "Windfury Weapon suppresses personal Windfury Totem; both cannot proc for the same Shaman; extra attacks do not recursively trigger either proc.",
                "Stormstrike lands a weapon attack and enhances the next nature spell by 20%; fully verified cooldown reset/totem party AI is future work.",
                "Damage values, spell hit, armor, Maelstrom proc chance and Mana regeneration are configurable provisional Forever assumptions.",
                "Rage of the Farseer grants 30% attack speed for 25s, Improved Stormstrike grants 15s casting Spirit regen and incoming avoidance cooldown resets, and Maelstrom Weapon now discounts normal Lightning Bolt cast time and cost by 4% per rank per stack while preserving five-stack instant Bolt. Full Maelstrom internal proc chance remains tunable.",
                "Windfury Totem can be explicitly deployed for its current 250 Mana/300s lifetime; the older static assumption remains selectable for regression compatibility. Party radius and competing air totems remain deferred.",
                "CP167 weapon imbues are hand-local with real aura placement and Mana costs. Rockbiter extra threat and Frostbrand proc chance are configurable until verified; speed-dependent Flametongue damage currently uses an explicitly provisional curve."
            ]
        };
    }

    private static RotationProfile BuildRotation(EnhancementShamanSimRequest request)
    {
        var rotation = new RotationProfile
        {
            Name = "Enhancement Shaman CP130", ClassKey = "shaman",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        var p = 0;
        if(request.UseStoneskinTotem)
            rotation.Entries.Add(new RotationEntry {Priority=++p,AbilityKey="stoneskin-totem",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="shaman-stoneskin-totem"
                }]});
        if(request.UseCallOfElements)
            rotation.Entries.Add(new RotationEntry {Priority=++p,AbilityKey="call-of-elements",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="shaman-strength-of-earth-totem"
                }]});
        if(request.UseTotemicRecall)
            rotation.Entries.Add(new RotationEntry {Priority=++p,AbilityKey="totemic-recall",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraActive,Key="shaman-strength-of-earth-totem"
                }]});
        if(request.DeployWindfuryTotem && request.UseWindfuryTotem && !request.UseCallOfElements)
            rotation.Entries.Add(new RotationEntry {Priority=++p,AbilityKey="windfury-totem",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="shaman-windfury-totem"
                }]});
        if(request.UseLightningShield)
            rotation.Entries.Add(new RotationEntry {Priority=++p,AbilityKey="lightning-shield",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="shaman-lightning-shield"
                }]});
        void add(bool enabled, string key)
        {
            if (enabled) rotation.Entries.Add(new RotationEntry { Priority = ++p, AbilityKey = key });
        }
        void imbue(string hand,string name)
        {
            if(name is not ("flametongue" or "rockbiter" or "frostbrand"))return;
            var key=$"{name}-weapon-{hand}";
            rotation.Entries.Add(new RotationEntry {
                Priority=++p,AbilityKey=key,
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,
                    Key=$"shaman-imbue-{hand}-{name}"
                }]
            });
        }
        var mainImbue=request.MainHandImbue ??
            (request.UseWindfuryWeapon?"windfury":"none");
        imbue("main-hand",mainImbue);
        if(request.DualWield)imbue("off-hand",request.OffHandImbue);
        add(request.UseRageOfTheFarseer &&
            request.Talents.Rank("rage-of-the-farseer") > 0,"rage-of-the-farseer");
        add(request.UseMaelstromLightningBolt && request.Talents.Rank("maelstrom-weapon") > 0,
            "maelstrom-lightning-bolt");
        add(request.UseStormstrike && request.Talents.Rank("stormstrike") > 0, "stormstrike");
        add(request.UseEarthShock, "earth-shock");
        add(request.UseLightningBoltFiller, "lightning-bolt");
        return rotation;
    }

    private sealed class EnhancementRollResolver(EnhancementShamanSimRequest req) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState dst, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(ctx, src, dst, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState src,
            SimulationActorState dst, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            var isMelee = effect.WeaponHandKey is not null;
            // The dual-wield miss penalty applies only to WHITE auto attacks.
            // Stormstrike and additional Windfury attacks are special attacks.
            var dualWhite = req.DualWield &&
                ability.Key is ("main-hand-auto" or "off-hand-auto");
            var hitChance = isMelee
                ? Math.Max(0m,req.BaseMeleeHitPercent - (dualWhite ? 19m : 0m))
                : req.BaseSpellHitPercent;
            if (effect.CanMiss && (decimal)ctx.Random.NextDouble() * 100m >= hitChance)
                return CombatRollResult.Miss();
            if (isMelee && effect.CanBeDodged && (decimal)ctx.Random.NextDouble() * 100m < 5m)
                return CombatRollResult.Dodge();
            if (isMelee && effect.CanGlance &&
                (decimal)ctx.Random.NextDouble() * 100m < req.ProvisionalGlancingChancePercent)
                return CombatRollResult.Glancing(.70m);
            if (effect.CanCrit && (decimal)ctx.Random.NextDouble() * 100m <
                Math.Min(100m, req.CriticalChancePercent +
                    req.Talents.Rank("thundering-strikes")))
                return CombatRollResult.Critical(2m);
            return CombatRollResult.Hit();
        }
    }
    private sealed class EnhancementArmorMitigation(decimal armor) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
        {
            if (effect.MitigationType != DamageMitigationTypes.Armor)
                return DamageMitigationResult.Unmitigated(rawAmount);
            // Same 75% armor ceiling enforced for previously audited physical
            // classes (CP166 Stage 2); do not let Shaman diverge.
            var reduction = ForeverArmorMitigationMath.ClampArmorReductionFraction(
                armor / (armor + 400m + 85m * Math.Max(1, source.Level)));
            var result = rawAmount * (1m - reduction);
            return new DamageMitigationResult
            {
                RawAmount = rawAmount, FinalAmount = result,
                MitigatedAmount = rawAmount - result,
                ReductionPercent = reduction * 100m, DefenseValue = armor
            };
        }
    }
}
