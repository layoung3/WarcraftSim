using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

public sealed class PaladinCombatRolls(PaladinSimRequest r,bool shield):ICombatRollResolver
{
    internal decimal LastAmountMultiplier { get; private set; } = 1m;
    private readonly Dictionary<string,(decimal Minimum,decimal Maximum)> _healingBase=new(StringComparer.Ordinal);
    internal static bool Active(SimulationActorState actor,string key,decimal at)=>actor.ActiveAuras.Any(a=>a.Definition.Key==key&&a.IsActiveAt(at));
    public CombatRollResult Resolve(SimulationContext c,SimulationActorState source,SimulationActorState target,
        AbilityDefinition a,AbilityEffectDefinition e)=>Resolve(c,source,target,a,e,CombatEffectDeliveryType.Direct);
    public CombatRollResult Resolve(SimulationContext c,SimulationActorState source,SimulationActorState target,
        AbilityDefinition a,AbilityEffectDefinition e,CombatEffectDeliveryType delivery)
    {
        source.Stats.Set("paladin-holy-spell-power",source.Stats.Get("spell-power"));
        if(a.Key=="templars-bulwark"&&e.EffectType==AbilityEffectTypes.Absorb)
            e.MinimumValue=e.MaximumValue=source.MaximumHealth;
        if(source.Key!="paladin"&&target.Key=="paladin")return Incoming(c,target,e);
        var healing=e.EffectType is AbilityEffectTypes.DirectHealing or AbilityEffectTypes.PeriodicHealing;
        var melee=e.ResolutionType==CombatResolutionTypes.Melee||e.WeaponHandKey is not null;
        var ranged=e.ResolutionType==CombatResolutionTypes.Ranged;
        var physicalTable=melee||ranged;
        var hit=(ranged?r.BaseRangedHitPercent:melee?r.BaseMeleeHitPercent:r.BaseSpellHitPercent)+
            source.Stats.Get(physicalTable?ForeverCombatStatKeys.HitChancePercent:ForeverCombatStatKeys.SpellHitChancePercent)+r.Talents.Rank("precision");
        if(!physicalTable&&e.SchoolKey=="holy")hit+=4m*r.Talents.Rank("divine-precision");
        if((e.CanMiss||a.Key=="consecration")&&(decimal)c.Random.NextDouble()*100m>=Math.Clamp(hit,0m,100m))return CombatRollResult.Miss();
        if(melee&&e.CanBeDodged&&!r.UseManualCombatAssumptions&&(decimal)c.Random.NextDouble()*100m<5m)return CombatRollResult.Dodge();
        var crit=(r.UseManualCombatAssumptions?r.CriticalChancePercent:source.Stats.Get(physicalTable?
            ForeverCombatStatKeys.AttackCriticalChancePercent:ForeverCombatStatKeys.SpellCriticalChancePercent))+
            2m*r.Talents.Rank("conviction")+e.BonusCriticalChancePercent;
        if(a.Key is "holy-light" or "flash-of-light" or "holy-shock" or "holy-shock-heal"&&Active(source,"paladin-divine-favor",c.CurrentTimeSeconds))crit=100m;
        if(healing&&a.Key=="holy-shock-heal"&&!Active(target,"paladin-oath-of-light",c.CurrentTimeSeconds))
            crit+=15m*r.Talents.Rank("boundless-light");
        if(healing&&a.Key is "holy-light" or "flash-of-light" or "holy-shock-heal")
        {
            if(!_healingBase.TryGetValue(e.Key,out var original))_healingBase[e.Key]=original=(e.MinimumValue,e.MaximumValue);
            var bonus=Active(target,"paladin-oath-of-light",c.CurrentTimeSeconds)?a.Key switch
            {"holy-light"=>ForeverPaladinSourceData.OathHolyLightAt60,"flash-of-light"=>ForeverPaladinSourceData.OathFlashOfLightAt60,"holy-shock-heal"=>ForeverPaladinSourceData.OathHolyShockAt60,_=>0m}:0m;
            bonus*=1m+.04m*r.Talents.Rank("healing-light");
            e.MinimumValue=original.Minimum+bonus;e.MaximumValue=original.Maximum+bonus;
        }
        var critical=e.CanCrit&&(decimal)c.Random.NextDouble()*100m<Math.Clamp(crit,0m,100m);
        var multiplier=healing?1.5m*(1m+.1m*r.Talents.Rank("holy-power")):physicalTable?2m:1.5m;
        LastAmountMultiplier=critical?multiplier:1m;
        return critical?CombatRollResult.Critical(multiplier):CombatRollResult.Hit();
    }
    private CombatRollResult Incoming(SimulationContext c,SimulationActorState target,AbilityEffectDefinition e)
    {
        if(Active(target,"paladin-divine-shield",c.CurrentTimeSeconds)||Active(target,"paladin-divine-protection",c.CurrentTimeSeconds)||
            e.SchoolKey=="physical"&&Active(target,"paladin-blessing-of-protection",c.CurrentTimeSeconds))return CombatRollResult.Avoided("immune");
        if(e.Key=="blessing-of-sacrifice-transfer")return CombatRollResult.Hit();
        var defense=Math.Max(0m,target.Stats.Get("defense-skill")-300m)*.04m;
        var parry=e.ResolutionType==CombatResolutionTypes.Melee&&e.CanBeParried?
            (r.UseManualCombatAssumptions?0m:5m+target.Stats.Get(ForeverCombatStatKeys.ParryChancePercent))+r.Talents.Rank("deflection")+defense:0m;
        var dodge=e.ResolutionType==CombatResolutionTypes.Melee&&e.CanBeDodged?
            (r.UseManualCombatAssumptions?0m:target.Stats.Get(ForeverCombatStatKeys.DodgeChancePercent))+defense:0m;
        var spellBlock=(e.ResolutionType==CombatResolutionTypes.Spell||e.SchoolKey!="physical")&&Active(target,"paladin-spell-block",c.CurrentTimeSeconds);
        var block=shield&&(e.CanBeBlocked||spellBlock)?
            (r.UseManualCombatAssumptions?r.IncomingBlockChancePercent:5m+target.Stats.Get(ForeverCombatStatKeys.BlockChancePercent))+defense+
            (Active(target,"paladin-holy-shield",c.CurrentTimeSeconds)?30m:0m)+
            (Active(target,"paladin-redoubt",c.CurrentTimeSeconds)?6m*r.Talents.Rank("redoubt"):0m):0m;
        var roll=(decimal)c.Random.NextDouble()*100m;
        if(roll<dodge)return CombatRollResult.Dodge();
        if(roll<dodge+parry)return CombatRollResult.Parry();
        if(roll<dodge+parry+block)return CombatRollResult.Block(Math.Max(0m,target.Stats.Get("block-value"))*(1m+.15m*r.Talents.Rank("shield-specialization")));
        if(e.CanCrit&&(decimal)c.Random.NextDouble()*100m<Math.Max(0m,r.IncomingCriticalChancePercent-defense))
            return CombatRollResult.Critical(2m);
        return CombatRollResult.Hit();
    }
}

