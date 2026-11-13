using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

/// <summary>All mutable Paladin state belongs to one run. Generated attacks, seal impacts,
/// Judgement, periodic ticks and external incoming hits have distinct event identities.</summary>
internal sealed class PaladinCombatProcessor : ICombatEventProcessor
{
    private readonly PaladinSimRequest _r;
    private readonly SimulationActorState _actor;
    private readonly AuraManager _auras;
    private readonly AbsorbManager _absorbs;
    private readonly AbilityExecutor _executor;
    private readonly AutoAttackProcessor _autos;
    private readonly AutoAttackDefinition _weapon;
    private readonly string _weaponKind;
    private readonly bool _shield;
    private readonly PaladinCombatRolls _rolls;
    private readonly ForcedTargetManager _forcedTargets=new();
    private readonly Dictionary<string,decimal> _metrics=new(StringComparer.Ordinal);
    private readonly Dictionary<string,decimal> _auraStatBonuses=new(StringComparer.Ordinal);
    private readonly Dictionary<string,decimal> _kingsDerived=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _kingsPrimary=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _controlDamage=new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _illuminationExecutions=[];
    private readonly HashSet<Guid> _castActions=[];
    private Guid? _vigilExecution;
    private int _holyShieldCharges,_redoubtCharges,_reckoningStacks,_storedStacks,_consecrationGeneration;
    private decimal _shieldManaReadyAt,_reckoningBlockReadyAt,_commandReadyAt,_storedUntil;
    private string? _storedSeal;
    private bool _crusaderHaste;
    private decimal _lastUtilityTick;
    public PaladinCombatProcessor(PaladinSimRequest r,SimulationActorState actor,AuraManager auras,AbsorbManager absorbs,
        AbilityExecutor executor,AutoAttackProcessor autos,AutoAttackDefinition weapon,string weaponKind,bool shield,PaladinCombatRolls rolls)
    { _r=r;_actor=actor;_auras=auras;_absorbs=absorbs;_executor=executor;_autos=autos;_weapon=weapon;_weaponKind=weaponKind;_shield=shield;_rolls=rolls; }
    private int Rank(string key)=>_r.Talents.Rank(key);
    private void Count(string key,decimal value=1m)=>_metrics[key]=_metrics.GetValueOrDefault(key)+value;
    private bool Active(SimulationContext c,SimulationActorState a,string key)=>PaladinCombatRolls.Active(a,key,c.CurrentTimeSeconds);
    private string? ActiveSeal(SimulationContext c)=>PaladinSpellbook.Seals.FirstOrDefault(s=>Active(c,_actor,"paladin-seal-"+s));
    private string HealTargetKey=>_r.HealTarget=="friendly"?"friendly":"paladin";
    private bool Controlled(SimulationContext c,SimulationActorState a)=>new[]{"stun","charm","fear","disorient"}.Any(k=>Active(c,a,"paladin-control-"+k));
    public bool CanMelee(SimulationContext c)=>_actor.IsAlive&&_weaponKind!="none"&&_r.UseAutoAttacks&&
        _actor.Stats.Get("paladin-target-distance")<=5m&&!Controlled(c,_actor)&&
        !Active(c,_actor,"paladin-divine-protection")&&!Active(c,_actor,"paladin-blessing-of-protection")&&_r.UtilityScenario?.Mounted!=true;

    public void Initialize(SimulationContext c)
    {
        if(_r.UtilityScenario?.Mounted==true)Apply(c,_actor,"paladin-mounted",_r.DurationSeconds+1m,"scenario-mounted-travel");
        RefreshAuras(c);RefreshPolicy(c);UpdateAutos(c);
        for(var at=2m;at<_r.DurationSeconds;at+=_r.IncomingBossSwingSeconds)
            if(_r.IncomingBossHitDamage>0m||_r.IncomingAttackPower>0m)c.ScheduleEvent(new() {TimeSeconds=at,Type=CombatEventType.ScriptedDamage,
                SourceActorKey="target",TargetActorKey="paladin",EncounterEventKey="paladin-incoming"});
        if(_r.FriendlyTargetEnabled&&_r.FriendlyTargetIncomingDamage>0m)
            for(var at=2m;at<_r.DurationSeconds;at+=_r.FriendlyTargetIncomingIntervalSeconds)
                c.ScheduleEvent(new() {TimeSeconds=at,Type=CombatEventType.ScriptedDamage,SourceActorKey="target",TargetActorKey="friendly",EncounterEventKey="paladin-friendly-incoming"});
        if(_r.UtilityScenario is { } s)
            for(var i=0;i<s.Effects.Count;i++)c.ScheduleEvent(new() {TimeSeconds=s.Effects[i].AtSeconds,Type=CombatEventType.EncounterDamagePatternOccurrence,
                SourceActorKey="target",TargetActorKey=s.Effects[i].Target,EncounterEventKey="paladin-control",EncounterSequenceHitNumber=i,IsInternal=true});
        ScheduleTick(c,.25m);
    }
    private static void ScheduleTick(SimulationContext c,decimal at)
    {
        if(at<c.Options.DurationSeconds)c.ScheduleEvent(new() {TimeSeconds=at,Type=CombatEventType.EncounterDamagePatternOccurrence,
            SourceActorKey="paladin",EncounterEventKey="paladin-scenario-tick",IsInternal=true});
    }
    public void Process(SimulationContext c,CombatEvent e)
    {
        _forcedTargets.Process(c,e);
        if(e.EncounterEventKey=="paladin-consecration-tick"&&e.Type==CombatEventType.EncounterDamagePatternOccurrence&&e.EncounterSequenceHitNumber==_consecrationGeneration)ConsecrationTick(c);
        if(e.EncounterEventKey=="paladin-control"&&e.Type==CombatEventType.EncounterDamagePatternOccurrence&&e.EncounterSequenceHitNumber is int index)
            ApplyControl(c,_r.UtilityScenario!.Effects[index]);
        if(e.EncounterEventKey=="paladin-scenario-tick"&&e.Type==CombatEventType.EncounterDamagePatternOccurrence)
        {UtilityTick(c);ScheduleTick(c,c.CurrentTimeSeconds+.25m);}
        if(e.Type==CombatEventType.ScriptedDamage&&e.EncounterEventKey is "paladin-incoming" or "paladin-friendly-incoming")Incoming(c,e);
        if(e.Type==CombatEventType.AuraApplied)OnAura(c,e);
        if(e.Type==CombatEventType.AbilityCastCompleted&&e.SourceActorKey==_actor.Key&&e.AbilityExecutionId is Guid cast&&_castActions.Add(cast))
            OnCast(c,e);
        if(e.Type==CombatEventType.Damage)
        {
            if(e.TargetActorKey==_actor.Key)OnIncomingDamage(c,e);
            if(e.SourceActorKey==_actor.Key)OnOutgoingDamage(c,e);
        }
        if(e.Type==CombatEventType.Healing&&e.SourceActorKey==_actor.Key)OnHealing(c,e);
        if(e.Type is CombatEventType.Damage or CombatEventType.Healing&&e.SourceActorKey==_actor.Key&&
            e.AbilityKey is "holy-light" or "flash-of-light" or "holy-shock" or "holy-shock-heal")
            _auras.RemoveAuras(c,_actor,"paladin-divine-favor",_actor.Key,"consumed by the next qualifying spell");
        RefreshAuras(c);RefreshPolicy(c);UpdateAutos(c);
    }

