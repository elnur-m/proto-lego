namespace Proto.Lego.Persistence.InMemory;

public class InMemoryAliveWorkflowStore : IAliveWorkflowStore
{
    private readonly HashSet<string> _workflowIds = new();

    public Task SetAsync(string key)
    {
        if (!_workflowIds.Contains(key))
        {
            _workflowIds.Add(key);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _workflowIds.Remove(key);
        return Task.CompletedTask;
    }

    public async Task ActOnAllAsync(Func<string, Task> action)
    {
        foreach (var workflowId in _workflowIds)
        {
            await action(workflowId);
        }
    }
}