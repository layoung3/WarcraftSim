using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>Spec- and talent-aware baseline priority; mana decisions are updated
/// during combat by MageSpecializationProcessor, never frozen into a gear preset.</summary>
public static class MageAdaptiveRotation
{
    public static RotationProfile Build(MageSimRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.RotationPolicy is not ("adaptive" or "legacy") || r.CustomPriority is null)
            throw new ArgumentException("Mage rotationPolicy must be adaptive or legacy, and customPriority must not be null.");
        var permitted = r.Specialization switch
        {
            "arcane" => new HashSet<string>(["arcane-power","presence-of-mind","arcane-explosion","arcane-blast","arcane-missiles","arcane-wrath","fireball","frostbolt","frostfire-bolt"],StringComparer.OrdinalIgnoreCase),
            "fire" => new HashSet<string>(["flamestrike","combustion","pyroblast","fire-blast","scorch","fireball","frostfire-bolt","frostbolt"],StringComparer.OrdinalIgnoreCase),
            "frost" => new HashSet<string>(["blizzard","cold-snap","ice-lance","frostbolt","frost-blast","frostfire-bolt","fireball","cone-of-cold"],StringComparer.OrdinalIgnoreCase),
            _ => throw new ArgumentException("Unknown Mage specialization.")
        };
        var custom=r.CustomPriority.Count>0;
        var actions=new List<string>();
        if(custom)actions.AddRange(r.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase));
        else switch(r.Specialization)
        {
            case "arcane":
                if(r.UseArcanePower && r.Talents.Rank("arcane-power")>0)actions.Add("arcane-power");
                if(r.UsePresenceOfMind && r.Talents.Rank("presence-of-mind")>0)actions.Add("presence-of-mind");
                if(r.UseArcaneExplosion && r.AdditionalTargets>0)actions.Add("arcane-explosion");
                if(r.UseMissileBarrage&&r.Talents.Rank("missile-barrage")>0)
                    actions.Add("arcane-missiles");
                if(r.Talents.Rank("arcane-wrath")>0)actions.Add("arcane-wrath");
                if(r.Talents.Rank("arcane-blast")>0)actions.Add("arcane-blast");
                actions.Add(r.UseFrostfireBolt?"frostfire-bolt":"fireball");
                break;
            case "fire":
                if(r.UseFlamestrike && r.AdditionalTargets>0)actions.Add("flamestrike");
                if(r.UseCombustion&&r.Talents.Rank("combustion")>0)actions.Add("combustion");
                if(r.UseHeatingUp&&r.Talents.Rank("heating-up")>0&&r.Talents.Rank("pyroblast")>0)
                    actions.Add("pyroblast");
                actions.Add("fire-blast");
                if(r.UseScorch) actions.Add("scorch");
                actions.Add(r.UseFrostfireBolt?"frostfire-bolt":"fireball");
                break;
            case "frost":
                if(r.UseBlizzard && r.AdditionalTargets>0)actions.Add("blizzard");
                if(r.UseColdSnap&&r.Talents.Rank("cold-snap")>0)actions.Add("cold-snap");
                if(r.UseFingersOfFrost&&r.Talents.Rank("fingers-of-frost")>0&&r.Talents.Rank("ice-lance")>0)
                    actions.Add("ice-lance");
                actions.Add(r.UseFrostfireBolt?"frostfire-bolt":"frostbolt");
                break;
        }
        foreach(var key in actions)
        {
            if(!permitted.Contains(key))throw new ArgumentException($"Unsupported Mage custom priority for {r.Specialization}: {key}");
            if(key=="presence-of-mind"&&r.Talents.Rank("presence-of-mind")==0 ||
               key=="arcane-power"&&r.Talents.Rank("arcane-power")==0 ||
               key=="combustion"&&r.Talents.Rank("combustion")==0 ||
               key=="cold-snap"&&r.Talents.Rank("cold-snap")==0 ||
               key=="arcane-blast"&&r.Talents.Rank("arcane-blast")==0 ||
               key=="arcane-wrath"&&r.Talents.Rank("arcane-wrath")==0 ||
               key=="ice-lance"&&r.Talents.Rank("ice-lance")==0 ||
               key=="pyroblast"&&r.Talents.Rank("pyroblast")==0)
                throw new ArgumentException($"Mage priority requires a missing talent: {key}");
        }
        var result=new RotationProfile { Name=$"Mage {r.Specialization} {r.RotationPolicy}",
            ClassKey="mage",RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        var priority=0;
        void recovery(string key,decimal threshold)
        {
            result.Entries.Add(new RotationEntry { Priority=priority++,AbilityKey=key,
                Conditions=[new RotationConditionDefinition {ConditionType=RotationConditionTypes.SourceResourcePercent,
                    Key="mana",ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,Value=threshold}] });
        }
        recovery("major-mana-potion",r.MajorManaPotionBelowManaPercent);
        recovery("evocation",r.EvocationBelowManaPercent);
        foreach(var key in actions)
        {
            var entry=new RotationEntry{Priority=priority++,AbilityKey=key};
            if(key=="flamestrike")entry.Conditions.Add(new RotationConditionDefinition {
                ConditionType=RotationConditionTypes.TargetAuraMissing,Key="flamestrike" });
            if(key=="cold-snap")entry.Conditions.Add(new RotationConditionDefinition {
                ConditionType=RotationConditionTypes.SourceAbilityOnCooldown,Key="cone-of-cold" });
            result.Entries.Add(entry);
        }
        return result;
    }
}