    private void OnCast(SimulationContext c,CombatEvent e)
    {
        var target=c.GetActor(e.TargetActorKey??"target")??c.GetActor("target")!;
        if(e.AbilityKey=="lights-vigil")_vigilExecution=e.AbilityExecutionId;
        if(e.AbilityKey=="holy-light")_auras.RemoveAuras(c,_actor,"paladin-infusion-of-light",_actor.Key,"consumed by completed Holy Light");
        if(e.AbilityKey=="consecration")
        {
            _consecrationGeneration++;var targets=c.Actors.Values.Where(x=>x.TeamKey=="enemy"&&x.IsAlive).OrderBy(x=>x.Key=="target"?0:1).ThenBy(x=>x.Key,StringComparer.Ordinal).ToArray();
            for(var i=0;i<targets.Length;i++)
            {
                Apply(c,targets[i],"paladin-consecration",8m,"consecration");
                if(i<4)Apply(c,targets[i],"paladin-consecration-first-four",8.6m,"consecration");
            }
            for(var i=1;i<=8;i++)if(c.CurrentTimeSeconds+i<_r.DurationSeconds)c.ScheduleEvent(new(){TimeSeconds=c.CurrentTimeSeconds+i,Type=CombatEventType.EncounterDamagePatternOccurrence,
                SourceActorKey=_actor.Key,EncounterEventKey="paladin-consecration-tick",EncounterSequenceHitNumber=_consecrationGeneration,IsInternal=true});
        }
        if(e.AbilityKey is "judgement" or "swift-judgement")Judge(c,target,e.AbilityKey);
        if(e.AbilityKey is "templars-bulwark" or "divine-shield" or "divine-protection")Forbearance(c,_actor);
        if(e.AbilityKey=="blessing-of-protection")Forbearance(c,target);
        if(e.AbilityKey is "cleanse" or "purify")
        {
            foreach(var kind in e.AbilityKey=="cleanse"?new[]{"poison","disease","magic"}:new[]{"poison","disease"})
                if(Active(c,target,"paladin-control-"+kind))
                { _auras.RemoveAuras(c,target,"paladin-control-"+kind,null,"cleansed");Count("cleansed-effects"); }
        }
        if(e.AbilityKey=="lay-on-hands")
        {
            Heal(c,target,"lay-on-hands",_actor.MaximumHealth,0m,true);
            var mana=_actor.Resources["mana"];var spent=mana.Current;mana.SetCurrent(0m);
            // The class adapter filters this sourced exception from the shared five-second-rule processor.
            c.EmitEvent(new() {TimeSeconds=c.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,SourceActorKey=_actor.Key,
                TargetActorKey=_actor.Key,AbilityKey="lay-on-hands",ResourceKey="mana",Amount=-spent,ResourceTotalAfter=mana.Current,
                Description=$"Lay on Hands drained {spent:0.##} Mana without interrupting regeneration."});
            GainMana(c,target,"lay-on-hands",550m);
        }
    }
    private void OnAura(SimulationContext c,CombatEvent e)
    {
        if(e.SourceActorKey!=_actor.Key)return;
        var target=c.GetActor(e.TargetActorKey??_actor.Key);if(target is null)return;
        if(e.AbilityKey?.StartsWith("seal-of-",StringComparison.Ordinal)==true)
            foreach(var seal in PaladinSpellbook.Seals.Where(s=>"seal-of-"+s!=e.AbilityKey))
                _auras.RemoveAuras(c,_actor,"paladin-seal-"+seal,_actor.Key,"replaced by another seal");
        if(e.AbilityKey?.StartsWith("aura-",StringComparison.Ordinal)==true)
        {
            foreach(var aura in PaladinSpellbook.Auras.Where(s=>"aura-"+s!=e.AbilityKey))
                _auras.RemoveAuras(c,_actor,"paladin-aura-"+aura,_actor.Key,"replaced by another aura");
        }
        if(e.AbilityKey?.StartsWith("blessing-of-",StringComparison.Ordinal)==true)
            foreach(var aura in target.ActiveAuras.Where(a=>a.SourceActorKey==_actor.Key&&a.Definition.Key.StartsWith("paladin-blessing-of-",StringComparison.Ordinal)&&
                a.Definition.Key!="paladin-"+e.AbilityKey).ToArray())_auras.RemoveAura(c,target,aura,"replaced by another blessing from the same Paladin");
        if(e.AbilityKey=="blessing-of-freedom")
        {
            _auras.RemoveAuras(c,target,"paladin-control-root",null,"Blessing of Freedom");
            _auras.RemoveAuras(c,target,"paladin-control-slow",null,"Blessing of Freedom");
        }
        if(e.AbilityKey=="holy-shield")_holyShieldCharges=4;
        if(e.AbilityKey=="oath-of-light")
            foreach(var ally in c.Actors.Values.Where(x=>x.TeamKey==_actor.TeamKey&&x.Key!=target.Key))
                _auras.RemoveAuras(c,ally,"paladin-oath-of-light",_actor.Key,"Oath moved to another ally");
        if(e.AbilityKey=="lights-vigil")
            foreach(var ally in c.Actors.Values.Where(x=>x.TeamKey==_actor.TeamKey&&x.Key!=target.Key))
                _auras.RemoveAuras(c,ally,"paladin-lights-vigil",_actor.Key,"one Vigil per bounded party");
    }
    private void Forbearance(SimulationContext c,SimulationActorState target)=>Apply(c,target,"paladin-forbearance",60m-5m*Rank("sacred-duty"),"forbearance");
    private void Apply(SimulationContext c,SimulationActorState target,string key,decimal duration,string ability,int maxStacks=1)
    { _auras.ApplyAura(c,target,new() {Key=key,Name=key,DurationSeconds=duration,StackingMode=maxStacks>1?AuraStackingMode.Stack:AuraStackingMode.Refresh,MaxStacks=maxStacks},_actor.Key,ability,key); }

