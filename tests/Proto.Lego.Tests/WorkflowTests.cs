using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.InMemory;
using Proto.Lego.Tests.Aggregates;
using Proto.Lego.Tests.Setup;
using Proto.Lego.Tests.Workflows;
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Tests;

public class WorkflowTests : IAsyncDisposable, IClassFixture<InMemoryAggregateStore>, IClassFixture<InMemoryWorkflowStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IWorkflowStore WorkflowStore => _host.Services.GetRequiredService<IWorkflowStore>();
    private IAggregateStore AggregateStore => _host.Services.GetRequiredService<IAggregateStore>();

    public WorkflowTests(
        ITestOutputHelper outputHelper,
        InMemoryAggregateStore aggregateStore,
        InMemoryWorkflowStore workflowStore
    )
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddActorSystem("TestOne");
            services.AddHostedService<ActorSystemClusterHostedService>();
            services.AddSingleton<IAggregateStore>(aggregateStore);
            services.AddSingleton<IWorkflowStore>(workflowStore);
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

        var input = new TestWorkflowInput
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo
        };

        await RequestWorkflowAsync(workflowId, input);
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

        var input = new TestWorkflowInput
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo,
        };

        await RequestWorkflowAsync(workflowId, input);
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

        var input = new TestWorkflowInput
        {
            AggregateOneId = aggregateOneId,
            AggregateTwoId = aggregateTwoId,
            StringToSave = stringToSave,
            ResultToReturnOne = resultToReturnOne,
            ResultToReturnTwo = resultToReturnTwo
        };

        await RequestWorkflowAsync(workflowId, input);
        await Task.Delay(100);

        var aggregateOneStateWrapper = await GetAggregateStateWrapperAsync(aggregateOneId);

        aggregateOneStateWrapper.ShouldNotBeNull();
        aggregateOneStateWrapper.WorkflowStates.ShouldNotContainKey(workflowId);

        var aggregateTwoStateWrapper = await GetAggregateStateWrapperAsync(aggregateTwoId);

        aggregateTwoStateWrapper.ShouldNotBeNull();
        aggregateTwoStateWrapper.WorkflowStates.ShouldNotContainKey(workflowId);
    }

    private async Task RequestWorkflowAsync(string workflowId, TestWorkflowInput input)
    {
        await Cluster.RequestAsync<Empty>(
            identity: workflowId,
            kind: TestWorkflow.WorkflowKind,
            message: input,
            ct: CancellationToken.None
        );
    }

    private async Task<TestAggregateState?> GetAggregateStateAsync(string testAggregateId)
    {
        var key = $"{TestAggregate.AggregateKind}/{testAggregateId}";

        var aggregateStateWrapper = await AggregateStore.GetAsync(key);

        if (aggregateStateWrapper == null)
        {
            return null;
        }

        var aggregateState = aggregateStateWrapper.InnerState.Unpack<TestAggregateState>();
        return aggregateState;
    }

    private async Task<AggregateStateWrapper?> GetAggregateStateWrapperAsync(string testAggregateId)
    {
        var key = $"{TestAggregate.AggregateKind}/{testAggregateId}";

        var aggregateStateWrapper = await AggregateStore.GetAsync(key);

        return aggregateStateWrapper;
    }
}