internal sealed class PaladinMitigation(PaladinSimRequest r,string weaponKind,PaladinCombatRolls rolls):IDamageMitigationResolver
{
    internal Action<SimulationContext,SimulationActorState,string,decimal>? TransferSacrifice { get; set; }
    public DamageMitigationResult Resolve(SimulationContext c,SimulationActorState source,SimulationActorState target,
        AbilityDefinition a,AbilityEffectDefinition e,decimal raw)
    {
        bool active(SimulationActorState x,string key)=>PaladinCombatRolls.Active(x,key,c.CurrentTimeSeconds);
        var amount=raw;decimal armor=0m,reduction=0m;
        // Redirected damage has already passed the friendly target's mitigation.
        // The bounded provisional ordering keeps caster immunity/absorption, but
        // never applies a second armor/resistance/Righteous Fury reduction.
        if(a.Key=="blessing-of-sacrifice-transfer")return DamageMitigationResult.Unmitigated(raw);
        if(source.Key=="paladin"&&a.Key!="eye-for-an-eye")
        {
            // Current source puts Crusader's target bonus inside Righteousness's
            // proc base. Strike/Command apply theirs after done multipliers.
            var righteousness=a.Key is "seal-proc-righteousness" or "unsealed-righteousness";
            if(righteousness&&active(target,"paladin-judgement-crusader"))
                amount+=161m*e.ScalingCoefficient*rolls.LastAmountMultiplier;
            if(e.SchoolKey is "physical" or "holy")
            {
                if(weaponKind=="two-hand"&&a.Key is not ("exorcism" or "hammer-of-wrath" or "retribution-aura"))amount*=1m+.02m*r.Talents.Rank("two-handed-weapon-specialization");
                var vengeance=source.ActiveAuras.FirstOrDefault(x=>x.Definition.Key=="paladin-vengeance"&&x.IsActiveAt(c.CurrentTimeSeconds));
                amount*=1m+.01m*r.Talents.Rank("vengeance")*(vengeance?.Stacks??0);
            }
            if(e.SchoolKey=="holy"&&active(target,"paladin-consecration-first-four"))amount*=1m+.05m*r.Talents.Rank("consecrated-ground");
            if(a.Key.StartsWith("seal-proc-",StringComparison.Ordinal)||a.Key.StartsWith("judgement-",StringComparison.Ordinal)||a.Key.StartsWith("unsealed-",StringComparison.Ordinal))
                amount*=1m+.03m*r.Talents.Rank("improved-seals");
            if(active(source,"paladin-divine-shield"))amount*=.5m;
            if(a.Key is "main-hand-auto" or "reckoning-extra" or "windfury-totem-extra"&&active(source,"paladin-seal-crusader"))
                amount*=ForeverPaladinSourceData.SealCrusaderAutoDamageMultiplier;
            if(e.SchoolKey=="holy"&&!righteousness&&active(target,"paladin-judgement-crusader"))
            {
                var coefficient=a.Key=="holy-strike"?ForeverPaladinSourceData.DirectHolySpellPower:
                    a.Key is "seal-proc-command" or "unsealed-command"?.29m:e.ScalingCoefficient;
                amount+=161m*coefficient*rolls.LastAmountMultiplier;
            }
        }
        if(target.TeamKey=="raid")
        {
            if(active(target,"paladin-divine-shield")||active(target,"paladin-divine-protection")||
                e.SchoolKey=="physical"&&active(target,"paladin-blessing-of-protection"))amount=0m;
            if(target.Key=="paladin")
            {
                if(active(target,"paladin-righteous-fury"))amount*=1m-.03m*r.Talents.Rank("improved-righteous-fury");
                if(active(target,"paladin-iron-creed"))amount*=1m-.02m*r.Talents.Rank("iron-creed");
            }
        }
        if(target.TeamKey=="raid"&&e.SchoolKey is not (null or "physical" or "holy"))
        {
            var resistance=Math.Max(0m,target.Stats.Get(e.SchoolKey+"-resistance"));
            var resisted=resistance/(resistance+ForeverPaladinSourceData.ResistanceLevelFactor*Math.Max(1,source.Level));
            amount*=1m-resisted;
        }
        if(e.MitigationType==DamageMitigationTypes.Armor)
        {
            armor=Math.Max(0m,target.Stats.Get("armor"));
            reduction=ForeverArmorMitigationMath.ClampArmorReductionFraction(armor/(armor+400m+85m*Math.Max(1,source.Level)));
            amount*=1m-reduction;
        }
        if(target.Key=="friendly"&&amount>0m&&active(target,"paladin-blessing-of-sacrifice")&&
            c.GetActor("paladin") is {IsAlive:true}&&TransferSacrifice is not null)
        {
            var transfer=Math.Min(ForeverPaladinSourceData.BlessingOfSacrificeTransferDamage,amount);
            amount-=transfer;TransferSacrifice(c,source,e.SchoolKey??"physical",transfer);
        }
        return new(){RawAmount=raw,FinalAmount=Math.Max(0m,amount),MitigatedAmount=Math.Max(0m,raw-amount),
            ReductionPercent=reduction*100m,DefenseValue=armor};
    }
}

