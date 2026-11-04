using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared time-driven add activation for solo DPS. Only the availability
/// window controls whether an add is an active combat target. Damage does
/// not force a fixed-duration benchmark to end early.
/// Existing Warrior/raid add policies remain unchanged when disabled.
/// </summary>
public sealed class SoloDpsAddLifecycleProcessor(
    SoloDpsEncounterScript? options,string ownerKey,string bossKey="target")
    : ICombatEventProcessor
{
    public const string SpawnEvent="solo-dps-add-spawn";
    public const string DespawnEvent="solo-dps-add-despawn";
    public const decimal BenchmarkHealth=1_000_000_000_000_000_000m;
    private readonly Dictionary<string,SoloDpsAddWindow> _adds=
        new(StringComparer.OrdinalIgnoreCase);

    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(options?.Enabled!=true)return;
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            if(options.AddWindows is null || options.AddWindows.Count>8)
                throw new ArgumentException("Solo DPS supports at most eight configured add windows.");
            if(options.AddWindows.Select(x=>x?.TargetKey)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()!=options.AddWindows.Count)
                throw new ArgumentException("Duplicate scripted add identity.");
            foreach(var add in options.AddWindows)
            {
                if(add is null || string.IsNullOrWhiteSpace(add.TargetKey) ||
                   add.TargetKey==bossKey ||
                   add.SpawnTimeSeconds<0m || add.SpawnTimeSeconds>=ctx.Options.DurationSeconds ||
                   add.DespawnTimeSeconds<=add.SpawnTimeSeconds ||
                   add.DespawnTimeSeconds>ctx.Options.DurationSeconds ||
                   add.StartingHealthPercent is <=0m or >100m || add.HealthMilestones is null ||
                   add.HealthMilestones.Count>30)
                    throw new ArgumentException("Invalid scripted add timing, target or health milestones.");
                var target=ctx.GetActor(add.TargetKey) ??
                    throw new ArgumentException($"Scripted add '{add.TargetKey}' is not a configured enemy actor.");
                if(target.TeamKey!="enemy")
                    throw new ArgumentException("Scripted add must target an enemy.");
                _adds.Add(add.TargetKey,add);
                var timeline=new List<ScriptedTargetHealthCheckpoint> {
                    new(0m,add.StartingHealthPercent)
                };
                decimal last=add.SpawnTimeSeconds,lastHp=add.StartingHealthPercent;
                foreach(var checkpoint in add.HealthMilestones)
                {
                    var absolute=checkpoint is null ? -1m : checkpoint.TimeSeconds+
                        (add.HealthMilestonesRelativeToSpawn?add.SpawnTimeSeconds:0m);
                    if(checkpoint is null || absolute<=last ||
                       absolute>add.DespawnTimeSeconds ||
                       checkpoint.HealthPercent is <=0m or >100m ||
                       checkpoint.HealthPercent>lastHp)
                        throw new ArgumentException("Add HP milestones must follow spawn and be monotonically nonincreasing.");
                    if(absolute<ctx.Options.DurationSeconds)
                        timeline.Add(new(absolute,checkpoint.HealthPercent));
                    last=absolute;lastHp=checkpoint.HealthPercent;
                    if(absolute<ctx.Options.DurationSeconds)
                        Schedule(ctx,absolute,add.TargetKey,"solo-dps-health-checkpoint");
                }
                ctx.SetScriptedTargetHealth(add.TargetKey,new ScriptedTargetHealthTimeline(timeline));
                // The client runner may have created an initially active add;
                // reset it to the source-backed scripted window at t=0.
                target.InitializeHealth(BenchmarkHealth,add.SpawnTimeSeconds==0m?
                    BenchmarkHealth:0m);
                if(add.SpawnTimeSeconds>0m)
                    Schedule(ctx,add.SpawnTimeSeconds,add.TargetKey,SpawnEvent);
                if(add.DespawnTimeSeconds<ctx.Options.DurationSeconds)
                    Schedule(ctx,add.DespawnTimeSeconds,add.TargetKey,DespawnEvent);
            }
            foreach(var milestone in options.BossHealthMilestones??
                     Enumerable.Empty<SoloDpsHealthMilestone>())
                if(milestone.TimeSeconds>0m &&
                   milestone.TimeSeconds<ctx.Options.DurationSeconds)
                    Schedule(ctx,milestone.TimeSeconds,bossKey,"solo-dps-health-checkpoint");
            return;
        }
        if(ev.Type!=CombatEventType.ScriptedTargetStateChanged ||
           ev.TargetActorKey is null)return;
        if(ev.EffectKey=="solo-dps-health-checkpoint")
        {
            // The scripted timeline already owns the HP value at this time.
            // Publish its effective percentage, not the giant actor HP.
            var target=ctx.GetActor(ev.TargetActorKey);
            if(target is null)return;
            // This event itself is the observable published state transition.
            // Its Amount was populated at scheduling time from the script.
            ctx.ScheduleEvent(new CombatEvent {
                TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.RotationDecision,
                SourceActorKey=ownerKey,TargetActorKey=bossKey,IsInternal=true,
                Description="Scripted health milestone reached"
            });
            return;
        }
        if(ev.EffectKey is not (SpawnEvent or DespawnEvent) ||
           !_adds.ContainsKey(ev.TargetActorKey))return;
        var actor=ctx.GetActor(ev.TargetActorKey);
        if(actor is null)return;
        bool spawned=ev.EffectKey==SpawnEvent;
        actor.InitializeHealth(BenchmarkHealth,spawned?BenchmarkHealth:0m);
        if(!spawned)
        {
            // Expired temporary adds cannot carry DoTs or other auras forward
            // to a hypothetical subsequent activation. This state belongs to
            // the add; it does not remove external boss buffs.
            actor.ActiveAuras.Clear();
        }
        // The published event is processed as the state transition itself.
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.RotationDecision,
            SourceActorKey=ownerKey,TargetActorKey=bossKey,
            IsInternal=true,Description=spawned?
                "Scripted add entered combat":"Scripted add left combat"
        });
    }

    private void Schedule(SimulationContext context,decimal time,string key,string effect)
    {
        decimal health=0m;
        if(effect!=DespawnEvent)
        {
            var target=context.GetActor(key);
            if(target is null)throw new ArgumentException($"Unknown scripted target '{key}'.");
            health=context.GetHealthPercentForConditions(target);
            // When scheduling at t=0, the timeline's current value may differ
            // from the requested checkpoint; read the scripted checkpoint data
            // at the event timestamp instead of the current event clock.
            if(effect=="solo-dps-health-checkpoint" &&
               options?.Enabled==true)
            {
                if(key==bossKey)health=options.BossHealthMilestones
                    .Where(x=>x.TimeSeconds<=time)
                    .Select(x=>x.HealthPercent)
                    .DefaultIfEmpty(options.StartingHealthPercent).Last();
                else if(_adds.TryGetValue(key,out var add))
                {
                    var milestones=add.HealthMilestones
                        .Select(x=>new {
                            At=x.TimeSeconds+(add.HealthMilestonesRelativeToSpawn?
                                add.SpawnTimeSeconds:0m),x.HealthPercent
                        })
                        .Where(x=>x.At<=time).OrderBy(x=>x.At);
                    health=milestones.Select(x=>x.HealthPercent)
                        .DefaultIfEmpty(add.StartingHealthPercent).Last();
                }
            }
            if(effect==SpawnEvent && _adds.TryGetValue(key,out var spawning))
                health=spawning.StartingHealthPercent;
        }
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds=time,Type=CombatEventType.ScriptedTargetStateChanged,
            SourceActorKey=ownerKey,TargetActorKey=key,EffectKey=effect,
            Amount=health,IsInternal=false,
            Description=$"Solo DPS scripted {effect}: {key}; HP {health:0.##}%"
        });
    }
}
