using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow.Messages;

namespace Proto.Lego.Workflow;

public abstract class Workflow<TState> : IActor where TState : IMessage, new()
{
    private readonly IKeyValueStateStore _stateStore;
    private readonly IAliveWorkflowStore _aliveWorkflowStore;
    private readonly ILogger<Workflow<TState>> _logger;

    protected TState? InnerState;
    protected WorkflowStateWrapper? State;

    protected string? Kind;
    protected string? Id;
    protected string PersistenceId => $"{Kind}/{Id}";
    protected string Key => PersistenceId;
    private IContext? _context;

    private readonly Dictionary<string, long> _sequences = new();
    private bool _hasPersistedState;
    private bool _isExecuting;

    protected Workflow(
        IKeyValueStateStore stateStore,
        IAliveWorkflowStore aliveWorkflowStore,
        ILogger<Workflow<TState>> logger
    )
    {
        _stateStore = stateStore;
        _aliveWorkflowStore = aliveWorkflowStore;
        _logger = logger;
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

            case Trigger:
                await HandleTriggerAsync();
                break;

            case TState state:

                if (_isExecuting)
                {
                    _context!.Send(_context.Sender!, new Empty());
                    return;
                }

                await TryInitializeAsync(state);
                ExecuteInBackground();
                break;
        }
    }

    protected virtual byte[] SerializeState()
    {
        return State.ToByteArray();
    }

    protected virtual WorkflowStateWrapper DeserializeState(byte[] bytes)
    {
        return WorkflowStateWrapper.Parser.ParseFrom(bytes);
    }

    protected abstract Task ExecuteWorkflowAsync();

    protected async Task CleanUpAggregatesAsync()
    {
        _logger.LogDebug("{self} entered CleanUpAggregatesAsync", Key);

        var tasks = State!.AggregatesToCleanUp
            .Select(GetAggregateKindAndId)
            .Select(kindAndId => RequestAggregateAsync<Empty>(kindAndId.kind, kindAndId.id, new WipeWorkflowState
            {
                WorkflowId = Key
            }));

        await Task.WhenAll(tasks);

        _logger.LogDebug("{self} exited CleanUpAggregatesAsync", Key);
    }

    protected virtual async Task CleanUpAsync()
    {
        _logger.LogDebug("{self} entered CleanUpAsync", Key);

        await CleanUpAggregatesAsync();

        _logger.LogDebug("{self} exited CleanUpAsync", Key);
    }

    private void ExecuteInBackground()
    {
        _logger.LogDebug("{self} entered ExecuteInBackground", Key);

        if (!State!.Completed)
        {
            var executeTask = ExecuteWorkflowAsync();
            _isExecuting = true;

            _context!.ReenterAfter(executeTask, () =>
            {
                _isExecuting = false;
                State.Completed = true;
                PersistStateAsync().GetAwaiter().GetResult();
                CleanUpAsync().GetAwaiter().GetResult();

                _logger.LogDebug("{self} exited ExecuteInBackground", Key);
            });
        }
        else
        {
            CleanUpAsync().GetAwaiter().GetResult();

            _logger.LogDebug("{self} exited ExecuteInBackground", Key);
        }
    }

    private async Task RecoverStateAsync()
    {
        _logger.LogDebug("{self} entered RecoverStateAsync", Key);

        var bytes = await _stateStore.GetAsync(Key);
        if (bytes != null)
        {
            _hasPersistedState = true;
            State = DeserializeState(bytes);
            InnerState = State.InnerState.Unpack<TState>();
        }

        _logger.LogDebug("{self} exited RecoverStateAsync", Key);
    }

    protected async Task PersistStateAsync()
    {
        _logger.LogDebug("{self} entered PersistStateAsync", Key);

        State!.InnerState = Any.Pack(InnerState);
        var bytes = SerializeState();

        await _stateStore.SetAsync(Key, bytes);

        _logger.LogDebug("{self} exited PersistStateAsync", Key);
    }

    private async Task HandleTriggerAsync()
    {
        _logger.LogDebug("{self} entered HandleTriggerAsync", Key);

        if (_context!.Sender != null)
        {
            _context.Send(_context.Sender, new Empty());
        }

        if (State == null)
        {
            _logger.LogWarning(
                "Found no state for {self}. Workflow state probably deleted. Deleting {self} from AliveWorkflowStore",
                Key, Key);

            await _aliveWorkflowStore.DeleteAsync(Key);
            _context.Self.Stop(_context.System);

            _logger.LogDebug("{self} exited HandleTriggerAsync", Key);

            return;
        }

        ExecuteInBackground();

        _logger.LogDebug("{self} exited HandleTriggerAsync", Key);
    }

    protected async Task RemoveFromAliveWorkflowStoreAsync()
    {
        _logger.LogDebug("{self} entered RemoveFromAliveWorkflowStoreAsync", Key);

        await _aliveWorkflowStore.DeleteAsync(Key);

        _logger.LogDebug("{self} exited RemoveFromAliveWorkflowStoreAsync", Key);
    }

    protected async Task RemoveFromWorkflowStoreAsync()
    {
        _logger.LogDebug("{self} entered RemoveFromWorkflowStoreAsync", Key);

        await _stateStore.DeleteAsync(Key);
        _hasPersistedState = false;

        _logger.LogDebug("{self} exited RemoveFromWorkflowStoreAsync", Key);
    }

    protected void Stop()
    {
        _context!.Self.Stop(_context.System);
    }

    private async Task TryInitializeAsync(TState state)
    {
        _logger.LogDebug("{self} entered TryInitializeAsync", Key);

        if (!_hasPersistedState)
        {
            await _aliveWorkflowStore.SetAsync(Key);

            InnerState = state;
            State = new WorkflowStateWrapper
            {
                InnerState = Any.Pack(InnerState)
            };
            await PersistStateAsync();
            _hasPersistedState = true;
        }

        _context!.Send(_context.Sender!, new Empty());

        _logger.LogDebug("{self} exited TryInitializeAsync", Key);
    }

    protected Task<OperationResponse> PrepareAsync(string aggregateKind, string aggregateId, IMessage action)
    {
        return RequestOperationAsync(aggregateKind, aggregateId, action, OPERATION_TYPE.Prepare);
    }

    protected Task<OperationResponse> ConfirmAsync(string aggregateKind, string aggregateId, IMessage action)
    {
        return RequestOperationAsync(aggregateKind, aggregateId, action, OPERATION_TYPE.Confirm);
    }

    protected Task<OperationResponse> CancelAsync(string aggregateKind, string aggregateId, IMessage action)
    {
        return RequestOperationAsync(aggregateKind, aggregateId, action, OPERATION_TYPE.Cancel);
    }

    protected Task<OperationResponse> ExecuteAsync(string aggregateKind, string aggregateId, IMessage action)
    {
        return RequestOperationAsync(aggregateKind, aggregateId, action, OPERATION_TYPE.Execute);
    }

    private async Task<OperationResponse> RequestOperationAsync(string aggregateKind, string aggregateId, IMessage action, OPERATION_TYPE operationType)
    {
        AddAggregateToCleanUp(aggregateKind, aggregateId);

        var sequence = GetOrCreateAggregateSequence(aggregateKind, aggregateId);

        var operation = new Operation
        {
            WorkflowId = Key,
            Sequence = sequence + 1,
            OperationType = operationType,
            Action = Any.Pack(action)
        };

        var response = await RequestAggregateAsync<OperationResponse>(aggregateKind, aggregateId, operation);

        IncrementAggregateSequence(aggregateKind, aggregateId);

        return response;
    }

    private async Task<TResponse> RequestAggregateAsync<TResponse>(string aggregateKind, string aggregateId, IMessage action)
    {
        _logger.LogDebug("{self} entered RequestAggregateAsync<{responseType}>", Key, typeof(TResponse));

        var response = await _context!
            .Cluster()
            .RequestAsync<TResponse>(
                kind: aggregateKind,
                identity: aggregateId,
                message: action,
                ct: CancellationToken.None
            );

        _logger.LogDebug("{self} exited RequestAggregateAsync<{responseType}>", Key, typeof(TResponse));

        return response;
    }

    protected void AddAggregateToCleanUp(string aggregateKind, string aggregateId)
    {
        _logger.LogDebug("{self} entered AddAggregateToCleanUp", Key);

        var key = GetAggregateKey(aggregateKind, aggregateId);

        if (!State!.AggregatesToCleanUp.Contains(key))
        {
            State.AggregatesToCleanUp.Add(key);
        }

        _logger.LogDebug("{self} exited AddAggregateToCleanUp", Key);
    }

    private long GetOrCreateAggregateSequence(string aggregateKind, string aggregateId)
    {
        var aggregateKey = GetAggregateKey(aggregateKind, aggregateId);

        if (!_sequences.ContainsKey(aggregateKey))
        {
            _sequences.Add(aggregateKey, 0);
        }

        return _sequences[aggregateKey];
    }

    private void IncrementAggregateSequence(string aggregateKind, string aggregateId)
    {
        var aggregateKey = GetAggregateKey(aggregateKind, aggregateId);

        _sequences[aggregateKey]++;
    }

    private string GetAggregateKey(string aggregateKind, string aggregateId)
    {
        return $"{aggregateKind}/{aggregateId}";
    }

    private (string kind, string id) GetAggregateKindAndId(string aggregateKey)
    {
        var parts = aggregateKey.Split("/");
        var kind = string.Join("/", parts.Take(parts.Length - 1));
        var id = parts.Last();
        return (kind, id);
    }
}