internal sealed class PaladinTiming(PaladinSimRequest r):IAbilityTimingProvider
{
    public AbilityTimingSnapshot Resolve(SimulationContext c,SimulationActorState source,AbilityDefinition a)
    {
        var cast=a.CastTimeSeconds;
        if(a.Key=="holy-light"&&PaladinCombatRolls.Active(source,"paladin-infusion-of-light",c.CurrentTimeSeconds))
            cast=Math.Max(0m,cast-.5m*r.Talents.Rank("infusion-of-light"));
        var meleeHaste=PaladinCombatRolls.Active(source,"paladin-seal-crusader",c.CurrentTimeSeconds)?1.4m:1m;
        var spellHaste=Math.Max(1m,(1m+source.Stats.Get("haste-percent")/100m)/meleeHaste);
        return new(){CastTimeSeconds=cast/spellHaste,GlobalCooldownSeconds=a.GlobalCooldownSeconds,
            ChannelDurationSeconds=a.ChannelDurationSeconds,ChannelTickIntervalSeconds=a.ChannelTickIntervalSeconds};
    }
}

internal sealed class PaladinThreat(PaladinSimRequest r,string weaponKind):IThreatGenerationResolver
{
    public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext c,CombatEvent e)
    {
        if(e.SourceActorKey!="paladin"||e.Amount is not >0m||e.Type is not (CombatEventType.Damage or CombatEventType.Healing))return [];
        var source=c.GetActor("paladin")!;var multiplier=1m;
        if(PaladinCombatRolls.Active(source,"paladin-righteous-fury",c.CurrentTimeSeconds))
            multiplier*=1.33m*(e.SchoolKey=="holy"?1.9m:1m);
        if(weaponKind=="two-hand")multiplier*=1m-(r.Talents.Rank("two-handed-weapon-specialization") switch {1=>.07m,2=>.14m,3=>.20m,_=>0m});
        if(e.AbilityKey=="holy-strike")multiplier*=1m+.1m*r.Talents.Rank("iron-creed");
        if(e.AbilityKey=="holy-shield-retaliation")multiplier*=1.2m;
        if(e.Type==CombatEventType.Damage&&e.TargetActorKey is string target)
            return [new(){ThreatOwnerActorKey=target,ThreatSourceActorKey=source.Key,Amount=e.Amount.Value*multiplier}];
        var enemies=c.Actors.Values.Where(x=>x.TeamKey=="enemy"&&x.IsAlive).ToArray();
        return enemies.Select(x=>new ThreatGenerationContribution {ThreatOwnerActorKey=x.Key,ThreatSourceActorKey=source.Key,
            Amount=e.Amount.Value*(e.AbilityKey is "holy-light" or "flash-of-light" or "holy-shock-heal"?ForeverPaladinSourceData.DirectHealingThreatFraction:ForeverPaladinSourceData.VigilHealingThreatFraction)*multiplier/Math.Max(1,enemies.Length)}).ToArray();
    }
}
