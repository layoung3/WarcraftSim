using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Opt-in time-driven boss health for Solo DPS. Scripted health
/// percentages drive execute conditions independent of actual damage;
/// ordinary raid and survivability actors keep real HP semantics.</summary>
public sealed class SoloDpsEncounterScript
{
    public bool Enabled { get; set; }
    public decimal StartingHealthPercent { get; set; } = 100m;
    public List<SoloDpsHealthMilestone> BossHealthMilestones { get; set; } = [];
    // Optional timed adds already materialized by the typed class runner.
    // All specified health milestones are absolute encounter times.
    public List<SoloDpsAddWindow> AddWindows { get; set; } = [];


    public ScriptedTargetHealthTimeline Build(decimal duration)
    {
        if(duration is <=0m or >1200m || StartingHealthPercent is <=0m or >100m ||
           BossHealthMilestones is null || BossHealthMilestones.Count>30)
            throw new ArgumentException("Invalid solo DPS health milestone configuration.");
        var checkpoints=new List<ScriptedTargetHealthCheckpoint> {new(0m,StartingHealthPercent)};
        decimal lastTime=0m,lastHp=StartingHealthPercent;
        foreach(var m in BossHealthMilestones)
        {
            if(m is null || m.TimeSeconds<=lastTime || m.TimeSeconds>duration ||
               m.HealthPercent<0m || m.HealthPercent>lastHp ||
               m.HealthPercent==0m && m.TimeSeconds!=duration)
                throw new ArgumentException("Boss milestones must increase in time, preserve or decrease HP, and reach 0 only at fight end.");
            if(m.TimeSeconds<duration)checkpoints.Add(new(m.TimeSeconds,m.HealthPercent));
            // The final zero-health marker is a display endpoint, not a kill.
            lastTime=m.TimeSeconds;lastHp=m.HealthPercent;
        }
        return new ScriptedTargetHealthTimeline(checkpoints);
    }
    public void Apply(SimulationContext context,string targetKey="target")
    {
        if(Enabled)context.SetScriptedTargetHealth(targetKey,Build(context.Options.DurationSeconds));
    }
}
public sealed class SoloDpsHealthMilestone
{
    public decimal TimeSeconds { get; set; }
    public decimal HealthPercent { get; set; }
}

public sealed class SoloDpsAddWindow
{
    public string TargetKey { get; set; } = "";
    public decimal SpawnTimeSeconds { get; set; }
    public decimal DespawnTimeSeconds { get; set; }
    public decimal StartingHealthPercent { get; set; } = 100m;
    // false=absolute encounter seconds; true=seconds after SpawnTimeSeconds.
    public bool HealthMilestonesRelativeToSpawn { get; set; }
    public List<SoloDpsHealthMilestone> HealthMilestones { get; set; } = [];
}
