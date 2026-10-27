using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP142 target-owned combo points, landed-hit generators, finishers,
/// seeded poison procs and temporary Energy regeneration. This is one processor
/// per solo Rogue run; it is not shared/static state.</summary>
public sealed class RogueCombatProcessor : ICombatEventProcessor
{
    private static readonly HashSet<string> Generators = new(StringComparer.OrdinalIgnoreCase)
    { "sinister-strike", "backstab", "mutilate", "hemorrhage", "ambush", "garrote", "ghostly-strike", "cheap-shot" };
    private readonly RogueSimRequest _request;
    private readonly SimulationActorState _actor;
    private readonly RotationProfile _rotation;
    private readonly AbilityExecutor _executor;
    private readonly AuraManager _auras;
    private readonly AutoAttackProcessor _auto;
    private readonly AutoAttackDefinition[] _weapons;
    private bool _inCombat;
    private int _stealthOpenerCount;
    public int StealthOpeners => _stealthOpenerCount;
    public bool IsStealthed(SimulationContext context) => HasAura(context, "rogue-stealth");
    private readonly Dictionary<string, int> _points = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Guid> _generatorsAwarded = [];
    private readonly HashSet<Guid> _finishersSpent = [];
    private readonly HashSet<Guid> _sealFateAwarded = [];
    private int _ruptureSnapshotPoints;
    private readonly HashSet<Guid> _quickRecoveryRefunded = [];
    public int PreparationResets { get; private set; }
    public int HackAndSlashAttacks { get; private set; }
    public decimal QuickRecoveryEnergyRestored { get; private set; }
    public int ExposeArmorPoints { get; private set; }
    private Guid? _pendingExposeExecutionId;
    private int _thousandCutsStacks;
    public int ThousandCutsStacks => _thousandCutsStacks;
    public int FinishersUsed { get; private set; }
    public int ComboPointsFor(string target) => _points.GetValueOrDefault(target);
    public int RuptureSnapshotPoints => _ruptureSnapshotPoints;

