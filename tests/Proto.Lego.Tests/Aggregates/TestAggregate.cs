using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Lego.Aggregate;
using Proto.Lego.Persistence;

namespace Proto.Lego.Tests.Aggregates;

public class TestAggregate : Aggregate<TestAggregateState>
{
    public const string AggregateKind = nameof(TestAggregate);

    public TestAggregate(IAggregateStore store, ILogger<Aggregate<TestAggregateState>> logger) : base(store, logger)
    {
        Kind = AggregateKind;
    }

    protected override OperationResponse Prepare(Any action)
    {
        if (action.Is(TestAction.Descriptor))
        {
            return PrepareTestAction(action.Unpack<TestAction>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Confirm(Any action)
    {
        if (action.Is(TestAction.Descriptor))
        {
            return ConfirmTestAction(action.Unpack<TestAction>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Cancel(Any action)
    {
        if (action.Is(TestAction.Descriptor))
        {
            return CancelTestAction(action.Unpack<TestAction>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    protected override OperationResponse Execute(Any action)
    {
        if (action.Is(TestAction.Descriptor))
        {
            return ExecuteTestAction(action.Unpack<TestAction>());
        }

        return new OperationResponse
        {
            Success = false,
            ErrorMessage = "Unknown action"
        };
    }

    private OperationResponse PrepareTestAction(TestAction testAction)
    {
        InnerState.OperationsPerformed++;

        return new OperationResponse { Success = testAction.ResultToReturn };
    }

    private OperationResponse ConfirmTestAction(TestAction testAction)
    {
        InnerState.SavedString = testAction.StringToSave;
        InnerState.OperationsPerformed++;

        return new OperationResponse { Success = testAction.ResultToReturn };
    }

    private OperationResponse CancelTestAction(TestAction testAction)
    {
        InnerState.OperationsPerformed++;

        return new OperationResponse { Success = true };
    }

    private OperationResponse ExecuteTestAction(TestAction testAction)
    {
        InnerState.SavedString = testAction.StringToSave;
        InnerState.OperationsPerformed++;

        return new OperationResponse { Success = testAction.ResultToReturn };
    }
}