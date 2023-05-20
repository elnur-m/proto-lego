using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Workflow.Messages;
using Proto.Lego.Workflow.Persistence;

namespace Proto.Lego.Workflow;

public abstract class Workflow<TState> : IActor where TState : IMessage, new()
{
    private readonly IWorkflowStateStore _stateStore;
    private readonly IAliveWorkflowStore _aliveWorkflowStore;

    protected WorkflowStateWrapper? State;

    protected string? Kind;
    protected string? Id;
    protected string PersistenceId => $"{Kind}/{Id}";
    protected string Key => PersistenceId;
    private IContext? _context;

    private readonly Dictionary<string, long> _sequences = new();
    private bool _hasPersistedState;
    private bool _isExecuting;

    protected Workflow(IWorkflowStateStore stateStore, IAliveWorkflowStore aliveWorkflowStore)
    {
        _stateStore = stateStore;
        _aliveWorkflowStore = aliveWorkflowStore;
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

    protected TState GetInnerState()
    {
        return State!.InnerState.Unpack<TState>();
    }

    protected void SetInnerState(TState innerState)
    {
        State!.InnerState = Any.Pack(innerState);
    }

    protected virtual byte[] SerializeState()
    {
        return State.ToByteArray();
    }

    protected virtual WorkflowStateWrapper DeserializeState(byte[] bytes)
    {
        return WorkflowStateWrapper.Parser.ParseFrom(bytes);
    }

    protected abstract Task ExecuteAsync();

    private void ExecuteInBackground()
    {
        if (State!.Completed)
        {
            return;
        }

        var executeTask = ExecuteAsync();
        _isExecuting = true;

        _context!.ReenterAfter(executeTask, () =>
        {
            _isExecuting = false;
        });
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
        }
    }

    private async Task HandleTriggerAsync()
    {
        if (_context!.Sender != null)
        {
            _context.Send(_context.Sender, new Empty());
        }

        if (State == null)
        {
            Console.WriteLine($"Workflow {Key} probably was finished, but not delete from the Alive store. Deleting and stopping.");
            await _aliveWorkflowStore.DeleteAsync(Key);
            _context.Self.Stop(_context.System);
            return;
        }

        ExecuteInBackground();
    }

    protected async Task CleanUpAndStopAsync()
    {
        await _aliveWorkflowStore.DeleteAsync(Key);
        await _stateStore.DeleteAsync(Key);
        _hasPersistedState = false;
        _context!.Self.Stop(_context.System);
    }

    private async Task TryInitializeAsync(TState state)
    {
        if (!_hasPersistedState)
        {
            await _aliveWorkflowStore.PutAsync(Key);

            State = new WorkflowStateWrapper
            {
                InnerState = Any.Pack(state)
            };
            await PersistStateAsync();
            _hasPersistedState = true;
        }

        _context!.Send(_context.Sender!, new Empty());
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
        var sequence = GetOrCreateAggregateSequence(aggregateKind, aggregateId);

        var operation = new Operation
        {
            WorkflowId = Key,
            Sequence = sequence + 1,
            OperationType = operationType,
            Action = Any.Pack(action)
        };

        var response = await _context!
            .Cluster()
            .RequestAsync<OperationResponse>(
                kind: aggregateKind,
                identity: aggregateId,
                message: operation,
                ct: CancellationToken.None
            );

        IncrementAggregateSequence(aggregateKind, aggregateId);

        return response;
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
}