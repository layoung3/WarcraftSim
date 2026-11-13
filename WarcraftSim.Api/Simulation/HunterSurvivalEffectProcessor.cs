using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared existing Survival event mechanics across Hunter starting presets.
/// 70753: marked hits grant Mongoose, landed Mongoose snapshots a replacing
/// seven-tick bleed, and explicit proximity triggers the shared trap package.
/// Trap AP remains editable/provisional; no movement or enemy AI is inferred.
/// </summary>
public sealed class HunterSurvivalEffectProcessor(
    SimulationContext ctx, SimulationActorState hunter, SimulationActorState boss,
    AuraManager aura, AbilityExecutor executor, ForeverSurvivalHunterTalents talents,
    ForeverSurvivalHunterTuning t, bool triggerTraps, bool triggerRangedTraps,
    decimal trapTriggerDelaySeconds, bool bossImmuneToRoots,
    Func<decimal,bool>? meleePlacement = null,
    string rangedAttackPowerStat = "hunter-ranged-attack-power") : ICombatEventProcessor
{
    private int _fireGeneration, _frostGeneration;
    private readonly SurvivalLaceratingBleedLedger _bleeds = new();
    private readonly Dictionary<string,(int Count,decimal Last)> _rootDr = new();
    private readonly Dictionary<string,bool> _trapCastInMelee = new(StringComparer.Ordinal);
    public int TrapTriggers { get; private set; }
    public int EntrapmentRoots { get; private set; }
    public int ExposePreyProcs { get; private set; }
    public int BleedTicks { get; private set; }

    public static bool IsTrap(string? key) => key is
        "explosive-trap" or "immolation-trap" or "frost-trap" or "freezing-trap";
    public static decimal TrapHitBonus(string? key, ForeverSurvivalHunterTalents ranks) =>
        IsTrap(key) ? 5m * ranks.Rank("survival-tactics") : 0m;
    private bool IsMelee(decimal now) => meleePlacement?.Invoke(now) ??
        hunter.ActiveAuras.Any(a => a.Definition.Key == "hunter-melee-stance" && a.IsActiveAt(now));
    private void schedule(decimal at,string effect,int? generation=null,string? target=null,decimal? amount=null)
    {
        if(at>=ctx.Options.DurationSeconds)return;
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=at,Type=CombatEventType.RotationDecision,
            SourceActorKey=hunter.Key,TargetActorKey=target??boss.Key,
            EffectKey=effect,EncounterSequenceHitNumber=generation,Amount=amount,IsInternal=true
        });
    }
    private void status(SimulationActorState target,string key,decimal duration,string origin)
    {
        if(duration<=0m)return;
        aura.ApplyAura(ctx,target,new AuraDefinition {
            Key=key,Name=key,DurationSeconds=duration
        },hunter.Key,origin,"survival-status");
    }
    private bool IsOwnerAttack(CombatEvent ev) =>
        !ev.IsPeriodic && (ev.AbilityKey is "melee-auto" or "offhand-auto" or "auto-shot" or "ranged-auto" ||
            ev.AbilityKey is not null && !IsTrap(ev.AbilityKey) &&
            hunter.Abilities.TryGetValue(ev.AbilityKey,out var ability) &&
            ability.Definition.ClassKey == "hunter" && !ability.Definition.IsPassive);
    public void WriteMetrics(SimulationRunResult run)
    {
        run.CustomMetrics["hunter-expose-prey-procs"]=ExposePreyProcs;
        run.CustomMetrics["hunter-lacerating-bleed-ticks"]=BleedTicks;
        run.CustomMetrics["hunter-trap-triggers"]=TrapTriggers;
        run.CustomMetrics["hunter-entrapment-roots"]=EntrapmentRoots;
    }
        private void ApplyEntrapment(SimulationActorState target, decimal now)
        {
            if (bossImmuneToRoots && target.Key == boss.Key) return;
            _rootDr.TryGetValue(target.Key, out var dr);
            var count = now - dr.Last >= 18m ? 0 : dr.Count;
            // Three-step diminishing returns; fourth application is immune until reset.
            var scale = count switch { 0 => 1m, 1 => .5m, 2 => .25m, _ => 0m };
            _rootDr[target.Key] = (Math.Min(3, count + 1), now);
            if (scale <= 0m) return;
            status(target, "hunter-entrapment", talents.Rank("entrapment") * scale, "entrapment");
            EntrapmentRoots++;
        }
        private void damage(string ability, string key, string targetKey, decimal min, decimal max,
            string school, bool periodic = false, bool crit = true) {
            var target = ctx.GetActor(targetKey);
            if (target is null || !target.IsAlive) return;
            executor.ExecuteBackgroundDirectDamage(ctx, hunter, target, ability, ability,
                new AbilityEffectDefinition { Key = key,
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
                    MitigationType = school == "physical" && !periodic ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
                    ResolutionType = CombatResolutionTypes.Spell, CanMiss = !periodic,
                    CanCrit = crit, MinimumValue = min, MaximumValue = max },
                deliveryType: periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct);
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            var now = context.CurrentTimeSeconds;
            if (ev.Type == CombatEventType.AbilityCastStarted && ev.SourceActorKey == hunter.Key &&
                ev.AbilityKey is ("explosive-trap" or "immolation-trap" or "frost-trap" or "freezing-trap")) {
                // The action may be queued while in melee, but dispatched after
                // departure at the exact same timestamp. That placement still
                // belongs to the melee action window, not the travel window.
                _trapCastInMelee[ev.AbilityKey] = IsMelee(now);
            }
            if (ev.Type == CombatEventType.AuraApplied && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey == hunter.Key &&
                ev.AbilityKey is ("explosive-trap" or "immolation-trap" or "frost-trap" or "freezing-trap")) {
                bool fire = ev.AbilityKey is "explosive-trap" or "immolation-trap";
                int gen = fire ? ++_fireGeneration : ++_frostGeneration;
                // The placement is legitimate if the hunter was in melee when
                // casting, even when a stance transition shares its timestamp.
                bool placedInMelee = _trapCastInMelee.Remove(ev.AbilityKey, out var atPlacement)
                    ? atPlacement : IsMelee(now);
                if (triggerTraps && (triggerRangedTraps || placedInMelee))
                    schedule(now + trapTriggerDelaySeconds, "hunter-trap-activate-" + ev.AbilityKey, gen);
            }
            // Any landed damage on the victim breaks this Hunter's Freezing
            // Trap, including pet/Hawk attacks and periodic ticks. Keep this
            // separate from the owner-only talent-proc eligibility below.
            if (ev.Type == CombatEventType.Damage && ev.Amount > 0m &&
                ev.TargetActorKey is not null && ctx.GetActor(ev.TargetActorKey) is { } recipient)
                aura.RemoveAuras(ctx, recipient, "hunter-frozen", hunter.Key, "broken by damage");
            if (ev.Type == CombatEventType.Damage && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey is not null && ev.Amount > 0m) {
                if (ev.AbilityKey == "strider-kick")
                    status(hunter,"hunter-strider-speed",3m,"strider-kick");
                if (ev.AbilityKey == "wing-clip") {
                    status(ctx.GetActor(ev.TargetActorKey)!,"hunter-wing-clip-slow",10m,"wing-clip");
                    var chance = talents.Rank("improved-wing-clip") switch {
                        1 => 7m, 2 => 14m, 3 => 20m, _ => 0m
                    };
                    if ((decimal)ctx.Random.NextDouble()*100m < chance)
                        status(ctx.GetActor(ev.TargetActorKey)!,"hunter-wing-clip-root",5m,"wing-clip");
                }
                if (ev.AbilityKey == "mongoose-bite" &&
                    talents.Rank("lacerating-strikes") > 0) {
                    // November 2 70753: ALWAYS replace this Hunter's previous
                    // bleed on this target—even a weaker Mongoose Bite.
                    // Other targets' generations are independent.
                    var target=ctx.GetActor(ev.TargetActorKey);
                    if(target is not null)
                    {
                        var fresh=_bleeds.Replace(target.Key,ev.Amount.Value);
                        aura.ApplyAura(ctx,target,new AuraDefinition {
                            Key="hunter-lacerating-strikes",Name="Lacerating Strikes",
                            DurationSeconds=21m,StackingMode=AuraStackingMode.Refresh
                        },hunter.Key,"lacerating-strikes","hunter-lacerating-bleed");
                        for(int tick=1;tick<=7;tick++)
                            schedule(now+tick*3m,"hunter-lacerate-tick",
                                fresh.Generation,target.Key,fresh.TickDamage);
                    }
                }
                // Expose Prey can proc on melee/ranged hits on the marked enemy; periodic
                // effects and trap ticks are not attacks for this talent.
                if (talents.Rank("expose-prey") > 0 && IsOwnerAttack(ev) &&
                    ctx.GetActor(ev.TargetActorKey)?.ActiveAuras.Any(a =>
                        a.Definition.Key == "hunters-mark" && a.IsActiveAt(now)) == true &&
                    (decimal)ctx.Random.NextDouble()*100m < 5m*talents.Rank("expose-prey")) {
                    hunter.GrantReactiveOpportunity("hunter-mongoose-ready", ev.TargetActorKey, now, 10m);
                    ExposePreyProcs++;
                    ctx.EmitEvent(new CombatEvent { TimeSeconds = now,
                        Type = CombatEventType.ReactiveOpportunityGranted,
                        SourceActorKey = hunter.Key, TargetActorKey = ev.TargetActorKey,
                        AbilityKey = "mongoose-bite", Description = "Expose Prey: Mongoose Bite activated." });
                }
            }
            if (ev.Type != CombatEventType.RotationDecision || !ev.IsInternal || ev.SourceActorKey != hunter.Key)
                return;
            if (ev.EffectKey == "hunter-lacerate-tick") {
                var targetKey=ev.TargetActorKey??boss.Key;
                if(!_bleeds.IsCurrent(targetKey,ev.EncounterSequenceHitNumber))
                    return;
                BleedTicks++;
                damage("lacerating-strikes","lacerating-bleed-tick",targetKey,
                    ev.Amount??0m,ev.Amount??0m,"physical",periodic:true);
                return;
            }
            if (ev.EffectKey is "hunter-trap-fire-tick" or "hunter-trap-immolation-tick") {
                // Applied DoTs continue after a new trap has been placed.
                var ability = ev.EffectKey == "hunter-trap-fire-tick" ? "explosive-trap" : "immolation-trap";
                damage(ability, "trap-fire-periodic", ev.TargetActorKey ?? boss.Key,
                    ev.Amount ?? 0m, ev.Amount ?? 0m, "fire", periodic:true);
                return;
            }
            if (ev.EffectKey?.StartsWith("hunter-trap-activate-", StringComparison.Ordinal) != true)
                return;
            var key = ev.EffectKey["hunter-trap-activate-".Length..];
            var fireTrap = key is "immolation-trap" or "explosive-trap";
            if (ev.EncounterSequenceHitNumber != (fireTrap ? _fireGeneration : _frostGeneration)) return;
            TrapTriggers++;
            var victims = key == "explosive-trap" || key == "frost-trap" ?
                ctx.Actors.Values.Where(a => a.TeamKey == "enemy" && a.IsAlive).ToArray() : new[] { boss };
            // Entrapment affects every victim of either Fire or Frost trap.
            if (talents.Rank("entrapment") > 0) {
                foreach (var target in victims) {
                    ApplyEntrapment(target, now);
                }
            }
            if (key == "explosive-trap") {
                foreach (var target in victims) {
                    var buff = 1m + .15m*talents.Rank("clever-traps");
                    var ap = hunter.Stats.Get(rangedAttackPowerStat)*
                        t.TrapAttackPowerCoefficient;
                    damage(key,"explosive-impact",target.Key,
                        (t.ExplosiveTrapMinDamage+ap)*buff, (t.ExplosiveTrapMaxDamage+ap)*buff,"fire");
                    for (int tick=1;tick<=5;tick++)
                        schedule(now+4m*tick,"hunter-trap-fire-tick",ev.EncounterSequenceHitNumber,
                            target.Key, t.ExplosiveTrapDotTotalDamage*buff/5m);
                }
            } else if (key == "immolation-trap") {
                var buff = 1m + .15m*talents.Rank("clever-traps");
                for (int tick=1;tick<=5;tick++)
                    schedule(now+3m*tick,"hunter-trap-immolation-tick",ev.EncounterSequenceHitNumber,
                        boss.Key, (t.ImmolationTrapTotalDamage+
                             hunter.Stats.Get(rangedAttackPowerStat)*
                             t.TrapAttackPowerCoefficient)*buff/5m);
            } else {
                foreach (var target in victims) {
                    decimal duration = key == "freezing-trap" ? 20m : 30m;
                    duration *= 1m + .15m*talents.Rank("clever-traps");
                    status(target,key == "freezing-trap" ? "hunter-frozen" : "hunter-frost-slow",
                        duration,key);
                }
            }
        }
}
