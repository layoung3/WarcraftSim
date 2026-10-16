using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

/// <summary>Two pre-raid gear *candidates* and three talent strategies. Individual
/// non-weapon item stats remain unverified; only explicit aggregate budgets simulate.
/// No current-raid gear is used and no gear-name string changes DPS on its own.</summary>
public static class BalancePreRaidProfileCatalog
{
    private static readonly (string Slot,string Throughput,string Sustain)[] Items=
    [
        ("head","Spellweaver's Turban","Crimson Felt Hat"),
        ("neck","Nacreous Shell Necklace","Tempest Talisman"),
        ("shoulder","Burial Shawl","Cyclone Spaulders"),
        ("back","Spritecaster Cape","Amplifying Cloak"),
        ("chest","Robe of Everlasting Night","Chestplate of Tranquility"),
        ("wrist","Sublime Wristguards","Arena Wristguards"),
        ("hands","Hands of Power","Earth Warder's Gloves"),
        ("waist","Ban'thok Sash","Oddly Magical Belt"),
        ("legs","Skyshroud Leggings","Luminary Kilt"),
        ("feet","Waterspout Boots","Omnicast Boots"),
        ("finger1","Maiden's Circle","Songstone of Ironforge"),
        ("finger2","Eye of Orgrimmar","Band of the Unicorn"),
        ("trinket1","Briarwood Reed","Burst of Knowledge"),
        ("trinket2","Eye of the Beast","Eye of the Beast"),
        ("main-hand","Rod of the Ogre Magi","Witchblade"),
        ("off-hand","Empty (two-hand staff)","Spirit of Aquementas"),
        ("relic","Idol of the Moon (availability uncertain)","Idol of the Moon (availability uncertain)")
    ];
    public static CharacterProfile Character(string gear)
    {
        if(gear is not ("throughput" or "sustain"))throw new ArgumentException("Unknown Balance gear profile.");
        var isThroughput=gear=="throughput";
        var profile=new CharacterProfile
        {
            Name=isThroughput?"Balance pre-raid throughput candidate (provisional)":
                "Balance pre-raid mana-sustain candidate (provisional)",
            RulesetKey="wow-forever",ClassKey="druid",SpecializationKey="balance",Level=60,
            BaseStats=new StatCollection{Values=new Dictionary<string,decimal>
            {
                ["intellect"]=isThroughput?230m:255m,
                ["spirit"]=isThroughput?180m:215m,
                ["spell-power"]=isThroughput?335m:285m,
                ["mp5"]=isThroughput?24m:47m,
                ["spell-hit-chance-percent"]=isThroughput?5m:4m,
                ["spell-critical-chance-percent"]=isThroughput?2m:1m
            }}
        };
        foreach(var (slot,throughput,sustain) in Items)
        {
            var name=isThroughput?throughput:sustain;
            if(name.StartsWith("Empty (",StringComparison.Ordinal))continue;
            profile.Equipment.Items.Add(new CharacterEquipmentItem
            {
                SlotKey=slot,ItemKey=$"cp138-{gear}-{slot}",Name=name
            });
        }
        return profile;
    }
    public static ForeverBalanceDruidTalents Talents(string build)
    {
        var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-wrath"]=5,["genesis"]=5,["improved-moonfire"]=2,
            ["moonglow"]=3,["natures-majesty"]=2,["natures-reach"]=2,
            ["natures-splendor"]=1,["insect-swarm"]=1,["vengeance"]=5,
            ["eclipse"]=3,["moonfury"]=5,["moonkin-form"]=1,
            ["natures-grace"]=1,["improved-starfire"]=5,
            ["naturalist"]=5,["natures-focus"]=5
        };
        ForeverBalanceDruidTalents result;
        switch(build)
        {
            case "throughput":
                result=new(){BalancePointsSpent=41,FeralPointsSpent=0,RestorationPointsSpent=10,Ranks=ranks};break;
            case "raid-sustain":
                ranks["genesis"]=2;ranks["reflection"]=3;
                result=new(){BalancePointsSpent=38,FeralPointsSpent=0,RestorationPointsSpent=13,Ranks=ranks};break;
            case "deep-regen":
                ranks.Remove("genesis");ranks["reflection"]=3;ranks["furor"]=2;
                result=new(){BalancePointsSpent=36,FeralPointsSpent=0,RestorationPointsSpent=15,Ranks=ranks};break;
            default:throw new ArgumentException("Unknown Balance talent build.");
        }
        result.Validate();return result;
    }
    public static BalanceDruidSimRequest Setup(string gear,string talents)=>new()
    {
        Character=Character(gear),Talents=Talents(talents),RotationPolicy="adaptive",
        UseOmenOfClarity=true,UseMoonfire=true,UseInsectSwarm=true,UseStarfire=true,
        UseEclipse=true,StarfireOnlyWithEclipse=true,ConserveManaForEncounter=true,
        CaptureTimeline=false,DurationSeconds=120m
    };
}
