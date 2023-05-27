using Xunit;

namespace Proto.Lego.Persistence.Tests.Common;

public abstract class AliveWorkflowStoreTestsBase
{
    private readonly IAliveWorkflowStore _aliveWorkflowStore;

    protected AliveWorkflowStoreTestsBase(IAliveWorkflowStore aliveWorkflowStore)
    {
        _aliveWorkflowStore = aliveWorkflowStore;
    }

    [Fact]
    public async Task PutAsync_WhenNewValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _aliveWorkflowStore.PutAsync(key);
    }

    [Fact]
    public async Task PutAsync_WhenValueExists_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _aliveWorkflowStore.PutAsync(key);
        await _aliveWorkflowStore.PutAsync(key);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueExists_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _aliveWorkflowStore.PutAsync(key);
        await _aliveWorkflowStore.DeleteAsync(key);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _aliveWorkflowStore.DeleteAsync(key);
    }

    [Fact]
    public async Task ActOnAllAsync_Succeeds()
    {
        var actedOnKeys = new List<string>();
        var key = Guid.NewGuid().ToString();

        await _aliveWorkflowStore.PutAsync(key);
        await _aliveWorkflowStore.ActOnAllAsync(s =>
        {
            actedOnKeys.Add(s);
            return Task.CompletedTask;
        });

        Assert.Contains(key, actedOnKeys);
    }
}