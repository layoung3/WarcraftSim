namespace WarcraftSim.Core.Simulation.Batch;

/// <summary>Actual seeded iteration observation, never a fabricated percentile sample.</summary>
public sealed record SimulationBatchSample(int Seed,decimal DamagePerSecond);
