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
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "shaman", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, "enhancement", StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m || request.MaximumHealth <= 0m ||
            request.TargetMaximumHealth <= 0m || request.TargetArmor < 0m ||
            request.WeaponMinDamage < 0m || request.WeaponMaxDamage < request.WeaponMinDamage ||
            request.WeaponSpeedSeconds <= 0m || request.BaseMeleeHitPercent is < 0m or > 100m ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.CriticalChancePercent is < 0m or > 100m ||
            request.ProvisionalGlancingChancePercent is < 0m or > 100m ||
            request.Tuning.ManaRegenPerSecond < 0m ||
            request.Tuning.MaelstromChancePerMeleePercentPerRank < 0m ||
            request.Tuning.WindfuryWeaponProcChancePercent is < 0m or > 100m)
            throw new ArgumentException("Invalid Enhancement Shaman setup or level-60 specialization.");
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
            request.WeaponMinDamage, request.WeaponMaxDamage)) shaman.AddAbility(ability);
        var auras = new AuraManager();
        var rolls = new EnhancementRollResolver(request);
        var executor = new AbilityExecutor(auras, rolls,
            new EnhancementArmorMitigation(request.TargetArmor));
        var auto = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mainHand = ForeverAutoAttackFactory.CreatePlayerMelee("main-hand-auto",
            "Two-handed Shaman main-hand", request.WeaponMinDamage,
            request.WeaponMaxDamage, request.WeaponSpeedSeconds, "main-hand-weapon-skill");
        var windfuryWeapon = request.UseWindfuryWeapon
            ? new TriggeredMeleeExtraAttackProcessor(executor, "shaman", mainHand,
                "windfury-weapon-extra", request.Tuning.WindfuryWeaponProcChancePercent,
                request.Tuning.WindfuryWeaponBonusAttackPower *
                    (1m + .40m * request.Talents.Rank("elemental-weapons") / 3m),
                2, request.Tuning.WindfuryWeaponInternalCooldownSeconds)
            : null;
        var windfuryTotem = request.UseWindfuryTotem && !request.UseWindfuryWeapon
            ? new TriggeredMeleeExtraAttackProcessor(executor, "shaman", mainHand,
                "windfury-totem-extra", 20m, 246m, 1)
            : null;
        var reactive = new ForeverEnhancementShamanReactiveProcessor("shaman", auto,
            auras, request.Talents, request.Tuning);
        var rotation = BuildRotation(request);
        var priority = new PriorityRotationExecutor(rotation, "shaman", "target",
            executor, autoAttackProcessor: auto);
        var regen = new FiveSecondManaRegenerationProcessor("shaman",
            request.Tuning.ManaRegenPerSecond, stats.Get("mp5"),
            0m);
        var engine = new SimulationEngine([
            executor, auras, auto,
            ..(windfuryWeapon is null ? Array.Empty<ICombatEventProcessor>() : new ICombatEventProcessor[] { windfuryWeapon }),
            ..(windfuryTotem is null ? Array.Empty<ICombatEventProcessor>() : new ICombatEventProcessor[] { windfuryTotem }),
            reactive, regen, priority
        ]);
        regen.Initialize(context);
        var run = engine.Run(context, started =>
        {
            if (!auto.Start(started, "shaman", "target", mainHand, 0m))
                throw new InvalidOperationException("Could not start Shaman melee auto attacks.");
        });
        var report = run.Summary.ActorSummaries["shaman"];
        return new EnhancementShamanSimResponse
        {
            Dps = report.DamageDone / request.DurationSeconds,
            DamageDone = report.DamageDone, EndingMana = shaman.Resources["mana"].Current,
            MaximumMana = mana, AttackPower = attackPower, SpellPower = spellPower,
            WindfuryWeaponProcs = windfuryWeapon?.ProcCount ?? 0,
            WindfuryWeaponExtraAttacks = windfuryWeapon?.ExtraAttackCount ?? 0,
            WindfuryTotemProcs = windfuryTotem?.ProcCount ?? 0,
            MaelstromInstantBolts = reactive.MaelstromInstantBolts,
            MaxMaelstromStacks = reactive.MaxMaelstromStacks,
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase),
            Run = run,
            ModelingNotes = [
                "CP130: Enhancement uses the shared combat queue, real seeded Windfury weapon attacks, melee procs, Flurry and Maelstrom five-stack instant Lightning Bolt.",
                "Two-handed Shaman main-hand only. Windfury Weapon suppresses personal Windfury Totem; both cannot proc for the same Shaman.",
                "Stormstrike lands a weapon attack and enhances the next nature spell by 20%; fully verified cooldown reset/totem party AI is future work.",
                "Damage values, spell hit, armor, Maelstrom proc chance and Mana regeneration are configurable provisional Forever assumptions.",
                "Maelstrom partial-stack cast-time/cost reduction, Improved Stormstrike casting regen/reset and Rage of the Farseer haste have not been modeled yet.",
                "Totems in this solo preview are static party assumptions; range, destruction, air slot conflict and refresh GCD are not simulated."
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
        void add(bool enabled, string key)
        {
            if (enabled) rotation.Entries.Add(new RotationEntry { Priority = ++p, AbilityKey = key });
        }
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
            var hitChance = isMelee ? req.BaseMeleeHitPercent : req.BaseSpellHitPercent;
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
            var reduction = armor / (armor + 400m + 85m * Math.Max(1, source.Level));
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
