namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 playable race/class matrix. Class eligibility is source-backed;
/// race/class/level base health/primary stat progressions are NOT encoded in this
/// build. Do not synthesize numeric level-1..60 growth from classic expansions.
/// </summary>
public sealed record ForeverRaceDefinition(
    string Key,string Name,string Faction,IReadOnlyList<string> Classes,
    string SourceBuild,string SourceUrl);

public static class ForeverPlayableRaceCatalog
{
    public const string SourceBuild="1.60.1.70532";
    public const string SourceUrl="https://wow-forever.gg/races/";
    public static IReadOnlyList<ForeverRaceDefinition> Races { get; }=[
        new("human","Human","alliance",["warrior","paladin","hunter","rogue","priest","mage","warlock"],SourceBuild,SourceUrl),
        new("dwarf","Dwarf","alliance",["warrior","paladin","hunter","rogue","priest","shaman"],SourceBuild,SourceUrl),
        new("night-elf","Night Elf","alliance",["warrior","hunter","rogue","priest","druid"],SourceBuild,SourceUrl),
        new("gnome","Gnome","alliance",["warrior","rogue","priest","mage","warlock"],SourceBuild,SourceUrl),
        new("high-order-skyborne","High Order Skyborne","alliance",["warrior","hunter","rogue","mage","druid"],SourceBuild,SourceUrl),
        new("orc","Orc","horde",["warrior","hunter","rogue","shaman","mage","warlock"],SourceBuild,SourceUrl),
        new("undead","Undead","horde",["warrior","paladin","rogue","priest","mage","warlock"],SourceBuild,SourceUrl),
        new("tauren","Tauren","horde",["warrior","hunter","shaman","druid"],SourceBuild,SourceUrl),
        new("troll","Troll","horde",["warrior","hunter","rogue","priest","shaman","mage","druid"],SourceBuild,SourceUrl),
        new("windshaper-skyborne","Windshaper Skyborne","horde",["warrior","hunter","rogue","shaman","druid"],SourceBuild,SourceUrl)
    ];
    public static ForeverRaceDefinition Find(string key) =>
        Races.FirstOrDefault(x=>x.Key.Equals(key?.Trim(),StringComparison.OrdinalIgnoreCase))
        ??throw new ArgumentException($"Unsupported or ambiguous Forever race '{key}'. Select a faction-specific Skyborne.");
    public static void Validate(string race,string classKey,int level)
    {
        if(level is <1 or >60)throw new ArgumentException("Forever character level must be 1–60.");
        if(!Find(race).Classes.Contains(classKey,StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Race '{race}' cannot select class '{classKey}' in the pinned Forever race table.");
    }
    public static object BaseStats(string race,string classKey,int level)
    {
        Validate(race,classKey,level);
        // Only Warrior level-60 race preview is already available, and the
        // existing source explicitly labels it NOT verified Forever.
        if(classKey.Equals("warrior",StringComparison.OrdinalIgnoreCase) && level==60 &&
           ForeverWarriorRaceProfiles.Races.FirstOrDefault(x=>x.Id.Equals(race,StringComparison.OrdinalIgnoreCase))
               is { } proxy)
            return new {
                race,classKey,level,sourceStatus="classic-era-level60-proxy-not-forever-verified",
                strength=(decimal?)proxy.Strength,agility=(decimal?)proxy.Agility,
                stamina=(decimal?)proxy.Stamina,intellect=(decimal?)proxy.Intellect,
                spirit=(decimal?)proxy.Spirit,
                missing="Health/Mana baseline still configurable, no verified Forever level growth"
            };
        return new {
            race,classKey,level,sourceStatus="forever-primary-attributes-not-established",
            strength=(decimal?)null,agility=(decimal?)null,
            stamina=(decimal?)null,intellect=(decimal?)null,
            spirit=(decimal?)null,
            missing="70707 PlayerExpectedStat contains class/level BaseMana and conversion fields, but race-specific primary attributes are absent and ContentSetID needs reconciliation; legacy explicit profiles remain supported"
        };
    }
}
