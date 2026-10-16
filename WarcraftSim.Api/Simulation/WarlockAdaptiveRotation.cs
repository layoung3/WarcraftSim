using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Default spell selection is rebuilt from selected talents; per-decision
/// Mana and DoT horizon checks are separate from the user's explicit priorities.</summary>
public static class WarlockAdaptiveRotation
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {"corruption","bane-of-agony","immolate","wrack","shadow-bolt","soul-fire","conflagrate","incinerate"};
    public static RotationProfile Build(WarlockSimRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.RotationPolicy is not ("adaptive" or "legacy") || r.CustomPriority is null)
            throw new ArgumentException("Warlock rotationPolicy must be adaptive or legacy, and customPriority must not be null.");
        var keys=r.CustomPriority.Count>0 ? r.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : DefaultKeys(r);
        foreach(var key in keys)
        {
            if(!Allowed.Contains(key) ||
               key=="wrack"&&(r.Specialization!="affliction"||r.Talents.Rank("wrack")==0) ||
               key=="incinerate"&&(r.Specialization!="destruction"||r.Talents.Rank("incinerate")==0) ||
               key=="conflagrate"&&r.Specialization!="destruction" ||
               key=="soul-fire"&&r.Specialization!="demonology")
                throw new ArgumentException($"Unsupported Warlock priority for {r.Specialization}: {key}");
        }
        var rot=new RotationProfile {Name=$"Warlock {r.Specialization} {r.RotationPolicy}",ClassKey="warlock",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        var order=0;
        if(r.UseLifeTap)
        {
            var c=new List<RotationConditionDefinition>{new(){ConditionType=RotationConditionTypes.SourceResourcePercent,
                Key="mana",ComparisonOperator=RotationComparisonOperators.LessThanOrEqual,
                Value=r.LifeTapBelowManaPercent}};
            if(!r.AssumeExternalHealingForLifeTap)c.Add(new(){ConditionType=RotationConditionTypes.SourceHealthPercent,
                ComparisonOperator=RotationComparisonOperators.GreaterThan,Value=r.LifeTapAboveHealthPercent});
            rot.Entries.Add(new RotationEntry{Priority=order++,AbilityKey="life-tap",Conditions=c});
        }
        foreach(var key in keys)
        {
            var entry=new RotationEntry{Priority=order++,AbilityKey=key};
            if(key is "corruption" or "bane-of-agony" or "immolate")
                entry.Conditions.Add(new RotationConditionDefinition{ConditionType=RotationConditionTypes.TargetAuraMissing,Key=key});
            if(key=="conflagrate")entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.TargetAuraActive,Key="immolate"});
            rot.Entries.Add(entry);
        }
        return rot;
    }
    private static List<string> DefaultKeys(WarlockSimRequest r)
    {
        var a=new List<string>();
        if(r.UseCorruption)a.Add("corruption");
        if(r.UseBaneOfAgony)a.Add("bane-of-agony");
        if(r.UseImmolate)a.Add("immolate");
        switch(r.Specialization)
        {
            case "affliction":
                if(r.UseWrack&&r.Talents.Rank("wrack")>0)a.Add("wrack");
                a.Add("shadow-bolt"); break;
            case "demonology": a.Add("soul-fire");a.Add("shadow-bolt");break;
            case "destruction":
                if(r.UseConflagrate&&r.UseImmolate)a.Add("conflagrate");
                if(r.UseIncinerate&&r.Talents.Rank("incinerate")>0)a.Add("incinerate");
                a.Add("shadow-bolt");break;
        }
        return a;
    }
}

/// <summary>Horizon and Mana gates applied only to generated priorities.
/// Does not alter damage formulas or pretend that raid healer costs are free.</summary>
public sealed class WarlockAdaptivePriorityProcessor(WarlockSimRequest request,RotationProfile rotation,
    SimulationActorState actor):ICombatEventProcessor
{
    public void Process(SimulationContext context, CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.RotationDecision)return;
        var remaining=Math.Max(0m,request.DurationSeconds-context.CurrentTimeSeconds);
        if(request.RotationPolicy!="adaptive"||request.CustomPriority.Count>0)return;
        var mana=actor.Resources.TryGetValue("mana",out var resource)? resource.Current : 0m;
        var manaPercent=resource is not null&&resource.Maximum>0m ? mana*100m/resource.Maximum:100m;
        // Minimum remaining time for useful periodic applications; no late Life Tap.
        foreach(var entry in rotation.Entries)
            entry.IsEnabled=entry.AbilityKey switch
            {
                "corruption" => remaining>=7m,
                "bane-of-agony" => remaining>=12m,
                "immolate" => remaining>=6m,
                "wrack" => remaining>=6m,
                "soul-fire" => remaining>=4m && (remaining<=20m || manaPercent>=40m),
                "life-tap" => remaining>=3m,
                "incinerate" => remaining>=2.5m && (!request.UseImmolate ||
                    context.GetActor("target")?.ActiveAuras.Any(a=>a.Definition.Key=="immolate"&&a.ExpiresAtSeconds>context.CurrentTimeSeconds)==true),
                _ => true
            };
    }
}
