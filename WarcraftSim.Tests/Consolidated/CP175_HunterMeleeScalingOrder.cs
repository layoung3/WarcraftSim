using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175HunterMeleeScalingOrder
{
    private static ForeverSurvivalHunterTalents Talents() => new() {
        Ranks = new() {
            ["improved-tracking"]=5,["deflection"]=5,["survivalist"]=5,
            ["savage-strikes"]=2,["surefooted"]=3,["deterrence"]=1,
            ["counterattack"]=1,["strider-kick"]=1
        }
    };
    private static ForeverSurvivalHunterTuning Tuning() => new() {
        MeleeWeaponMinDamage=100m,MeleeWeaponMaxDamage=100m,
        MeleeWeaponSpeedSeconds=2m
    };

    [Theory]
    [InlineData("strider-kick",false,100,120)]
    [InlineData("strider-kick",true,100,120)]
    [InlineData("counterattack",false,160,170)]
    [InlineData("counterattack",true,160,170)]
    public void TalentGrantedStrikesReadLiveMeleeAttackPower(
        string key,bool imported,int baseDamage,int increasedDamage)
    {
        var source=new SimulationActorState{Key="hunter",Name="Hunter",TeamKey="raid",Level=60};
        source.InitializeHealth(1000m);
        AbilityDefinition spell;
        if(imported) {
            HunterOffTreeMeleeActions.Attach(source,Talents(),Tuning());
            spell=source.Abilities[key].Definition;
            Assert.Contains("hunter-melee-stance",spell.RequiredSourceAuraKeys);
        } else spell=ForeverSurvivalHunterSpellbook.Create(Talents(),Tuning()).Single(a=>a.Key==key);
        var effect=Assert.Single(spell.Effects);
        var stat=imported?"hunter-melee-attack-power":"attack-power";
        Assert.Equal(stat,effect.ScalingStatKey);
        Assert.Equal(2m/14m*(key=="counterattack"?.5m:1m),effect.ScalingCoefficient);
        Assert.Equal(CombatResolutionTypes.Melee,effect.ResolutionType);
        if(key=="counterattack") {
            Assert.Equal("hunter-counterattack-ready",spell.RequiredTargetOpportunityKey);
            Assert.False(effect.CanBeDodged);
            Assert.False(effect.CanBeParried);
        }
        var target=new SimulationActorState{Key="target",Name="Target",TeamKey="enemy",Level=60};
        target.InitializeHealth(100000m);
        var context=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=1m,PrimaryActorKey="hunter",CaptureTimeline=true,Seed=17591
        });
        context.AddActor(source);context.AddActor(target);
        var executor=new AbilityExecutor(new AuraManager(),new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="hunter-melee-scaling",Version="1",
                MitigationRules={
                    [DamageMitigationTypes.Armor]=new DamageMitigationRuleDefinition{
                        MitigationType=DamageMitigationTypes.Armor,
                        FormulaType=DamageMitigationFormulaTypes.RationalLevelScaled,
                        DefenseStatKey="armor",BaseConstant=400m,
                        PerAttackerLevelConstant=85m,MaximumReductionPercent=75m
                    }
                }
            }));
        var result=new SimulationEngine([new ReactiveAbilityOpportunityProcessor()]).Run(context,ctx=> {
            source.Stats.Set(stat,0m);
            executor.ExecuteBackgroundDirectDamage(ctx,source,target,key,spell.Name,effect);
            source.Stats.Set(stat,140m);
            executor.ExecuteBackgroundDirectDamage(ctx,source,target,key,spell.Name,effect);
            source.Stats.Set(stat,0m);
            executor.ExecuteBackgroundDirectDamage(ctx,source,target,key,spell.Name,effect);
        });
        var damage=result.Timeline.Where(e=>e.Type==CombatEventType.Damage).Select(e=>e.Amount).ToArray();
        Assert.Equal(3,damage.Length);
        Assert.Equal((decimal)baseDamage,damage[0]);
        // 2/14 has repeating decimal expansion; compare the numerical result at 12 places.
        Assert.Equal((decimal)increasedDamage,damage[1]!.Value,12);
        Assert.Equal((decimal)baseDamage,damage[2]);
    }

    [Theory]
    [InlineData("strider-kick")]
    [InlineData("counterattack")]
    public void RemovedTalentDoesNotImportItsStrike(string key)
    {
        var ranks=Talents();ranks.Ranks[key]=0;
        Assert.DoesNotContain(ForeverSurvivalHunterSpellbook.Create(ranks,Tuning()),a=>a.Key==key);
        var actor=new SimulationActorState{Key="hunter",Name="Hunter"};
        HunterOffTreeMeleeActions.Attach(actor,ranks,Tuning());
        Assert.False(actor.Abilities.ContainsKey(key));
        ranks.Ranks[key]=2;
        Assert.Throws<ArgumentException>(()=>ForeverSurvivalHunterSpellbook.Create(ranks,Tuning()));
    }
}
