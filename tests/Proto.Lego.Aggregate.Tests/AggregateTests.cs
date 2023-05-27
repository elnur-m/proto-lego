using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Aggregate.Tests.Setup;
using Proto.Lego.Aggregate.Tests.TestAggregate;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.InMemory;
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Aggregate.Tests;

public class AggregateTests : IAsyncDisposable, IClassFixture<InMemoryKeyValueStateStore>, IClassFixture<InMemoryAliveWorkflowStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IKeyValueStateStore KeyValueStateStore => _host.Services.GetRequiredService<IKeyValueStateStore>();

    public AggregateTests(
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
    public async Task Prepare_IsSavedToState()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var response = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        response.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Prepare_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var responseOne = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        var responseTwo = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);

        responseTwo.ShouldBeEquivalentTo(responseOne);
    }

    [Fact]
    public async Task PrepareThenConfirm_ShouldSetName()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        prepareResponse.Success.ShouldBe(true);

        var confirmResponse = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Confirm, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(2);
        aggregateState.SavedString.ShouldBe(stringToSave);
    }

    [Fact]
    public async Task PrepareThenConfirm_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        var confirmResponseOne = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Confirm, aggregateId, stringToSave, true);
        var confirmResponseTwo = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Confirm, aggregateId, stringToSave, true);

        confirmResponseTwo.ShouldBeEquivalentTo(confirmResponseOne);
    }

    [Fact]
    public async Task Confirm_WhenNotPrepared_ReturnsError()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var confirmResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Confirm, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(false);
    }

    [Fact]
    public async Task PrepareThenConfirm_WhenWorkflowIdsAreDifferent_ShouldReturnError()
    {
        var workflowIdOne = Guid.NewGuid().ToString();
        var workflowIdTwo = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowIdOne, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        prepareResponse.Success.ShouldBe(true);

        var confirmResponse = await RequestTestActionAsync(workflowIdTwo, 2, OPERATION_TYPE.Confirm, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(false);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Cancel_WhenNotPrepared_ReturnsError()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var confirmResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Cancel, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(false);
    }

    [Fact]
    public async Task PrepareThenCancel_ShouldNotSetName()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        prepareResponse.Success.ShouldBe(true);

        var confirmResponse = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Cancel, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(2);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task PrepareThenCancel_WhenWorkflowIdsAreDifferent_ShouldReturnError()
    {
        var workflowIdOne = Guid.NewGuid().ToString();
        var workflowIdTwo = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowIdOne, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        prepareResponse.Success.ShouldBe(true);

        var confirmResponse = await RequestTestActionAsync(workflowIdTwo, 2, OPERATION_TYPE.Cancel, aggregateId, stringToSave, true);
        confirmResponse.Success.ShouldBe(false);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task PrepareThenCancel_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var prepareResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Prepare, aggregateId, stringToSave, true);
        var cancelResponseOne = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Cancel, aggregateId, stringToSave, true);
        var cancelResponseTwo = await RequestTestActionAsync(workflowId, 2, OPERATION_TYPE.Cancel, aggregateId, stringToSave, true);

        cancelResponseTwo.ShouldBeEquivalentTo(cancelResponseOne);
    }

    [Fact]
    public async Task Execute_ShouldSetName()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var executeResponse = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Execute, aggregateId, stringToSave, true);
        executeResponse.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(stringToSave);
    }

    [Fact]
    public async Task Execute_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var workflowId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var executeResponseOne = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Execute, aggregateId, stringToSave, true);
        var executeResponseTwo = await RequestTestActionAsync(workflowId, 1, OPERATION_TYPE.Execute, aggregateId, stringToSave, true);

        executeResponseTwo.ShouldBeEquivalentTo(executeResponseOne);
    }

    private async Task<OperationResponse> RequestTestActionAsync(
        string workflowId,
        long sequence,
        OPERATION_TYPE operationType,
        string testAggregateId,
        string stringToSave,
        bool resultToReturn
    )
    {
        var setName = new TestAction
        {
            StringToSave = stringToSave,
            ResultToReturn = resultToReturn
        };

        var operation = new Operation
        {
            WorkflowId = workflowId,
            Sequence = sequence,
            OperationType = operationType,
            Action = Any.Pack(setName)
        };

        var response = await Cluster.RequestAsync<OperationResponse>(
            identity: testAggregateId,
            kind: TestAggregate.TestAggregate.AggregateKind,
            message: operation,
            ct: CancellationToken.None
        );

        response.ShouldBeOfType<OperationResponse>();

        return response;
    }

    private async Task<TestAggregateState?> GetAggregateStateAsync(string testAggregateId)
    {
        var key = $"{TestAggregate.TestAggregate.AggregateKind}/{testAggregateId}";

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
}