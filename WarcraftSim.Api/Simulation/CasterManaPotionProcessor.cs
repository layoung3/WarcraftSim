using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Shared single-target caster potion policy for the new Shaman/Druid runners.
/// Uses a real AbilityDefinition with the same two-minute potion group as Mage,
/// not an out-of-band resource adjustment. Inventory is per simulation.</summary>
public sealed class CasterManaPotionProcessor : ICombatEventProcessor
{
    private readonly string _actor;
    private readonly AbilityExecutor _executor;
    private readonly decimal _below;
    private int _charges;
    public int Uses {get;private set;}
    public CasterManaPotionProcessor(SimulationActorState actor,AbilityExecutor executor,
        int charges,decimal threshold)
    {
        if(charges is < 0 or > 20 || threshold is < 0m or > 100m)
            throw new ArgumentException("Invalid Mana Potion charges or threshold.");
        _actor=actor.Key;_executor=executor;_charges=charges;_below=threshold;
        actor.AddAbility(new AbilityDefinition
        {
            Key="major-mana-potion",Name="Major Mana Potion",ClassKey=null,
            CastTimeSeconds=0m,GlobalCooldownSeconds=0m,IsOffGlobalCooldown=true,
            CooldownSeconds=120m,SharedCooldownGroupKey="combat-potion",SharedCooldownSeconds=120m,
            Effects=[new AbilityEffectDefinition
            {
                Key="major-mana-potion-gain",EffectType=AbilityEffectTypes.ResourceChange,
                TargetType=AbilityTargetTypes.Self,ResourceChangeOperation=ResourceChangeOperationTypes.Gain,
                ResourceKey="mana",ResolutionType=CombatResolutionTypes.AlwaysHits,
                MinimumValue=1350m,MaximumValue=2250m,CanMiss=false,CanCrit=false
            }]
        });
    }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.RotationDecision||ev.SourceActorKey!=_actor||_charges<=0)return;
        var actor=context.GetActor(_actor);
        if(actor is null||!actor.IsAlive||!actor.Resources.TryGetValue("mana",out var mana)||
            mana.Maximum<=0m||mana.Current*100m/mana.Maximum>_below||
            mana.Maximum-mana.Current<1350m)return;
        if(!_executor.TryStartAbility(context,_actor,"target","major-mana-potion").Success)return;
        _charges--;Uses++;
    }
}
