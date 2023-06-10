using Proto.Cluster;
using Proto.Lego.AggregateGrain;
using Proto.Lego.Persistence;

namespace Proto.Lego.CodeGen.Tests.Aggregates;

public class TestAggregate : TestAggregateBase
{
    private readonly IAggregateGrainStore _store;

    public TestAggregate(IContext context, ClusterIdentity clusterIdentity, IAggregateGrainStore store) : base(context, clusterIdentity)
    {
        _store = store;
    }

    protected override async Task RecoverStateAsync()
    {
        var stateWrapper = await _store.GetAsync(PersistenceId);
        if (stateWrapper != null)
        {
            StateWrapper = stateWrapper;
        }
    }

    protected override async Task PersistStateAsync()
    {
        await _store.SetAsync(PersistenceId, StateWrapper);
    }

    public override Task<OperationResponse> PrepareTestAction(TestActionRequest request)
    {
        State.OperationsPerformed++;

        return Task.FromResult(new OperationResponse
        {
            Success = request.ResultToReturn
        });
    }

    public override Task<OperationResponse> ConfirmTestAction(TestActionRequest request)
    {
        State.OperationsPerformed++;
        State.SavedString = request.StringToSave;

        return Task.FromResult(new OperationResponse
        {
            Success = request.ResultToReturn
        });
    }

    public override Task<OperationResponse> CancelTestAction(TestActionRequest request)
    {
        State.OperationsPerformed++;

        return Task.FromResult(new OperationResponse
        {
            Success = request.ResultToReturn
        });
    }

    public override Task<OperationResponse> ExecuteTestAction(TestActionRequest request)
    {
        State.OperationsPerformed++;
        State.SavedString = request.StringToSave;

        return Task.FromResult(new OperationResponse
        {
            Success = request.ResultToReturn
        });
    }
}