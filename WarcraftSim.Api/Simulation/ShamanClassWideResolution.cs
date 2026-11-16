using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

internal sealed class ShamanClassWideRolls(EnhancementShamanSimRequest request,
    ShamanElementalDevastationProcessor devastation):ICombatRollResolver
{
    public CombatRollResult Resolve(SimulationContext context,SimulationActorState source,
        SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect) =>
        Resolve(context,source,target,ability,effect,CombatEffectDeliveryType.Direct);

    public CombatRollResult Resolve(SimulationContext context,SimulationActorState source,
        SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,
        CombatEffectDeliveryType deliveryType)
    {
        var melee=effect.WeaponHandKey is not null;
        var healing=effect.EffectType is AbilityEffectTypes.DirectHealing or AbilityEffectTypes.PeriodicHealing;
        var dualWhite=request.DualWield&&ability.Key is "main-hand-auto" or "off-hand-auto";
        var hit=(melee?request.BaseMeleeHitPercent-(dualWhite?19m:0m):request.BaseSpellHitPercent)+
            source.Stats.Get(melee?ForeverCombatStatKeys.HitChancePercent:ForeverCombatStatKeys.SpellHitChancePercent)+
            request.Talents.Rank("tidal-focus");
        if(effect.CanMiss&&(decimal)context.Random.NextDouble()*100m>=Math.Clamp(hit,0m,100m))return CombatRollResult.Miss();
        if(melee&&effect.CanBeDodged&&(decimal)context.Random.NextDouble()*100m<5m)return CombatRollResult.Dodge();
        if(melee&&effect.CanGlance&&(decimal)context.Random.NextDouble()*100m<request.ProvisionalGlancingChancePercent)
            return CombatRollResult.Glancing(.7m);
        var crit=(request.UseManualCombatAssumptions?request.CriticalChancePercent:
            source.Stats.Get(melee?ForeverCombatStatKeys.AttackCriticalChancePercent:ForeverCombatStatKeys.SpellCriticalChancePercent))+
            request.Talents.Rank("thundering-strikes")+effect.BonusCriticalChancePercent;
        if(melee)crit+=devastation.MeleeCriticalChanceBonus(source,context.CurrentTimeSeconds);
        if(healing)crit+=request.Talents.Rank("tidal-mastery");
        else if(ShamanClassWideCombatProcessor.IsLightning(ability.Key)||ability.Key.StartsWith("lightning-overload-",StringComparison.Ordinal))
            crit+=5m*request.Talents.Rank("call-of-thunder");
        var isCrit=effect.CanCrit&&(decimal)context.Random.NextDouble()*100m<Math.Clamp(crit,0m,100m);
        var multiplier=isCrit?(melee?2m:healing?1.5m:1.5m+.1m*request.Talents.Rank("elemental-fury")):1m;
        if(healing&&ability.Key=="chain-heal"&&target.ActiveAuras.Any(x=>x.Definition.Key=="riptide"&&
            x.SourceActorKey==source.Key&&x.IsActiveAt(context.CurrentTimeSeconds)))multiplier*=1.25m;
        return new CombatRollResult{Landed=true,IsCritical=isCrit,
            ResultKey=isCrit?CombatResultTypes.Critical:CombatResultTypes.Hit,AmountMultiplier=multiplier};
    }
}

/// <summary>Reads armor and owned effects at impact. Target debuff expirations
/// and source gear changes therefore alter the next hit without rebuilding.</summary>
internal sealed class ShamanClassWideMitigation(ForeverEnhancementShamanTalents talents):IDamageMitigationResolver
{
    public DamageMitigationResult Resolve(SimulationContext context,SimulationActorState source,
        SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount)
    {
        var amount=rawAmount;
        if(source.Key=="shaman")
        {
            if(ability.Key is "magma-totem" or "searing-totem" or "fire-nova")
            {
                var radius=ability.Key=="magma-totem"?8m:ability.Key=="searing-totem"?20m:10m+3m*talents.Rank("elemental-reach");
                if(source.Stats.Get("shaman-fire-target-distance")>radius)amount=0m;
            }
            if((ShamanClassWideCombatProcessor.IsLightning(ability.Key)||ability.Key=="earth-shock")&&
                target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-stormstrike-nature"&&
                    x.SourceActorKey==source.Key&&x.IsActiveAt(context.CurrentTimeSeconds)))amount*=1.2m;
            if(ability.Key is "chain-lightning" or "lightning-overload-chain")
            {
                var targets=context.Actors.Values.Where(x=>x.TeamKey!=source.TeamKey)
                    .OrderBy(x=>x.Key=="target"?0:1).ThenBy(x=>x.Key,StringComparer.Ordinal).ToArray();
                var jump=Array.FindIndex(targets,x=>x.Key==target.Key);
                amount*=jump==0?1m:jump==1?.7m:.49m;
            }
        }
        else if(target.Key=="shaman"&&effect.SchoolKey is "fire" or "frost" or "nature")
            amount*=1m-ForeverShamanClassTalents.WardingFraction(talents.Rank("elemental-warding"));
        decimal armor=0m,reduction=0m;
        if(effect.MitigationType==DamageMitigationTypes.Armor)
        {
            armor=Math.Max(0m,target.Stats.Get("armor"));
            if(target.Key=="shaman"&&target.ActiveAuras.Any(x=>x.Definition.Key=="ancestral-healing-armor"&&
                x.IsActiveAt(context.CurrentTimeSeconds)))
                armor*=1m+ForeverShamanClassTalents.AncestralHealingArmorFraction(talents.Rank("ancestral-healing"));
            reduction=ForeverArmorMitigationMath.ClampArmorReductionFraction(armor/(armor+400m+85m*Math.Max(1,source.Level)));
            amount*=1m-reduction;
        }
        return new DamageMitigationResult{RawAmount=rawAmount,FinalAmount=Math.Max(0m,amount),
            MitigatedAmount=Math.Max(0m,rawAmount-amount),ReductionPercent=reduction*100m,DefenseValue=armor};
    }
}

