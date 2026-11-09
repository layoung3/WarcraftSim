using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// D1: converts the common character-builder equipment and ranked talents
/// into the existing authoritative Warrior runner. No combat rules are
/// duplicated in JavaScript or a second Warrior engine.
/// </summary>
[ApiController]
[Route("api/forever/shared/warrior")]
public sealed class ForeverWarriorBuilderController : ControllerBase
{
    public sealed class Input
    {
        public WarriorSimRequest Warrior {get;set;} = new();
        public CharacterEquipmentLoadout Equipment {get;set;} = new();
    }
    [HttpGet("defaults")]
    public IActionResult Defaults([FromQuery]string spec="arms")
    {
        if(spec is not ("arms" or "fury" or "protection"))
            return BadRequest(new {error="Unsupported Warrior starting preset."});
        return Ok(new WarriorSimRequest {
            Preset=spec,TalentMode="tree",Race="human",StatSourceMode="race-base",
            DurationSeconds=120m,Iterations=1,Seed=175600,
            CaptureRepresentativeTimeline=true,StartingStance="automatic",
            RotationMode="automatic"
        });
    }
    [HttpGet("talents")]
    public IActionResult Talents() => Ok(new {
        classKey="warrior",level=60,maxPoints=51,
        talents=ForeverWarriorTalentCatalog.Talents.Select(x=>new {
            key=x.Id,name=x.Name,tree=x.Tree,maxRank=x.MaxRank,
            row=x.Row,column=x.Column,requiredEarlierPoints=(x.Row-1)*5,
            enginePrerequisiteKey=x.PrerequisiteId,
            enginePrerequisiteRank=x.PrerequisiteId is null ? 0 :
                x.RequiredPrerequisiteRank ??
                ForeverWarriorTalentCatalog.Talents.Single(t=>t.Id==x.PrerequisiteId).MaxRank,
            modeled=x.SimulationStatus!="not-modeled",
            simulationStatus=x.SimulationStatus,implementationStatus=x.ImplementationStatus,
            dataConfidence=x.DataConfidence,sourceBuild=x.SourceBuild,
            sourceUrl="https://wowfdb.com/talents/warrior/",
            visualSourceStatus=ForeverWarriorTalentCatalog.Version,
            historicalRankDescriptions=new[]{x.Description}
        }).ToArray()
    });
    [HttpPost("validate")]
    public IActionResult Validate([FromBody]WarriorSimRequest? request)
    {
        try
        {
            if(request is null)throw new ArgumentException("Warrior request is missing.");
            if(request.Level!=60 || request.Preset is not ("arms" or "fury" or "protection"))
                throw new ArgumentException("Warrior builder only supports level 60 and a known starting preset.");
            if(request.TalentMode!="tree")
                throw new ArgumentException("Warrior builder needs rank-derived tree mode.");
            ForeverWarriorTalentCatalog.Validate(request);
            return Ok(new {valid=true,pointsSpent=request.TalentRanks.Values.Sum()});
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
    }
    [HttpPost("rotation-preview")]
    public IActionResult RotationPreview([FromBody]WarriorSimRequest? warrior)
    {
        try
        {
            if(warrior is null)throw new ArgumentException("Warrior is required.");
            ForeverWarriorTalentCatalog.Validate(warrior);
            WarriorRotationRules.Validate(warrior);
            var rotation=warrior.RotationMode=="custom" ?
                WarriorRotationRules.BuildCustom(warrior) :
                ForeverWarriorSimRunner.BuildRotation(warrior,
                    string.Equals(warrior.Preset,"arms",StringComparison.OrdinalIgnoreCase));
            SoloDpsPriorityConditionBridge.Apply(rotation,warrior.PriorityConditions,
                warrior.RotationMode=="custom"?warrior.CustomRotation
                    .Where(x=>x.Enabled).Select(x=>x.Action):[],
                new[]{"target"}.Concat(warrior.AdditionalTargets.Select(x=>x.Key)));
            return Ok(new {
                entries=rotation.Entries.Select(x=>new {
                    abilityKey=string.IsNullOrEmpty(x.AbilityKey) ?
                        x.NextSwingReplacementKey:x.AbilityKey,
                    enabled=x.IsEnabled,
                    conditionCount=x.Conditions.Count
                }).ToArray(),
                warnings=WarriorRotationRules.GetUnavailableTalentActions(warrior)
            });
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
    }
    [HttpPost("prepare")]
    public IActionResult Prepare([FromBody]Input? input)
    {
        try
        {
            if(input?.Warrior is null || input.Equipment?.Items is null)
                throw new ArgumentException("Warrior and equipment selections are required.");
            var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var warrior=JsonSerializer.Deserialize<WarriorSimRequest>(
                JsonSerializer.Serialize(input.Warrior,options),options) ??
                throw new ArgumentException("Could not clone Warrior input.");
            if(warrior.Level!=60 || warrior.Preset is not ("arms" or "fury" or "protection") ||
               warrior.TalentMode!="tree")
                throw new ArgumentException("A level-60 Warrior and itemized talents are required.");
            ForeverWarriorRaceProfiles.Get(warrior.Race);
            ForeverWarriorTalentCatalog.Validate(warrior);
            warrior.StatSourceMode="race-base";
            warrior.CaptureRepresentativeTimeline=true;
            var currentHands=WarriorBuilderHandInputs.Capture(warrior);
            var previousHands=warrior.BuilderHandState;
            if(previousHands is not null && (previousHands.ManualInputs is null || previousHands.LastResolvedInputs is null))
                throw new ArgumentException("Builder hand state requires original manual inputs and last resolved inputs.");
            if(previousHands is null && warrior.EquipmentMode=="gear-bonuses" &&
                warrior.Gear?.Any(x=>x.ItemId.HasValue && x.Slot is "main-hand" or "off-hand")==true)
                throw new ArgumentException("This previously resolved hand loadout has no original manual inputs. Reopen the original build or submit explicit manual inputs with empty Gear and no BuilderHandState; removed catalog weapon or shield values cannot be guessed.");
            var manualHands=previousHands is null?currentHands:
                previousHands.ManualInputs.WithEditsSince(previousHands.LastResolvedInputs,currentHands);
            manualHands.ApplyTo(warrior);
            // Common resolver validates source IDs, class restrictions, slots,
            // unique items, permanent and temporary enchant compatibility.
            var equipment=ForeverSharedEquipmentResolver.Resolve(
                input.Equipment,"warrior",60,warrior.Race);
            var selected=equipment.Loadout.Items;
            // The explicit shared loadout is authoritative, including an empty
            // selection after unequipping. Never retain a prior prepared Gear list.
            warrior.EquipmentMode="gear-bonuses";
            warrior.Gear=[];
            if(selected.Any(x=>x.ItemId.HasValue && x.SlotKey=="main-hand"))
            {
                warrior.EquipmentMode="catalog";
                warrior.Gear=selected.Select(x=>new WarriorGearEntry {
                    Slot=x.SlotKey,ItemId=x.ItemId,EnchantSpellId=x.EnchantSpellId,
                    ItemKey=x.ItemId.HasValue ? "" : x.ItemKey,
                    Name=x.Name
                }).ToList();
                // Reuse the existing Warrior weapon and permanent-enchant
                // bridge; then merge duration-eligible temporary enhancement
                // stat deltas from the common equipment catalog.
                warrior=ForeverEquipmentCatalog.ResolveWarriorLoadout(warrior);
                foreach(var selection in selected.Where(x=>x.TemporaryEnchantSpellId.HasValue))
                {
                    var effect=ForeverEquipmentCatalog.FindEnchant(
                        selection.TemporaryEnchantSpellId!.Value);
                    if(!effect.IsTemporary || !effect.Modeled ||
                       effect.DurationSeconds<warrior.DurationSeconds)
                        throw new ArgumentException("Temporary enhancement is not executable for the full requested Warrior duration.");
                    var gear=warrior.Gear.Single(x=>x.Slot==selection.SlotKey);
                    foreach(var (key,amount) in effect.Stats)
                        gear.EnchantStats[key]=gear.EnchantStats.GetValueOrDefault(key)+amount;
                    gear.EnchantName=(gear.EnchantName is null ? "" : gear.EnchantName+" + ")+effect.Name;
                }
            }
            else if(selected.Count>0)
            {
                // Until a real main-hand is equipped, keep the established
                // editable manual weapon baseline; no fabricated weapon ID.
                warrior.EquipmentMode="gear-bonuses";
                var offHand=equipment.Loadout.Items.SingleOrDefault(x=>x.SlotKey=="off-hand");
                if(offHand?.ItemId is int offHandId)
                {
                    if(warrior.IsTwoHanded)
                        throw new ArgumentException("A selected off-hand cannot accompany the manual two-handed main-hand. Choose an explicit one-handed main-hand configuration or unequip the off-hand.");
                    var source=ForeverEquipmentCatalog.FindItem(offHandId);
                    warrior.ShieldEquipped=source.ShieldBlockValue>0m;
                    warrior.ShieldIntrinsicBlockValue=source.ShieldBlockValue;
                    if(warrior.ShieldEquipped)
                        warrior.PlayerBlockValue=warrior.TankStatMode=="derived"?
                            source.ShieldBlockValue:warrior.PlayerBlockValue+source.ShieldBlockValue;
                    warrior.DualWield=equipment.Weapons.TryGetValue("off-hand",out var weapon);
                    warrior.OffHandWeaponType=weapon?.Type??"";
                    if(weapon is not null)
                    {
                        warrior.OffHandMinimumDamage=weapon.MinimumDamage;
                        warrior.OffHandMaximumDamage=weapon.MaximumDamage;
                        warrior.OffHandSpeedSeconds=weapon.SpeedSeconds;
                    }
                }
                warrior.Gear=equipment.Loadout.Items.Select(x=>new WarriorGearEntry {
                    Slot=x.SlotKey,ItemKey=x.ItemKey,ItemId=x.ItemId,Name=x.Name,
                    EnchantSpellId=x.EnchantSpellId,
                    Stats=new(x.Stats.Values,StringComparer.OrdinalIgnoreCase),
                    SetBonusStats=x.Modifiers.Where(y=>y.ModifierType=="set-bonus")
                        .SelectMany(y=>y.Stats.Values).GroupBy(y=>y.Key,StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(y=>y.Key,y=>y.Sum(v=>v.Value),StringComparer.OrdinalIgnoreCase),
                    SetBonusName=x.Modifiers.Any(y=>y.ModifierType=="set-bonus")?
                        string.Join(" + ",x.Modifiers.Where(y=>y.ModifierType=="set-bonus").Select(y=>y.Name)):null,
                    EnchantStats=x.Modifiers.Where(y=>y.PlacementKey is "permanent" or "temporary")
                        .SelectMany(y=>y.Stats.Values).GroupBy(y=>y.Key,StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(y=>y.Key,y=>y.Sum(v=>v.Value),StringComparer.OrdinalIgnoreCase),
                    EnchantName=x.Modifiers.Any(y=>y.PlacementKey is "permanent" or "temporary")?
                        "Resolved equipment enhancements":null
                }).ToList();
            }
            if(warrior.ShieldEquipped && (warrior.IsTwoHanded || warrior.DualWield))
                throw new ArgumentException("A shield requires an explicit one-handed main-hand configuration without an off-hand weapon.");
            if(warrior.IsTwoHanded && warrior.DualWield)
                throw new ArgumentException("A two-handed main-hand cannot be combined with dual wield.");
            warrior.BuilderHandState=new() {
                ManualInputs=manualHands,LastResolvedInputs=WarriorBuilderHandInputs.Capture(warrior)
            };
            // Existing Warrior consumers resolve talent effects, modifiers,
            // actual combat item stock, health costs and seeded outcomes.
            var resolvedStats=WarriorGearStatCalculator.Calculate(warrior);
            return Ok(new {warrior,normalizedLoadout=equipment.Loadout,
                effectiveStats=ForeverWarriorSimRunner.BuildPrecombatReportedStats(warrior,resolvedStats.EffectiveStats),
                profileStats=resolvedStats.EffectiveStats.Values,
                contributions=resolvedStats.StatContributions,
                statPreviewScope=warrior.EnableBossAutoAttacks ?
                    warrior.TankStatMode=="manual" ?
                        "Precombat profile with engine-applied manual dodge/parry/block assumptions and learned defensive talents. Raw profile stats and source contributions are retained separately; manual shield values are not catalog equipment." :
                        "Precombat profile with engine-derived defenses from stats, shield and learned talents. Base avoidance and Agility-to-Dodge remain explicit provisional calibrations." :
                    "Precombat profile stats. Incoming boss attacks are disabled, so manual tank-defense overrides are inactive.",
                notes="Uses existing Warrior runner; race base and several combat formulas remain provisional."});
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(InvalidOperationException e){return BadRequest(new{error=e.Message});}
    }
}
