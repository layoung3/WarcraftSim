using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterOffTreeCombatPassives
{
    private static SimulationRunResult Run(string preset,int predator=0,int savage=0,
        int surefooted=0,decimal crit=100m,decimal hit=100m,int seed=175911,
        bool melee=true,bool pet=true,decimal duration=12m)
    {
        void Ranks(Dictionary<string,int> ranks)
        {
            ranks["predators-edge"]=predator;ranks["savage-strikes"]=savage;
            ranks["surefooted"]=surefooted;ranks["deterrence"]=1;
            ranks["counterattack"]=1;ranks["strider-kick"]=1;
        }
        if(preset=="marksmanship")
        {
            var r=Cp175HunterOffTreeAgilityParity.Marksmanship(0,melee,crit:crit);
            Ranks(r.Talents.Ranks);r.Talents.SurvivalPointsSpent=r.Talents.Ranks.Values.Sum();
            r.Seed=seed;r.DurationSeconds=duration;r.HasPet=pet;
            r.Tuning.HitChancePercent=hit;r.ScriptedParryEverySeconds=2m;
            if(melee){r.CustomPriority=["strider-kick","counterattack","arcane-shot"];r.UseArcaneShot=true;}
            return new MarksmanshipHunterSimRunner().Run(r).Run;
        }
        if(preset=="beast-mastery")
        {
            var r=Cp175HunterOffTreeAgilityParity.BeastMastery(0,melee,crit:crit);
            Ranks(r.Talents.Ranks);r.Seed=seed;r.DurationSeconds=duration;r.PetEnabled=pet;
            r.Tuning.HunterHitPercent=hit;r.ScriptedParryEverySeconds=2m;
            if(melee){r.CustomPriority=["strider-kick","counterattack","arcane-shot"];r.UseArcaneShot=true;}
            return new BeastMasteryHunterSimRunner().Run(r).Run;
        }
        var sv=new SurvivalHunterSimRequest {
            DurationSeconds=duration,Seed=seed,TargetMaximumHealth=10000000m,
            HasPet=pet,PetFamily="wolf",CombatMode=melee?"melee":"ranged",
            TargetIsTracked=false,UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
            UseAimedShot=false,UseMultiShot=false,UseVolley=false,UseWingClip=false,
            ApplyHuntersMark=false,ScriptedAvoidanceEverySeconds=2m,
            UsePetSpecial=false,UsePetBite=false,UsePetClaw=false,
            Talents=new() {Ranks=new(StringComparer.OrdinalIgnoreCase) {
                ["improved-tracking"]=5,["deflection"]=5,["entrapment"]=5,
                ["survivalist"]=5,["improved-wing-clip"]=3,["clever-traps"]=2
            }},CustomPriority=melee?["strider-kick","counterattack","arcane-shot"]:[]
        };
        Ranks(sv.Talents.Ranks);sv.Tuning.CriticalChancePercent=crit;
        sv.Tuning.HitChancePercent=hit;sv.Tuning.TargetArmor=0m;
        sv.Tuning.TargetDodgePercent=sv.Tuning.TargetParryPercent=0m;
        sv.Tuning.MeleeWeaponMinDamage=sv.Tuning.MeleeWeaponMaxDamage=100m;
        sv.PetTuning.PetHitPercent=100m;sv.PetTuning.PetAvoidancePercent=0m;
        sv.PetTuning.HunterMeleeCriticalPercent=crit;
        return new SurvivalHunterSimRunner().Run(sv).Run;
    }
    private static CombatEvent[] Damage(SimulationRunResult run,string? ability=null)=>run.Timeline
        .Where(x=>x.Type==CombatEventType.Damage && x.Amount>0m &&
            (ability is null || x.AbilityKey==ability)).ToArray();

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("beast-mastery")]
    [InlineData("survival")]
    public void PredatorsEdgeScalesOnlyMeleeAbilityCriticalBonusIncludingCounterattack(string preset)
    {
        var ranked=Run(preset,predator:5);var removed=Run(preset);
        foreach(var key in new[]{"strider-kick","counterattack"})
        {
            var expected=Assert.Single(Damage(removed,key).Take(1));
            var actual=Assert.Single(Damage(ranked,key).Take(1));
            Assert.True(expected.IsCritical && actual.IsCritical);
            Assert.Equal(expected.Amount!.Value/2m*2.3m,actual.Amount!.Value,24);
        }
        foreach(var key in new[]{"melee-auto","pet-melee"})
        {
            var before=Damage(removed,key);var after=Damage(ranked,key);
            Assert.NotEmpty(before);Assert.Equal(before.Select(x=>x.Amount),after.Select(x=>x.Amount));
        }
        // Survival's stance gates Arcane Shot, so verify its ranged control
        // in a separate ranged encounter instead of comparing empty arrays.
        var rangedWith=Run(preset,predator:5,melee:false);
        var rangedWithout=Run(preset,melee:false);
        var ranged=Damage(rangedWithout,"auto-shot");
        Assert.NotEmpty(ranged);
        Assert.Equal(ranged.Select(x=>x.Amount),Damage(rangedWith,"auto-shot").Select(x=>x.Amount));
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("beast-mastery")]
    [InlineData("survival")]
    public void SavageStrikesRaisesMeleeAbilityCritToCapWithoutChangingAutosRangedOrPet(string preset)
    {
        // Fixed-seed cohort includes a roll in the 96-100 interval without
        // assuming random draws are identical across different runner engines.
        var observedReversal=false;
        for(var seed=1;seed<=40;seed++)
        {
            var ranked=Run(preset,savage:2,crit:96m,seed:seed);
            var removed=Run(preset,crit:96m,seed:seed);
            var eligible=Damage(ranked).Where(x=>x.SourceActorKey=="hunter" &&
                HunterOffTreeMeleeActions.IsMeleeAbility(x.AbilityKey)).ToArray();
            Assert.NotEmpty(eligible);Assert.All(eligible,x=>Assert.True(x.IsCritical));
            observedReversal|=Damage(removed).Any(x=>x.SourceActorKey=="hunter" &&
                HunterOffTreeMeleeActions.IsMeleeAbility(x.AbilityKey) && !x.IsCritical);
            foreach(var key in new[]{"melee-auto","arcane-shot","pet-melee"})
                Assert.Equal(Damage(removed,key).Select(x=>x.IsCritical),
                    Damage(ranked,key).Select(x=>x.IsCritical));
        }
        Assert.True(observedReversal,"Removing the four-point crit bonus must reverse a real critical outcome.");
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("beast-mastery")]
    public void SurefootedRaisesOwnerHitToCapAndRankRemovalRestoresMisses(string preset)
    {
        var observedMiss=false;
        for(var seed=1;seed<=100;seed++)
        {
            var ranked=Run(preset,surefooted:3,hit:97m,crit:0m,seed:seed,
                melee:false,pet:false,duration:1m);
            var removed=Run(preset,hit:97m,crit:0m,seed:seed,
                melee:false,pet:false,duration:1m);
            Assert.Single(Damage(ranked,"auto-shot"));
            observedMiss|=Damage(removed,"auto-shot").Length==0;
        }
        Assert.True(observedMiss,"Rank removal must restore the owner's 3% miss window.");
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("beast-mastery")]
    public void OffTreeResourcefulnessExecutesRealManaEventsAndRankRemovalRevokesThem(string preset)
    {
        SimulationRunResult RunResourcefulness(int rank)
        {
            if(preset=="marksmanship")
            {
                var r=Cp175HunterOffTreeAgilityParity.Marksmanship(0,crit:100m);
                r.Talents.Ranks["resourcefulness"]=rank;
                r.Talents.SurvivalPointsSpent=r.Talents.Ranks.Values.Sum();
                r.UseArcaneShot=true;r.CustomPriority=["arcane-shot"];
                r.Tuning.SpiritRegenPerSecond=20m;r.Tuning.ManaPerFiveSeconds=0m;
                return new MarksmanshipHunterSimRunner().Run(r).Run;
            }
            var bm=Cp175HunterOffTreeAgilityParity.BeastMastery(0,crit:100m);
            bm.Talents.Ranks["resourcefulness"]=rank;
            bm.UseArcaneShot=true;bm.CustomPriority=["arcane-shot"];
            bm.Tuning.HunterSpiritRegenPerSecond=20m;bm.Tuning.HunterMp5=0m;
            return new BeastMasteryHunterSimRunner().Run(bm).Run;
        }
        var ranked=RunResourcefulness(2);var removed=RunResourcefulness(0);
        var gains=ranked.Timeline.Where(x=>x.Type==CombatEventType.ResourceChanged &&
            x.AbilityKey=="resourcefulness").ToArray();
        Assert.NotEmpty(gains);
        Assert.All(gains,x=> { Assert.Equal("hunter",x.SourceActorKey);Assert.Equal(10m,x.Amount); });
        Assert.Equal(gains.Sum(x=>x.Amount!.Value),ranked.CustomMetrics["hunter-resourcefulness-mana"]);
        Assert.True(ranked.CustomMetrics["hunter-resourcefulness-procs"]>0m);
        Assert.Equal(0m,removed.CustomMetrics["hunter-resourcefulness-procs"]);
        Assert.DoesNotContain(removed.Timeline,x=>x.AbilityKey=="resourcefulness");
    }

    [Fact]
    public void OffTreeCoreEffectsAreNoLongerMisreportedAsUnavailable()
    {
        var r=Cp175HunterOffTreeAgilityParity.Marksmanship(0);
        r.Talents.Ranks["predators-edge"]=5;
        r.Talents.Ranks["expose-prey"]=2;r.Talents.Ranks["lacerating-strikes"]=1;
        r.Talents.SurvivalPointsSpent=r.Talents.Ranks.Values.Sum();
        var mm=new MarksmanshipHunterSimRunner().Run(r);
        Assert.DoesNotContain(mm.ModelingNotes,x=>x.Contains("without runtime effects") &&
            (x.Contains("expose-prey") || x.Contains("lacerating-strikes")));
        var bm=Cp175HunterOffTreeAgilityParity.BeastMastery(0);
        bm.Talents.Ranks=new(r.Talents.Ranks);
        var result=new BeastMasteryHunterSimRunner().Run(bm);
        Assert.DoesNotContain(result.ModelingNotes,x=>x.Contains("without runtime effects") &&
            (x.Contains("expose-prey") || x.Contains("lacerating-strikes")));
        Assert.DoesNotContain(HunterOffTreeMeleeActions.UnsupportedTalentNotes(new() {
            Ranks=new() {["survivalists-discipline"]=2}
        }),x=>x.Contains("survivalists-discipline"));
    }

    [Fact]
    public void CounterattackIsMeleeButRangedAndOffhandAutosDoNotReceiveMeleeAbilityBonuses()
    {
        Assert.True(HunterOffTreeMeleeActions.IsMeleeAbility("counterattack"));
        Assert.False(HunterOffTreeMeleeActions.IsMeleeAbility("offhand-auto"));
        Assert.True(HunterOffTreeMeleeActions.IsMeleeAttack("offhand-auto"));
        Assert.False(HunterOffTreeMeleeActions.IsMeleeAttack("auto-shot"));
        Assert.False(HunterOffTreeMeleeActions.IsMeleeAttack("ranged-auto"));
    }
}
