using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever 70532 Mage Mana Shield rank 6 (spell 10193): 140 Mana cast,
/// 570 physical damage absorb, 2 Mana per point. 70707 Arcane Shielding
/// -17/-33% Mana drain. Uses existing AbsorbManager; scripted incoming only.
/// https://www.wowhead.com/forever/spell=10193/mana-shield
/// https://foreverdb.net/spell/11252
/// </summary>
public sealed class MageManaShieldAbsorption(
    SimulationActorState mage,AbsorbManager absorbs,int arcaneShieldingRanks)
{
    private const string ShieldKey="mage-mana-shield";
    public decimal TotalAbsorbed {get;private set;}
    public decimal TotalManaSpent {get;private set;}
    public decimal ManaPerAbsorbedDamage => 2m*(arcaneShieldingRanks switch {
        0=>1m,1=>.83m,2=>.67m,
        _=>throw new ArgumentOutOfRangeException(nameof(arcaneShieldingRanks))
    });

    public DamageAbsorptionResult Apply(SimulationContext context,decimal damage,string school)
    {
        var shield=mage.ActiveAbsorbs.FirstOrDefault(a=>
            a.AbsorbKey==ShieldKey && a.IsActiveAt(context.CurrentTimeSeconds));
        DamageAbsorptionResult Resolve()=>absorbs.ApplyDamage(context,mage,damage,
            "target","scripted-caster-incoming-damage","scripted-hit");
        if(shield is null)return Resolve();
        // A physical-only Mana Shield cannot absorb magic, but Ice Barrier can.
        if(!string.Equals(school,"physical",StringComparison.OrdinalIgnoreCase))
        {
            var index=mage.ActiveAbsorbs.IndexOf(shield);
            mage.ActiveAbsorbs.RemoveAt(index);
            try{return Resolve();}
            finally{mage.ActiveAbsorbs.Insert(Math.Min(index,mage.ActiveAbsorbs.Count),shield);}
        }
        var mana=mage.Resources["mana"];
        mana.AdvanceTo(context.CurrentTimeSeconds);
        var original=shield.RemainingAmount;
        var affordable=Math.Min(original,mana.Current/ManaPerAbsorbedDamage);
        shield.RemainingAmount=affordable;
        DamageAbsorptionResult result;
        try{result=Resolve();}
        finally
        {
            // Restore capacity not consumed by this hit's Mana availability limit.
            if(mage.ActiveAbsorbs.Contains(shield))
                shield.RemainingAmount+=original-affordable;
        }
        var stillPresent=mage.ActiveAbsorbs.Contains(shield);
        var remaining=stillPresent?Math.Max(0m,shield.RemainingAmount-(original-affordable)):0m;
        var absorbed=Math.Clamp(affordable-remaining,0m,affordable);
        var spent=absorbed*ManaPerAbsorbedDamage;
        if(spent>0m)
        {
            if(!mana.Spend(spent))throw new InvalidOperationException(
                "Mana Shield exceeded affordable physical absorption.");
            TotalAbsorbed+=absorbed;
            TotalManaSpent+=spent;
            context.EmitEvent(new CombatEvent {
                TimeSeconds=context.CurrentTimeSeconds,
                Type=CombatEventType.ResourceChanged,
                SourceActorKey=mage.Key,TargetActorKey=mage.Key,
                AbilityKey="mana-shield",EffectKey="mana-shield-absorption-cost",
                ResourceKey="mana",Amount=-spent,ResourceTotalAfter=mana.Current,
                Description="Mana Shield paid Mana for absorbed physical damage."
            });
        }
        return result;
    }
}
