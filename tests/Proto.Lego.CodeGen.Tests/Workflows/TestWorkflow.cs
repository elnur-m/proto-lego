﻿using Proto.Cluster;
using Proto.Lego.CodeGen.Tests.Aggregates;
using Proto.Lego.Persistence;
using Proto.Lego.WorkflowGrain;

namespace Proto.Lego.CodeGen.Tests.Workflows;

public class TestWorkflow : TestWorkflowBase
{
    protected override TimeSpan ClearAfter { get; } = TimeSpan.FromSeconds(1);

    public TestWorkflow(IContext context, ClusterIdentity clusterIdentity, IWorkflowGrainStore store)
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

        await Cluster
            .GetTestAggregate(input.AggregateOneId)
            .PrepareTestAction(GetNextOperation(TestAggregateActor.Kind, input.AggregateOneId, testAction), CancellationToken.None);

        await Cluster
            .GetTestAggregate(input.AggregateTwoId)
            .PrepareTestAction(GetNextOperation(TestAggregateActor.Kind, input.AggregateTwoId, testAction), CancellationToken.None);

        await Cluster
            .GetTestAggregate(input.AggregateOneId)
            .ConfirmTestAction(GetNextOperation(TestAggregateActor.Kind, input.AggregateOneId, testAction), CancellationToken.None);

        await Cluster
            .GetTestAggregate(input.AggregateTwoId)
            .ConfirmTestAction(GetNextOperation(TestAggregateActor.Kind, input.AggregateTwoId, testAction), CancellationToken.None);

        return new WorkflowResult
        {
            Succeeded = true
        };
    }
}