    internal static bool IsEligibleWeaponEvent(CombatEvent e)=>e.Type==CombatEventType.Damage&&e.SourceActorKey=="paladin"&&
        !e.IsPeriodic&&!e.IsChannelTick&&e.WeaponHandKey=="main-hand"&&e.Amount is >0m&&e.TargetActorKey is not null&&
        e.AbilityKey is "main-hand-auto" or "holy-strike" or "reckoning-extra" or "windfury-totem-extra";
    internal static bool IsWhiteWeaponEvent(CombatEvent e)=>IsEligibleWeaponEvent(e)&&e.AbilityKey!="holy-strike";
    internal static bool IsEligibleEchoEvent(CombatEvent e)=>IsWhiteWeaponEvent(e)&&e.AbilityKey is not ("windfury-totem-extra" or "windfury-weapon-extra")&&
        e.AbilityKey?.StartsWith("seal-proc-",StringComparison.Ordinal)!=true&&e.AbilityKey?.StartsWith("unsealed-",StringComparison.Ordinal)!=true;
    private void OnOutgoingDamage(SimulationContext c,CombatEvent e)
    {
        var target=c.GetActor(e.TargetActorKey??"");if(target is null)return;
        if(e.Amount is >0m)
        {
            if(Active(c,target,"paladin-incapacitated"))_auras.RemoveAuras(c,target,"paladin-incapacitated",null,"broken by damage");
            if(Active(c,target,"paladin-turned"))_auras.RemoveAuras(c,target,"paladin-turned",null,"bounded scenario: damage breaks Turn Undead");
            if(e.IsCritical&&!e.IsPeriodic&&!e.IsChannelTick&&Rank("vengeance")>0)
            {Apply(c,_actor,"paladin-vengeance",30m,"vengeance",5);Count("vengeance-triggers");}
        }
        if(e.AbilityKey=="holy-strike"&&Rank("shieldsworn")>0&&_shield)Apply(c,_actor,"paladin-spell-block",6m,"shieldsworn");
        if(e.AbilityKey=="holy-strike"&&Rank("iron-creed")>0&&Active(c,_actor,"paladin-righteous-fury"))
            Apply(c,_actor,"paladin-iron-creed",6m,"iron-creed");
        if(e.AbilityKey=="holy-strike"&&e.Amount is >0m)
        {
            if(Rank("sacred-arbiter")>0)RefreshJudgements(c,target,allSources:true);
            if(Rank("shieldsworn")>0&&_shield)
            {
                BlockTriggers(c,target,true);
                Count("shieldsworn-free-block-triggers");
            }
        }
        if(e.AbilityKey is "seal-proc-fury" or "unsealed-fury"&&e.Amount is >0m&&_shield)
            _absorbs.ApplyAbsorb(c,_actor,"paladin-seal-of-fury-shield","Seal of Fury",e.Amount.Value*ForeverPaladinSourceData.FuryAbsorbFraction,ForeverPaladinSourceData.FuryAbsorbSeconds,
                AbsorbStackingMode.Replace,1,_actor.Key,"seal-of-fury","seal-of-fury-shield");
        if(e.AbilityKey=="holy-wrath"&&e.Amount is >0m&&!_r.TargetControlImmune)Apply(c,target,"paladin-stunned",2m,"holy-wrath");
        if(e.AbilityKey=="holy-shock"&&e.IsCritical&&Rank("infusion-of-light")>0)
            Apply(c,_actor,"paladin-infusion-of-light",15m,"infusion-of-light");
        if(e.Amount is >0m&&!e.IsPeriodic&&!e.IsChannelTick&&e.AbilityKey is not null&&
            !e.AbilityKey.StartsWith("seal-proc-",StringComparison.Ordinal)&&!e.AbilityKey.StartsWith("unsealed-",StringComparison.Ordinal)&&
            !e.AbilityKey.StartsWith("equipment-",StringComparison.Ordinal)&&e.AbilityKey is not ("holy-shield-retaliation" or "retribution-aura" or "eye-for-an-eye"))
        {
            if(Active(c,target,"paladin-judgement-wisdom")&&Roll(c,50m))GainMana(c,_actor,"judgement-of-wisdom",59m);
            if(IsEligibleWeaponEvent(e)&&Active(c,target,"paladin-judgement-light")&&Roll(c,50m))Heal(c,_actor,"judgement-of-light",61m,0m,false);
        }
        // The next ordinary auto consumes Reckoning even if that auto is avoided.
        // The generated white attacks roll independently and cannot consume again.
        if(e.AbilityKey=="main-hand-auto"&&_reckoningStacks>0)
        {
            var count=_reckoningStacks;_reckoningStacks=0;
            for(var i=0;i<count&&target.IsAlive;i++)
            {
                var extra=PaladinSpellbook.Damage("reckoning-swing",_r.WeaponMinDamage,_r.WeaponMaxDamage,0m,"physical",CombatResolutionTypes.Melee);
                extra.WeaponHandKey="main-hand";extra.ScalingStatKey="attack-power";extra.ScalingCoefficient=_r.WeaponSpeedSeconds/14m;extra.CanBeDodged=true;
                _executor.ExecuteBackgroundDirectDamage(c,_actor,target,"reckoning-extra","Reckoning extra attack",extra);Count("reckoning-extra-attacks");
            }
        }
        if(!IsEligibleWeaponEvent(e))return;
        if(e.AbilityKey!="holy-strike"||Rank("sacred-arbiter")==0)RefreshJudgements(c,target);
        var seal=ActiveSeal(c);
        if(seal is not null&&(seal is "light" or "wisdom" or "justice"||IsWhiteWeaponEvent(e)))TriggerSeal(c,target,seal,false);
        var vindicationWeaponSeconds=_r.WeaponSpeedSeconds/(seal=="crusader"?1m+ForeverPaladinSourceData.SealCrusaderHasteFraction:1m);
        if(Rank("vindication")>0&&Roll(c,Math.Min(100m,ForeverPaladinSourceData.VindicationPpm*vindicationWeaponSeconds/60m*100m)))
        {
            Count("vindication-proc-rolls");
            var debuff=PaladinSpellbook.Damage("vindication-debuff",0m,0m,0m);debuff.CanCrit=false;
            var result=_rolls.Resolve(c,_actor,target,new(){Key="vindication",Name="Vindication"},debuff);
            if(result.Landed){Apply(c,target,"paladin-vindication",30m,"vindication");Count("vindication-procs");}
            else Count("vindication-misses");
        }
        if(Rank("unsealed-wrath")>0&&seal is not null&&IsEligibleEchoEvent(e))
        {
            if(c.CurrentTimeSeconds>=_storedUntil){_storedSeal=null;_storedStacks=0;}
            if(_storedSeal is not null&&_storedSeal!=seal)
            {
                var stored=_storedSeal;var count=_storedStacks;_storedStacks=0;_storedSeal=null;
                for(var i=0;i<count;i++){Count("seal-echo-rolls");TriggerSeal(c,target,stored,true);}
                Count("seal-echo-releases");
            }
            if(ForeverPaladinSourceData.EchoSpellIds.ContainsKey(seal))
            {
                _storedSeal=seal;_storedStacks=Math.Min(ForeverPaladinSourceData.UnsealedMaximumStacks,_storedStacks+1);_storedUntil=c.CurrentTimeSeconds+ForeverPaladinSourceData.UnsealedEchoLifetimeSeconds;
                _metrics["maximum-stored-seals"]=Math.Max(_metrics.GetValueOrDefault("maximum-stored-seals"),_storedStacks);
                Apply(c,_actor,"paladin-unsealed-wrath-storage",ForeverPaladinSourceData.UnsealedEchoLifetimeSeconds,"unsealed-wrath");
            }
            else _auras.RemoveAuras(c,_actor,"paladin-unsealed-wrath-storage",_actor.Key,"Crusader has no stored-hit aura");
            _actor.Stats.Set("paladin-stored-seal-stacks",_storedStacks);
        }
    }

