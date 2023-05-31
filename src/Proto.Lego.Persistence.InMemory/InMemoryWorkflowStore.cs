using Proto.Lego.Workflow;

namespace Proto.Lego.Persistence.InMemory;

public class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly Dictionary<string, WorkflowState> _workflows = new();

    public Task<WorkflowState?> GetAsync(string key)
    {
        return Task.FromResult(_workflows.TryGetValue(key, out var value) ? value : null);
    }

    public Task SetAsync(string key, WorkflowState state)
    {
        _workflows[key] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _workflows.Remove(key);
        return Task.CompletedTask;
    }

    public async Task ActOnAllAsync(Func<string, WorkflowState, Task> action)
    {
        await Task.WhenAll(_workflows.Select(x => action(x.Key, x.Value)));
    }
}