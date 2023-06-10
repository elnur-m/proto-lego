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
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Tests;

public class AggregateTests : IAsyncDisposable, IClassFixture<InMemoryAggregateStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IAggregateStore AggregateStore => _host.Services.GetRequiredService<IAggregateStore>();

    public AggregateTests(
        ITestOutputHelper outputHelper,
        InMemoryAggregateStore aggregateStore
    )
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddActorSystem("AggregateTests");
            services.AddHostedService<ActorSystemClusterHostedService>();
            services.AddSingleton<IAggregateStore>(aggregateStore);
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
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var response = await client.PrepareTestAction(operation, CancellationToken.None);

        response!.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Prepare_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var responseOne = await client.PrepareTestAction(operation, CancellationToken.None);
        var responseTwo = await client.PrepareTestAction(operation, CancellationToken.None);

        responseTwo.ShouldBeEquivalentTo(responseOne);
    }

    [Fact]
    public async Task PrepareThenConfirm_ShouldSetString()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(callerId, 2, true, stringToSave);

        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(2);
        aggregateState.SavedString.ShouldBe(stringToSave);
    }

    [Fact]
    public async Task PrepareThenConfirm_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(callerId, 2, true, stringToSave);

        var confirmResponseOne = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponseOne!.Success.ShouldBe(true);

        var confirmResponseTwo = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponseTwo!.Success.ShouldBe(true);

        confirmResponseTwo.ShouldBeEquivalentTo(confirmResponseOne);
    }

    [Fact]
    public async Task Confirm_WhenNotPrepared_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var confirmOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task PrepareThenConfirm_WhenCallerIdsAreDifferent_ShouldReturnError()
    {
        var callerIdOne = Guid.NewGuid().ToString();
        var callerIdTwo = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerIdOne, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(callerIdTwo, 2, true, stringToSave);

        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(false);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Cancel_WhenNotPrepared_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var confirmOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);

        confirmResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task PrepareThenCancel_ShouldNotSetString()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(callerId, 2, true, stringToSave);

        var cancelResponse = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(2);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task PrepareThenCancel_WhenCallerIdsAreDifferent_ShouldReturnError()
    {
        var callerIdOne = Guid.NewGuid().ToString();
        var callerIdTwo = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerIdOne, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(callerIdTwo, 2, true, stringToSave);

        var cancelResponse = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(false);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task PrepareThenCancel_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(callerId, 2, true, stringToSave);

        var cancelResponseOne = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponseOne!.Success.ShouldBe(true);

        var cancelResponseTwo = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponseTwo!.Success.ShouldBe(true);

        cancelResponseTwo.ShouldBeEquivalentTo(cancelResponseOne);

        cancelResponseTwo.ShouldBeEquivalentTo(cancelResponseOne);
    }

    [Fact]
    public async Task Execute_ShouldSetName()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var response = await client.ExecuteTestAction(operation, CancellationToken.None);
        response!.Success.ShouldBe(true);

        var aggregateState = await GetAggregateStateAsync(aggregateId);
        aggregateState.ShouldNotBeNull();
        aggregateState.OperationsPerformed.ShouldBe(1);
        aggregateState.SavedString.ShouldBe(stringToSave);
    }

    [Fact]
    public async Task Execute_WhenSentWithPreviousSequence_ShouldReturnSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);

        var responseOne = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var responseTwo = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseTwo!.Success.ShouldBe(true);

        responseTwo.ShouldBeEquivalentTo(responseOne);
    }

    private Operation GenerateTestActionOperation(string callerId, long sequence, bool resultToReturn, string stringToSave)
    {
        var testAction = new TestActionRequest
        {
            ResultToReturn = resultToReturn,
            StringToSave = stringToSave
        };

        var operation = new Operation
        {
            CallerId = callerId,
            Sequence = sequence,
            Action = Any.Pack(testAction)
        };

        return operation;
    }

    private async Task<TestAggregateState?> GetAggregateStateAsync(string testAggregateId)
    {
        var key = $"{TestAggregateActor.Kind}/{testAggregateId}";

        var aggregateStateWrapper = await AggregateStore.GetAsync(key);

        if (aggregateStateWrapper == null)
        {
            return null;
        }
        var aggregateState = aggregateStateWrapper.InnerState.Unpack<TestAggregateState>();
        return aggregateState;
    }
}