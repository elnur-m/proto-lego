using Proto.Lego.Aggregate;
using Shouldly;
using Xunit;

namespace Proto.Lego.Persistence.Tests.Common;

public abstract class AggregateStoreTestsBase
{
    private readonly IAggregateStore _aggregateStore;

    protected AggregateStoreTestsBase(IAggregateStore aggregateStore)
    {
        _aggregateStore = aggregateStore;
    }

    [Fact]
    public async Task GetAsync_WhenValueDoesNotExist_ReturnsNull()
    {
        var key = Guid.NewGuid().ToString();

        var value = await _aggregateStore.GetAsync(key);

        value.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_WhenNewValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var state = GenerateRandomState();

        await _aggregateStore.SetAsync(key, state);

        var value = await _aggregateStore.GetAsync(key);
        value.ShouldBeEquivalentTo(state);
    }

    [Fact]
    public async Task SetAsync_WhenExistingValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var stateOne = GenerateRandomState();
        var stateTwo = GenerateRandomState();

        await _aggregateStore.SetAsync(key, stateOne);
        await _aggregateStore.SetAsync(key, stateTwo);

        var value = await _aggregateStore.GetAsync(key);
        value.ShouldBeEquivalentTo(stateTwo);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();

        await _aggregateStore.DeleteAsync(key);
    }

    [Fact]
    public async Task DeleteAsync_RemovesValue()
    {
        var key = Guid.NewGuid().ToString();
        var state = GenerateRandomState();
        await _aggregateStore.SetAsync(key, state);

        await _aggregateStore.DeleteAsync(key);

        var value = await _aggregateStore.GetAsync(key);
        value.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenKeyDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();

        await _aggregateStore.DeleteAsync(key);
        await _aggregateStore.DeleteAsync(key);

        var value = await _aggregateStore.GetAsync(key);
        value.ShouldBeNull();
    }

    private AggregateStateWrapper GenerateRandomState()
    {
        var state = new AggregateStateWrapper();
        var workflowsNum = Random.Shared.Next(1, 10);

        for (int i = 0; i < workflowsNum; i++)
        {
            state.CallerStates.Add(i.ToString(), new CallerCommunicationState
            {
                Sequence = Random.Shared.Next(1, 10)
            });
        }

        return state;
    }
}