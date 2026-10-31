using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current-build racial passives only, additive sources in the already ordered
/// character-stat pipeline. A live-server hotfix may differ from published
/// 70532 tooltips; unsupported racials are not silently approximated.
/// </summary>
public static class ForeverRacialPassives
{
    public const string SourceUrl="https://wow-forever.gg/races/";
    public const string SourceBuild="1.60.1.70532";

    public static CharacterProfile ApplyToProfile(CharacterProfile source,
        bool observedPrimariesAlreadyContainRacials=false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(string.IsNullOrWhiteSpace(source.Race))return source;
        ForeverPlayableRaceCatalog.Validate(source.Race,source.ClassKey,source.Level);
        var delta=StaticStats(source.Race);
        if(!observedPrimariesAlreadyContainRacials &&
           source.Race.Equals("human",StringComparison.OrdinalIgnoreCase))
        {
            // Spirit % sees already resolved equipment and selected external
            // sources; derive via CP170, then re-run its contributor chain.
            var current=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(source).EffectiveStats.Get("spirit");
            delta["spirit"]=current*.05m;
        }
        if(delta.Count==0)return source;
        var profile=JsonSerializer.Deserialize<CharacterProfile>(
            JsonSerializer.Serialize(source)) ??
            throw new InvalidOperationException("Racial profile source copy failed.");
        profile.Buffs.Selections.Add(new CharacterBuffSelection{
            BuffKey="cp173-racial-"+source.Race,Name="Forever racial passive: "+source.Race,
            IsEnabled=true,Stats=new StatCollection{Values=delta}
        });
        return profile;
    }

    public static Dictionary<string,decimal> StaticStats(string race)=>
        race.ToLowerInvariant() switch {
            "human"=>new(StringComparer.OrdinalIgnoreCase) {
                ["attack-critical-chance-percent"]=2m,["spell-critical-chance-percent"]=2m
            },
            "orc"=>new(StringComparer.OrdinalIgnoreCase) {
                ["attack-critical-chance-percent"]=1m,["spell-critical-chance-percent"]=1m
            },
            "night-elf"=>new(StringComparer.OrdinalIgnoreCase) {
                ["dodge-chance-percent"]=1m
            },
            "high-order-skyborne" or "windshaper-skyborne"=>
                new(StringComparer.OrdinalIgnoreCase){["haste-percent"]=3m},
            _=>new(StringComparer.OrdinalIgnoreCase)
        };

    public static void ApplyHunterStats(SimulationActorState hunter,string? race)
    {
        if(string.IsNullOrWhiteSpace(race))return;
        ForeverPlayableRaceCatalog.Validate(race,"hunter",hunter.Level);
        var source=StaticStats(race);
        foreach(var (key,value) in source)hunter.Stats.Add(key,value);
        var crit=source.GetValueOrDefault("attack-critical-chance-percent");
        if(crit!=0m)
        {
            hunter.Stats.Add("ranged-critical-chance-percent",crit);
            hunter.Stats.Add("melee-critical-chance-percent",crit);
        }
    }

    /// <summary>
    /// Gnome Expansive Mind is +5% maximum Mana in Forever, NOT Classic +5%
    /// Intellect. Tauren Endurance is +5% maximum Health. Start with the same
    /// resource percentage as the configured encounter initial pool.
    /// </summary>
    public static void ApplyResourcePassives(SimulationActorState actor,string? race,
        bool observedManaAlreadyContainsRacial=false,
        bool observedHealthAlreadyContainsRacial=false)
    {
        if(!observedManaAlreadyContainsRacial &&
           string.Equals(race,"gnome",StringComparison.OrdinalIgnoreCase) &&
           actor.Resources.TryGetValue("mana",out var mana))
        {
            var percent=mana.Maximum>0m?mana.Current/mana.Maximum:0m;
            mana.Maximum*=1.05m;
            mana.SetCurrent(mana.Maximum*percent);
        }
        if(!observedHealthAlreadyContainsRacial &&
           string.Equals(race,"tauren",StringComparison.OrdinalIgnoreCase))
        {
            var percent=actor.MaximumHealth>0m?actor.CurrentHealth/actor.MaximumHealth:0m;
            actor.InitializeHealth(actor.MaximumHealth*1.05m,
                actor.MaximumHealth*1.05m*percent);
        }
    }
}
