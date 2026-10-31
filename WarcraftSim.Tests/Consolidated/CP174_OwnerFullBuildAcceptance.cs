using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Owner's 2026-10-31 120-second Orc Arcane Mage export, reduced to stable
/// typed inputs and exact IDs; no fabricated combat event or predetermined DPS.
/// The original export showed invisible Major Mana Potion at 19.53s, and a
/// Faerie Fire removal at exactly 40s after its initial time-zero application.
/// </summary>
public sealed class Cp174OwnerFullBuildAcceptanceTests(ITestOutputHelper output)
{
    private static readonly Dictionary<string,int> OwnerRanks=new(StringComparer.OrdinalIgnoreCase){
        ["improved-channeling"]=5,["arcane-impact"]=3,
        ["arcane-concentration"]=5,["arcane-meditation"]=3,
        ["arcane-mind"]=5,["arcane-instability"]=3,
        ["arcane-blast"]=1,["missile-barrage"]=2,
        ["arcane-wrath"]=1,["presence-of-mind"]=1,
        ["arcane-power"]=1,["accelerant"]=5,
        ["arcane-subtlety"]=2,["ignite"]=5,
        ["incineration"]=5,["precision"]=3,["pyroblast"]=1
    };

    private static MageSimRequest Build(bool selectedPotion=false,string debuffMode="maintained")
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Character.Race="orc";r.UseProvisionalLevel60Baseline=true;
        r.UseRacialActive=true;r.Seed=132001;r.DurationSeconds=120m;
        r.TargetMaximumHealth=200000m;r.CaptureTimeline=true;
        r.UseArcanePower=true;r.UsePresenceOfMind=true;
        r.UseMajorManaPotion=selectedPotion;
        r.MajorManaPotionCharges=selectedPotion?1:0;
        r.CombatItems=new(){
            ManaRubyCharges=1,ManaCitrineCharges=1,ManaJadeCharges=1,ManaAgateCharges=1,
            DarkRuneCharges=5
        };
        r.ExternalModifiers=new(){
            Buffs=["arcane-intellect","power-word-fortitude"],
            Consumables=["elixir-of-the-sages","flask-of-supreme-power",
                "greater-arcane-elixir","runn-tum-tuber-surprise"],
            TargetDebuffs=["curse-of-elements","faerie-fire-rank4"],
            TargetDebuffMode=debuffMode
        };
        r.Talents=new ForeverMageTalentBuild{
            ArcanePointsSpent=32,FirePointsSpent=16,FrostPointsSpent=3,
            // arcane-subtlety is explicitly unmodeled and must NOT be passed
            // to the typed Mage runner as a phantom combat bonus.
            Ranks=OwnerRanks.Where(x=>x.Key!="arcane-subtlety")
                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase)
        };
        r.Character.Equipment=new CharacterEquipmentLoadout{
            Items=[
                new(){SlotKey="head",ItemId=22498},
                new(){SlotKey="main-hand",ItemId=19102,EnchantSpellId=1219580,
                    TemporaryEnchantSpellId=25122},
                new(){SlotKey="ranged",ItemId=11287},
                new(){SlotKey="shoulder",ItemId=22499},
                new(){SlotKey="chest",ItemId=22496},
                new(){SlotKey="hands",ItemId=22501},
                new(){SlotKey="legs",ItemId=22497},
                new(){SlotKey="finger1",ItemId=23062},
                new(){SlotKey="trinket1",ItemId=19339},
                new(){SlotKey="trinket2",ItemId=18820},
                new(){SlotKey="finger2",ItemId=18500},
                new(){SlotKey="feet",ItemId=22500},
                new(){SlotKey="waist",ItemId=22502},
                new(){SlotKey="wrist",ItemId=22503},
                new(){SlotKey="back",ItemId=13340},
                new(){SlotKey="neck",ItemId=18404}
            ]
        };
        return r;
    }

    [Fact]
    public async System.Threading.Tasks.Task OwnerItemizedRequestExecutesLearnedCooldownsWithoutUnselectedPotion()
    {
        var r=Build();
        Assert.Equal(51,OwnerRanks.Values.Sum());
        Assert.Equal(16,r.Character.Equipment.Items.Count);
        var wrapped=new ForeverLevel60SimulationRequest{
            ClassKey="mage",Specialization="arcane",ItemizedTalentMode=true,
            ItemizedTalentRanks=new(OwnerRanks,StringComparer.OrdinalIgnoreCase),Mage=r
        };
        var result=Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController()
            .Simulate(wrapped,CancellationToken.None));
        // Underlying engine response contains the real combat run; the typed
        // request's event processor is also directly covered below.
        Assert.NotNull(result.Value);
        var simulation=new MageSimRunner().Run(Build());
        var events=simulation.Run.Timeline;
        Assert.Contains(events,e=>e.AbilityKey=="arcane-power"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(events,e=>e.AbilityKey=="presence-of-mind"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.DoesNotContain(events,e=>e.AbilityKey=="major-mana-potion"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Equal(0,simulation.MajorManaPotionUses);
        Assert.True(simulation.DamageDone>0m);
        output.WriteLine($"OWNER-ARCARC 120s: simulated DPS {simulation.Dps:0.####}, " +
            $"Arcane Power casts {events.Count(x=>x.AbilityKey=="arcane-power"&&x.Type==CombatEventType.AbilityCastCompleted)}, " +
            $"Presence of Mind casts {events.Count(x=>x.AbilityKey=="presence-of-mind"&&x.Type==CombatEventType.AbilityCastCompleted)}, " +
            $"unselected Mana Potion uses {simulation.MajorManaPotionUses}.");
        Assert.True(simulation.Run.Summary.Abilities.Count>0);
        Assert.Contains(events,e=>e.AbilityKey=="faerie-fire-rank4"&&
            e.Type==CombatEventType.AuraApplied);
    }

    [Fact]
    public void SelectedPotionActivatesOnlyWithPositivePreparedInventory()
    {
        var disabled=new MageSimRunner().Run(Build());
        var enabled=new MageSimRunner().Run(Build(selectedPotion:true));
        Assert.Equal(0,disabled.MajorManaPotionUses);
        Assert.DoesNotContain(disabled.Run.Timeline,e=>e.AbilityKey=="major-mana-potion"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Equal(1,enabled.MajorManaPotionUses);
        Assert.Contains(enabled.Run.Timeline,e=>e.AbilityKey=="major-mana-potion"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        var noInventory=Build(selectedPotion:true);
        noInventory.MajorManaPotionCharges=0;
        var absent=new MageSimRunner().Run(noInventory);
        Assert.Equal(0,absent.MajorManaPotionUses);
        output.WriteLine($"OWNER-ARCARC potion control: OFF {disabled.MajorManaPotionUses} uses, " +
            $"explicitly selected ON {enabled.MajorManaPotionUses}, zero prepared charges {absent.MajorManaPotionUses}.");
    }

    [Fact]
    public void ExternallyMaintainedFaerieFireRefreshesAtFortyAndEightyWithoutStatStacking()
    {
        var initial=new MageSimRunner().Run(Build(debuffMode:"initial-only"));
        var active=new MageSimRunner().Run(Build(debuffMode:"maintained"));
        static decimal[] Times(MageSimResponse r,CombatEventType type)=>
            r.Run.Timeline.Where(e=>e.AbilityKey=="faerie-fire-rank4"&&
                e.Type==type).Select(e=>e.TimeSeconds).ToArray();
        Assert.Equal(new[]{0m},Times(initial,CombatEventType.AuraApplied));
        Assert.Contains(40m,Times(initial,CombatEventType.AuraRemoved));
        Assert.Equal(new[]{0m,40m,80m},Times(active,CombatEventType.AuraApplied));
        Assert.Contains(40m,Times(active,CombatEventType.AuraRemoved));
        Assert.Contains(80m,Times(active,CombatEventType.AuraRemoved));
        // Maintenance never adds another spell damage event under the
        // configured external actor or creates duplicate same-time auras.
        Assert.DoesNotContain(active.Run.Timeline,e=>e.SourceActorKey=="configured-external"&&
            e.Type==CombatEventType.Damage);
        Assert.Equal(3,Times(active,CombatEventType.AuraApplied).Distinct().Count());
        Assert.True(active.DamageDone>=0m);
        output.WriteLine($"OWNER-ARCARC Faerie Fire: initial-only applications " +
            $"{string.Join(",",Times(initial,CombatEventType.AuraApplied))}s; maintained " +
            $"{string.Join(",",Times(active,CombatEventType.AuraApplied))}s.");
    }

    [Fact]
    public void LearnedOffensiveCooldownsRespectExplicitUseFlags()
    {
        var permitted=Build();
        permitted.UseArcanePower=false;permitted.UsePresenceOfMind=false;
        var disabled=new MageSimRunner().Run(permitted);
        Assert.DoesNotContain(disabled.Run.Timeline,e=>
            e.AbilityKey is "arcane-power" or "presence-of-mind" &&
            e.Type==CombatEventType.AbilityCastCompleted);
        var enabled=new MageSimRunner().Run(Build());
        Assert.Contains(enabled.Run.Timeline,e=>e.AbilityKey=="arcane-power"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(enabled.Run.Timeline,e=>e.AbilityKey=="presence-of-mind"&&
            e.Type==CombatEventType.AbilityCastCompleted);
        output.WriteLine($"OWNER-ARCARC learned cooldown: explicitly OFF {disabled.Dps:0.####} DPS, " +
            $"explicitly ON {enabled.Dps:0.####} DPS (seed 132001, same gear/consumables).");
    }
}
