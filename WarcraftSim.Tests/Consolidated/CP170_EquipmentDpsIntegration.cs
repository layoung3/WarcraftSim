using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp170EquipmentChangesRealDps
{
    private static CharacterEquipmentLoadout Gear(params (string Slot,int Item)[] entries)=>
        new(){Items=entries.Select(x=>new CharacterEquipmentItem{
            SlotKey=x.Slot,ItemId=x.Item
        }).ToList()};

    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void MageStaffIncreasesRealSpellPowerAndDamageWithoutMutatingRequest(string spec)
    {
        MageSimRequest Make()
        {
            var req=MageSimRequest.CreatePreset(spec);
            req.DurationSeconds=15m;
            req.TargetMaximumHealth=1000000m;
            req.BaseSpellHitPercent=100m;
            req.SpellCritPercent=0m;
            req.CaptureTimeline=true;
            return req;
        }
        var original=Make();
        var gear=Make();
        gear.Character.Equipment=Gear(("main-hand",19102));
        var baseline=new MageSimRunner().Run(original);
        var first=new MageSimRunner().Run(gear);
        var repeated=new MageSimRunner().Run(gear);
        Assert.Equal(baseline.SpellPower+25m,first.SpellPower);
        Assert.True(first.DamageDone>baseline.DamageDone);
        Assert.Equal(first.DamageDone,repeated.DamageDone);
        Assert.Empty(original.Character.Equipment.Items);
        Assert.Empty(gear.Character.Equipment.Items[0].Stats.Values);
        Assert.InRange(Math.Abs(first.DamageDone-first.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void WarlockOwnerAndDemonRespondToSameVerifiedStaffWithoutDuplicateStats()
    {
        WarlockSimRequest Setup()
        {
            var req=WarlockSimRequest.CreatePreset("demonology");
            req.DurationSeconds=35m;
            req.TargetMaximumHealth=1000000m;
            req.BaseSpellHitPercent=100m;
            req.SpellCritPercent=0m;
            req.PetType="imp";
            req.PrePullSacrificedDemon=null;
            req.UseLifeTap=false;
            req.CaptureTimeline=true;
            return req;
        }
        var basic=Setup();
        var geared=Setup();
        geared.Character.Equipment=Gear(("main-hand",19102));
        var before=new WarlockSimRunner().Run(basic);
        var after=new WarlockSimRunner().Run(geared);
        Assert.Equal(before.SpellPower+25m,after.SpellPower);
        Assert.True(after.WarlockDamage>before.WarlockDamage);
        Assert.True(after.PetDamage>before.PetDamage);
        Assert.Equal(after.WarlockDamage+after.PetDamage,after.DamageDone);
        Assert.InRange(Math.Abs(after.DamageDone-after.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void HunterBowChangesActualAutoShotSpeedAndDamage()
    {
        MarksmanshipHunterSimRequest Setup()
        {
            var req=new MarksmanshipHunterSimRequest{
                DurationSeconds=14m,TargetMaximumHealth=1000000m,
                HasPet=false,UseRapidFire=false,UseAimedShot=false,
                UseSniperShot=false,UseMultiShot=false,
                UseArcaneShot=false,UseSerpentSting=false,
                CaptureTimeline=true
            };
            req.Tuning.HitChancePercent=100m;
            req.Tuning.CriticalChancePercent=0m;
            return req;
        }
        var original=Setup();
        var geared=Setup();
        geared.Equipment=Gear(("ranged",18729));
        var baseline=new MarksmanshipHunterSimRunner().Run(original);
        var equipped=new MarksmanshipHunterSimRunner().Run(geared);
        decimal Interval(MarksmanshipHunterSimResponse run)
        {
            var times=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="auto-shot" && e.Amount>0m)
                .Select(e=>e.TimeSeconds).Take(2).ToArray();
            Assert.Equal(2,times.Length);
            return times[1]-times[0];
        }
        // The first stationary Auto Shot is always at 0.5s; the next one
        // resolves using the equipped bow's actual swing interval.
        Assert.True(Interval(equipped)<Interval(baseline));
        Assert.NotEqual(baseline.DamageDone,equipped.DamageDone);
        Assert.Equal(2.8m,original.Tuning.WeaponSpeedSeconds);
        Assert.Equal(2.8m,geared.Tuning.WeaponSpeedSeconds);
        Assert.InRange(Math.Abs(equipped.DamageDone-equipped.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void HunterAttackPowerRingImprovesBothOwnerAndInheritedPetDamage()
    {
        MarksmanshipHunterSimRequest Setup()
        {
            var req=new MarksmanshipHunterSimRequest{
                HasPet=true,PetFamily="cat",DurationSeconds=28m,
                TargetMaximumHealth=1000000m,UseRapidFire=false,
                CaptureTimeline=true
            };
            req.Tuning.HitChancePercent=100m;
            req.Tuning.CriticalChancePercent=0m;
            req.PetTuning.PetHitPercent=100m;
            req.PetTuning.PetAvoidancePercent=0m;
            return req;
        }
        var plain=Setup();
        var selected=Setup();
        selected.Equipment=Gear(("finger1",17713));
        var before=new MarksmanshipHunterSimRunner().Run(plain);
        var after=new MarksmanshipHunterSimRunner().Run(selected);
        Assert.Equal(before.RangedAttackPower+20m,after.RangedAttackPower);
        Assert.True(after.HunterDamage>before.HunterDamage);
        Assert.True(after.PetDamage>before.PetDamage);
        Assert.Equal(after.HunterDamage+after.PetDamage,after.DamageDone);
        Assert.Empty(selected.Equipment.Items[0].Stats.Values);
    }

    [Fact]
    public void SurvivalMeleeWeaponAndBeastMasteryOwnerPetStatsUseSameCatalog()
    {
        var survival=new SurvivalHunterSimRequest{
            CombatMode="melee",AdditionalTargets=0,DurationSeconds=15m,
            UseFireTraps=false,UseFrostTraps=false,HasPet=false,
            ScriptedAvoidanceEverySeconds=null
        };
        survival.Tuning.HitChancePercent=100m;
        survival.Tuning.TargetDodgePercent=0m;
        survival.Tuning.TargetParryPercent=0m;
        var baseSurvival=new SurvivalHunterSimRunner().Run(survival);
        survival.Equipment=Gear(("main-hand",279259));
        var gearedSurvival=new SurvivalHunterSimRunner().Run(survival);
        Assert.NotEqual(baseSurvival.MeleeDamage,gearedSurvival.MeleeDamage);

        var bm=new BeastMasteryHunterSimRequest{
            DurationSeconds=35m,TargetMaximumHealth=1000000m,
            PetFamily="cat",UseSummonHawk=false,UseRapidFire=false
        };
        bm.Tuning.HunterHitPercent=100m;
        bm.Tuning.PetHitPercent=100m;
        bm.Tuning.PetAvoidancePercent=0m;
        var plain=new BeastMasteryHunterSimRunner().Run(bm);
        bm.Equipment=Gear(("finger1",17713));
        var selected=new BeastMasteryHunterSimRunner().Run(bm);
        Assert.True(selected.HunterDamage>plain.HunterDamage);
        Assert.True(selected.PetDamage>plain.PetDamage);
        Assert.Equal(selected.HunterDamage+selected.PetDamage+selected.HawkDamage,
            selected.DamageDone);
    }

    [Fact]
    public void ExistingStatOnlyModeAndApiRequestSerializationRemainSupported()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.Character.Equipment.Items.Add(new CharacterEquipmentItem {
            SlotKey="finger1",ItemKey="user-preview-stat-item",
            Stats=new WarcraftSim.Core.Stats.StatCollection{
                Values=new Dictionary<string,decimal>{{"spell-power",15m}}
            }
        });
        var resolved=ForeverEquipmentStatBridge.ResolveProfile(request.Character);
        Assert.Equal(15m,resolved.Equipment.Items.Single().Stats.Get("spell-power"));
        var serialized=JsonSerializer.Serialize(request);
        var copy=JsonSerializer.Deserialize<MageSimRequest>(serialized);
        Assert.NotNull(copy);
        Assert.Equal(15m,copy.Character.Equipment.Items.Single().Stats.Get("spell-power"));
        Assert.NotNull(new MageSimRunner().Run(copy));
    }
}
