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
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 2, true, stringToSave);
        var response = await client.PrepareTestAction(operation, CancellationToken.None);
        response!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Prepare_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);
        var responseOne = await client.PrepareTestAction(operation, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var responseTwo = await client.PrepareTestAction(operation, CancellationToken.None);
        responseTwo!.ShouldBeEquivalentTo(responseOne);
    }

    [Fact]
    public async Task Confirm_WhenSequenceIsTooAhead_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var confirmOperation = GenerateTestActionOperation(callerId, 3, true, stringToSave);
        var confirmResponse = await client.ConfirmTestAction(confirmOperation, CancellationToken.None);
        confirmResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Confirm_WhenThereIsSavedResponse_ReturnsSavedResponse()
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
        confirmResponseTwo!.ShouldBeEquivalentTo(confirmResponseOne);
    }

    [Fact]
    public async Task Confirm_WhenActionWasNotPrepared_ReturnsError()
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
    public async Task Cancel_WhenSequenceIsTooAhead_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var prepareOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);
        var prepareResponse = await client.PrepareTestAction(prepareOperation, CancellationToken.None);
        prepareResponse!.Success.ShouldBe(true);

        var cancelOperation = GenerateTestActionOperation(callerId, 3, true, stringToSave);
        var cancelResponse = await client.CancelTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Cancel_WhenThereIsSavedResponse_ReturnsSavedResponse()
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
        cancelResponseTwo!.ShouldBeEquivalentTo(cancelResponseOne);
    }

    [Fact]
    public async Task Cancel_WhenActionWasNotPrepared_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var cancelOperation = GenerateTestActionOperation(callerId, 1, true, stringToSave);
        var cancelResponse = await client.ConfirmTestAction(cancelOperation, CancellationToken.None);
        cancelResponse!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Execute_WhenSequenceIsTooAhead_ReturnsError()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 2, true, stringToSave);
        var response = await client.ExecuteTestAction(operation, CancellationToken.None);
        response!.Success.ShouldBe(false);
    }

    [Fact]
    public async Task Execute_WhenThereIsSavedResponse_ReturnsSavedResponse()
    {
        var callerId = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();
        var client = Cluster.GetTestAggregate(aggregateId);

        var operation = GenerateTestActionOperation(callerId, 1, true, stringToSave);
        var responseOne = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseOne!.Success.ShouldBe(true);

        var responseTwo = await client.ExecuteTestAction(operation, CancellationToken.None);
        responseTwo!.ShouldBeEquivalentTo(responseOne);
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