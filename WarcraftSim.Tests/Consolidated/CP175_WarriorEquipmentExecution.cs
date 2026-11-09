using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorEquipmentExecution
{
    private static WarriorSimRequest Build(int? trinket=null,int? enchant=null) => new()
    {
        Preset="fury",EquipmentMode="catalog",Iterations=1,DurationSeconds=45m,
        Seed=175905,CaptureRepresentativeTimeline=true,
        TargetLevel=60,TargetArmor=0m,TargetDodgePercent=0m,
        TargetParryPercent=0m,TargetBlockPercent=0m,MeleeHitPercent=100m,
        MeleeCritPercent=0m,StartingStance="battle",StartingRage=0m,
        DeepWoundsRank=0,ImpaleRank=0,UseBloodrage=false,UseRend=false,
        UseExecute=true,ExecutePhaseDurationSeconds=0m,UseHeroicStrike=false,UseSlam=false,
        RotationMode="custom",
        CustomRotation=[new WarriorRotationRule { Action="execute" }],
        Gear=trinket is int id
            ? [new WarriorGearEntry { Slot="main-hand",ItemId=279260,EnchantSpellId=enchant },
               new WarriorGearEntry { Slot="trinket1",ItemId=id }]
            : [new WarriorGearEntry { Slot="main-hand",ItemId=279260,EnchantSpellId=enchant }]
    };

    private static CombatEvent[] Hits(WarriorSimResponse run) =>
        run.Batch.RepresentativeRun.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="warrior" && e.AbilityKey=="main-hand-auto").ToArray();

    [Fact]
    public void SlayersCrestAddsExactly260AttackPowerForTwentySecondsThenExpires()
    {
        var baseline=Build();
        baseline.AttackPower+=64m; // Same passive stats, without the on-use identity.
        var plain=new ForeverWarriorSimRunner().Run(baseline);
        var equipped=new ForeverWarriorSimRunner().Run(Build(23041));
        var run=equipped.Batch.RepresentativeRun;
        Assert.Equal(1m,run.CustomMetrics["equipment-slayers-crest@trinket1-activations"]);
        Assert.Equal(20m,run.CustomMetrics["equipment-slayers-crest@trinket1-uptime-seconds"]);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="equipment-slayers-crest@trinket1" && e.TimeSeconds==20m);
        var baseHits=Hits(plain);
        var activeHits=Hits(equipped);
        Assert.Equal(baseHits.Select(e=>e.TimeSeconds),activeHits.Select(e=>e.TimeSeconds));
        var speed=ForeverEquipmentCatalog.FindItem(279260).Weapon!.SpeedSeconds;
        var compared=0;
        for(var i=0;i<baseHits.Length;i++)
        {
            if(baseHits[i].ResultKey!="hit")continue;
            var expected=baseHits[i].TimeSeconds<20m?260m*speed/14m:0m;
            Assert.InRange(Math.Abs(activeHits[i].Amount!.Value-baseHits[i].Amount!.Value-expected),0m,.000001m);
            compared++;
        }
        Assert.True(compared>=3);
        Assert.True(equipped.MeanDps>plain.MeanDps);
    }

    [Fact]
    public void KissOfTheSpiderChangesActualSwingIntervalsAndRestoresThemAtExpiry()
    {
        var request=Build(22954);
        var equipped=new ForeverWarriorSimRunner().Run(request);
        var run=equipped.Batch.RepresentativeRun;
        var hits=Hits(equipped);
        var speed=ForeverEquipmentCatalog.FindItem(279260).Weapon!.SpeedSeconds;
        Assert.Equal(15m,run.CustomMetrics["equipment-kiss-of-the-spider@trinket1-uptime-seconds"]);
        Assert.InRange(Math.Abs((hits[2].TimeSeconds-hits[1].TimeSeconds)-speed/1.2m),0m,.000001m);
        var late=hits.Where(e=>e.TimeSeconds>20m).ToArray();
        Assert.InRange(Math.Abs((late[1].TimeSeconds-late[0].TimeSeconds)-speed),0m,.000001m);
        Assert.True(Hits(new ForeverWarriorSimRunner().Run(Build())).Length<hits.Length);
        var replay=new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(equipped.MeanDps,replay.MeanDps);
        Assert.Equal(hits.Select(e=>(e.TimeSeconds,e.Amount)),Hits(replay).Select(e=>(e.TimeSeconds,e.Amount)));
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="main-hand-auto" && e.ResourceKey=="rage" && e.Amount>0m);
    }

    [Fact]
    public void CurseOfElementsNeverAmplifiesNativePhysicalAttacks()
    {
        var baseline=new ForeverWarriorSimRunner().Run(Build());
        var request=Build();
        request.ExternalModifiers.TargetDebuffs=["curse-of-elements"];
        var cursed=new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(cursed.Batch.RepresentativeRun.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="curse-of-elements");
        Assert.Equal(baseline.MeanDps,cursed.MeanDps);
        Assert.Equal(Hits(baseline).Select(e=>e.Amount),Hits(cursed).Select(e=>e.Amount));
        Assert.All(Hits(cursed),e=>Assert.Equal("physical",e.SchoolKey));
    }

    [Fact]
    public void CurseOfElementsStillAmplifiesTheSelectedFireProcByTenPercent()
    {
        var request=Build(enchant:13898);
        request.DurationSeconds=120m;
        request.ExternalModifiers.TargetDebuffs=["curse-of-elements"];
        var run=new ForeverWarriorSimRunner().Run(request).Batch.RepresentativeRun;
        var procs=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="equipment-fiery-weapon@main-hand").ToArray();
        Assert.NotEmpty(procs);
        Assert.All(procs,e=>Assert.Equal(44m,e.Amount));
    }

    [Fact]
    public void LegacyWarriorWeaponProxyCanStillExecuteItsSelectedSourcedEnchant()
    {
        var request=Build(enchant:13898);
        request.DurationSeconds=120m;
        request.Gear[0].ItemId=12940; // Existing Warrior-compatible Dal'Rend proxy.
        var run=new ForeverWarriorSimRunner().Run(request).Batch.RepresentativeRun;
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="equipment-fiery-weapon@main-hand" && e.Amount==40m);
    }

    [Fact]
    public void ManualSheetModeDoesNotActivateIgnoredEquipmentIdentities()
    {
        var request=ForeverEquipmentCatalog.ResolveWarriorLoadout(Build(23041,13898));
        request.EquipmentMode="manual";
        var run=new ForeverWarriorSimRunner().Run(request).Batch.RepresentativeRun;
        Assert.DoesNotContain(run.Timeline,e=>e.AbilityKey?.StartsWith("equipment-")==true);
        Assert.DoesNotContain(run.CustomMetrics.Keys,key=>key.StartsWith("equipment-"));
    }
    [Fact]
    public void SelectedFieryWeaponProducesFortyDamageProcsAndNoUnequippedPhantomEffect()
    {
        var request=Build(enchant:13898);
        request.DurationSeconds=120m;
        var enchanted=new ForeverWarriorSimRunner().Run(request);
        var run=enchanted.Batch.RepresentativeRun;
        var procs=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="equipment-fiery-weapon@main-hand").ToArray();
        Assert.NotEmpty(procs);
        Assert.All(procs,e=>{
            Assert.Equal("warrior",e.SourceActorKey);
            Assert.Equal("target",e.TargetActorKey);
            Assert.Equal(40m,e.Amount);
        });
        Assert.Equal(40m*procs.Length,run.CustomMetrics["equipment-fiery-weapon@main-hand-damage"]);
        Assert.Equal(enchanted.MeanDps,new ForeverWarriorSimRunner().Run(request).MeanDps);
        var absent=new ForeverWarriorSimRunner().Run(Build());
        Assert.DoesNotContain(absent.Batch.RepresentativeRun.Timeline,e=>
            e.AbilityKey?.StartsWith("equipment-")==true);
    }
}
