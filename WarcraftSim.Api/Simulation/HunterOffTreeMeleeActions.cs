using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Reuses Survival's existing legal talent-granted melee spells in other
/// Hunter presets. Melee stance and real reactive opportunities remain
/// mandatory; this is an adapter, not a second spell or combat engine.
/// </summary>
public static class HunterOffTreeMeleeActions
{
    public static void Attach(SimulationActorState hunter,
        ForeverSurvivalHunterTalents ranks,ForeverSurvivalHunterTuning tuning)
    {
        foreach(var spell in ForeverSurvivalHunterSpellbook.Create(ranks,tuning))
        {
            if(spell.Key is not ("strider-kick" or "counterattack"))continue;
            spell.RequiredSourceAuraKeys.Add("hunter-melee-stance");
            foreach(var effect in spell.Effects)
                if(effect.ScalingStatKey=="attack-power")
                    effect.ScalingStatKey="hunter-melee-attack-power";
            hunter.AddAbility(spell);
        }
        if(ranks.Rank("counterattack")>0)
            hunter.AddReactiveOpportunityDefinition(new ReactiveAbilityOpportunityDefinition{
                OpportunityKey="hunter-counterattack-ready",
                AbilityKey="counterattack",DodgeWindowSeconds=5m
            });
    }
}

/// <summary>
/// Optional explicit deterministic incoming-attack parry fixture for solo
/// simulations without enemy combat AI. An unconfigured encounter grants
/// no Counterattack opportunity; no grant happens outside melee engagement.
/// </summary>
public sealed class HunterScriptedParryProcessor(
    string hunterKey,string targetKey,decimal interval) : ICombatEventProcessor
{
    public const string Key="hunter-scripted-parry";
    public int ParryWindows {get;private set;}

    public void Start(SimulationContext context)
    {
        if(interval>0m && interval<context.Options.DurationSeconds)
            Schedule(context,interval);
    }

    private void Schedule(SimulationContext context,decimal when)
    {
        if(when>=context.Options.DurationSeconds)return;
        context.ScheduleEvent(new CombatEvent{
            TimeSeconds=when,Type=CombatEventType.RotationDecision,
            SourceActorKey=hunterKey,TargetActorKey=targetKey,
            EffectKey=Key,IsInternal=true
        });
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
           ev.EffectKey!=Key || ev.SourceActorKey!=hunterKey)return;
        var hunter=context.GetActor(hunterKey);
        var target=context.GetActor(targetKey);
        if(hunter is {IsAlive:true} && target is {IsAlive:true} &&
           hunter.Abilities.ContainsKey("counterattack") &&
           hunter.ActiveAuras.Any(a=>a.Definition.Key=="hunter-melee-stance" &&
               a.IsActiveAt(context.CurrentTimeSeconds)))
        {
            hunter.GrantReactiveOpportunity("hunter-counterattack-ready",
                target.Key,context.CurrentTimeSeconds,5m);
            ParryWindows++;
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,
                Type=CombatEventType.ReactiveOpportunityGranted,
                SourceActorKey=hunterKey,TargetActorKey=targetKey,
                AbilityKey="counterattack",
                Description="Explicit scripted incoming attack was parried."
            });
        }
        Schedule(context,context.CurrentTimeSeconds+interval);
    }
}


/// <summary>
/// Preserve Survival Strider Kick's on-hit three-second movement marker when
/// the learned melee action runs under another Hunter starting preset.
/// Movement geometry itself remains outside the Solo DPS encounter model.
/// </summary>
public sealed class HunterOffTreeMeleeEffectProcessor(AuraManager auras) : ICombatEventProcessor
{
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.Damage ||
           ev.SourceActorKey!="hunter" || ev.AbilityKey!="strider-kick" ||
           ev.Amount is not >0m)return;
        var hunter=context.GetActor("hunter");
        if(hunter is null)return;
        auras.ApplyAura(context,hunter,new AuraDefinition{
            Key="hunter-strider-speed",Name="Strider Kick movement",
            DurationSeconds=3m
        },hunter.Key,"strider-kick","hunter-strider-speed");
    }
}