internal sealed class ShamanClassWideIncomingDamage(EnhancementShamanSimRequest request,
    IDamageMitigationResolver mitigation,ShamanClassWideUtilityProcessor utility):ICombatEventProcessor
{
    private readonly AbsorbManager _absorbs=new();
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.ScriptedDamage||ev.TargetActorKey!="shaman")return;
        var target=context.GetActor("shaman");var source=context.GetActor(ev.SourceActorKey??"");
        if(target is null||source is null||!target.IsAlive)return;
        var raw=Math.Max(0m,ev.RawAmount??ev.Amount??0m);
        var kind=request.UtilityScenario?.IncomingAttackKind??(ev.SchoolKey=="physical"?"melee":"spell");
        if(kind=="melee"&&utility.Distance>5m)return;
        if(utility.TryRedirectIncoming(context,ev,raw))return;
        var physical=ev.SchoolKey=="physical";
        if(physical&&kind=="melee")
        {
            var dodge=Math.Clamp(target.Stats.Get(ForeverCombatStatKeys.DodgeChancePercent)+2m*request.Talents.Rank("anticipation"),0m,100m);
            var parry=request.Talents.Rank("spirit-weapons")>0?5m+target.Stats.Get(ForeverCombatStatKeys.ParryChancePercent):0m;
            var roll=(decimal)context.Random.NextDouble()*100m;
            if(roll<dodge+parry)
            {
                context.EmitEvent(new CombatEvent
                {
                    Type=CombatEventType.Damage,TimeSeconds=context.CurrentTimeSeconds,
                    SourceActorKey=source.Key,TargetActorKey=target.Key,AbilityKey="encounter:shaman-classwide-incoming",
                    SchoolKey="physical",RawAmount=raw,Amount=0m,ResultKey=roll<dodge?CombatResultTypes.Dodge:CombatResultTypes.Parry
                });
                return;
            }
        }
        if(physical&&target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-stoneskin-totem"&&x.IsActiveAt(context.CurrentTimeSeconds))&&
            utility.InTotemRange(context,"stoneskin-totem"))raw=Math.Max(0m,raw-30m*(1m+.1m*request.Talents.Rank("guardian-totems")));
        if(physical&&kind=="ranged"&&target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-windwall-totem"&&x.IsActiveAt(context.CurrentTimeSeconds))&&
            utility.InTotemRange(context,"windwall-totem"))raw=Math.Max(0m,raw-64m*(1m+.1m*request.Talents.Rank("guardian-totems")));
        var ability=new AbilityDefinition{Key="encounter:shaman-classwide-incoming",Name="Incoming scenario damage"};
        var effect=new AbilityEffectDefinition{SchoolKey=ev.SchoolKey,
            MitigationType=physical?DamageMitigationTypes.Armor:DamageMitigationTypes.None};
        var reduced=mitigation.Resolve(context,source,target,ability,effect,raw);
        var damage=_absorbs.ApplyDamage(context,target,reduced.FinalAmount,source.Key,ability.Key);
        context.EmitEvent(new CombatEvent
        {
            Type=CombatEventType.Damage,TimeSeconds=context.CurrentTimeSeconds,SourceActorKey=source.Key,TargetActorKey=target.Key,
            AbilityKey=ability.Key,SchoolKey=ev.SchoolKey,RawAmount=raw,Amount=damage.HealthDamage,
            AbsorbedAmount=damage.AbsorbedDamage,MitigatedAmount=reduced.MitigatedAmount,
            MitigationPercent=reduced.ReductionPercent,ResultKey=CombatResultTypes.Hit
        });
        if(!target.IsAlive)context.EmitEvent(new CombatEvent
        {
            Type=CombatEventType.ActorDied,TimeSeconds=context.CurrentTimeSeconds,
            SourceActorKey=source.Key,TargetActorKey=target.Key,AbilityKey=ability.Key
        });
    }
}

internal sealed class ShamanClassWideThreat(EnhancementShamanSimRequest request,
    ShamanWeaponImbueProcessor imbues):IThreatGenerationResolver
{
    public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext context,CombatEvent ev)
    {
        if(ev.SourceActorKey!="shaman"||ev.Amount is not >0m||
            ev.AbilityKey is "lightning-overload-bolt" or "lightning-overload-chain")return [];
        var source=context.GetActor("shaman");
        if(source is null)return [];
        var multiplier=request.Talents.Rank("spirit-weapons")>0?
            (imbues.MainHand=="rockbiter"?1.3m:.7m):1m;
        if(ev.WeaponHandKey is null)multiplier*=1m-.05m*request.Talents.Rank("natural-grace");
        if(ev.AbilityKey=="earth-shock")multiplier*=2m;
        if(ev.Type==CombatEventType.Damage&&ev.TargetActorKey is string target)
            return [new ThreatGenerationContribution{ThreatOwnerActorKey=target,ThreatSourceActorKey=source.Key,Amount=ev.Amount.Value*multiplier}];
        if(ev.Type!=CombatEventType.Healing)return [];
        var enemies=context.Actors.Values.Where(x=>x.TeamKey!=source.TeamKey&&x.IsAlive).ToArray();
        return enemies.Select(x=>new ThreatGenerationContribution{ThreatOwnerActorKey=x.Key,
            ThreatSourceActorKey=source.Key,Amount=ev.Amount.Value*.5m*multiplier/Math.Max(1,enemies.Length)}).ToArray();
    }
}
