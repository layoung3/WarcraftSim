using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Builds the default priority from talents, fight length, target count and resource budget.
/// A user-defined list overrides it. No special-case priority is needed in the generic engine.
/// </summary>
public static class ElementalAdaptiveRotation
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "mana-spring-totem", "searing-totem", "elemental-mastery", "flame-shock",
        "lava-burst", "chain-lightning", "earth-shock", "fire-nova", "lightning-shield", "lightning-bolt", "lightning-bolt-rank7"
    };

    public static RotationProfile Build(ElementalShamanSimRequest request, decimal spellPower)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RotationPolicy is not ("adaptive" or "legacy"))
            throw new ArgumentException("RotationPolicy must be 'adaptive' or 'legacy'.");
        if (request.FlameShockPolicy is not ("auto" or "always" or "never"))
            throw new ArgumentException("FlameShockPolicy must be 'auto', 'always', or 'never'.");
        if (request.TargetCount is < 1 or > 3)
            throw new ArgumentException("TargetCount must be 1, 2, or 3.");
        var t=request.Talents;
        var rotation=new RotationProfile
        {
            Name=$"Elemental {request.RotationPolicy} ({request.TargetCount} target(s))",
            ClassKey="shaman",RulesetKey="wow-forever",SimulationType=SimulationType.Dps
        };
        var keys=new List<string>();
        if (request.CustomPriority.Count>0)
        {
            foreach (var key in request.CustomPriority)
            {
                if (!Allowed.Contains(key) || (key=="lava-burst" && t.Rank("lava-burst")==0) ||
                    (key=="elemental-mastery" && t.Rank("elemental-mastery")==0) ||
                    (key=="lightning-bolt-rank7" && !request.AllowDownranking))
                    throw new ArgumentException($"Unsupported Elemental custom priority ability: {key}.");
                if (!keys.Contains(key,StringComparer.OrdinalIgnoreCase)) keys.Add(key);
            }
        }
        else
        {
            if (request.UseLightningShield) keys.Add("lightning-shield");
            if (request.UseManaSpringTotem) keys.Add("mana-spring-totem");
            if (request.UseSearingTotem) keys.Add("searing-totem");
            if (request.UseFireNova) keys.Add("fire-nova");
            if (request.UseElementalMastery && t.Rank("elemental-mastery")>0) keys.Add("elemental-mastery");
            if (request.UseFlameShock && (request.RotationPolicy=="legacy" ||
                 request.FlameShockPolicy=="always" ||
                 request.FlameShockPolicy=="auto" && ShouldUseFlameShock(request,spellPower)))
                keys.Add("flame-shock");
            if (request.UseLavaBurst && t.Rank("lava-burst")>0) keys.Add("lava-burst");
            if (request.UseEarthShock) keys.Add("earth-shock");
            if (request.UseChainLightning && (request.TargetCount>1 || request.RotationPolicy=="legacy"))
                keys.Add("chain-lightning");
            keys.Add("lightning-bolt");
            if (request.AllowDownranking) keys.Add("lightning-bolt-rank7");
        }
        var order=0;
        foreach (var key in keys)
        {
            var entry=new RotationEntry {Priority=order++,AbilityKey=key};
            if (key=="lightning-shield")
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.SourceAuraMissing,Key="shaman-lightning-shield"});
            if (key=="mana-spring-totem")
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.SourceAuraMissing,Key=key});
            if (key=="searing-totem")
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.TargetAuraMissing,Key=key});
            if (key=="flame-shock")
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.TargetAuraMissing,Key=key});
            if (key=="chain-lightning" && request.TargetCount>1)
                entry.Conditions.Add(new RotationConditionDefinition
                {ConditionType=RotationConditionTypes.ActiveEnemyCount,Value=2m});
            // Keep the lowest rank for the end of the fight only, never spam it ahead of max rank.
            rotation.Entries.Add(entry);
        }
        return rotation;
    }

    public static bool ShouldUseFlameShock(ElementalShamanSimRequest req, decimal sp)
    {
        if (req.FlameShockPolicy=="always") return true;
        if (req.FlameShockPolicy=="never") return false;
        var v=req.Tuning; var t=req.Talents;
        var extension=12m+3m*t.Rank("call-of-flame");
        var expected=(v.FlameShockDirectDamage+sp*.214m)+
            (v.FlameShockTickDamage+sp*.10m)*(extension/3m);
        expected*=1m+.05m*t.Rank("call-of-flame");
        if (req.UseLavaBurst && t.Rank("lava-burst")>0)
            expected+=(v.LavaBurstDamage+sp*.714m)*.2m*(extension/10m);
        // Approximate foregone filler/other uses; conservative on fights shorter than DoT duration.
        var filler=(v.LightningBoltDamage+sp*.714m)*1.05m;
        var manaEfficiency=expected/Math.Max(1m,v.FlameShockMana);
        var boltEfficiency=filler/Math.Max(1m,v.LightningBoltMana);
        return req.DurationSeconds>=extension && manaEfficiency>=boltEfficiency*.90m;
    }
}
