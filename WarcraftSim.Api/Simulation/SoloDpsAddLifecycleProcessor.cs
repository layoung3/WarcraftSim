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
                decimal last=0m,lastHp=add.StartingHealthPercent;
                foreach(var checkpoint in add.HealthMilestones)
                {
                    if(checkpoint is null || checkpoint.TimeSeconds<=last ||
                       checkpoint.TimeSeconds<=add.SpawnTimeSeconds ||
                       checkpoint.TimeSeconds>add.DespawnTimeSeconds ||
                       checkpoint.HealthPercent is <=0m or >100m ||
                       checkpoint.HealthPercent>lastHp)
                        throw new ArgumentException("Add HP milestones must follow spawn and be monotonically nonincreasing.");
                    if(checkpoint.TimeSeconds<ctx.Options.DurationSeconds)
                        timeline.Add(new(checkpoint.TimeSeconds,checkpoint.HealthPercent));
                    last=checkpoint.TimeSeconds;lastHp=checkpoint.HealthPercent;
                    if(checkpoint.TimeSeconds<ctx.Options.DurationSeconds)
                        Schedule(ctx,checkpoint.TimeSeconds,bossKey,"solo-dps-health-checkpoint");
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
        if(ev.Type!=CombatEventType.RotationDecision ||
           ev.EffectKey is not (SpawnEvent or DespawnEvent) ||
           ev.TargetActorKey is null || !_adds.ContainsKey(ev.TargetActorKey))return;
        var actor=ctx.GetActor(ev.TargetActorKey);
        if(actor is null)return;
        actor.InitializeHealth(BenchmarkHealth,ev.EffectKey==SpawnEvent?
            BenchmarkHealth:0m);
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.RotationDecision,
            SourceActorKey=ownerKey,TargetActorKey=bossKey,
            IsInternal=true,Description=ev.EffectKey==SpawnEvent?
                "Scripted add entered combat":"Scripted add left combat"
        });
    }

    private void Schedule(SimulationContext context,decimal time,string key,string effect)
    {
        context.ScheduleEvent(new CombatEvent {
            TimeSeconds=time,Type=CombatEventType.RotationDecision,
            SourceActorKey=ownerKey,TargetActorKey=key,EffectKey=effect,
            IsInternal=true,Description=$"Solo DPS scripted event: {effect} for {key}"
        });
    }
}
