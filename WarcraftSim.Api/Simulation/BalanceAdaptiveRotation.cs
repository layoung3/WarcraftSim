using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>Balance's default: meaningful priority resolved from equipped stats/talents and
/// re-evaluated at each decision by BalanceState; explicit user priorities take precedence.</summary>
public static class BalanceAdaptiveRotation
{
    private static readonly HashSet<string> Allowed=new(StringComparer.OrdinalIgnoreCase)
    {"moonfire","insect-swarm","starfire","wrath"};
    public static RotationProfile Build(BalanceDruidSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.RotationPolicy is not ("adaptive" or "legacy"))
            throw new ArgumentException("RotationPolicy must be 'adaptive' or 'legacy'.");
        if(request.CustomPriority is null)throw new ArgumentException("CustomPriority cannot be null.");
        var keys=new List<string>();
        if(request.CustomPriority.Count>0)
        {
            foreach(var key in request.CustomPriority)
            {
                if(!Allowed.Contains(key)||key=="insect-swarm"&&request.Talents.Rank("insect-swarm")==0)
                    throw new ArgumentException($"Unsupported Balance custom priority: {key}");
                if(!keys.Contains(key,StringComparer.OrdinalIgnoreCase))keys.Add(key);
            }
        }
        else
        {
            if(request.UseMoonfire)keys.Add("moonfire");
            if(request.UseInsectSwarm&&request.Talents.Rank("insect-swarm")>0)keys.Add("insect-swarm");
            if(request.UseStarfire)keys.Add("starfire");
            keys.Add("wrath");
        }
        var rotation=new RotationProfile{Name=$"Balance {request.RotationPolicy}",ClassKey="druid",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        for(var i=0;i<keys.Count;i++)
        {
            var entry=new RotationEntry{Priority=i,AbilityKey=keys[i]};
            if(keys[i] is "moonfire" or "insect-swarm")
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.TargetAuraMissing,Key=keys[i]});
            rotation.Entries.Add(entry);
        }
        return rotation;
    }
}