    public RogueCombatProcessor(RogueSimRequest request, SimulationActorState actor,
        RotationProfile rotation, AbilityExecutor executor, AuraManager auras,
        AutoAttackProcessor auto, AutoAttackDefinition[] weapons)
    {
        _request = request; _actor = actor; _rotation = rotation; _executor = executor;
        _auras = auras; _auto = auto; _weapons = weapons;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type == CombatEventType.SimulationStarted)
        {
            if (_request.StartInStealth) EnterStealth(context, "opening-stealth");
            UpdateRegen(context); return;
        }
        if (!string.Equals(ev.SourceActorKey, _actor.Key, StringComparison.OrdinalIgnoreCase))
        {
            if (ev.Type == CombatEventType.AuraRemoved && ev.TargetActorKey == _actor.Key)
                UpdateRegen(context);
            return;
        }
        if (ev.Type == CombatEventType.AuraApplied || ev.Type == CombatEventType.AuraRemoved)
        {
            // Expose Armor is the Rogue finisher whose combo points are spent
            // only when the debuff actually lands, not on cast completion.
            // Garrote grants its combo point only when the missable bleed actually applies.
            // Awarding at cast completion incorrectly gave points after a failed application.
            if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "garrote" &&
                ev.TargetActorKey is { } garroteTarget)
                AddPoints(garroteTarget, 1);
            if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "expose-armor" &&
                ev.TargetActorKey is not null)
            {
                SpendFinisher(context, _pendingExposeExecutionId, ev.TargetActorKey, "expose-armor");
                _pendingExposeExecutionId = null;
            }
            UpdateRegen(context);
            return;
        }
        if (ev.Type == CombatEventType.RotationDecision)
        {
            UpdateRotation(context); return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "preparation")
        {
            // Reset only explicitly eligible Rogue cooldowns. Do not reset
            // Preparation itself, shared GCDs, resources or auras.
            foreach (var key in new[] { "vanish", "evasion", "sprint", "blind",
                "cold-blood", "ghostly-strike" })
                if (_actor.Abilities.TryGetValue(key, out var state))
                {
                    state.ResetCooldown(context.CurrentTimeSeconds);
                    PreparationResets++;
                }
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "vanish")
        {
            EnterStealth(context, "vanish");
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "riposte")
        {
            _auras.RemoveAuras(context, _actor, "riposte-ready", _actor.Key,
                "riposte opportunity spent");
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "premeditation" && ev.TargetActorKey is not null)
        {
            AddPoints(ev.TargetActorKey, 2);
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("ambush" or "garrote" or "cheap-shot"))
        {
            _stealthOpenerCount++;
            _inCombat = true;
            if (ev.AbilityKey == "ambush")
            {
                _auras.RemoveAuras(context, _actor, "rogue-cutthroat", _actor.Key);
            }
            ExitStealth(context);
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "cheap-shot" && ev.TargetActorKey is not null)
            AddPoints(ev.TargetActorKey, 2);
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("eviscerate" or "slice-and-dice" or "venom" or "rupture" or "kidney-shot") &&
            ev.TargetActorKey is not null)
        {
            SpendFinisher(context, ev.AbilityExecutionId, ev.TargetActorKey, ev.AbilityKey!);
            return;
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "expose-armor")
            _pendingExposeExecutionId = ev.AbilityExecutionId;
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("backstab" or "hemorrhage") &&
            _request.Talents.Rank("thousand-cuts") > 0)
        {
            _thousandCutsStacks = 0;
            return;
        }
        // Missed finishers do not earn damage or combo-point-on-hit bonuses.
        // Quick Recovery refunds a configured portion of the spent Energy.
        if (ev.Type == CombatEventType.Damage && !ev.IsPeriodic &&
            ev.ResultKey == CombatResultTypes.Miss &&
            ev.AbilityExecutionId is Guid missId &&
            ev.AbilityKey is ("eviscerate" or "rupture") &&
            _quickRecoveryRefunded.Add(missId) &&
            _request.Talents.Rank("quick-recovery") > 0)
        {
            // Refund against the actually configured cast cost. Talent-driven
            // energy discounts change the AbilityDefinition, so a separate
            // hard-coded base cost would over- or under-refund missed finishers.
            var energyCost = _actor.Abilities.TryGetValue(ev.AbilityKey!, out var castState)
                ? castState.Definition.ResourceCosts
                    .Where(c => string.Equals(c.ResourceKey, "energy", StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.Amount)
                : 0m;
            var refund = energyCost * _request.Talents.Rank("quick-recovery") *
                _request.Tuning.QuickRecoveryEnergyRefundPercentPerRank / 100m;
            _actor.RefreshResources(context.CurrentTimeSeconds);
            var gained = _actor.Resources["energy"].Gain(refund);
            QuickRecoveryEnergyRestored += gained;
            if (gained > 0m)
                context.EmitEvent(new CombatEvent {
                    Type = CombatEventType.ResourceChanged,
                    TimeSeconds = context.CurrentTimeSeconds,
                    SourceActorKey = _actor.Key, TargetActorKey = _actor.Key,
                    AbilityKey = "quick-recovery", ResourceKey = "energy",
                    Amount = gained,
                    ResourceTotalAfter = _actor.Resources["energy"].Current,
                    Description = "Quick Recovery refunded Energy after missed finisher."
                });
        }
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m) return;
        if (!ev.IsPeriodic && ev.AbilityKey != "blade-flurry-cleave")
        {
            _inCombat = true;
            if (IsStealthed(context)) ExitStealth(context);
            if (ev.AbilityKey == "backstab" && _request.Talents.Rank("cutthroat") > 0 &&
                (decimal)context.Random.NextDouble() * 100m < 20m * _request.Talents.Rank("cutthroat"))
            {
                // A narrow source-aura gate allows an Ambush without Stealth.
                _auras.ApplyAura(context, _actor, new AuraDefinition {
                    Key = "rogue-cutthroat", Name = "Cutthroat opportunity", DurationSeconds = 12m
                }, _actor.Key, "cutthroat", "cutthroat-opportunity");
            }
            if (ev.AbilityKey != "instant-poison" &&
                ev.AbilityKey != "blade-flurry-cleave" &&
                HasAura(context, "blade-flurry"))
                Cleave(context, ev);
            TriggerHackAndSlash(context, ev);
        }
        if (ev.AbilityKey is not null && ev.TargetActorKey is not null &&
            !ev.IsPeriodic && Generators.Contains(ev.AbilityKey) &&
            ev.AbilityExecutionId is Guid generatorId)
        {
            if (_generatorsAwarded.Add(generatorId))
                AddPoints(ev.TargetActorKey, ev.AbilityKey == "mutilate" ? 2 : 1);
            // Puncturing Wounds gives Backstab a rank-dependent extra point.
            if (ev.AbilityKey == "backstab" && _request.Talents.Rank("puncturing-wounds") > 0 &&
                (decimal)context.Random.NextDouble() * 100m <
                    _request.Talents.Rank("puncturing-wounds") *
                    _request.Tuning.PuncturingWoundsExtraPointChancePerRank)
                AddPoints(ev.TargetActorKey, 1);
            // Either hit of Mutilate can trigger Seal Fate, but at most once.
            if (ev.IsCritical && _request.Talents.Rank("seal-fate") > 0 &&
                _sealFateAwarded.Add(generatorId) &&
                (decimal)context.Random.NextDouble() * 100m <
                _request.Talents.Rank("seal-fate") * 20m)
                AddPoints(ev.TargetActorKey, 1);
        }
        if (ev.IsPeriodic && ev.AbilityKey == "rupture" &&
            _request.Talents.Rank("thousand-cuts") > 0)
            _thousandCutsStacks = Math.Min(5, _thousandCutsStacks + 1);

    }

    public bool ConsumeColdBlood(SimulationContext context, string ability)
    {
        if (ability is not ("ambush" or "mutilate" or "eviscerate" or "backstab" or
            "sinister-strike" or "hemorrhage") || !HasAura(context, "cold-blood")) return false;
        _auras.RemoveAuras(context, _actor, "cold-blood", _actor.Key, "consumed by successful special");
        return true;
    }

    public decimal DamageMultiplier(string abilityKey, CombatEffectDeliveryType delivery,
        SimulationContext context, SimulationActorState target)
    {
        decimal conditional = 1m;
        if (HasAura(context, "master-of-subtlety-window"))
            conditional *= 1m + _request.Talents.Rank("master-of-subtlety") *
                _request.Tuning.MasterOfSubtletyDamagePercentPerRank / 100m;
        if (target.ActiveAuras.Any(a => a.Definition.Key == "kidney-shot-stun" &&
            a.SourceActorKey == _actor.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
            conditional += _request.Talents.Rank("improved-kidney-shot") *
                _request.Tuning.ImprovedKidneyShotDamagePerRank;
        if (abilityKey == "garrote" && delivery == CombatEffectDeliveryType.Periodic)
            conditional *= 1m + _request.Talents.Rank("improved-garrote") *
                _request.Tuning.ImprovedGarroteDamagePerRank;
        if (abilityKey == "eviscerate") return conditional * Math.Max(1, ComboPointsFor(target.Key));
        if (abilityKey == "rupture")
        {
            var buff = target.ActiveAuras.Any(a => a.Definition.Key == "hemorrhage-rupture" &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            return conditional * Math.Max(1, _ruptureSnapshotPoints) * (buff ? 1.10m : 1m);
        }
        if (abilityKey == "instant-poison")
            return conditional * (1m + _request.Talents.Rank("vile-poisons") * .04m) *
                (HasAura(context, "venom") ? 1.30m : 1m);
        if (abilityKey == "mutilate")
        {
            var poisoned = target.ActiveAuras.Any(a => a.Definition.Key == "deadly-poison" &&
                a.IsActiveAt(context.CurrentTimeSeconds));
            return poisoned ? 1.20m : 1m;
        }
        return conditional;
    }

    private void UpdateRotation(SimulationContext context)
    {
        var cp = ComboPointsFor("target");
        foreach (var entry in _rotation.Entries)
            entry.IsEnabled = entry.AbilityKey switch
            {
                "slice-and-dice" => cp >= 2 && !HasAura(context, "slice-and-dice"),
                "venom" => cp >= 3 && !HasAura(context, "venom"),
                "rupture" => cp >= 4 && !context.GetActor("target")!.ActiveAuras.Any(a =>
                    a.Definition.Key == "rupture" && a.IsActiveAt(context.CurrentTimeSeconds)),
                "eviscerate" => cp >= 4,
                "ambush" => HasAura(context, "rogue-stealth") || HasAura(context, "rogue-cutthroat"),
                "garrote" or "cheap-shot" => HasAura(context, "rogue-stealth"),
                "premeditation" => HasAura(context, "rogue-stealth") && cp <= 3,
                "vanish" => !HasAura(context, "rogue-stealth") && _inCombat &&
                    !HasAura(context, "rogue-cutthroat"),
                "expose-armor" => cp >= 4 && !context.GetActor("target")!.ActiveAuras.Any(a =>
                    a.Definition.Key == "expose-armor" && a.IsActiveAt(context.CurrentTimeSeconds)),
                "kidney-shot" => cp >= 4,
                "adrenaline-rush" => !HasAura(context, "adrenaline-rush"),
                "blade-flurry" => !HasAura(context, "blade-flurry"),
                _ => true
            };
        var discount = _request.Talents.Rank("thousand-cuts") > 0 ? 3m * _thousandCutsStacks : 0m;
        if (_actor.Abilities.TryGetValue("eviscerate", out var eviscerate))
            eviscerate.Definition.ResourceCosts[0].Amount =
                _request.Talents.Rank("flawless-execution") > 0 ? 25m : 35m;
        if (_actor.Abilities.TryGetValue("backstab", out var backstab))
            backstab.Definition.ResourceCosts[0].Amount = Math.Max(0m, 60m - discount);
        if (_actor.Abilities.TryGetValue("hemorrhage", out var hemorrhage))
            hemorrhage.Definition.ResourceCosts[0].Amount = Math.Max(0m, 35m - discount);
        if (_actor.Abilities.TryGetValue("slice-and-dice", out var slice))
            slice.Definition.Effects[0].DurationSeconds = (6m + 3m * Math.Max(1, cp)) *
                (1m + _request.Talents.Rank("improved-slice-and-dice") * .15m);
        if (_actor.Abilities.TryGetValue("venom", out var venom))
            venom.Definition.Effects[0].DurationSeconds = 6m + Math.Max(1, cp) * 3m;
        if (_actor.Abilities.TryGetValue("rupture", out var rupture))
            rupture.Definition.Effects[0].DurationSeconds = 6m + Math.Max(1, cp) * 2m;
    }

    private void SpendFinisher(SimulationContext context, Guid? id, string target, string key)
    {
        if (id is not Guid executionId || !_finishersSpent.Add(executionId)) return;
        var spent = ComboPointsFor(target);
        if (spent == 0) return;
        if (key == "rupture") _ruptureSnapshotPoints = spent;
        if (key == "expose-armor") ExposeArmorPoints = spent;
        _points[target] = 0;
        FinishersUsed++;
        // Forever's redesigned Improved Expose Armor returns Combo Points on a
        // five-point application. Refund magnitude is provisional tuning.
        if (key == "expose-armor" && spent == 5 &&
            _request.Talents.Rank("improved-expose-armor") > 0)
            AddPoints(target, _request.Tuning.ImprovedExposeArmorFivePointRefund);
        if (_request.Talents.Rank("ruthlessness") > 0 &&
            (decimal)context.Random.NextDouble() * 100m < _request.Talents.Rank("ruthlessness") * 20m)
            AddPoints(target, 1);
    }
    private void AddPoints(string target, int amount) =>
        _points[target] = Math.Min(5, ComboPointsFor(target) + amount);
    private bool HasAura(SimulationContext context, string key) =>
        _actor.ActiveAuras.Any(a => a.Definition.Key == key && a.IsActiveAt(context.CurrentTimeSeconds));
    private void UpdateRegen(SimulationContext context)
    {
        // Settle elapsed regeneration at the OLD rate before changing the
        // rate at an Adrenaline Rush aura boundary.
        _actor.RefreshResources(context.CurrentTimeSeconds);
        _actor.Resources["energy"].RegenerationPerSecond = _request.Tuning.EnergyPerSecond *
            (HasAura(context, "adrenaline-rush") ? 2m : 1m);
    }
    private void EnterStealth(SimulationContext context, string source)
    {
        foreach (var weapon in _weapons)
            _auto.Stop(context, _actor.Key, weapon.Key);
        _auras.ApplyAura(context, _actor, new AuraDefinition {
            Key = "rogue-stealth", Name = "Stealth", DurationSeconds = 3600m
        }, _actor.Key, source, "stealth-state");
    }

    private void ExitStealth(SimulationContext context)
    {
        if (!IsStealthed(context)) return;
        _auras.RemoveAuras(context, _actor, "rogue-stealth", _actor.Key, "stealth broken");
        if (_request.Talents.Rank("master-of-subtlety") > 0)
            _auras.ApplyAura(context, _actor, new AuraDefinition {
                Key = "master-of-subtlety-window", Name = "Master of Subtlety",
                DurationSeconds = _request.Tuning.MasterOfSubtletyDurationSeconds
            }, _actor.Key, "master-of-subtlety", "post-stealth-damage");
        var target = context.GetActor("target");
        if (target is not { IsAlive: true }) return;
        foreach (var weapon in _weapons)
            _auto.Start(context, _actor.Key, target.Key, weapon);
    }

    private void TriggerHackAndSlash(SimulationContext context, CombatEvent ev)
    {
        var rank = _request.Talents.Rank("hack-and-slash");
        if (rank <= 0 || ev.IsPeriodic || ev.SchoolKey != "physical" ||
            ev.AbilityKey is null ||
            ev.AbilityKey is ("hack-and-slash-extra" or "blade-flurry-cleave" or
                "instant-poison" or "deadly-poison") ||
            ev.WeaponHandKey is not (WeaponHandKeys.MainHand or WeaponHandKeys.OffHand))
            return;
        var weaponType = ev.WeaponHandKey == WeaponHandKeys.MainHand
            ? _request.MainHandWeaponType : _request.OffHandWeaponType;
        if (weaponType is not ("sword" or "axe") ||
            (decimal)context.Random.NextDouble() * 100m >= rank *
                _request.Tuning.HackAndSlashExtraAttackPercentPerRank)
            return;
        var target = ev.TargetActorKey is null ? null : context.GetActor(ev.TargetActorKey);
        if (target is not { IsAlive: true }) return;
        var hand = ev.WeaponHandKey;
        // An extra weapon attack must use the same AP-derived weapon damage and
        // off-hand damage modifiers as the normal swing, not only the weapon's
        // unscaled base damage. Resolve its own hit, crit and mitigation.
        var weapon = _weapons.FirstOrDefault(w => w.WeaponHandKey == hand);
        if (weapon is null) return;
        _executor.ExecuteBackgroundDirectDamage(context, _actor, target,
            "hack-and-slash-extra", "Hack and Slash (extra attack)",
            weapon.DamageEffect);
        HackAndSlashAttacks++;
    }

    private void Cleave(SimulationContext context, CombatEvent ev)
    {
        // Only a real second enemy receives cleave; prevent self-retriggering.
        var other = context.Actors.Values.FirstOrDefault(x => x.IsAlive &&
            !string.Equals(x.TeamKey, _actor.TeamKey, StringComparison.OrdinalIgnoreCase) &&
            x.Key != ev.TargetActorKey);
        if (other is null) return;
        var amount = Math.Max(0m, ev.Amount ?? 0m);
        if (amount <= 0m) return;
        _executor.ExecuteBackgroundDirectDamage(context, _actor, other,
            "blade-flurry-cleave", "Blade Flurry (additional target)",
            new AbilityEffectDefinition {
                Key = "cleave-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false,
                CanCrit = false, MinimumValue = amount, MaximumValue = amount
            });
    }

}
