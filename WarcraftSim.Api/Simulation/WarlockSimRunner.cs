using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP131: three functional Warlock DPS rotations in the same event-driven
/// simulation engine as Shadow Priest and Enhancement. Pet attacks are a real
/// second actor, with independent swing timing and damage attribution.</summary>
public sealed class WarlockSimRunner
{
    public WarlockSimResponse Run(WarlockSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "warlock", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, request.Specialization, StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.WarlockMaximumHealth <= 0m ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.SpellCritPercent is < 0m or > 100m ||
            request.PetMeleeHitPercent is < 0m or > 100m ||
            request.StartingManaPercent is < 0m or > 100m ||
            request.LifeTapBelowManaPercent is < 0m or > 100m ||
            request.LifeTapAboveHealthPercent is < 0m or > 100m ||
            request.CombatItems is null || request.Tuning.PetSwingSeconds <= 0m || request.Tuning.PetBaseDamage < 0m ||
            request.Tuning.LifeTapHealthCost < 0m || request.Tuning.LifeTapMana < 0m)
            throw new ArgumentException("Invalid Warlock level-60 configuration.");
        request.CombatItems.Validate("warlock");
        request.Talents.Validate(60, request.Specialization);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.WarlockMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var intellect = Math.Max(0m, stats.Get("intellect"));
        var spellPower = Math.Max(0m, stats.Get("spell-power"));
        var maximumMana = (1376m + Math.Max(0m, intellect - 20m) * 15m) *
            (1m + .05m * request.Talents.Rank("fel-vitality"));
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            CaptureTimeline = request.CaptureTimeline, PrimaryActorKey = "warlock",
            MaximumProcessedEvents = 50000, CancellationToken = cancellationToken
        });
        var warlock = new SimulationActorState
        {
            Key = "warlock", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.WarlockMaximumHealth, Stats = stats
        };
        warlock.InitializeHealth(warlock.MaximumHealth);
        warlock.AddResource(new ResourceState("mana", maximumMana,
            maximumMana * request.StartingManaPercent / 100m));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Warlock training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(warlock);
        context.AddActor(boss);
        foreach (var ability in ForeverWarlockDpsSpellbook.Create(
            request.Specialization, request.Talents, request.Tuning, spellPower, spirit))
        {
            if (request.AssumeExternalHealingForLifeTap && ability.Key == "life-tap")
            {
                // Solo-DPS abstraction: an unseen raid healer immediately
                // replaces Life Tap HP. Keep the GCD, spell use, and Mana gained
                // without inventing healer mana expenditure or self-healing HPS.
                var healthCost = ability.Effects.First(effect => effect.Key == "life-tap-health");
                healthCost.MinimumValue = 0m;
                healthCost.MaximumValue = 0m;
            }
            warlock.AddAbility(ability);
        }
        var auras = new AuraManager();
        var rolls = new WarlockRollResolver(request);
        var executor = new AbilityExecutor(auras, rolls, new WarlockNoMitigation());
        var items = new CombatUseItemProcessor(request.CombatItems, executor, warlock, "target", "warlock");
        var rotation = BuildRotation(request);
        var priority = new PriorityRotationExecutor(rotation, "warlock", "target", executor,
            reactToTargetStateChanges: true);
        var regen = new FiveSecondManaRegenerationProcessor("warlock",
            request.Tuning.ManaRegenerationPerSecond, Math.Max(0m, stats.Get("mp5")), 0m);
        var auto = new AutoAttackProcessor(executor);
        AutoAttackDefinition? petAttack = null;
        if (request.EnablePet)
        {
            var petAttackPower = 200m + intellect * request.Tuning.PetAttackPowerFromIntellect;
            var pet = new SimulationActorState
            {
                Key = "warlock-pet", Name = "Warlock demon (provisional melee pet)",
                TeamKey = "raid", Level = 60, MaximumHealth = 2500m,
                Stats = new StatCollection { Values = new Dictionary<string, decimal>
                {
                    ["attack-power"] = petAttackPower, ["main-hand-weapon-skill"] = 300m
                } }
            };
            pet.InitializeHealth(pet.MaximumHealth);
            context.AddActor(pet);
            var petBase = request.Tuning.PetBaseDamage *
                (1m + request.Talents.Rank("unholy-power") * .04m);
            var petScaling = petAttackPower * request.Tuning.PetSwingSeconds / 14m;
            petAttack = new AutoAttackDefinition
            {
                Key = "pet-melee", Name = "Demon melee attack",
                SwingIntervalSeconds = request.Tuning.PetSwingSeconds,
                DamageEffect = new AbilityEffectDefinition
                {
                    Key = "damage", EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                    ResolutionType = "forever-player-melee-auto",
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    CanMiss = true, CanCrit = true,
                    MinimumValue = petBase + petScaling,
                    MaximumValue = petBase + petScaling * 1.1m
                }
            };
        }
        var engine = new SimulationEngine([executor, auras, auto, regen, items, priority]);
        regen.Initialize(context);
        var run = engine.Run(context, started =>
        {
            if (petAttack is not null && !auto.Start(started, "warlock-pet", "target", petAttack, 0m))
                throw new InvalidOperationException("Failed to start the Warlock pet melee stream.");
        });
        var owner = run.Summary.ActorSummaries["warlock"];
        var petDamage = run.Summary.ActorSummaries.TryGetValue("warlock-pet", out var petReport) ?
            petReport.DamageDone : 0m;
        var breakdown = new Dictionary<string, decimal>(owner.DamageDoneByAbility,
            StringComparer.OrdinalIgnoreCase);
        if (petDamage > 0m) breakdown["pet-melee"] = petDamage;
        return new WarlockSimResponse
        {
            Specialization = request.Specialization,
            DamageDone = owner.DamageDone + petDamage,
            Dps = (owner.DamageDone + petDamage) / request.DurationSeconds,
            PetDamage = petDamage, EndingMana = warlock.Resources["mana"].Current,
            MaximumMana = maximumMana, EndingHealth = warlock.CurrentHealth,
            SpellPower = spellPower,
            CombatItemUses = items.Uses.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            LifeTapCasts = owner.Abilities.TryGetValue("life-tap", out var tap) ?
                tap.CastCompletedCount : 0,
            DamageByAbility = breakdown, Run = run,
            ModelingNotes =
            [
                "CP131 broad Warlock DPS foundations: Corruption, Bane of Agony, Immolate, Shadow Bolt, Wrack, Soul Fire, Incinerate, Conflagrate and Spirit-scaling Life Tap.",
                "Affliction, Demonology and Destruction use separate modeled talent subsets and rotation priorities.",
                "The demon is a separate melee actor with its own swing stream and damage; pet specials, individual demon types, Demonic Pact, advanced AI and threat still require implementation.",
                "Warlock Banes are selected as one active damage Bane for the solo rotation; utility Curses and multiple targets are deferred.",
                "Conflagrate is gated on Immolate but does not consume its aura yet; Incinerate's conditional Immolate bonus is not yet implemented.",
                "Warlock beta spell ranks, hit, spell coefficients, pet scaling and Mana regeneration are configurable illustrative assumptions, not calibrated DPS predictions.",
                request.AssumeExternalHealingForLifeTap
                    ? "Solo external healing assumption: Life Tap uses its GCD and yields Mana without HP limiting the rotation; external healer output and mana are not simulated."
                    : "Actual Life Tap health expenditure is enabled. Without healer support, the Warlock may eventually become HP-limited.",
                "This is a solo training target, not a survival or raid utility encounter."
            ]
        };
    }

    private static RotationProfile BuildRotation(WarlockSimRequest request)
    {
        var rotation = new RotationProfile
        {
            Name = $"Warlock {request.Specialization} CP131", ClassKey = "warlock",
            RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        var next = 0;
        void add(string key, params RotationConditionDefinition[] conditions) =>
            rotation.Entries.Add(new RotationEntry
            {
                Priority = next++, AbilityKey = key, Conditions = conditions.ToList()
            });
        static RotationConditionDefinition missing(string key) => new()
        {
            ConditionType = RotationConditionTypes.TargetAuraMissing, Key = key
        };
        if (request.UseLifeTap)
        {
            var tapConditions = new List<RotationConditionDefinition>
            {
                new()
                {
                    ConditionType = RotationConditionTypes.SourceResourcePercent,
                    Key = "mana", ComparisonOperator = RotationComparisonOperators.LessThanOrEqual,
                    Value = request.LifeTapBelowManaPercent
                }
            };
            if (!request.AssumeExternalHealingForLifeTap)
                tapConditions.Add(new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.SourceHealthPercent,
                    ComparisonOperator = RotationComparisonOperators.GreaterThan,
                    Value = request.LifeTapAboveHealthPercent
                });
            add("life-tap", tapConditions.ToArray());
        }
        if (request.UseCorruption) add("corruption", missing("corruption"));
        if (request.UseBaneOfAgony) add("bane-of-agony", missing("bane-of-agony"));
        if (request.UseImmolate) add("immolate", missing("immolate"));
        switch (request.Specialization)
        {
            case "affliction":
                if (request.UseWrack) add("wrack");
                add("shadow-bolt");
                break;
            case "demonology":
                add("soul-fire");
                add("shadow-bolt");
                break;
            case "destruction":
                if (request.UseConflagrate) add("conflagrate", new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.TargetAuraActive, Key = "immolate"
                });
                if (request.UseIncinerate) add("incinerate");
                add("shadow-bolt");
                break;
        }
        return rotation;
    }

    private sealed class WarlockRollResolver(WarlockSimRequest request) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            var pet = string.Equals(source.Key, "warlock-pet", StringComparison.OrdinalIgnoreCase);
            var hit = pet ? request.PetMeleeHitPercent :
                Math.Min(100m, request.BaseSpellHitPercent +
                    request.Talents.Rank("suppression") + source.Stats.Get("spell-hit-percent"));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            if (effect.CanCrit && (decimal)context.Random.NextDouble() * 100m <
                (pet ? 10m : request.SpellCritPercent + effect.BonusCriticalChancePercent))
                return CombatRollResult.Critical(pet ? 2m : 1.5m + request.Talents.Rank("ruin") * .1m);
            return CombatRollResult.Hit();
        }
    }
    private sealed class WarlockNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
}
