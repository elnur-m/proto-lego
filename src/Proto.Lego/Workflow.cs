using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace Proto.Lego;

public abstract class Workflow<TInput> : IActor where TInput : IMessage, new()
{
    private readonly IWorkflowStore _store;
    protected readonly ILogger<Workflow<TInput>> Logger;

    protected WorkflowState? State;

    protected string? Kind;
    protected string? Id;
    protected string PersistenceId => $"{Kind}/{Id}";
    protected string Key => PersistenceId;
    private IContext? _context;

    private readonly Dictionary<string, long> _sequences = new();
    private readonly List<PID> _completedStateSubscribers = new();
    private bool _hasPersistedState;
    private bool _isExecuting;
    private bool _isCleaningUp;
    private bool IsBusy => _isExecuting || _isCleaningUp;

    protected Workflow(IWorkflowStore store, ILogger<Workflow<TInput>> logger)
    {
        _store = store;
        Logger = logger;
    }

    public async Task ReceiveAsync(IContext context)
    {
        Logger.LogDebug("{self} received {message}", Key, context.Message);
        _context = context;

        switch (context.Message)
        {
            case Started:
                Id = context.Get<ClusterIdentity>()!.Identity;
                await RecoverStateAsync();
                break;

            case Trigger:
                _context!.Send(_context.Sender!, new Empty());
                if (!IsBusy)
                {
                    ExecuteInBackground(State!.Input.Unpack<TInput>());
                }
                break;

            case TInput rawInput:
                if (!IsBusy)
                {
                    await TryInitializeAsync(rawInput);
                    ExecuteInBackground(rawInput);
                }
                _context!.Send(_context.Sender!, new Empty());
                break;

            case GetCurrentState:
                HandleGetCurrentState();
                break;

            case GetStateWhenCompleted:
                if (State == null)
                {
                    _context!.Send(_context.Sender!, new Empty());
                    return;
                }
                HandleGetStateWhenCompleted();
                break;
        }
    }

    protected abstract Task ExecuteWorkflowAsync(TInput input);

    protected async Task CleanUpAggregatesAsync()
    {
        Logger.LogDebug("{self} entered CleanUpAggregatesAsync", Key);

        var tasks = State!.AggregatesToCleanUp
            .Select(GetAggregateKindAndId)
            .Select(kindAndId => RequestAggregateAsync<Empty>(kindAndId.kind, kindAndId.id, new WipeWorkflowState
            {
                WorkflowId = Key
            }));

        await Task.WhenAll(tasks);

        Logger.LogDebug("{self} exited CleanUpAggregatesAsync", Key);
    }

    private void CleanUpInBackground()
    {
        Logger.LogDebug("{self} entered CleanUpInBackground", Key);

        _isCleaningUp = true;

        var cleanUpTask = CleanUpAsync();
        _context!.ReenterAfter(cleanUpTask, () =>
        {
            _isCleaningUp = false;
            Stop();
            Logger.LogDebug("{self} exited CleanUpInBackground", Key);
        });
    }

    protected virtual Task BeforeCleanUpAsync()
    {
        return Task.CompletedTask;
    }

    private async Task CleanUpAsync()
    {
        Logger.LogDebug("{self} entered CleanUpAsync", Key);

        await BeforeCleanUpAsync();
        await CleanUpAggregatesAsync();
        await RemoveFromStoreAsync();

        Logger.LogDebug("{self} exited CleanUpAsync", Key);
    }

    private void ExecuteInBackground(TInput input)
    {
        Logger.LogDebug("{self} entered ExecuteInBackground", Key);

        if (!State!.Result.Completed)
        {
            var executeTask = ExecuteWorkflowAsync(input);
            _isExecuting = true;

            _context!.ReenterAfter(executeTask, () =>
            {
                _isExecuting = false;
                State.Result.Completed = true;
                _completedStateSubscribers.ForEach(x => _context.Send(x, State));
                _completedStateSubscribers.Clear();
                PersistStateAsync().GetAwaiter().GetResult();
                CleanUpInBackground();

                Logger.LogDebug("{self} exited ExecuteInBackground", Key);
            });
        }
        else
        {
            CleanUpInBackground();

            Logger.LogDebug("{self} exited ExecuteInBackground", Key);
        }
    }

    private async Task RecoverStateAsync()
    {
        Logger.LogDebug("{self} entered RecoverStateAsync", Key);

        var state = await _store.GetAsync(Key);
        if (state != null)
        {
            _hasPersistedState = true;
            State = state;
        }

        Logger.LogDebug("{self} exited RecoverStateAsync", Key);
    }

    private async Task PersistStateAsync()
    {
        Logger.LogDebug("{self} entered PersistStateAsync", Key);

        await _store.SetAsync(Key, State!);

        Logger.LogDebug("{self} exited PersistStateAsync", Key);
    }

    protected async Task RemoveFromStoreAsync()
    {
        Logger.LogDebug("{self} entered RemoveFromStoreAsync", Key);

        await _store.DeleteAsync(Key);
        _hasPersistedState = false;

        Logger.LogDebug("{self} exited RemoveFromStoreAsync", Key);
    }

    protected void Stop()
    {
        _context!.Self.Stop(_context.System);
    }

    private async Task TryInitializeAsync(TInput state)
    {
        Logger.LogDebug("{self} entered TryInitializeAsync", Key);

        if (!_hasPersistedState)
        {
            State = new WorkflowState
            {
                Input = Any.Pack(state),
                Result = new WorkflowResult()
            };
            await PersistStateAsync();
            _hasPersistedState = true;
        }

        Logger.LogDebug("{self} exited TryInitializeAsync", Key);
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
        Logger.LogDebug("{self} entered RequestAggregateAsync<{responseType}>", Key, typeof(TResponse));

        var response = await _context!
            .Cluster()
            .RequestAsync<TResponse>(
                kind: aggregateKind,
                identity: aggregateId,
                message: action,
                ct: CancellationToken.None
            );

        Logger.LogDebug("{self} exited RequestAggregateAsync<{responseType}>", Key, typeof(TResponse));

        return response;
    }

    protected void AddAggregateToCleanUp(string aggregateKind, string aggregateId)
    {
        Logger.LogDebug("{self} entered AddAggregateToCleanUp", Key);

        var key = GetAggregateKey(aggregateKind, aggregateId);

        if (!State!.AggregatesToCleanUp.Contains(key))
        {
            State.AggregatesToCleanUp.Add(key);
        }

        Logger.LogDebug("{self} exited AddAggregateToCleanUp", Key);
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

    private void HandleGetCurrentState()
    {
        _context!.Send(_context.Sender!, State == null ? new Empty() : State);
    }

    private void HandleGetStateWhenCompleted()
    {
        if (State!.Result.Completed)
        {
            _context!.Send(_context.Sender!, State!);
        }
        else
        {
            _completedStateSubscribers.Add(_context!.Sender!);
        }
    }
}