    private void ConsecrationTick(SimulationContext c)
    {
        if(!_actor.IsAlive||_actor.Stats.Get("paladin-target-distance")>8m)return;
        foreach(var target in c.Actors.Values.Where(x=>x.TeamKey=="enemy"&&x.IsAlive))
        {
            var firstFour=Active(c,target,"paladin-consecration-first-four");
            var damage=firstFour?39m:12m;
            var effect=PaladinSpellbook.Damage("consecration-tick",damage,damage,firstFour?ForeverPaladinSourceData.ConsecrationFirstFourSpellPowerPerTick:0m);
            _executor.ExecuteBackgroundDirectDamage(c,_actor,target,"consecration","Consecration",effect,deliveryType:CombatEffectDeliveryType.Periodic);
        }
        Count("consecration-ground-ticks");
    }

    private void TriggerSeal(SimulationContext c,SimulationActorState target,string seal,bool echo)
    {
        var key=(echo?"unsealed-":"seal-proc-")+seal;
        if(seal=="crusader")return; // Crusader's AP/swing aura has no on-hit activation to echo.
        if(seal=="command")
        {
            if(!echo&&c.CurrentTimeSeconds<_commandReadyAt)return;
            if(!Roll(c,Math.Min(100m,ForeverPaladinSourceData.SealCommandPpm*_r.WeaponSpeedSeconds/60m*100m)))return;
            if(!echo)_commandReadyAt=c.CurrentTimeSeconds+1m;
            var effect=PaladinSpellbook.Damage(key,.7m*(_r.WeaponMinDamage+_actor.Stats.Get("attack-power")*_r.WeaponSpeedSeconds/14m),
                .7m*(_r.WeaponMaxDamage+_actor.Stats.Get("attack-power")*_r.WeaponSpeedSeconds/14m),.7m*.29m);
            effect.ResolutionType=CombatResolutionTypes.Melee;effect.WeaponHandKey="main-hand";effect.CanBeDodged=true;
            _executor.ExecuteBackgroundDirectDamage(c,_actor,target,key,key,effect);Count(echo?"seal-echo-activations":"seal-activations");return;
        }
        if(seal=="righteousness")
        {
            var speed=_r.WeaponSpeedSeconds;
            var damage=18.8m*(_weaponKind=="two-hand"?1.2m:.85m-.2m*Math.Max(0m,2m-speed))*speed;
            var effect=PaladinSpellbook.Damage(key,damage,damage,_weaponKind=="two-hand"?.12m:.10m);
            effect.CanMiss=false;effect.CanCrit=true;effect.ResolutionType=CombatResolutionTypes.Melee;effect.WeaponHandKey="main-hand";
            _executor.ExecuteBackgroundDirectDamage(c,_actor,target,key,key,effect);
        }
        else if(seal=="fury")
        {
            var effect=PaladinSpellbook.Damage(key,35m,35m,ForeverPaladinSourceData.FuryProcSpellPower);effect.CanMiss=false;effect.CanCrit=true;effect.ResolutionType=CombatResolutionTypes.Melee;effect.WeaponHandKey="main-hand";
            _executor.ExecuteBackgroundDirectDamage(c,_actor,target,key,key,effect);
        }
        else if(seal is "light" or "wisdom")
        {
            if(!Roll(c,Math.Min(100m,ForeverPaladinSourceData.LightWisdomSealPpm*_r.WeaponSpeedSeconds/60m*100m)))return;
            if(seal=="light")Heal(c,_actor,key,94m,0m,false);else GainMana(c,_actor,key,90m);
        }
        else if(seal=="justice")
        {
            if(_r.TargetControlImmune||!Roll(c,_r.SealOfJusticeProcChancePercent??0m))return;
            Apply(c,target,"paladin-stunned",2m,key);
        }
        Count(echo?"seal-echo-activations":"seal-activations");
    }
    private void Judge(SimulationContext c,SimulationActorState target,string action)
    {
        var seal=ActiveSeal(c);if(seal is null)return;
        var effect=PaladinSpellbook.Damage("judgement-"+seal,0m,0m,0m,resolution:CombatResolutionTypes.Ranged);
        effect.CanBeDodged=false;effect.CanBeParried=false;effect.CanBeBlocked=false;effect.WeaponHandKey=null;
        if(seal=="righteousness"){effect.MinimumValue=ForeverPaladinSourceData.JudgementOfRighteousnessMinimumAt60;effect.MaximumValue=ForeverPaladinSourceData.JudgementOfRighteousnessMaximumAt60;effect.ScalingCoefficient=ForeverPaladinSourceData.DirectHolySpellPower;}
        if(seal=="fury"){effect.MinimumValue=ForeverPaladinSourceData.JudgementOfFuryMinimumAt60;effect.MaximumValue=ForeverPaladinSourceData.JudgementOfFuryMaximumAt60;effect.ScalingCoefficient=ForeverPaladinSourceData.DirectHolySpellPower;}
        if(seal=="command")
        {
            var controlled=Active(c,target,"paladin-stunned")||Active(c,target,"paladin-incapacitated");
            effect.MinimumValue=controlled?339m:169.5m;effect.MaximumValue=controlled?373m:186.5m;effect.ScalingCoefficient=.429m;
        }
        var rolled=_executor.ExecuteBackgroundDirectDamage(c,_actor,target,"judgement-"+seal,"Judgement of "+seal,effect);
        if(rolled.Landed)
        {
            if(seal is "light" or "wisdom" or "justice" or "crusader")
            {
                foreach(var actor in c.Actors.Values.Where(x=>x.TeamKey!="raid"))
                    foreach(var aura in actor.ActiveAuras.Where(a=>a.SourceActorKey==_actor.Key&&a.Definition.Key.StartsWith("paladin-judgement-",StringComparison.Ordinal)).ToArray())
                        _auras.RemoveAura(c,actor,aura,"replaced by the Paladin's next Judgement");
                Apply(c,target,"paladin-judgement-"+seal,seal=="justice"?10m:40m,"judgement-"+seal);
            }
            if(seal=="fury")
            {
                var threat=ThreatManipulator.Apply(c,target,_actor,ThreatManipulationOperationTypes.MatchHighest,0m);
                if(threat.Delta!=0m)c.EmitEvent(new(){TimeSeconds=c.CurrentTimeSeconds,Type=CombatEventType.ThreatChanged,
                    SourceActorKey=_actor.Key,TargetActorKey=target.Key,AbilityKey="judgement-fury",Amount=threat.Delta,
                    Description="Judgement of Fury matched the highest threat."});
                _forcedTargets.ApplyForcedTarget(c,target,_actor,3m,_actor.Key,"judgement-fury","fury-taunt");
                Count("fury-taunts");
            }
        }
        var rank=Rank("sanctified-judgement");
        if(rank>0&&Roll(c,rank==3?100m:33m*rank))
            GainMana(c,_actor,"sanctified-judgement",PaladinSpellbook.SealCost(seal,ForeverClientBaseManaCatalog.GetBaseMana("paladin",60))*
                (1m+.02m*Rank("benediction"))*.2m*rank);
        Count(action+"-casts");
    }
    private void RefreshJudgements(SimulationContext c,SimulationActorState target,bool allSources=false)
    {
        foreach(var aura in target.ActiveAuras.Where(a=>(allSources||a.SourceActorKey==_actor.Key)&&a.Definition.Key.StartsWith("paladin-judgement-",StringComparison.Ordinal)&&
            a.IsActiveAt(c.CurrentTimeSeconds)).ToArray())
            _auras.ApplyAura(c,target,aura.Definition,aura.SourceActorKey,aura.AbilityKey,aura.Definition.Key);
    }

