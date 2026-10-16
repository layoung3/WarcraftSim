using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Research-backed item NAMES plus explicit modeled budget stats. Gear listings are
/// pre-raid candidates, not verified BIS and not authoritative individual-item stat records.
/// Known weapon stats are assigned to the weapon; the other 15 slots have no fabricated stats.
/// BaseStats is a provisional modeled aggregate for the rest of the loadout.
/// </summary>
public static class ElementalPreRaidProfileCatalog
{
    private static readonly (string Slot,string Throughput,string Sustain)[] Items =
    [
        ("head","Blue Dragonscale Helm","Spellpower Goggles Xtreme Plus"),
        ("neck","Chains of the Lich","Amulet of the Dawn"),
        ("shoulder","Mantle of the Timbermaw","Argent Shoulders"),
        ("back","Arcanoweave Cloak","Hide of the Wild"),
        ("chest","Fernpulse Jerkin","Vest of Elements"),
        ("wrist","Dryad's Wrist Bindings","Runecloth Cuffs"),
        ("hands","Gloves of the Greatfather","Storm Gauntlets"),
        ("waist","Belt of the Archmage","Stormseeker's Girdle"),
        ("legs","Ironfeather Leggings","Red Dragonscale Leggings"),
        ("feet","Earthen Silk Slippers","Omnicast Boots"),
        ("finger1","Brilliant Watcher's Signet","Maiden's Circle"),
        ("finger2","Blessed Band of Light","Channeler's Ring"),
        ("trinket1","Talisman of Ascendance","Smokey's Lighter"),
        ("trinket2","Weakness Analyzer","Serenity Field"),
        ("main-hand","Ironbark Staff","Crackling Staff"),
        ("relic","Totem of the Storm","Burning Totem")
    ];
    public static CharacterProfile Character(string gearKey)
    {
        if(gearKey is not ("throughput" or "sustain"))
            throw new ArgumentException("Unknown Elemental gear profile (throughput/sustain).");
        var throughput=gearKey=="throughput";
        var profile=new CharacterProfile
        {
            Name=throughput?"Elemental — Pre-raid throughput candidate":"Elemental — Accessible sustain candidate",
            RulesetKey="wow-forever",ClassKey="shaman",SpecializationKey="elemental",Level=60,
            // These are explicitly ASSUMED remaining-sheet budgets, not item-verified stat totals.
            // The verified weapon contribution is added separately by Equipment.
            BaseStats=new StatCollection{Values=new Dictionary<string,decimal>
            {
                ["intellect"]=throughput?208m:226m,
                ["spirit"]=throughput?178m:200m,
                ["spell-power"]=throughput?300m:260m,
                ["mp5"]=throughput?27m:48m,
                ["spell-hit-chance-percent"]=throughput?5m:4m
            }}
        };
        foreach (var (slot,through,sustain) in Items)
        {
            var name=throughput?through:sustain;
            var item=new CharacterEquipmentItem
            {
                SlotKey=slot,ItemKey=$"cp137-{gearKey}-{slot}",Name=name
            };
            if(slot=="main-hand")
            {
                // Partial sourced weapon stats only. 2% spell crit on Ironbark;
                // Crackling's 25 Intellect and 99 spell power are sourced candidates.
                item.Stats.Set("spell-power",throughput?94m:99m);
                if(throughput)item.Stats.Set("spell-critical-chance-percent",2m);
                else item.Stats.Set("intellect",25m);
                var enchant=new CharacterEquipmentModifier
                {
                    PlacementKey="main-hand",ModifierKey="two-hand-mighty-spell-power",
                    ModifierType=CharacterEquipmentModifierTypes.Enchant,
                    Name="Enchant 2H Weapon - Mighty Spell Power (amount pending verification)"
                };
                item.Modifiers.Add(enchant);
            }
            profile.Equipment.Items.Add(item);
        }
        return profile;
    }

    public static ForeverElementalShamanTalents Talents(string build)
    {
        var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
        {
            ["convection"]=5,["concussion"]=5,["call-of-flame"]=3,
            ["elemental-alacrity"]=3,["elemental-focus"]=1,
            ["lightning-overload"]=3,["elemental-fury"]=5,
            ["lava-burst"]=1,["elemental-mastery"]=1,
        };
        ForeverElementalShamanTalents result;
        switch (build)
        {
            case "raid-sustain":
                ranks["elemental-warding"]=3; ranks["reverberation"]=1;
                ranks["thundering-strikes"]=5; ranks["ancestral-knowledge"]=2;
                ranks["tidal-focus"]=5; ranks["mindfulness"]=3; ranks["totemic-focus"]=5;
                result=new(){ElementalPointsSpent=31,EnhancementPointsSpent=7,RestorationPointsSpent=13,Ranks=ranks};
                break;
            case "deep-elemental":
                ranks["elemental-warding"]=3; ranks["reverberation"]=5;
                ranks["elemental-reach"]=2; ranks["eye-of-the-storm"]=3;
                ranks["trial-by-fire"]=1;
                ranks["thundering-strikes"]=5; ranks["ancestral-knowledge"]=5;
                result=new(){ElementalPointsSpent=41,EnhancementPointsSpent=10,RestorationPointsSpent=0,Ranks=ranks};
                break;
            case "cleave-hybrid":
                ranks["elemental-warding"]=3; ranks["reverberation"]=5;
                ranks["elemental-reach"]=2; ranks["trial-by-fire"]=1;
                ranks["thundering-strikes"]=5; ranks["ancestral-knowledge"]=5;
                ranks["improved-ghost-wolf"]=2; ranks["shamanistic-focus"]=1;
                result=new(){ElementalPointsSpent=38,EnhancementPointsSpent=13,RestorationPointsSpent=0,Ranks=ranks};
                break;
            default: throw new ArgumentException("Unknown Elemental talent build.");
        }
        result.Validate();
        return result;
    }

    public static ElementalShamanSimRequest Setup(string gear, string talents)
    {
        var build=Talents(talents);
        return new ElementalShamanSimRequest
        {
            Character=Character(gear),Talents=build,
            RotationPolicy="adaptive",FlameShockPolicy="auto",UseSearingTotem=true,
            UseManaSpringTotem=true,UseElementalMastery=true,
            UseChainLightning=true, TargetCount=1,
            CaptureTimeline=false, DurationSeconds=120m
        };
    }
}
