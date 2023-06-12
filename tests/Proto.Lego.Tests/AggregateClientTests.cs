using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Persistence.InMemory;
using Proto.Lego.Persistence;
using Proto.Lego.Tests.Aggregates;
using Proto.Lego.Tests.Setup;
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Tests;

public class AggregateClientTests : IAsyncDisposable, IClassFixture<InMemoryAggregateStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IAggregateStore AggregateStore => _host.Services.GetRequiredService<IAggregateStore>();

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
    }

    public AggregateClientTests(
        ITestOutputHelper outputHelper,
        InMemoryAggregateStore aggregateStore
    )
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddActorSystem("AggregateClientTests");
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Prepare_SentOperationIsCorrect(int times)
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId, caller);

        for (int i = 0; i < times; i++)
        {
            var request = new TestActionRequest
            {
                ResultToReturn = true,
                StringToSave = stringToSave
            };

            var response = await client.PrepareTestAction(request, CancellationToken.None);
            response!.Success.ShouldBeTrue();

            await client.ClearAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Confirm_SentOperationIsCorrect(int times)
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId, caller);

        for (int i = 0; i < times; i++)
        {
            var request = new TestActionRequest
            {
                ResultToReturn = true,
                StringToSave = stringToSave
            };

            var prepareResponse = await client.PrepareTestAction(request, CancellationToken.None);
            prepareResponse!.Success.ShouldBeTrue();

            var confirmResponse = await client.ConfirmTestAction(request, CancellationToken.None);
            confirmResponse!.Success.ShouldBeTrue();

            await client.ClearAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cancel_SentOperationIsCorrect(int times)
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId, caller);

        for (int i = 0; i < times; i++)
        {
            var request = new TestActionRequest
            {
                ResultToReturn = true,
                StringToSave = stringToSave
            };

            var prepareResponse = await client.PrepareTestAction(request, CancellationToken.None);
            prepareResponse!.Success.ShouldBeTrue();

            var cancelResponse = await client.CancelTestAction(request, CancellationToken.None);
            cancelResponse!.Success.ShouldBeTrue();

            await client.ClearAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Execute_SentOperationIsCorrect(int times)
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();
        var stringToSave = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId, caller);

        for (int i = 0; i < times; i++)
        {
            var request = new TestActionRequest
            {
                ResultToReturn = true,
                StringToSave = stringToSave
            };

            var response = await client.ExecuteTestAction(request, CancellationToken.None);
            response!.Success.ShouldBeTrue();

            await client.ClearAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Clear_SendsWipeCallerState()
    {
        var caller = Guid.NewGuid().ToString();
        var aggregateId = Guid.NewGuid().ToString();

        var client = Cluster.GetTestAggregate(aggregateId, caller);

        var clearResponse = await client.ClearAsync(CancellationToken.None);
        clearResponse.ShouldNotBeNull();
    }
}