    private void OnHealing(SimulationContext c,CombatEvent e)
    {
        if(e.AbilityKey=="holy-shock-heal"&&e.TargetActorKey is string key&&c.GetActor(key) is { } target&&Active(c,target,"paladin-lights-vigil"))
        {
            _auras.RemoveAuras(c,target,"paladin-lights-vigil",_actor.Key,"consumed by Holy Shock");
            foreach(var shock in new[]{"holy-shock","holy-shock-heal"})
                if(_actor.Abilities.TryGetValue(shock,out var state))state.ResetCooldown(c.CurrentTimeSeconds);
            _actor.ResetSharedCooldown("paladin-holy-shock");
            foreach(var ally in c.Actors.Values.Where(x=>x.TeamKey==_actor.TeamKey&&x.IsAlive))
                Heal(c,ally,"lights-vigil-heal",684m+ForeverPaladinSourceData.LightsVigilSpellPower*_actor.Stats.Get("healing-power"),40m,true,_vigilExecution);
            _vigilExecution=null;
            Count("lights-vigil-holy-shock-resets");
        }
        if(e.IsCritical)
        {
            if(e.AbilityKey is "flash-of-light" or "holy-shock-heal"&&Rank("infusion-of-light")>0)
                Apply(c,_actor,"paladin-infusion-of-light",15m,"infusion-of-light");
            if(PaladinSpellbook.BaseHealCost(e.AbilityKey??"")>0m&&Rank("illumination")>0&&
                (e.AbilityExecutionId is not Guid id||_illuminationExecutions.Add(id))&&Roll(c,20m*Rank("illumination")))
            {
                GainMana(c,_actor,"illumination",PaladinSpellbook.BaseHealCost(e.AbilityKey!)*.5m);Count("illumination-procs");
            }
        }
    }
    private void Heal(SimulationContext c,SimulationActorState target,string key,decimal minimum,decimal range,bool canCrit,Guid? execution=null)
    {
        if(!target.IsAlive)return;
        var effect=PaladinSpellbook.Heal(key,minimum,minimum+range,0m);effect.CanCrit=canCrit;
        var ability=new AbilityDefinition {Key=key,Name=key,Effects=[effect]};
        var roll=_rolls.Resolve(c,_actor,target,ability,effect);
        var raw=HealingReceivedModifiers.Apply(target,c.CurrentTimeSeconds,(minimum+range*(decimal)c.Random.NextDouble())*roll.AmountMultiplier);
        var actual=target.Heal(raw);
        c.EmitEvent(new() {TimeSeconds=c.CurrentTimeSeconds,Type=CombatEventType.Healing,SourceActorKey=_actor.Key,TargetActorKey=target.Key,
            AbilityKey=key,AbilityExecutionId=execution,EffectKey=key,SchoolKey="holy",ResolutionType=CombatResolutionTypes.Healing,
            RawAmount=raw,Amount=actual,OverhealingAmount=raw-actual,ResultKey=roll.ResultKey,IsCritical=roll.IsCritical});
    }
    private void GainMana(SimulationContext c,SimulationActorState target,string ability,decimal amount)
    {
        if(!target.Resources.TryGetValue("mana",out var mana))return;
        mana.AdvanceTo(c.CurrentTimeSeconds);var actual=mana.Gain(Math.Max(0m,amount));
        Count(ability+"-mana",actual);
        c.EmitEvent(new() {TimeSeconds=c.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,SourceActorKey=_actor.Key,TargetActorKey=target.Key,
            AbilityKey=ability,ResourceKey="mana",Amount=actual,ResourceTotalAfter=mana.Current,Description=$"{ability} restored {actual:0.##} Mana (requested {amount:0.##})."});
    }

