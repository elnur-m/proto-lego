namespace Proto.Lego.Workflow.Persistence;

public interface IAliveWorkflowStore
{
    Task PutAsync(string key);

    Task DeleteAsync(string key);

    Task ActOnAllAsync(Func<string, Task> action);
}