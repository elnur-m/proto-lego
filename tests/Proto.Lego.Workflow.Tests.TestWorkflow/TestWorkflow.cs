using Microsoft.Extensions.Logging;
using Proto.Lego.Aggregate.Tests.TestAggregate;
using Proto.Lego.Persistence;

namespace Proto.Lego.Workflow.Tests.TestWorkflow;

public class TestWorkflow : Workflow<TestWorkflowState>
{
    public const string WorkflowKind = "TestWorkflow";

    public TestWorkflow(IKeyValueStateStore stateStore, IAliveWorkflowStore aliveWorkflowStore, ILogger<Workflow<TestWorkflowState>> logger)
        : base(stateStore, aliveWorkflowStore, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync()
    {
        var innerState = GetInnerState();

        var testActionOne = new TestAction
        {
            StringToSave = innerState.StringToSave,
            ResultToReturn = innerState.ResultToReturnOne
        };

        var testActionTwo = new TestAction
        {
            StringToSave = innerState.StringToSave,
            ResultToReturn = innerState.ResultToReturnTwo
        };

        var prepareOneTask = PrepareAsync(TestAggregate.AggregateKind, innerState.AggregateOneId, testActionOne);
        var prepareTwoTask = PrepareAsync(TestAggregate.AggregateKind, innerState.AggregateTwoId, testActionTwo);

        var prepareResults = await Task.WhenAll(prepareOneTask, prepareTwoTask);

        if (prepareResults.All(x => x.Success))
        {
            var confirmOneTask = ConfirmAsync(TestAggregate.AggregateKind, innerState.AggregateOneId, testActionOne);
            var confirmTwoTask = ConfirmAsync(TestAggregate.AggregateKind, innerState.AggregateTwoId, testActionTwo);

            await Task.WhenAll(confirmOneTask, confirmTwoTask);
        }
        else
        {
            var cancelOneTask = CancelAsync(TestAggregate.AggregateKind, innerState.AggregateOneId, testActionOne);
            var cancelTwoTask = CancelAsync(TestAggregate.AggregateKind, innerState.AggregateTwoId, testActionTwo);

            await Task.WhenAll(cancelOneTask, cancelTwoTask);
        }
    }

    protected override async Task CleanUpAsync()
    {
        await base.CleanUpAsync();
        await RemoveFromAliveWorkflowStoreAsync();
        await RemoveFromWorkflowStoreAsync();
        Stop();
    }
}