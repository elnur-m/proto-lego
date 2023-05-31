using Proto.Lego.Workflow;
using Shouldly;
using Xunit;

namespace Proto.Lego.Persistence.Tests.Common;

public abstract class WorkflowStoreTestsBase
{
    private readonly IWorkflowStore _workflowStore;

    protected WorkflowStoreTestsBase(IWorkflowStore workflowStore)
    {
        _workflowStore = workflowStore;
    }

    [Fact]
    public async Task SetAsync_WhenNewValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var state = GenerateRandomState();
        await _workflowStore.SetAsync(key, state);

        var value = await _workflowStore.GetAsync(key);
        value.ShouldBeEquivalentTo(state);
    }

    [Fact]
    public async Task SetAsync_WhenValueExists_Overwrites()
    {
        var key = Guid.NewGuid().ToString();
        var stateOne = GenerateRandomState();
        var stateTwo = GenerateRandomState();

        await _workflowStore.SetAsync(key, stateOne);
        await _workflowStore.SetAsync(key, stateTwo);

        var value = await _workflowStore.GetAsync(key);
        value.ShouldBeEquivalentTo(stateTwo);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueExists_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var state = GenerateRandomState();
        await _workflowStore.SetAsync(key, state);
        await _workflowStore.DeleteAsync(key);

        var value = await _workflowStore.GetAsync(key);
        value.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenValueDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _workflowStore.DeleteAsync(key);

        var value = await _workflowStore.GetAsync(key);
        value.ShouldBeNull();
    }

    [Fact]
    public async Task ActOnAllAsync_Succeeds()
    {
        var actedOnKeys = new List<string>();
        var key = Guid.NewGuid().ToString();
        var state = GenerateRandomState();

        await _workflowStore.SetAsync(key, state);
        await _workflowStore.ActOnAllAsync((workflowKey, workflowState) =>
        {
            actedOnKeys.Add(workflowKey);
            return Task.CompletedTask;
        });

        actedOnKeys.ShouldContain(key);
    }

    private WorkflowState GenerateRandomState()
    {
        var state = new WorkflowState();
        var success = Random.Shared.Next() % 2 == 0;
        if (success)
        {
            state.Result = new WorkflowResult
            {
                Completed = true,
                Succeeded = true
            };
        }
        else
        {
            var errors = Random.Shared.Next(1, 10);

            state.Result = new WorkflowResult
            {
                Completed = true,
                Succeeded = false,
                ErrorMessages = { Enumerable.Range(1, errors).Select(_ => Guid.NewGuid().ToString()) }
            };
        }

        return state;
    }
}