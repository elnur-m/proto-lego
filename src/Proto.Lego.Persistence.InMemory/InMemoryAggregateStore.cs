using System.Collections.Concurrent;
using Proto.Lego.Aggregate;

namespace Proto.Lego.Persistence.InMemory;

public class InMemoryAggregateStore : IAggregateStore
{
    private readonly ConcurrentDictionary<string, AggregateStateWrapper> _aggregates = new();

    public Task<AggregateStateWrapper?> GetAsync(string key)
    {
        return Task.FromResult(_aggregates.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, AggregateStateWrapper state)
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