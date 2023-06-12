using Proto.Cluster;
using Proto.Lego.CodeGen.Tests.Workflows;
using Proto.Lego.Persistence;
using Proto.Lego.Tests.Aggregates;
using Proto.Lego.Workflow;

namespace Proto.Lego.Tests.Workflows;

public class TestWorkflow : TestWorkflowBase
{
    protected override TimeSpan ClearAfter { get; } = TimeSpan.FromMilliseconds(100);

    public TestWorkflow(IContext context, ClusterIdentity clusterIdentity, IWorkflowStore store)
        : base(context, clusterIdentity, store)
    {
    }

    public override async Task<WorkflowResult> ExecuteAsync(TestWorkflowInput input)
    {
        var testAction = new TestActionRequest
        {
            ResultToReturn = true,
            StringToSave = input.StringToSave
        };

        var aggregateOneClient = GetClient<TestAggregateClient>(input.AggregateOneId);
        var aggregateTwoClient = GetClient<TestAggregateClient>(input.AggregateTwoId);

        await aggregateOneClient.PrepareTestAction(testAction, CancellationToken.None);
        await aggregateTwoClient.PrepareTestAction(testAction, CancellationToken.None);

        await aggregateOneClient.ConfirmTestAction(testAction, CancellationToken.None);
        await aggregateTwoClient.ConfirmTestAction(testAction, CancellationToken.None);

        return new WorkflowResult
        {
            Succeeded = true
        };
    }
}