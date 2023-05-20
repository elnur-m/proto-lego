using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Aggregate.Persistence;

namespace Proto.Lego.Aggregate;

public abstract class Aggregate<TState> : IActor where TState : IMessage, new()
{
    private readonly IAggregateStateStore _stateStore;

    protected string? Kind;
    protected string? Id;
    protected string PersistenceId => $"{Kind}/{Id}";
    protected string Key => PersistenceId;
    private IContext? _context;

    protected AggregateStateWrapper State = new()
    {
        InnerState = Any.Pack(new TState())
    };

    private bool _hasPersistedState;

    protected Aggregate(IAggregateStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public async Task ReceiveAsync(IContext context)
    {
        _context = context;

        switch (context.Message)
        {
            case Started:
                Id = context.Get<ClusterIdentity>()!.Identity;
                await RecoverStateAsync();
                break;

            case Operation operation:
                await HandleOperation(operation);
                break;
        }
    }

    protected TState GetInnerState()
    {
        return State.InnerState.Unpack<TState>();
    }

    protected void SetInnerState(TState innerState)
    {
        State.InnerState = Any.Pack(innerState);
    }

    private async Task RecoverStateAsync()
    {
        var bytes = await _stateStore.GetAsync(Key);
        if (bytes != null)
        {
            _hasPersistedState = true;
            State = DeserializeState(bytes);
        }
    }

    protected async Task PersistStateAsync()
    {
        var bytes = SerializeState();

        if (_hasPersistedState)
        {
            await _stateStore.UpdateAsync(Key, bytes);
        }
        else
        {
            await _stateStore.PutAsync(Key, bytes);
            _hasPersistedState = true;
        }
    }

    protected virtual byte[] SerializeState()
    {
        return State.ToByteArray();
    }

    protected virtual AggregateStateWrapper DeserializeState(byte[] bytes)
    {
        return AggregateStateWrapper.Parser.ParseFrom(bytes);
    }

    private async Task HandleOperation(Operation operation)
    {
        var workflowState = GetOrCreateWorkflowState(operation.WorkflowId);

        if (operation.Sequence - workflowState.Sequence > 1)
        {
            Reply(false, "Invalid sequence");
            return;
        }

        var savedResponse = GetSavedResponse(operation.WorkflowId, operation.Sequence);
        if (savedResponse != null)
        {
            Reply(savedResponse);
            return;
        }

        OperationResponse response;

        switch (operation.OperationType)
        {
            case OPERATION_TYPE.Prepare:
                response = HandlePrepare(operation.WorkflowId, operation.Action);
                break;

            case OPERATION_TYPE.Confirm:
                response = HandleConfirm(operation.WorkflowId, operation.Action);
                break;

            case OPERATION_TYPE.Cancel:
                response = HandleCancel(operation.WorkflowId, operation.Action);
                break;

            case OPERATION_TYPE.Execute:
                response = Execute(operation.Action);
                break;

            default:
                Reply(false, "Invalid operation type");
                return;
        }

        workflowState.Sequence++;
        workflowState.Responses.Add(operation.Sequence, response);
        await PersistStateAsync();
        Reply(response);
    }

    private OperationResponse HandlePrepare(string workflowId, Any action)
    {
        var response = Prepare(action);

        if (response.Success)
        {
            var workflowState = GetOrCreateWorkflowState(workflowId);
            workflowState.PreparedActions.Add(action);
        }

        return response;
    }

    private OperationResponse HandleConfirm(string workflowId, Any action)
    {
        var workflowState = GetOrCreateWorkflowState(workflowId);
        var preparedAction = workflowState.PreparedActions.FirstOrDefault(x => x.Equals(action));

        if (preparedAction == null)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "This action was not prepared"
            };
        }

        var response = Confirm(action);
        workflowState.PreparedActions.Remove(action);

        return response;
    }

    private OperationResponse HandleCancel(string workflowId, Any action)
    {
        var workflowState = GetOrCreateWorkflowState(workflowId);
        var preparedAction = workflowState.PreparedActions.FirstOrDefault(x => x.Equals(action));

        if (preparedAction == null)
        {
            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "This action was not prepared"
            };
        }

        var response = Cancel(action);
        workflowState.PreparedActions.Remove(action);

        return response;
    }

    protected abstract OperationResponse Prepare(Any action);

    protected abstract OperationResponse Confirm(Any action);

    protected abstract OperationResponse Cancel(Any action);

    protected abstract OperationResponse Execute(Any action);

    private OperationResponse? GetSavedResponse(string workflowId, long sequence)
    {
        if (!State.WorkflowStates.TryGetValue(workflowId, out var workflowState))
        {
            return null;
        }

        if (workflowState.Sequence < sequence)
        {
            return null;
        }

        var savedResponse = workflowState.Responses[sequence];
        return savedResponse;
    }

    private WorkflowState GetOrCreateWorkflowState(string workflowId)
    {
        if (!State.WorkflowStates.ContainsKey(workflowId))
        {
            State.WorkflowStates.Add(workflowId, new WorkflowState());
        }

        return State.WorkflowStates[workflowId];
    }

    private void Reply(bool success, string? errorMessage = null)
    {
        _context!.Send(_context.Sender!, new OperationResponse
        {
            Success = success,
            ErrorMessage = errorMessage
        });
    }

    private void Reply(OperationResponse response)
    {
        _context!.Send(_context.Sender!, response);
    }
}