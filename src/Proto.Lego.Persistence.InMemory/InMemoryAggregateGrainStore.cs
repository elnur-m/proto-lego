using System.Collections.Concurrent;
using Proto.Lego.AggregateGrain;

namespace Proto.Lego.Persistence.InMemory;

public class InMemoryAggregateGrainStore : IAggregateGrainStore
{
    private readonly ConcurrentDictionary<string, AggregateGrainStateWrapper> _aggregates = new();

    public Task<AggregateGrainStateWrapper?> GetAsync(string key)
    {
        return Task.FromResult(_aggregates.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, AggregateGrainStateWrapper state)
    {
        _aggregates[key] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _aggregates.Remove(key, out _);
        return Task.CompletedTask;
    }
}