    internal void TransferSacrifice(SimulationContext c,SimulationActorState attacker,string school,decimal amount)
    {
        var effect=PaladinSpellbook.Damage("blessing-of-sacrifice-transfer",amount,amount,0m,school,CombatResolutionTypes.AlwaysHits);
        effect.CanMiss=false;effect.CanCrit=false;effect.MitigationType=DamageMitigationTypes.None;
        _executor.ExecuteBackgroundDirectDamage(c,attacker,_actor,"blessing-of-sacrifice-transfer","Blessing of Sacrifice transfer",effect);
        Count("sacrifice-redirected-damage",amount);
    }

    private void Incoming(SimulationContext c,CombatEvent e)
    {
        var source=c.GetActor("target");var target=c.GetActor(e.TargetActorKey??"");
        if(source is null||target is null||!source.IsAlive||!target.IsAlive||Active(c,source,"paladin-stunned")||
            Active(c,source,"paladin-incapacitated")||Active(c,source,"paladin-turned"))return;
        var friendly=e.EncounterEventKey=="paladin-friendly-incoming";
        if(friendly&&source.ForcedTarget is { } forced&&forced.ExpiresAtSeconds>c.CurrentTimeSeconds&&
            c.GetActor(forced.ForcedTargetActorKey) is {IsAlive:true} forcedTarget)
        {target=forcedTarget;Count("fury-taunt-redirected-hits");}
        if(!friendly&&_r.IncomingAttackType=="melee"&&_actor.Stats.Get("paladin-target-distance")>5m)return;
        var baseAmount=friendly?_r.FriendlyTargetIncomingDamage:_r.IncomingBossHitDamage;
        if(!friendly&&_r.IncomingAttackType=="melee")baseAmount+=Math.Max(0m,_r.IncomingAttackPower-
            (Active(c,source,"paladin-vindication")?ForeverPaladinSourceData.VindicationAttackPowerPerRankAt60*Rank("vindication"):0m))*_r.IncomingBossSwingSeconds/14m;
        var type=friendly?CombatResolutionTypes.Spell:_r.IncomingAttackType switch {"melee"=>CombatResolutionTypes.Melee,"ranged"=>CombatResolutionTypes.Ranged,_=>CombatResolutionTypes.Spell};
        var school=friendly?"shadow":_r.IncomingDamageSchool;
        var effect=PaladinSpellbook.Damage("incoming",baseAmount,baseAmount,0m,school,type);
        effect.CanMiss=false;effect.CanCrit=!friendly;effect.CanBeDodged=type==CombatResolutionTypes.Melee;
        effect.CanBeParried=type==CombatResolutionTypes.Melee;effect.CanBeBlocked=school=="physical"&&(type is CombatResolutionTypes.Melee or CombatResolutionTypes.Ranged);
        _executor.ExecuteBackgroundDirectDamage(c,source,target,friendly?"encounter:friendly-incoming":"encounter:paladin-incoming","Bounded incoming hit",effect);
    }
    private void OnIncomingDamage(SimulationContext c,CombatEvent e)
    {
        var attacker=c.GetActor(e.SourceActorKey??"");if(attacker is null||!_actor.IsAlive)return;
        var melee=e.ResolutionType==CombatResolutionTypes.Melee;
        var damaged=(e.Amount??0m)+(e.AbsorbedAmount??0m)>0m;
        if(damaged)Pushback(c);
        if(e.ResultKey==CombatResultTypes.Block)
        {
            Count("incoming-blocks");BlockTriggers(c,attacker,false);
            if(_redoubtCharges>0&&Active(c,_actor,"paladin-redoubt")&&--_redoubtCharges==0)
                _auras.RemoveAuras(c,_actor,"paladin-redoubt",_actor.Key,"five blocks consumed");
            if(Rank("reckoning")>0&&c.CurrentTimeSeconds>=_reckoningBlockReadyAt&&Roll(c,8m*Rank("reckoning")))
            {_reckoningBlockReadyAt=c.CurrentTimeSeconds+1.5m;_reckoningStacks=Math.Min(4,_reckoningStacks+1);Count("reckoning-block-grants");}
        }
        if(e.IsCritical&&!e.IsPeriodic&&!e.IsChannelTick)
        {
            if(Rank("reckoning")>0&&Roll(c,20m*Rank("reckoning")))
            {_reckoningStacks=Math.Min(4,_reckoningStacks+1);Count("reckoning-critical-grants");}
            if(Rank("eye-for-an-eye")>0&&e.Amount is >0m)
            {
                var amount=Math.Min(_actor.MaximumHealth*.5m,e.Amount.Value*.05m*Rank("eye-for-an-eye"));
                var effect=PaladinSpellbook.Damage("eye-for-an-eye",amount,amount,0m);effect.CanMiss=false;effect.CanCrit=false;
                _executor.ExecuteBackgroundDirectDamage(c,_actor,attacker,"eye-for-an-eye","Eye for an Eye",effect);Count("eye-for-an-eye-reflections");
            }
        }
        _metrics["maximum-reckoning-stacks"]=Math.Max(_metrics.GetValueOrDefault("maximum-reckoning-stacks"),_reckoningStacks);
        if(melee&&_shield&&Rank("redoubt")>0&&Roll(c,10m))
        {_redoubtCharges=5;Apply(c,_actor,"paladin-redoubt",10m,"redoubt");Count("redoubt-procs");}
        if(melee&&damaged&&Active(c,_actor,"paladin-aura-retribution"))
        {
            var effect=PaladinSpellbook.Damage("retribution-aura",20m,20m,ForeverPaladinSourceData.RetributionAuraSpellPower);effect.CanMiss=false;effect.CanCrit=false;
            _executor.ExecuteBackgroundDirectDamage(c,_actor,attacker,"retribution-aura","Retribution Aura",effect);
        }
    }
    private void BlockTriggers(SimulationContext c,SimulationActorState attacker,bool free)
    {
        // November 3 official all-on-block wording takes precedence over the narrower older model.
        // A free Shieldsworn block does not consume/check the ordinary block-proc cooldown.
        if(free&&Rank("reckoning")>0&&Roll(c,8m*Rank("reckoning")))
        { _reckoningStacks=Math.Min(ForeverPaladinSourceData.ReckoningMaximumAttacks,_reckoningStacks+1);Count("reckoning-shieldsworn-grants");_metrics["maximum-reckoning-stacks"]=Math.Max(_metrics.GetValueOrDefault("maximum-reckoning-stacks"),_reckoningStacks); }
        if(Active(c,_actor,"paladin-holy-shield")&&_holyShieldCharges>0)
        {
            var effect=PaladinSpellbook.Damage("holy-shield-retaliation",221m,221m,ForeverPaladinSourceData.HolyShieldSpellPower);effect.CanMiss=false;effect.CanCrit=false;
            _executor.ExecuteBackgroundDirectDamage(c,_actor,attacker,"holy-shield-retaliation","Holy Shield",effect);
            Count(free?"holy-shield-free-triggers":"holy-shield-charge-triggers");
            if(!free&&--_holyShieldCharges==0)_auras.RemoveAuras(c,_actor,"paladin-holy-shield",_actor.Key,"four blocks consumed");
        }
        if(Rank("shield-specialization")>0&&(free||c.CurrentTimeSeconds>=_shieldManaReadyAt)&&Roll(c,50m*Rank("shield-specialization")))
        {
            GainMana(c,_actor,"shield-specialization",_actor.Resources["mana"].Maximum*.04m);
            if(!free)_shieldManaReadyAt=c.CurrentTimeSeconds+3m;
            Count(free?"shield-specialization-free-triggers":"shield-specialization-block-triggers");
        }
    }
    private void Pushback(SimulationContext c)
    {
        if(_actor.CurrentCastExecutionId is not Guid id||!_actor.IsCasting(c.CurrentTimeSeconds)||
            c.GetAbilityExecution(id) is not {IsCancelled:false,CastCompletionProcessed:false} cast)return;
        var healing=cast.AbilityKey is "holy-light" or "flash-of-light" or "lights-vigil";
        var chance=(healing?14m*Rank("spiritual-focus"):0m)+(Active(c,_actor,"paladin-aura-concentration")?35m:0m);
        if(Roll(c,Math.Min(100m,chance)))Count("pushback-resisted");
        else Count("pushback-seconds",_executor.TryPushbackCurrentCast(c,_actor.Key,.5m,1m));
    }

