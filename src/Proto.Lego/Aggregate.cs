using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate;
using Proto.Lego.Persistence;

namespace Proto.Lego;

public abstract class Aggregate<TState> : IActor where TState : IMessage, new()
{
    private readonly IAggregateStore _store;
    private readonly ILogger<Aggregate<TState>> _logger;

    protected string? Kind;
    protected string? Id;
    protected string PersistenceId => $"{Kind}/{Id}";
    protected string Key => PersistenceId;
    private IContext? _context;

    protected TState InnerState;
    protected AggregateStateWrapper State;

    protected Aggregate(IAggregateStore store, ILogger<Aggregate<TState>> logger)
    {
        _store = store;
        _logger = logger;

        InnerState = new TState();
        State = new()
        {
            InnerState = Any.Pack(InnerState)
        };
    }

    public async Task ReceiveAsync(IContext context)
    {
        _logger.LogDebug("{self} received {message}", Key, context.Message);
        _context = context;

        switch (context.Message)
        {
            case Started:
                Id = context.Get<ClusterIdentity>()!.Identity;
                await RecoverStateAsync();
                break;

            case Operation operation:
                await HandleOperationAsync(operation);
                break;

            case WipeWorkflowState wipeWorkflowState:
                await HandleWipeWorkflowState(wipeWorkflowState);
                break;
        }
    }

    private async Task RecoverStateAsync()
    {
        _logger.LogDebug("{self} entered RecoverStateAsync", Key);

        var state = await _store.GetAsync(Key);
        if (state != null)
        {
            State = state;
            InnerState = State.InnerState.Unpack<TState>();
        }

        _logger.LogDebug("{self} exited RecoverStateAsync", Key);
    }

    protected async Task PersistStateAsync()
    {
        _logger.LogDebug("{self} entered PersistStateAsync", Key);

        State.InnerState = Any.Pack(InnerState);
        await _store.SetAsync(Key, State);

        _logger.LogDebug("{self} exited PersistStateAsync", Key);
    }

    private async Task HandleOperationAsync(Operation operation)
    {
        _logger.LogDebug("{self} entered HandleOperationAsync", Key);

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

        _logger.LogDebug("{self} exited HandleOperationAsync", Key);
    }

    private OperationResponse HandlePrepare(string workflowId, Any action)
    {
        _logger.LogDebug("{self} entered HandlePrepare", Key);

        var response = Prepare(action);

        if (response.Success)
        {
            var workflowState = GetOrCreateWorkflowState(workflowId);
            workflowState.PreparedActions.Add(action);
        }

        _logger.LogDebug("{self} exited HandlePrepare", Key);

        return response;
    }

    private OperationResponse HandleConfirm(string workflowId, Any action)
    {
        _logger.LogDebug("{self} entered HandleConfirm", Key);

        var workflowState = GetOrCreateWorkflowState(workflowId);
        var preparedAction = workflowState.PreparedActions.FirstOrDefault(x => x.Equals(action));

        if (preparedAction == null)
        {
            _logger.LogError("{self}: {action} for {workflowId} was not prepared", Key, action, workflowId);

            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "This action was not prepared"
            };
        }

        var response = Confirm(action);
        workflowState.PreparedActions.Remove(action);

        _logger.LogDebug("{self} exited HandleConfirm", Key);

        return response;
    }

    private OperationResponse HandleCancel(string workflowId, Any action)
    {
        _logger.LogDebug("{self} entered HandleCancel", Key);

        var workflowState = GetOrCreateWorkflowState(workflowId);
        var preparedAction = workflowState.PreparedActions.FirstOrDefault(x => x.Equals(action));

        if (preparedAction == null)
        {
            _logger.LogError("{self}: {action} for {workflowId} was not prepared", Key, action, workflowId);

            return new OperationResponse
            {
                Success = false,
                ErrorMessage = "This action was not prepared"
            };
        }

        var response = Cancel(action);
        workflowState.PreparedActions.Remove(action);

        _logger.LogDebug("{self} exited HandleCancel", Key);

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

    private WorkflowCommunicationState GetOrCreateWorkflowState(string workflowId)
    {
        if (!State.WorkflowStates.ContainsKey(workflowId))
        {
            State.WorkflowStates.Add(workflowId, new WorkflowCommunicationState());
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

    private void Reply(IMessage message)
    {
        _context!.Send(_context.Sender!, message);
    }

    private async Task HandleWipeWorkflowState(WipeWorkflowState wipeWorkflowState)
    {
        _logger.LogDebug("{self} entered HandleWipeWorkflowState", Key);

        if (State.WorkflowStates.ContainsKey(wipeWorkflowState.WorkflowId))
        {
            State.WorkflowStates.Remove(wipeWorkflowState.WorkflowId);
            await PersistStateAsync();
        }

        Reply(new Empty());

        _logger.LogDebug("{self} exited HandleWipeWorkflowState", Key);
    }
}