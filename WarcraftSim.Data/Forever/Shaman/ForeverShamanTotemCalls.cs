using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// Current Forever trainer rules: four elemental slots, 3s programmed call,
/// combined Mana cost, and 25% of active totem Mana costs on Recall.
/// Call of the Ancestors / Spirits are other selectable loadouts, not extra
/// simultaneous totem slots. No movement or spatial buff radius is inferred.
/// </summary>
public static class ForeverShamanTotemCalls
{
    public static AbilityDefinition StrengthOfEarth() => new()
    {
        Key="strength-of-earth-totem",Name="Strength of Earth Totem",ClassKey="shaman",
        RequiredLevel=60,GlobalCooldownSeconds=1m,
        ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=275m}],
        Effects=[new AbilityEffectDefinition{
            Key="strength-of-earth-aura",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Self,AuraKey="shaman-strength-of-earth-totem",
            DurationSeconds=300m,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        }]
    };

    /// <summary>Current rank 6: 210 Mana, reduce incoming physical hits by 30.</summary>
    public static AbilityDefinition Stoneskin() => new()
    {
        Key="stoneskin-totem",Name="Stoneskin Totem",ClassKey="shaman",
        RequiredLevel=54,GlobalCooldownSeconds=1m,
        ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=210m}],
        Effects=[new AbilityEffectDefinition {
            Key="stoneskin-aura",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Self,AuraKey="shaman-stoneskin-totem",
            DurationSeconds=300m,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        }]
    };

    public static readonly IReadOnlyDictionary<string,string> Slots =
        new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength-of-earth-totem"]="earth",
            ["stoneskin-totem"]="earth",
            ["searing-totem"]="fire",
            ["mana-spring-totem"]="water",
            ["windfury-totem"]="air"
        };

    public static string SelectedPresetAura(IReadOnlyList<string> selected)
    {
        if(selected is null || selected.Count==0)
            throw new ArgumentException("Totem preset must contain one or more totems.");
        var preferred=selected.FirstOrDefault(k=>Slots.GetValueOrDefault(k)=="earth") ??
            selected.FirstOrDefault(k=>Slots.GetValueOrDefault(k)=="air") ??
            selected.FirstOrDefault(k=>Slots.GetValueOrDefault(k)=="water") ??
            selected.FirstOrDefault(k=>Slots.GetValueOrDefault(k)=="fire");
        return preferred switch {
            "strength-of-earth-totem"=>"shaman-strength-of-earth-totem",
            "stoneskin-totem"=>"shaman-stoneskin-totem",
            "windfury-totem"=>"shaman-windfury-totem",
            "mana-spring-totem"=>"mana-spring-totem",
            "searing-totem"=>"shaman-fire-totem-active",
            _=>throw new ArgumentException("Unrecognized preset totem.")
        };
    }

    public static AbilityDefinition Call(string key,
        IReadOnlyCollection<AbilityDefinition> knownTotems, IReadOnlyList<string> selected)
    {
        if(key is not ("call-of-elements" or "call-of-ancestors" or "call-of-spirits") ||
            knownTotems is null || selected is null || selected.Count is <1 or >4)
            throw new ArgumentException("A Shaman totem call needs one to four learned slots.");
        var book=knownTotems.ToDictionary(x=>x.Key,StringComparer.OrdinalIgnoreCase);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var abilities=new List<AbilityDefinition>();
        foreach(var totemKey in selected)
        {
            if(!Slots.TryGetValue(totemKey,out var slot) || !seen.Add(slot) ||
                !book.TryGetValue(totemKey,out var definition) ||
                definition.ResourceCosts.Count!=1 ||
                definition.ResourceCosts[0].ResourceKey!="mana")
                throw new ArgumentException($"Invalid or duplicate Call totem slot: {totemKey}.");
            abilities.Add(definition);
        }
        return new AbilityDefinition {
            Key=key,Name=key,ClassKey="shaman",
            RequiredLevel=key switch {
                "call-of-ancestors"=>40,"call-of-spirits"=>30,_=>20 },
            CastTimeSeconds=3m,GlobalCooldownSeconds=1.5m,
            ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",
                Amount=abilities.Sum(x=>x.ResourceCosts[0].Amount)}],
            Effects=abilities.SelectMany(x=>x.Effects).ToList()
        };
    }

    public static AbilityDefinition Recall() => new()
    {
        Key="totemic-recall",Name="Totemic Recall",ClassKey="shaman",
        RequiredLevel=20,GlobalCooldownSeconds=1m,
        // Does not generate free Mana. The live slot manager computes the
        // eligible refund from actual active totems and emits its resource event.
        Effects=[]
    };
}
