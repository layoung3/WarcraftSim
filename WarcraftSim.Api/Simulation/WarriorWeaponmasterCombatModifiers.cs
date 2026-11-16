using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// The existing provisional Weaponmaster coefficients, scoped to explicit
/// weapon-hand effects. Homogeneous crit and single-weapon armor remain in
/// their existing paths; handless effects do not acquire new eligibility.
/// </summary>
public sealed class WarriorWeaponmasterCombatModifiers :
    ICombatRollContextAdjustmentProvider, IDamageMitigationResolver
{
    private readonly int _rank;
    private readonly bool _dualWield, _mixedCritWeapons;
    private readonly string _mainHandType, _offHandType;
    private readonly CombatRulesetDefinition _ruleset;
    private readonly IDamageMitigationResolver _fallback;

    public WarriorWeaponmasterCombatModifiers(WarriorSimRequest request,
        CombatRulesetDefinition ruleset)
    {
        _rank = ForeverWarriorTalentCatalog.Rank(request, "weaponmaster");
        _dualWield = request.DualWield;
        _mainHandType = request.MainHandWeaponType;
        _offHandType = request.OffHandWeaponType;
        _mixedCritWeapons = _dualWield &&
            GrantsCrit(_mainHandType) != GrantsCrit(_offHandType);
        _ruleset = ruleset;
        _fallback = new RulesetDamageMitigationResolver(ruleset);
    }

    public CombatRollContextAdjustment GetAdjustment(SimulationContext context,
        SimulationActorState source, SimulationActorState target,
        AbilityDefinition ability, AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule) =>
        GetAdjustment(context, source, target, ability, effect, rule,
            CombatEffectDeliveryType.Direct);

    public CombatRollContextAdjustment GetAdjustment(SimulationContext context,
        SimulationActorState source, SimulationActorState target,
        AbilityDefinition ability, AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule, CombatEffectDeliveryType deliveryType)
    {
        // The sheet already includes Weaponmaster when both weapons qualify.
        // Only supply the missing conditional bonus for mixed weapon types.
        if (_rank <= 0 || !_mixedCritWeapons || !effect.CanCrit ||
            deliveryType != CombatEffectDeliveryType.Direct ||
            !IsWeaponStrike(source, ability, effect) ||
            !GrantsCrit(WeaponType(effect)))
            return CombatRollContextAdjustment.None;
        return new CombatRollContextAdjustment { CriticalChancePercentDelta = _rank };
    }

    public DamageMitigationResult Resolve(SimulationContext context,
        SimulationActorState source, SimulationActorState target,
        AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
    {
        var rule = _ruleset.GetMitigationRule(effect.MitigationType);
        if (rawAmount <= 0m || _rank <= 0 || !_dualWield ||
            !IsWeaponStrike(source, ability, effect) ||
            WeaponType(effect) is not ("mace" or "staff") ||
            effect.MitigationType != DamageMitigationTypes.Armor ||
            rule?.FormulaType != DamageMitigationFormulaTypes.RationalLevelScaled ||
            string.IsNullOrWhiteSpace(rule.DefenseStatKey))
            return _fallback.Resolve(context, source, target, ability, effect, rawAmount);

        // Read this hit's target after live debuffs (including Sunder). Do not
        // modify its shared armor: another hand or actor must see the original.
        var armor = Math.Max(0m, target.Stats.Get(rule.DefenseStatKey)) *
            (1m - 0.03m * _rank);
        var denominator = armor + rule.BaseConstant +
            rule.PerAttackerLevelConstant * Math.Max(1, source.Level);
        var reductionPercent = Math.Clamp(
            armor <= 0m || denominator <= 0m ? 0m : 100m * armor / denominator,
            0m, Math.Max(0m, rule.MaximumReductionPercent));
        var safeRawAmount = Math.Max(0m, rawAmount);
        var finalAmount = safeRawAmount * (1m - reductionPercent / 100m);
        return new DamageMitigationResult
        {
            RawAmount = safeRawAmount,
            FinalAmount = Math.Max(0m, finalAmount),
            MitigatedAmount = Math.Max(0m, safeRawAmount - finalAmount),
            ReductionPercent = reductionPercent,
            DefenseValue = armor
        };
    }

    private string WeaponType(AbilityEffectDefinition effect) =>
        effect.WeaponHandKey == WeaponHandKeys.MainHand ? _mainHandType : _offHandType;

    private static bool GrantsCrit(string weaponType) => weaponType is "axe" or "polearm";

    private static bool IsWeaponStrike(SimulationActorState source,
        AbilityDefinition ability, AbilityEffectDefinition effect) =>
        source.Key == "warrior" &&
        ability.Key is not ("shield-bash" or "shield-slam") &&
        effect.EffectType == AbilityEffectTypes.DirectDamage &&
        !effect.ApplyOnChannelTick &&
        effect.WeaponHandKey is WeaponHandKeys.MainHand or WeaponHandKeys.OffHand &&
        effect.ResolutionType is ForeverCombatResolutionTypes.PlayerMeleeAuto or
            ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto or
            ForeverCombatResolutionTypes.PlayerMeleeSpecial &&
        effect.MitigationType == DamageMitigationTypes.Armor;
}
