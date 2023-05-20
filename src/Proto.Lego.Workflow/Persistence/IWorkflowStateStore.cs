namespace Proto.Lego.Workflow.Persistence;

public interface IWorkflowStateStore
{
    Task<byte[]?> GetAsync(string key);

    Task PutAsync(string key, byte[] value);

    Task UpdateAsync(string key, byte[] value);

    Task DeleteAsync(string key);
}