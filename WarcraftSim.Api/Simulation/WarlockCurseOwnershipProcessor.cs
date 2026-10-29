using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 separates Banes and Curses: each Warlock may own only one
/// Curse on a given enemy (independent of that Warlock's Bane). Recklessness
/// rank 4 applies -505 Armor while active. Do not add imaginary target
/// behavior for Fear/Horror suppression in a boss-only solo encounter.
/// </summary>
public sealed class WarlockCurseOwnershipProcessor(AuraManager auras) : ICombatEventProcessor
{
    private const string Elements="curse-of-the-elements";
    private const string Reckless="curse-of-recklessness";
    private readonly Dictionary<string,decimal> _baseArmor=new(StringComparer.OrdinalIgnoreCase);
    public int Replacements {get;private set;}

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.TargetActorKey is not { } key ||
           context.GetActor(key) is not {TeamKey:"enemy"} target)
            return;
        if(ev.Type==CombatEventType.ActorDied)
        {
            _baseArmor.Remove(key);
            return;
        }
        if(ev.Type==CombatEventType.AuraApplied &&
           ev.SourceActorKey=="warlock" &&
           ev.AbilityKey is (Elements or Reckless))
        {
            if(!_baseArmor.ContainsKey(key))
                _baseArmor[key]=target.Stats.Get(ForeverCombatStatKeys.Armor);
            var previous=ev.AbilityKey==Elements?Reckless:Elements;
            if(target.ActiveAuras.Any(a=>a.Definition.Key==previous &&
                a.SourceActorKey=="warlock" &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
            {
                auras.RemoveAuras(context,target,previous,"warlock",
                    "Replaced by the owner's other Curse");
                Replacements++;
            }
            SyncArmor(context,target);
        }
        else if(ev.Type==CombatEventType.AuraRemoved &&
            ev.AbilityKey is (Elements or Reckless) &&
            _baseArmor.ContainsKey(key))
            SyncArmor(context,target);
    }

    private void SyncArmor(SimulationContext context,SimulationActorState target)
    {
        if(!_baseArmor.TryGetValue(target.Key,out var original))return;
        var active=target.ActiveAuras.Any(a=>
            a.Definition.Key==Reckless && a.SourceActorKey=="warlock" &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        target.Stats.Set(ForeverCombatStatKeys.Armor,
            Math.Max(0m,original-(active?505m:0m)));
    }
}
