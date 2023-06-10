using System.Collections.Concurrent;
using Proto.Lego.WorkflowGrain;

namespace Proto.Lego.Persistence.InMemory;

public class InMemoryWorkflowGrainStore : IWorkflowGrainStore
{
    private readonly ConcurrentDictionary<string, WorkflowGrainState> _workflows = new();

    public Task<WorkflowGrainState?> GetAsync(string key)
    {
        return Task.FromResult(_workflows.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, WorkflowGrainState state)
    {
        _workflows[key] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _workflows.Remove(key, out _);
        return Task.CompletedTask;
    }

    public async Task ActOnAllAsync(Func<string, WorkflowGrainState, Task> action)
    {
        await Task.WhenAll(_workflows.Select(x => action(x.Key, x.Value)));
    }
}