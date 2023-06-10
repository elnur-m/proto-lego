using Proto.Lego.WorkflowGrain;

namespace Proto.Lego.Persistence;

public interface IWorkflowGrainStore
{
    Task<WorkflowGrainState?> GetAsync(string key);

    Task SetAsync(string key, WorkflowGrainState state);

    Task DeleteAsync(string key);

    Task ActOnAllAsync(Func<string, WorkflowGrainState, Task> action);
}