namespace WarcraftSim.Core.Stats;

public sealed class StatCollection
{
    public Dictionary<string, decimal> Values { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public decimal Get(string statKey)
    {
        return Values.TryGetValue(statKey, out var value)
            ? value
            : 0m;
    }

    public void Set(string statKey, decimal value)
    {
        Values[statKey] = value;
    }

    public void Add(string statKey, decimal value)
    {
        Values[statKey] = Get(statKey) + value;
    }

    public bool Contains(string statKey)
    {
        return Values.ContainsKey(statKey);
    }

    public bool TryGet(string statKey, out decimal value)
    {
        return Values.TryGetValue(statKey, out value);
    }
}