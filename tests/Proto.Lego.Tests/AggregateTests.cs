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
    public async Task Prepare_WhenSequenceIsTooAhead_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var operation = GenerateTestActionOperation(caller, 2, true, stringToSave);
        var response = await client.PrepareTestAction(operation, CancellationToken.None);
        response!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Prepare_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var operation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var responseOne = await client.PrepareTestAction(operation, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var responseTwo = await client.PrepareTestAction(operation, CancellationToken.None);
        responseTwo!.ShouldBeEquivalentTo(responseOne);
    }

    [Fact]
    public async Task Confirm_WhenSequenceIsTooAhead_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var prepareOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(caller, 3, true, stringToSave);
        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Confirm_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var prepareOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(caller, 2, true, stringToSave);
        var confirmResponseOne = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponseOne!.Success.ShouldBe(true);

        var confirmResponseTwo = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponseTwo!.ShouldBeEquivalentTo(confirmResponseOne);
    }

    [Fact]
    public async Task Confirm_WhenActionWasNotPrepared_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var confirmOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Cancel_WhenSequenceIsTooAhead_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var prepareOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(caller, 3, true, stringToSave);
        var cancelResponse = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Cancel_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var prepareOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(caller, 2, true, stringToSave);
        var cancelResponseOne = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponseOne!.Success.ShouldBe(true);

        var cancelResponseTwo = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponseTwo!.ShouldBeEquivalentTo(cancelResponseOne);
    }

    [Fact]
    public async Task Cancel_WhenActionWasNotPrepared_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var cancelOperation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var cancelResponse = await client.ConfirmTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Execute_WhenSequenceIsTooAhead_ReturnsError()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var operation = GenerateTestActionOperation(caller, 2, true, stringToSave);
        var response = await client.ExecuteTestAction(operation, CancellationToken.None);
        response!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Execute_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var operation = GenerateTestActionOperation(caller, 1, true, stringToSave);
        var responseOne = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var responseTwo = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseTwo!.ShouldBeEquivalentTo(responseOne);
    }

    [Fact]
    public async Task GetState_ReturnsState()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var action = new TestActionRequest
        {
            ResultToReturn = true,
            StringToSave = stringToSave
        };

        var responseOne = await client.ExecuteTestAction(action, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var getStateResponse = await client.GetStateAsync(CancellationToken.None);
        getStateResponse!.Success.ShouldBe(true);
        getStateResponse.GetPayload<TestAggregateState>().SavedString.ShouldBe(stringToSave);
    }

    private Operation GenerateTestActionOperation(string caller, long sequence, bool resultToReturn, string stringToSave)
    {
        var testAction = new TestActionRequest
        {
            ResultToReturn = resultToReturn,
            StringToSave = stringToSave
        };

        var operation = new Operation
        {
            Caller = caller,
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