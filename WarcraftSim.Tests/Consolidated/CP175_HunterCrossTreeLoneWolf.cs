using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Items;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterCrossTreeLoneWolf
{
    private static MarksmanshipHunterSimRequest Request(int lone,string action,
        string petState="absent")
    {
        var r=Cp175HunterOffTreeAgilityParity.Marksmanship(0,crit:0m);
        r.Talents.Ranks=new() {
            ["improved-tracking"]=5,["deflection"]=5,["entrapment"]=5,
            ["survivalist"]=5,["clever-traps"]=2,["surefooted"]=3,
            ["predators-edge"]=5,["expose-prey"]=2,["lacerating-strikes"]=1,
            ["hawk-eye"]=3,["improved-concussive-shot"]=2,["efficiency"]=5,
            ["lone-wolf"]=lone
        };
        r.Talents.SurvivalPointsSpent=33;r.Talents.MarksmanshipPointsSpent=10+lone;
        r.DurationSeconds=120m;r.Seed=175925;r.Tuning.MaximumMana=50000m;
        r.Tuning.RangedAttackPower=1000m;r.PetTuning.MeleeAttackPower=700m;
        r.PetTuning.HunterMeleeCriticalPercent=0m;r.TargetIsTracked=false;
        r.HasPet=petState!="absent";r.PetFamily="cat";
        r.PetEngagementDelaySeconds=petState=="delayed"?30m:0m;
        r.PetIncomingHitDamage=petState=="dead"?100000m:0m;
        r.PetIncomingHitEverySeconds=30m;
        r.ApplyHuntersMark=true;
        r.UseMeleeAuto=action=="survival";
        r.UseFireTraps=false;r.UseFrostTraps=false;
        r.TriggerRangedTraps=true;r.AdditionalTargets=2;
        r.CustomPriority=action switch {
            "survival"=>["explosive-trap","mongoose-bite","wing-clip"],
            "auto-shot"=>[],_=>[action]
        };
        return r;
    }
    private static CombatEvent[] Damage(SimulationRunResult r,string? ability=null,string actor="hunter")=>
        r.Timeline.Where(x=>x.Type==CombatEventType.Damage && x.SourceActorKey==actor &&
            x.Amount>0m && (ability is null || x.AbilityKey==ability)).ToArray();
    private static void EqualEvents(CombatEvent[] before,CombatEvent[] after,decimal multiplier)
    {
        Assert.NotEmpty(before);Assert.Equal(before.Length,after.Length);
        for(var i=0;i<before.Length;i++) {
            Assert.Equal((before[i].TimeSeconds,before[i].AbilityKey,before[i].TargetActorKey),
                (after[i].TimeSeconds,after[i].AbilityKey,after[i].TargetActorKey));
            Assert.Equal(before[i].Amount!.Value*multiplier,after[i].Amount!.Value,23);
        }
    }

    [Theory]
    [InlineData("dense-dynamite","absent")][InlineData("dense-dynamite","active")]
    [InlineData("dense-dynamite","delayed")][InlineData("dense-dynamite","dead")]
    [InlineData("goblin-sapper-charge","absent")][InlineData("goblin-sapper-charge","active")]
    [InlineData("goblin-sapper-charge","delayed")][InlineData("goblin-sapper-charge","dead")]
    public void LoneWolfDoesNotNewlyScaleEngineeringConsumables(string item,string petState)
    {
        MarksmanshipHunterSimResponse Run(int rank) {
            var request=Request(rank,"arcane-shot",petState);
            request.DurationSeconds=60m;
            request.CombatItems=new CombatUseItemsOptions {
                EngineeringSkill=300,DenseDynamiteCharges=item==ForeverCombatItemCatalog.DenseDynamite?1:0,
                GoblinSapperCharges=item==ForeverCombatItemCatalog.GoblinSapperCharge?1:0,
                FirstExplosiveUseSeconds=petState=="dead"?40m:10m
            };
            return new MarksmanshipHunterSimRunner().Run(request);
        }
        var before=Run(0);var after=Run(1);
        Assert.Equal(1,before.CombatItemUses.GetValueOrDefault(item));
        Assert.Equal(1,after.CombatItemUses.GetValueOrDefault(item));
        EqualEvents(Damage(before.Run,item),Damage(after.Run,item),1m);
        Assert.All(Damage(after.Run,item),e=>Assert.InRange(e.Amount!.Value,
            item==ForeverCombatItemCatalog.DenseDynamite?340m:450m,
            item==ForeverCombatItemCatalog.DenseDynamite?460m:750m));
    }

    [Theory]
    [InlineData("absent")][InlineData("active")][InlineData("delayed")][InlineData("dead")]
    public void LoneWolfDoesNotNewlyScaleSyntheticWeaponEnchantDamage(string petState)
    {
        SimulationRunResult Run(int rank) {
            var request=Request(rank,"survival",petState);
            request.Equipment=new CharacterEquipmentLoadout {
                Items=[new CharacterEquipmentItem {
                    SlotKey="main-hand",ItemId=279259,EnchantSpellId=13898
                }]
            };
            return new MarksmanshipHunterSimRunner().Run(request).Run;
        }
        var before=Run(0);var after=Run(1);
        const string key="equipment-fiery-weapon@main-hand";
        EqualEvents(Damage(before,key),Damage(after,key),1m);
        Assert.All(Damage(after,key),e=>Assert.Equal(40m,e.Amount));
        Assert.Equal(before.CustomMetrics[key+"-activations"],after.CustomMetrics[key+"-activations"]);
        Assert.Equal(before.CustomMetrics[key+"-damage"],after.CustomMetrics[key+"-damage"]);
        if(petState is "dead" or "delayed") {
            Assert.Contains(Damage(after,key),e=>e.TimeSeconds<30m);
            Assert.Contains(Damage(after,key),e=>e.TimeSeconds>30m);
        }
    }

    [Theory]
    [InlineData("survival")]
    [InlineData("immolation-trap")]
    public void PetlessMixedTreeLoneWolfScalesEveryImportedImpactAndTickExactlyOnce(string action)
    {
        var baseline=new MarksmanshipHunterSimRunner().Run(Request(0,action)).Run;
        var talented=new MarksmanshipHunterSimRunner().Run(Request(1,action)).Run;
        foreach(var key in action=="survival"?
            new[]{"mongoose-bite","lacerating-strikes","wing-clip","explosive-trap","melee-auto"}:
            new[]{"immolation-trap","auto-shot"})
            EqualEvents(Damage(baseline,key),Damage(talented,key),1.20m);
        if(action=="survival") {
            Assert.All(Damage(talented,"mongoose-bite"),x=>Assert.Equal(356.4m,x.Amount));
            Assert.All(Damage(talented,"wing-clip"),x=>Assert.Equal(60m,x.Amount));
            Assert.All(Damage(talented,"lacerating-strikes"),x=>Assert.Equal(356.4m*.40m/7m,x.Amount));
            Assert.All(Damage(talented,"explosive-trap").Where(x=>x.IsPeriodic),x=>Assert.Equal(102.96m,x.Amount));
        } else Assert.All(Damage(talented,"immolation-trap"),x=>Assert.Equal(246.48m,x.Amount));
    }

    [Theory]
    [InlineData("auto-shot")][InlineData("arcane-shot")][InlineData("serpent-sting")]
    [InlineData("aimed-shot")][InlineData("multi-shot")][InlineData("volley")]
    public void NativeMarksmanshipSpellsAndAutosRetainOneLoneWolfBonus(string action)
    {
        var baseline=new MarksmanshipHunterSimRunner().Run(Request(0,action)).Run;
        var talented=new MarksmanshipHunterSimRunner().Run(Request(1,action)).Run;
        EqualEvents(Damage(baseline,action),Damage(talented,action),1.20m);
        EqualEvents(Damage(baseline,"auto-shot"),Damage(talented,"auto-shot"),1.20m);
    }

    [Theory]
    [InlineData("survival")][InlineData("immolation-trap")][InlineData("arcane-shot")]
    public void ActivePetSuppressesOwnerLoneWolfAndPetNeverInheritsIt(string action)
    {
        var baseline=new MarksmanshipHunterSimRunner().Run(Request(0,action,"active")).Run;
        var talented=new MarksmanshipHunterSimRunner().Run(Request(1,action,"active")).Run;
        // The zero-time melee swing resolves before the queued pet-engage
        // event. All later owner impacts see the actually active companion.
        EqualEvents(Damage(baseline).Where(x=>x.TimeSeconds>0m).ToArray(),
            Damage(talented).Where(x=>x.TimeSeconds>0m).ToArray(),1m);
        if(action=="survival")EqualEvents(Damage(baseline).Where(x=>x.TimeSeconds==0m).ToArray(),
            Damage(talented).Where(x=>x.TimeSeconds==0m).ToArray(),1.20m);
        EqualEvents(Damage(baseline,actor:"hunter-pet"),Damage(talented,actor:"hunter-pet"),1m);
    }

    [Theory]
    [InlineData("survival","dead")][InlineData("survival","delayed")]
    [InlineData("immolation-trap","dead")][InlineData("immolation-trap","delayed")]
    [InlineData("arcane-shot","dead")][InlineData("arcane-shot","delayed")]
    public void PetTransitionsChangeOwnerImpactsButDoNotReapplyLoneWolfToBleedSnapshots(string action,string petState)
    {
        var baseline=new MarksmanshipHunterSimRunner().Run(Request(0,action,petState)).Run;
        var talented=new MarksmanshipHunterSimRunner().Run(Request(1,action,petState)).Run;
        var before=Damage(baseline);var after=Damage(talented);
        Assert.Equal(before.Length,after.Length);
        Assert.Contains(after,x=>x.TimeSeconds<30m);Assert.Contains(after,x=>x.TimeSeconds>30m);
        if(petState=="dead")Assert.Contains(talented.Timeline,x=>x.Type==CombatEventType.ActorDied && x.TargetActorKey=="hunter-pet");
        for(var i=0;i<before.Length;i++) {
            var hit=after[i];
            if(hit.AbilityKey=="lacerating-strikes") {
                var mongoose=after.Last(x=>x.AbilityKey=="mongoose-bite" &&
                    x.TargetActorKey==hit.TargetActorKey && x.TimeSeconds<=hit.TimeSeconds-3m);
                Assert.Equal(mongoose.Amount!.Value*.40m/7m,hit.Amount!.Value,23);
                continue;
            }
            // The first 30-second impact boundary is independently scheduled;
            // test both stable sides of the real engage/death transition.
            if(hit.TimeSeconds==30m)continue;
            var inactive=petState=="dead"
                ? hit.TimeSeconds>30m || hit.TimeSeconds==0m && hit.AbilityKey=="melee-auto"
                : hit.TimeSeconds<30m;
            Assert.Equal(before[i].Amount!.Value*(inactive?1.20m:1m),hit.Amount!.Value,23);
        }
        EqualEvents(Damage(baseline,actor:"hunter-pet"),Damage(talented,actor:"hunter-pet"),1m);
    }
}
