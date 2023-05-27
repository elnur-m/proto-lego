using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Persistence;

namespace Proto.Lego.Aggregate.Tests.TestAggregate;

public class TestAggregate : Aggregate<TestAggregateState>
{
    public const string AggregateKind = "TestAggregate";

    public TestAggregate(IKeyValueStateStore stateStore, ILogger<Aggregate<TestAggregateState>> logger) : base(stateStore, logger)
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
        var state = GetInnerState();
        state.OperationsPerformed++;
        SetInnerState(state);

        return new OperationResponse { Success = testAction.ResultToReturn };
    }

    private OperationResponse ConfirmTestAction(TestAction testAction)
    {
        var state = GetInnerState();
        state.SavedString = testAction.StringToSave;
        state.OperationsPerformed++;
        SetInnerState(state);

        return new OperationResponse { Success = testAction.ResultToReturn };
    }

    private OperationResponse CancelTestAction(TestAction testAction)
    {
        var state = GetInnerState();
        state.OperationsPerformed++;
        SetInnerState(state);

        return new OperationResponse { Success = true };
    }

    private OperationResponse ExecuteTestAction(TestAction testAction)
    {
        var state = GetInnerState();
        state.SavedString = testAction.StringToSave;
        state.OperationsPerformed++;
        SetInnerState(state);

        return new OperationResponse { Success = testAction.ResultToReturn };
    }
}