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
            request.Tuning.LifeTapHealthCost < 0m || request.Tuning.LifeTapMana < 0m ||
            request.PetType is not ("succubus" or "incubus" or "imp" or "felhunter" or "voidwalker"))
            throw new ArgumentException("Invalid Warlock level-60 configuration.");
        request.CombatItems.Validate("warlock");
        request.Talents.Validate(60, request.Specialization);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.WarlockMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var intellect = Math.Max(0m, stats.Get("intellect"));
        var spellPower = Math.Max(0m, stats.Get("spell-power")) +
            Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(
                request.Talents.Rank("demonic-knowledge"),60,request.EnablePet);
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
        var rotation = WarlockAdaptiveRotation.Build(request);
        var adaptive = new WarlockAdaptivePriorityProcessor(request,rotation,warlock);
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
                Key = "warlock-pet", Name = $"Warlock {request.PetType} (generic melee model)",
                TeamKey = "raid", Level = 60, MaximumHealth = 2500m *
                    (1m + request.Talents.Rank("fel-vitality") * .05m),
                Stats = new StatCollection { Values = new Dictionary<string, decimal>
                {
                    ["attack-power"] = petAttackPower, ["main-hand-weapon-skill"] = 300m
                } }
            };
            pet.InitializeHealth(pet.MaximumHealth);
            context.AddActor(pet);
            var petBase = request.Tuning.PetBaseDamage;
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
                    MinimumValue = (petBase + petScaling) *
                        (1m + request.Talents.Rank("unholy-power") * .02m),
                    MaximumValue = (petBase + petScaling * 1.1m) *
                        (1m + request.Talents.Rank("unholy-power") * .02m)
                }
            };
        }
        var engine = new SimulationEngine([executor, auras, auto, regen, items, adaptive, priority]);
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
            ResolvedRotation = string.Join(" > ",rotation.Entries.Select(x=>x.AbilityKey)),
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
                "Pet remains provisional generic melee; Imp Firebolt, pet specials, pet AI and threat remain unmodeled. Pet type selects Master Demonologist bonus.",
                "Demonic Knowledge and Soul Link now provide their level-based Spell Power / 3% pet-and-owner damage with an active pet. Unholy Power grants 2% pet damage per rank.",
                "Demonic Pact is NOT a raid Spell Power buff: it retains Demonic Sacrifice when a different demon is summoned, a future summon/sacrifice system feature.",
                "Warlock Banes are selected as one active damage Bane for the solo rotation; utility Curses and multiple targets are deferred.",
                "Incinerate gains 25% damage against a currently Immolated target; Wrack grants a six-second Shadow DoT vulnerability.",
                "Conflagrate consumes Immolate unless protected by Shadow and Flame; exact Shadow and Flame chance/cross-school procs remain pending.",
                "Warlock beta spell ranks, hit, spell coefficients, pet scaling and Mana regeneration are configurable illustrative assumptions, not calibrated DPS predictions.",
                request.AssumeExternalHealingForLifeTap
                    ? "Solo external healing assumption: Life Tap uses its GCD and yields Mana without HP limiting the rotation; external healer output and mana are not simulated."
                    : "Actual Life Tap health expenditure is enabled. Without healer support, the Warlock may eventually become HP-limited.",
                "CP140: default priority gates late DoT refreshes, channel duration, Soul Fire Mana spending, and Life Tap near fight end. User customPriority overrides spell gates.",
                "CP140: These are talent-aware solo defaults, not yet automatically optimized with all real pre-raid item stats, raid debuffs, trinket activation, add timers or per-pet abilities.",
                "This is a solo training target, not a survival or raid utility encounter."
            ]
        };
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
                    request.Talents.Rank("suppression") + source.Stats.Get("spell-hit-percent") +
                    source.Stats.Get("spell-hit-chance-percent"));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            var crit = effect.CanCrit && (decimal)context.Random.NextDouble() * 100m <
                (pet ? 10m : request.SpellCritPercent +
                    source.Stats.Get("spell-critical-chance-percent") + effect.BonusCriticalChancePercent);
            bool isDestructionSpell = ability.Key is "shadow-bolt" or "immolate" or
                "incinerate" or "conflagrate" or "soul-fire";
            var baseRoll = crit ? CombatRollResult.Critical(pet ? 2m :
                1.5m + (isDestructionSpell ? request.Talents.Rank("ruin") * .1m : 0m)) :
                CombatRollResult.Hit();
            bool immolate = target.ActiveAuras.Any(a => a.SourceActorKey == "warlock" &&
                a.Definition.Key == "immolate" && a.IsActiveAt(context.CurrentTimeSeconds));
            bool wrack = target.ActiveAuras.Any(a => a.SourceActorKey == "warlock" &&
                a.Definition.Key == "wrack-vulnerability" && a.IsActiveAt(context.CurrentTimeSeconds));
            var bonus = Warlock70425DamageModifiers.Multiplier(source.Key, ability.Key,
                effect.SchoolKey, deliveryType, immolate, wrack, request.PetType,
                request.EnablePet ? request.Talents.Rank("master-demonologist") : 0,
                request.EnablePet && request.Talents.Rank("soul-link") > 0,
                request.Talents.Rank("agonizing-flames"));
            return new CombatRollResult { Landed = true, IsCritical = baseRoll.IsCritical,
                ResultKey = baseRoll.ResultKey,
                AmountMultiplier = baseRoll.AmountMultiplier * bonus };
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
