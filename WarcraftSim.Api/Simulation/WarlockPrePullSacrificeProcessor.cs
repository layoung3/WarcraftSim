using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Forever Demonic Sacrifice family rules shared by pre-pull and
/// in-combat summoning. This does not create or simulate an additional demon.</summary>
public static class WarlockSacrificeRules
{
    public static readonly string[] SupportedFamilies =
        ["imp", "voidwalker", "succubus", "incubus", "felhunter"];

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key=value.Trim().ToLowerInvariant();
        if (!SupportedFamilies.Contains(key,StringComparer.Ordinal))
            throw new ArgumentException($"Cannot sacrifice unsupported Warlock demon '{value}'.");
        return key;
    }

    public static bool SameFamily(string sacrificed,string summoned) =>
        string.Equals(sacrificed,summoned,StringComparison.OrdinalIgnoreCase) ||
        sacrificed is ("succubus" or "incubus") &&
            summoned is ("succubus" or "incubus");

    public static string AuraKey(string family) => "demonic-sacrifice-"+family;

    public static string? CurrentFamily(SimulationActorState owner,decimal at) =>
        owner.ActiveAuras.FirstOrDefault(a =>
            a.Definition.Key.StartsWith("demonic-sacrifice-",StringComparison.Ordinal) &&
            a.IsActiveAt(at))?.Definition.Key["demonic-sacrifice-".Length..];

    public static void Clear(SimulationContext context,AuraManager manager,
        SimulationActorState owner,string reason)
    {
        foreach(var key in SupportedFamilies)
            manager.RemoveAuras(context,owner,AuraKey(key),owner.Key,reason);
    }

    public static bool CancelOnSummon(SimulationContext context,AuraManager manager,
        SimulationActorState owner,string summonedFamily,bool pactTalented)
    {
        var previous=CurrentFamily(owner,context.CurrentTimeSeconds);
        if(previous is null || pactTalented && !SameFamily(previous,summonedFamily))
            return false;
        Clear(context,manager,owner,"Demonic Sacrifice canceled by demon summoning");
        return true;
    }

    public static void Apply(SimulationContext context,AuraManager manager,
        SimulationActorState owner,string family,string sourceEffect)
    {
        Clear(context,manager,owner,"Replaced Demonic Sacrifice");
        manager.ApplyAura(context,owner,new AuraDefinition {
            Key=AuraKey(family),Name="Demonic Sacrifice: "+family,
            DurationSeconds=7200m
        },owner.Key,"demonic-sacrifice",sourceEffect);
    }
}

/// <summary>Models the already-completed pre-combat Sacrifice. The selected
/// active companion remains a separate PetType/EnablePet request choice.
/// Repeating ticks are only generated while the initial buff remains active.</summary>
public sealed class WarlockPrePullSacrificeProcessor : ICombatEventProcessor
{
    private const string TickKey="warlock-prepull-sacrifice-regeneration";
    private readonly WarlockSimRequest _request;
    private readonly SimulationActorState _owner;
    private readonly AuraManager _auras;

    public string? SacrificedFamily { get; }
    public string? InitialBuffFamily { get; }
    public bool CanceledBeforePull { get; }
    public bool PactTalented => _request.Talents.Rank("demonic-pact")>0;

    public WarlockPrePullSacrificeProcessor(WarlockSimRequest request,
        SimulationActorState owner,AuraManager auras)
    {
        _request=request;_owner=owner;_auras=auras;
        SacrificedFamily=WarlockSacrificeRules.Normalize(request.PrePullSacrificedDemon);
        if(SacrificedFamily is null)return;
        if(request.Talents.Rank("demonic-sacrifice")==0)
            throw new ArgumentException("Pre-pull Demonic Sacrifice requires the Demonic Sacrifice talent.");
        var initialSummon=request.EnablePet;
        CanceledBeforePull=initialSummon && (!PactTalented ||
            WarlockSacrificeRules.SameFamily(SacrificedFamily,request.PetType));
        InitialBuffFamily=CanceledBeforePull?null:SacrificedFamily;
    }

    public void Initialize(SimulationContext context)
    {
        if(InitialBuffFamily is null)return;
        WarlockSacrificeRules.Apply(context,_auras,_owner,InitialBuffFamily,"pre-pull-sacrifice");
        if(InitialBuffFamily is "voidwalker" or "felhunter")
            Schedule(context,4m);
    }

    private void Schedule(SimulationContext context,decimal time)
    {
        if(time>=_request.DurationSeconds)return;
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds=time,Type=CombatEventType.RotationDecision,
            SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
            EffectKey=TickKey,IsInternal=true
        });
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
            ev.SourceActorKey!=_owner.Key || ev.EffectKey!=TickKey ||
            InitialBuffFamily is null)return;
        var now=ev.TimeSeconds;
        if(WarlockSacrificeRules.CurrentFamily(_owner,now)!=InitialBuffFamily)return;
        if(InitialBuffFamily=="voidwalker")
        {
            var mana=_owner.Resources["mana"];
            var gained=mana.Gain(mana.Maximum*.02m);
            if(gained>0m)context.EmitEvent(new CombatEvent {
                TimeSeconds=now,Type=CombatEventType.ResourceChanged,
                SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
                AbilityKey="demonic-sacrifice",ResourceKey="mana",
                Amount=gained,ResourceTotalAfter=mana.Current
            });
        }
        else if(InitialBuffFamily=="felhunter")
        {
            var healed=_owner.Heal(_owner.MaximumHealth*.03m);
            if(healed>0m)context.EmitEvent(new CombatEvent {
                TimeSeconds=now,Type=CombatEventType.Healing,
                SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
                AbilityKey="demonic-sacrifice",Amount=healed
            });
        }
        Schedule(context,now+4m);
    }
}
