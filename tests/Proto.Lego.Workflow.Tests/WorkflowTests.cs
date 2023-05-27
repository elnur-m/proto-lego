using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Aggregate.Tests.TestAggregate;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.InMemory;
using Proto.Lego.Workflow.Tests.Setup;
using Proto.Lego.Workflow.Tests.TestWorkflow;
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Workflow.Tests;

public class WorkflowTests : IAsyncDisposable, IClassFixture<InMemoryKeyValueStateStore>, IClassFixture<InMemoryAliveWorkflowStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IKeyValueStateStore KeyValueStateStore => _host.Services.GetRequiredService<IKeyValueStateStore>();

    public WorkflowTests(
        ITestOutputHelper outputHelper,
        InMemoryKeyValueStateStore stateStore,
        InMemoryAliveWorkflowStore aliveWorkflowStore
    )
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddActorSystem("TestOne");
            services.AddHostedService<ActorSystemClusterHostedService>();
            services.AddSingleton<IKeyValueStateStore>(stateStore);
            services.AddSingleton<IAliveWorkflowStore>(aliveWorkflowStore);
        });

        hostBuilder.ConfigureLogging(builder =>
        {
            builder.Services.AddLogging(logger => logger.AddXUnit(outputHelper));
        });

        _host = hostBuilder.Build();

        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        Log.SetLoggerFactory(loggerFactory);

        _host.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreparesSucceed_ConfirmsBothOperations()
    {
        var aggregateOneId = Guid.NewGuid().ToString();
        var aggregateTwoId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var resultToReturnOne = true;
        var resultToReturnTwo = true;
        var workflowId = Guid.NewGuid().ToString();

        var workflowState = new TestWorkflowState
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo
        };

        await RequestWorkflowAsync(workflowId, workflowState);
        await Task.Delay(100);

        var aggregateOneState = await GetAggregateStateAsync(aggregateOneId);

        aggregateOneState.ShouldNotBeNull();
        aggregateOneState.OperationsPerformed.ShouldBe(2);
        aggregateOneState.SavedString.ShouldBe(stringToSave);

        var aggregateTwoState = await GetAggregateStateAsync(aggregateTwoId);

        aggregateTwoState.ShouldNotBeNull();
        aggregateTwoState.OperationsPerformed.ShouldBe(2);
        aggregateTwoState.SavedString.ShouldBe(stringToSave);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnyOfPreparesFails_CancelsBothOperations()
    {
        var aggregateOneId = Guid.NewGuid().ToString();
        var aggregateTwoId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var resultToReturnOne = true;
        var resultToReturnTwo = false;
        var workflowId = Guid.NewGuid().ToString();

        var workflowState = new TestWorkflowState
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo,
        };

        await RequestWorkflowAsync(workflowId, workflowState);
        await Task.Delay(100);

        var aggregateOneState = await GetAggregateStateAsync(aggregateOneId);

        aggregateOneState.ShouldNotBeNull();
        aggregateOneState.OperationsPerformed.ShouldBe(2);
        aggregateOneState.SavedString.ShouldBe(string.Empty);

        var aggregateTwoState = await GetAggregateStateAsync(aggregateTwoId);

        aggregateTwoState.ShouldNotBeNull();
        aggregateTwoState.OperationsPerformed.ShouldBe(1);
        aggregateTwoState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_TellsToInvolvedAggregatesToWipeWorkflowState()
    {
        var aggregateOneId = Guid.NewGuid().ToString();
        var aggregateTwoId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var resultToReturnOne = true;
        var resultToReturnTwo = true;
        var workflowId = Guid.NewGuid().ToString();

        var workflowState = new TestWorkflowState
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo
        };

        await RequestWorkflowAsync(workflowId, workflowState);
        await Task.Delay(100);

        var aggregateOneStateWrapper = await GetAggregateStateWrapperAsync(aggregateOneId);

        aggregateOneStateWrapper.ShouldNotBeNull();
        aggregateOneStateWrapper.WorkflowStates.ShouldNotContainKey(workflowId);

        var aggregateTwoStateWrapper = await GetAggregateStateWrapperAsync(aggregateTwoId);

        aggregateTwoStateWrapper.ShouldNotBeNull();
        aggregateTwoStateWrapper.WorkflowStates.ShouldNotContainKey(workflowId);
    }

    private async Task RequestWorkflowAsync(string workflowId, TestWorkflowState state)
    {
        await Cluster.RequestAsync<Empty>(
            identity: workflowId,
            kind: TestWorkflow.TestWorkflow.WorkflowKind,
            message: state,
            ct: CancellationToken.None
        );
    }

    private async Task<TestAggregateState?> GetAggregateStateAsync(string testAggregateId)
    {
        var key = $"{TestAggregate.AggregateKind}/{testAggregateId}";

        var aggregateStateWrapperBytes = await KeyValueStateStore.GetAsync(key);

        if (aggregateStateWrapperBytes == null)
        {
            return null;
        }

        var aggregateStateWrapper = AggregateStateWrapper.Parser.ParseFrom(aggregateStateWrapperBytes);
        aggregateStateWrapper.ShouldNotBeNull();

        var aggregateState = aggregateStateWrapper.InnerState.Unpack<TestAggregateState>();
        return aggregateState;
    }

    private async Task<AggregateStateWrapper?> GetAggregateStateWrapperAsync(string testAggregateId)
    {
        var key = $"{TestAggregate.AggregateKind}/{testAggregateId}";

        var aggregateStateWrapperBytes = await KeyValueStateStore.GetAsync(key);

        if (aggregateStateWrapperBytes == null)
        {
            return null;
        }

        var aggregateStateWrapper = AggregateStateWrapper.Parser.ParseFrom(aggregateStateWrapperBytes);
        return aggregateStateWrapper;
    }
}