using Microsoft.Extensions.Logging;
using Proto.Lego.Persistence;
using Proto.Lego.Tests.Aggregates;

namespace Proto.Lego.Tests.Workflows;

public class TestWorkflow : Workflow<TestWorkflowInput>
{
    public const string WorkflowKind = "TestWorkflow";

    public TestWorkflow(IWorkflowStore store, ILogger<Workflow<TestWorkflowInput>> logger) : base(store, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync(TestWorkflowInput input)
    {
        var testActionOne = new TestAction
        {
            StringToSave = input.StringToSave,
            ResultToReturn = input.ResultToReturnOne
        };

        var testActionTwo = new TestAction
        {
            StringToSave = input.StringToSave,
            ResultToReturn = input.ResultToReturnTwo
        };

        var prepareOneTask = PrepareAsync(TestAggregate.AggregateKind, input.AggregateOneId, testActionOne);
        var prepareTwoTask = PrepareAsync(TestAggregate.AggregateKind, input.AggregateTwoId, testActionTwo);

        var prepareResults = await Task.WhenAll(prepareOneTask, prepareTwoTask);

        if (prepareResults.All(x => x.Success))
        {
            var confirmOneTask = ConfirmAsync(TestAggregate.AggregateKind, input.AggregateOneId, testActionOne);
            var confirmTwoTask = ConfirmAsync(TestAggregate.AggregateKind, input.AggregateTwoId, testActionTwo);

            await Task.WhenAll(confirmOneTask, confirmTwoTask);
        }
        else
        {
            var cancelOneTask = CancelAsync(TestAggregate.AggregateKind, input.AggregateOneId, testActionOne);
            var cancelTwoTask = CancelAsync(TestAggregate.AggregateKind, input.AggregateTwoId, testActionTwo);

            await Task.WhenAll(cancelOneTask, cancelTwoTask);
        }
    }

    protected override async Task BeforeCleanUpAsync()
    {
        await Task.Delay(3000);
    }
}