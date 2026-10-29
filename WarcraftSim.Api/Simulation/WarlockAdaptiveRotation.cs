using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Default spell selection is rebuilt from selected talents; per-decision
/// Mana and DoT horizon checks are separate from the user's explicit priorities.</summary>
public static class WarlockAdaptiveRotation
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {"corruption","bane-of-agony","bane-of-havoc","immolate","wrack","shadow-bolt","soul-fire","conflagrate","incinerate","drain-life","siphon-life","searing-pain","shadowburn","rain-of-fire","death-coil","bane-of-doom","curse-of-the-elements","curse-of-recklessness","drain-soul","hellfire","amplify-curse","health-funnel"};
    public static RotationProfile Build(WarlockSimRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.RotationPolicy is not ("adaptive" or "legacy") || r.CustomPriority is null)
            throw new ArgumentException("Warlock rotationPolicy must be adaptive or legacy, and customPriority must not be null.");
        if(r.BaneOfHavocTargetKey is { } chosenHavoc &&
           (!Enumerable.Range(1,r.AdditionalTargets).Any(i=>
                string.Equals(chosenHavoc,$"target-{i}",StringComparison.OrdinalIgnoreCase))))
            throw new ArgumentException("Havoc secondary target must be a configured additional enemy.");
        var keys=r.CustomPriority.Count>0 ? r.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : DefaultKeys(r);
        foreach(var key in keys)
        {
            if(!Allowed.Contains(key) ||
               key=="amplify-curse"&&r.Talents.Rank("amplify-curse")==0 ||
               key=="wrack"&&(r.Specialization!="affliction"||r.Talents.Rank("wrack")==0) ||
               key=="siphon-life"&&(r.Specialization!="affliction"||r.Talents.Rank("siphon-life")==0) ||
               key=="shadowburn"&&(r.Specialization!="destruction"||r.Talents.Rank("shadowburn")==0) ||
               key=="bane-of-havoc"&&r.Talents.Rank("bane-of-havoc")==0 ||
               key=="incinerate"&&(r.Specialization!="destruction"||r.Talents.Rank("incinerate")==0) ||
               key=="conflagrate"&&(r.Specialization!="destruction"||r.Talents.Rank("conflagrate")==0) )
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
        if(r.AfflictionAdditionalTargetPriority is null ||
           r.AfflictionAdditionalTargetPriority.Count>0 && r.Specialization!="affliction")
            throw new ArgumentException("Additional Affliction target priorities require an Affliction request.");
        // Target-specific maintenance uses the same ordinary ability execution,
        // source-owned aura predicates, costs and GCD as the main priority.
        // Override entries take precedence over fallback spreading on that target.
        for(var targetIndex=1;targetIndex<=r.AdditionalTargets;targetIndex++)
        {
            var targetKey=$"target-{targetIndex}";
            if(!r.AfflictionAdditionalTargetPriority.TryGetValue(targetKey,out var chosen))
                continue;
            if(chosen is null)
                throw new ArgumentException($"Null Affliction target priority: {targetKey}");
            foreach(var effect in chosen.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if(effect is not ("corruption" or "bane-of-agony" or "bane-of-doom" or
                    "siphon-life" or "curse-of-the-elements" or "curse-of-recklessness") ||
                   effect=="siphon-life" && r.Talents.Rank("siphon-life")==0)
                    throw new ArgumentException($"Unsupported target-specific Affliction effect: {effect}");
                rot.Entries.Add(new RotationEntry {
                    Priority=order++,AbilityKey=effect,
                    Target=new RotationTargetDefinition {
                        Mode=RotationTargetSelectionModes.Fixed,ActorKey=targetKey
                    },
                    Conditions=[new RotationConditionDefinition {
                        ConditionType=RotationConditionTypes.TargetAuraMissing,
                        Key=effect,RequireSourceOwnership=true
                    }]
                });
            }
        }
        foreach(var requestedTarget in r.AfflictionAdditionalTargetPriority.Keys)
            if(!Enumerable.Range(1,r.AdditionalTargets).Any(i=>
                string.Equals(requestedTarget,$"target-{i}",StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Unknown Affliction target: {requestedTarget}");

        if(r.DestructionAdditionalTargetPriority is null ||
           r.DestructionAdditionalTargetPriority.Count>0 && r.Specialization!="destruction")
            throw new ArgumentException("Secondary Destruction priorities require Destruction.");
        foreach(var (targetKey,effects) in r.DestructionAdditionalTargetPriority)
        {
            if(!Enumerable.Range(1,r.AdditionalTargets).Any(i=>
                string.Equals(targetKey,$"target-{i}",StringComparison.OrdinalIgnoreCase)) ||
                effects is null)
                throw new ArgumentException($"Unknown Destruction target: {targetKey}");
            foreach(var spell in effects.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if(spell is not ("immolate" or "conflagrate" or "incinerate" or
                    "shadow-bolt" or "searing-pain" or "shadowburn") ||
                   spell=="conflagrate" && r.Talents.Rank("conflagrate")==0 ||
                   spell=="incinerate" && r.Talents.Rank("incinerate")==0 ||
                   spell=="shadowburn" && r.Talents.Rank("shadowburn")==0)
                    throw new ArgumentException($"Unsupported secondary Destruction ability: {spell}");
                var e=new RotationEntry {
                    Priority=order++,AbilityKey=spell,
                    Target=new RotationTargetDefinition {
                        Mode=RotationTargetSelectionModes.Fixed,ActorKey=targetKey
                    }
                };
                if(spell=="immolate")e.Conditions.Add(new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.TargetAuraMissing,Key="immolate",
                    RequireSourceOwnership=true
                });
                if(spell=="conflagrate")e.Conditions.Add(new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.TargetAuraActive,Key="immolate",
                    RequireSourceOwnership=true
                });
                rot.Entries.Add(e);
            }
        }
        foreach(var key in keys)
        {
            var entry=new RotationEntry{Priority=order++,AbilityKey=key};
            if(key=="bane-of-havoc" && r.BaneOfHavocTargetKey is { } havocKey)
                entry.Target=new RotationTargetDefinition {
                    Mode=RotationTargetSelectionModes.Fixed,ActorKey=havocKey
                };
            if(key is "corruption" or "bane-of-agony" or "bane-of-havoc" or "immolate" or "siphon-life" or "bane-of-doom" or "curse-of-the-elements" or "curse-of-recklessness")
                entry.Conditions.Add(new RotationConditionDefinition{ConditionType=RotationConditionTypes.TargetAuraMissing,Key=key,RequireSourceOwnership=true});
            if(key=="conflagrate")entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.TargetAuraActive,Key="immolate",RequireSourceOwnership=true});
            rot.Entries.Add(entry);
            // Give each additional enemy its own source-owned DoT/Curse entry.
            // The shared selector resolves each actual actor, and the normal
            // target-aura-missing condition prevents per-target recast spam.
            // Keep Bane of Havoc on the selected primary victim: spreading a
            // unique secondary-target Bane would actively move it each cast.
            if(r.SpreadAfflictionDots && r.Specialization=="affliction" &&
               key is ("corruption" or "bane-of-agony" or "bane-of-doom" or
                    "siphon-life" or "curse-of-the-elements" or "curse-of-recklessness"))
            {
                for(var targetIndex=1;targetIndex<=r.AdditionalTargets;targetIndex++)
                {
                    if(r.AfflictionAdditionalTargetPriority.ContainsKey($"target-{targetIndex}"))
                        continue;
                    var copy=new RotationEntry {
                        Priority=order++,AbilityKey=key,
                        Target=new RotationTargetDefinition {
                            Mode=RotationTargetSelectionModes.Fixed,
                            ActorKey=$"target-{targetIndex}"
                        },
                        Conditions=entry.Conditions.Select(condition=>new RotationConditionDefinition {
                            ConditionType=condition.ConditionType,Key=condition.Key,
                            RequireSourceOwnership=condition.RequireSourceOwnership
                        }).ToList()
                    };
                    rot.Entries.Add(copy);
                }
            }
        }
        return rot;
    }
    private static List<string> DefaultKeys(WarlockSimRequest r)
    {
        var a=new List<string>();
        if(r.UseAmplifyCurse&&r.Talents.Rank("amplify-curse")>0)a.Add("amplify-curse");
        if(r.UseCurseOfTheElements)a.Add("curse-of-the-elements");
        if(r.UseCurseOfRecklessness)a.Add("curse-of-recklessness");

        if(r.UseHellfire && r.AdditionalTargets>0)a.Add("hellfire");
        if(r.UseDrainSoul)a.Add("drain-soul");
        if(r.UseDeathCoil)a.Add("death-coil");
        if(r.UseCorruption)a.Add("corruption");
        if(r.UseBaneOfDoom)a.Add("bane-of-doom");
        if(r.UseBaneOfAgony)a.Add("bane-of-agony");
        if(r.UseBaneOfHavoc && r.Talents.Rank("bane-of-havoc")>0 &&
            r.AdditionalTargets>0)a.Insert(0,"bane-of-havoc");
        if(r.UseImmolate)a.Add("immolate");
        switch(r.Specialization)
        {
            case "affliction":
                if(r.UseSiphonLife&&r.Talents.Rank("siphon-life")>0)a.Add("siphon-life");
                if(r.UseWrack&&r.Talents.Rank("wrack")>0)a.Add("wrack");
                if(r.UseDrainLife)a.Add("drain-life");
                a.Add("shadow-bolt"); break;
            case "demonology": a.Add("soul-fire");a.Add("shadow-bolt");break;
            case "destruction":
                if(r.UseShadowburn && r.Talents.Rank("shadowburn")>0 && r.StartingSoulShards>0)a.Add("shadowburn");
                if(r.UseConflagrate&&r.UseImmolate&&r.Talents.Rank("conflagrate")>0)a.Add("conflagrate");
                if(r.UseIncinerate&&r.Talents.Rank("incinerate")>0)a.Add("incinerate");
                if(r.UseSoulFire)a.Add("soul-fire");
                a.Add("shadow-bolt");break;
        }
        if(r.UseRainOfFire && r.AdditionalTargets>0)a.Insert(0,"rain-of-fire");
        if(r.UseSearingPain)a.Insert(0,"searing-pain");
        // Pet survival can protect sustained DPS; check live pet HP on each
        // decision rather than preemptively channeling at full health.
        if(r.UseHealthFunnel && r.EnablePet)a.Insert(0,"health-funnel");
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
                "bane-of-doom" => remaining>=60m,
                "immolate" => remaining>=6m,
                "wrack" => remaining>=6m,
                "siphon-life" => remaining>=9m,
                "drain-life" => remaining>=5m,
                "drain-soul" => remaining>=3m,
                "hellfire" => remaining>=5m,
                "rain-of-fire" => remaining>=8m,
                "shadowburn" => remaining>=1.5m,
                "soul-fire" => remaining>=4m && (remaining<=20m || manaPercent>=40m),
                "life-tap" => remaining>=3m,
                "incinerate" => remaining>=2.5m && (!request.UseImmolate ||
                    context.GetActor("target")?.ActiveAuras.Any(a=>a.Definition.Key=="immolate"&&a.ExpiresAtSeconds>context.CurrentTimeSeconds)==true),
                _ => true
            };
    }
}