    public void RefreshAuras(SimulationContext c)
    {
        foreach(var ally in c.Actors.Values.Where(a=>a.TeamKey==_actor.TeamKey))
        {
            StatBonus(ally,"armor","devotion",Active(c,_actor,"paladin-aura-devotion")?735m:0m);
            var externalMight=ally.Key==_actor.Key&&_r.ExternalModifiers.Buffs.Contains("blessing-of-might",StringComparer.OrdinalIgnoreCase);
            StatBonus(ally,"attack-power","might",Active(c,ally,"paladin-blessing-of-might")&&!externalMight?133m:0m);
            foreach(var school in new[]{"fire","frost","shadow"})
                StatBonus(ally,school+"-resistance","resistance-"+school,Active(c,_actor,"paladin-aura-resistance-"+school)?60m:0m);
        }
        RefreshKings(c);
        var crusader=Active(c,_actor,"paladin-seal-crusader");
        StatBonus(_actor,"attack-power","crusader",crusader?ForeverPaladinSourceData.SealCrusaderAttackPowerAt60:0m);
        if(crusader!=_crusaderHaste)
        {
            var old=_actor.Stats.Get("haste-percent");var next=((1m+old/100m)*(crusader?1.4m:1m/1.4m)-1m)*100m;
            _actor.Stats.Set("haste-percent",next);_autos.RescheduleForHasteChange(c,_actor.Key,old,next);_crusaderHaste=crusader;
        }
        _actor.Stats.Set("paladin-holy-strike-power",_actor.Stats.Get("attack-power")*(_weaponKind=="two-hand"?3.3m:2.4m)/14m+.429m*_actor.Stats.Get("spell-power"));
        if(c.CurrentTimeSeconds>=_storedUntil){_storedStacks=0;_storedSeal=null;_actor.Stats.Set("paladin-stored-seal-stacks",0m);}
    }
    private void RefreshKings(SimulationContext c)
    {
        var active=Active(c,_actor,"paladin-blessing-of-kings");
        var raw=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var key in new[]{"strength","agility","stamina","intellect","spirit"})
        {
            var talentMultiplier=key=="strength"?1m+.02m*Rank("divine-strength"):
                key=="intellect"?1m+.02m*Rank("divine-intellect"):1m;
            // Remove only this aura's contribution before recomputing. The
            // remaining current total includes real equipment/modifier procs.
            raw[key]=active?Math.Max(0m,_actor.Stats.Get(key)-_kingsPrimary.GetValueOrDefault(key))*.1m/talentMultiplier:0m;
        }
        var next=ForeverEquipmentDynamicStatResolver.ResolveDelta("paladin",_actor.Level,raw);
        var delta=next.Keys.Concat(_kingsDerived.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key=>(key,value:next.GetValueOrDefault(key)-_kingsDerived.GetValueOrDefault(key)))
            .Where(x=>x.value!=0m).ToDictionary(x=>x.key,x=>x.value,StringComparer.OrdinalIgnoreCase);
        if(delta.Count>0)
        {
            foreach(var (key,value) in delta)_actor.Stats.Add(key,value);
            PaladinSimRunner.ApplyEquipmentStatDelta(c,_actor,delta,_r.Talents);
            _kingsDerived.Clear();foreach(var (key,value) in next)_kingsDerived[key]=value;
            foreach(var key in raw.Keys)_kingsPrimary[key]=raw[key]*(key=="strength"?1m+.02m*Rank("divine-strength"):
                key=="intellect"?1m+.02m*Rank("divine-intellect"):1m);
        }
    }
    private void StatBonus(SimulationActorState target,string stat,string component,decimal value)
    {
        var key=target.Key+"/"+component;var old=_auraStatBonuses.GetValueOrDefault(key);
        if(old!=value){target.Stats.Add(stat,value-old);_auraStatBonuses[key]=value;}
    }
    private void RefreshPolicy(SimulationContext c)
    {
        var desired=_r.SecondarySeal is not null&&(int)(c.CurrentTimeSeconds/_r.SealSwitchIntervalSeconds)%2==1?_r.SecondarySeal:_r.Seal;
        var current=ActiveSeal(c);
        _actor.Stats.Set("paladin-primary-seal-needed",desired==_r.Seal&&current!=desired?1m:0m);
        _actor.Stats.Set("paladin-secondary-seal-needed",_r.SecondarySeal is not null&&desired==_r.SecondarySeal&&current!=desired?1m:0m);
        if(c.GetActor(HealTargetKey) is { } friendly)
        {
            _actor.Stats.Set("paladin-heal-target-health-percent",100m*friendly.CurrentHealth/friendly.MaximumHealth);
            _actor.Stats.Set("paladin-heal-target-cleansable",new[]{"poison","disease","magic"}.Count(k=>Active(c,friendly,"paladin-control-"+k)));
            _actor.Stats.Set("paladin-heal-target-movement-impaired",new[]{"root","slow"}.Any(k=>Active(c,friendly,"paladin-control-"+k))?1m:0m);
        }
    }
    private void UpdateAutos(SimulationContext c)
    {
        var running=_actor.AutoAttacks.GetValueOrDefault(_weapon.Key)?.IsActive==true;
        var wanted=CanMelee(c);
        if(wanted&&!running)_autos.Start(c,_actor.Key,"target",_weapon,0m);
        else if(!wanted&&running)_autos.Stop(c,_actor.Key,_weapon.Key);
    }
    private void ApplyControl(SimulationContext c,PaladinControlEvent e)
    {
        var target=c.GetActor(e.Target);if(target is null||!target.IsAlive)return;
        if(Active(c,target,"paladin-divine-shield")||Active(c,target,"paladin-divine-protection")||
            e.Kind is "root" or "slow"&&Active(c,target,"paladin-blessing-of-freedom"))
        {Count("control-effects-prevented");return;}
        var duration=e.DurationSeconds;
        if(target.Key==_actor.Key)duration*=e.Kind switch {
            "silence"=>1m-.1m*Rank("divine-precision"),"stun" or "charm"=>1m-.1m*Rank("stoicism"),
            "fear" or "disorient"=>1m-.15m*Rank("unyielding-faith"),_=>1m};
        Apply(c,target,"paladin-control-"+e.Kind,duration,"encounter-control-"+e.Kind);
        _controlDamage[target.Key+"/"+e.Kind]=e.DamagePerSecond;
        if(e.Kind is "silence" or "stun" or "charm" or "fear" or "disorient")
            _executor.TryCancelCurrentCast(c,target.Key);
        Count("control-duration-"+e.Kind,duration);
    }
    private void UtilityTick(SimulationContext c)
    {
        var elapsed=c.CurrentTimeSeconds-_lastUtilityTick;_lastUtilityTick=c.CurrentTimeSeconds;
        if(_r.UtilityScenario is { } s&&_actor.IsAlive)
        {
            var distance=_actor.Stats.Get("paladin-target-distance");var old=distance;
            var target=c.GetActor("target")!;
            if(s.TargetFleeSpeedYardsPerSecond>0m&&!Active(c,target,"paladin-judgement-justice")&&!Active(c,target,"paladin-stunned"))
                distance+=s.TargetFleeSpeedYardsPerSecond*elapsed;
            if(s.MoveTowardTarget&&!Active(c,_actor,"paladin-control-root")&&!Controlled(c,_actor))
            {
                var bonus=Math.Max(s.OtherMovementSpeedBonusPercent,Rank("pursuit-of-justice") switch {1=>8m,2=>15m,_=>0m});
                var speed=s.BaseMovementSpeedYardsPerSecond*(1m+bonus/100m)*(Active(c,_actor,"paladin-control-slow")?.5m:1m);
                var travel=Math.Min(Math.Max(0m,distance-3m),speed*elapsed);distance-=travel;Count("distance-travelled",travel);
            }
            if(old!=distance)_actor.Stats.Set("paladin-target-distance",Math.Max(0m,distance));
            foreach(var actor in c.Actors.Values.Where(a=>a.TeamKey==_actor.TeamKey&&a.IsAlive))
                foreach(var kind in new[]{"poison","disease","magic"})
                    if(Active(c,actor,"paladin-control-"+kind)&&_controlDamage.GetValueOrDefault(actor.Key+"/"+kind) is >0m)
                    {
                        var amount=_controlDamage[actor.Key+"/"+kind]*elapsed;
                        var effect=PaladinSpellbook.Damage("control-periodic",amount,amount,0m,kind=="magic"?"shadow":"nature");
                        effect.CanMiss=false;effect.CanCrit=false;
                        _executor.ExecuteBackgroundDirectDamage(c,target,actor,"encounter:"+kind,"Bounded "+kind,effect,deliveryType:CombatEffectDeliveryType.Periodic);
                    }
        }
        // Wisdom blessing executes its source 40 Mana per 5 sec only while active.
        if(c.CurrentTimeSeconds%5m==0m)
            foreach(var ally in c.Actors.Values.Where(a=>a.TeamKey==_actor.TeamKey&&a.IsAlive&&Active(c,a,"paladin-blessing-of-wisdom")))
                GainMana(c,ally,"blessing-of-wisdom",40m);
    }
    private static bool Roll(SimulationContext c,decimal chance)=>chance>=100m||chance>0m&&(decimal)c.Random.NextDouble()*100m<chance;
    public void WriteMetrics(SimulationRunResult result)
    {
        foreach(var (key,value) in _metrics)result.CustomMetrics["paladin-"+key]=value;
        result.CustomMetrics["paladin-ending-holy-shield-charges"]=_holyShieldCharges;
        result.CustomMetrics["paladin-ending-stored-seal-stacks"]=_storedStacks;
        result.CustomMetrics["paladin-ending-reckoning-stacks"]=_reckoningStacks;
    }
}

internal sealed class PaladinWindfuryGate(TriggeredMeleeExtraAttackProcessor inner):ICombatEventProcessor
{
    public void Process(SimulationContext context,CombatEvent e)
    {
        // Seal damage may trigger weapon item procs, but never recursively generates another Windfury swing.
        if(PaladinCombatProcessor.IsEligibleWeaponEvent(e))inner.Process(context,e);
    }
}

internal sealed class PaladinManaRegenGate(FiveSecondManaRegenerationProcessor inner):ICombatEventProcessor
{
    public void Process(SimulationContext context,CombatEvent e)
    {
        if(e.Type==CombatEventType.ResourceChanged&&e.AbilityKey=="lay-on-hands"&&e.Amount<0m)return;
        inner.Process(context,e);
    }
}
