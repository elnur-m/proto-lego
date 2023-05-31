using Proto.Lego.Workflow;

namespace Proto.Lego.Persistence;

public interface IWorkflowStore
{
    Task<WorkflowState?> GetAsync(string key);

    Task SetAsync(string key, WorkflowState state);

    Task DeleteAsync(string key);

    Task ActOnAllAsync(Func<string, WorkflowState, Task> action);
}