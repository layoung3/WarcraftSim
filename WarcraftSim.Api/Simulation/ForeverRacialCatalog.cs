namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Source-indexed current Forever racial metadata, independent from what is
/// executable in the Mage/Warlock/Hunter solo combat engines. No unknown
/// cooldown, trigger or target is synthesized merely to fill out a roster.
/// </summary>
public sealed record ForeverRacialDefinition(
    string Key,string Name,string Race,string Type,int? SpellId,
    bool Modeled,string Effect,string SourceBuild,string SourceUrl,
    string VerificationStatus);

public static class ForeverRacialCatalog
{
    public const string SourceBuild="1.60.1.70532";
    private const string Source="https://wow-forever.gg/races/";
    public static IReadOnlyList<ForeverRacialDefinition> Entries { get; }=[
        new("ruthless","Ruthless","human","passive",null,true,
            "+2% attack and spell critical chance",SourceBuild,Source,"published-client-tooltip-provisional"),
        new("human-spirit","The Human Spirit","human","passive",null,true,
            "+5% Spirit applied after configured equipment/buff contributions",SourceBuild,Source,"published-client-tooltip-provisional"),
        new("savage","Savage","orc","passive",null,true,
            "+1% attack and spell critical chance",SourceBuild,Source,"published-client-tooltip-provisional"),
        new("blood-fury","Blood Fury","orc","active",20572,true,
            "+10% current Attack Power and Spell Power; 15s/180s; explicit opt-in",SourceBuild,
            "https://wow-forever.gg/db/spells/20572-blood-fury/","published-client-tooltip-provisional"),
        new("quickness","Quickness","night-elf","passive",null,true,
            "+1% Dodge; movement effects outside solo DPS",SourceBuild,Source,"published-client-tooltip-provisional"),
        new("elunes-light","Elune's Light","night-elf","active",1259799,true,
            "+10% melee/ranged/spell critical chance for 15s; 180s personal cooldown, opt-in",SourceBuild,
            "https://bindonforever.com/races/night-elf","published-client-tooltip-provisional"),
        new("expansive-mind","Expansive Mind","gnome","passive",20591,true,
            "+5% maximum Mana for Mana-using classes (not +5% Intellect)",SourceBuild,
            "https://wow-forever.gg/db/spells/20591-expansive-mind/","published-client-tooltip-provisional"),
        new("endurance","Endurance","tauren","passive",null,true,
            "+5% maximum Health; separate unmodeled critical damage/healing bonus",SourceBuild,Source,
            "partially-modeled"),
        new("wind-blessed-alliance","Wind Blessed","high-order-skyborne","passive",null,true,
            "+1% physical and spell Haste in the current published source; prior preview differed",SourceBuild,Source,
            "published-client-tooltip-provisional"),
        new("wind-blessed-horde","Wind Blessed","windshaper-skyborne","passive",null,true,
            "+1% physical and spell Haste in the current published source; prior preview differed",SourceBuild,Source,
            "published-client-tooltip-provisional"),
        new("berserking","Berserking","troll","active",20554,true,
            "+10% attack and cast Haste for 10s, 180s cooldown; launch duration subject to hotfix",SourceBuild,
            "https://bindonforever.com/races/troll","published-client-tooltip-provisional"),
        new("touch-of-the-grave","Touch of the Grave","undead","passive",null,false,
            "Health-drain chance reference; real trigger policy and coefficients not certified",SourceBuild,Source,
            "unmodeled-proc")
    ];
    public static IReadOnlyList<ForeverRacialDefinition> ForRace(string race)=>
        Entries.Where(x=>x.Race.Equals(race,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Key).ToArray();
}
