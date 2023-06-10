using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Lego.Aggregate;
using Proto.Lego.CodeGen.Tests.Workflows;
using Proto.Lego.Persistence;
using Proto.Lego.Persistence.InMemory;
using Proto.Lego.Tests.Aggregates;
using Proto.Lego.Tests.Setup;
using Proto.Lego.Workflow;
using Shouldly;
using Xunit.Abstractions;

namespace Proto.Lego.Tests;

public class WorkflowTests : IAsyncDisposable, IClassFixture<InMemoryAggregateStore>,
    IClassFixture<InMemoryWorkflowStore>
{
    private readonly IHost _host;

    private Cluster.Cluster Cluster => _host.Services.GetRequiredService<ActorSystem>().Cluster();
    private IAggregateStore AggregateStore => _host.Services.GetRequiredService<IAggregateStore>();
    private IWorkflowStore WorkflowStore => _host.Services.GetRequiredService<IWorkflowStore>();

    public WorkflowTests(
        ITestOutputHelper outputHelper,
        InMemoryAggregateStore aggregateStore,
        InMemoryWorkflowStore workflowStore
    )
    {
        var hostBuilder = Host.CreateDefaultBuilder();

        hostBuilder.ConfigureServices(services =>
        {
            services.AddActorSystem("AggregateTests");
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
    public async Task ExecuteAsync_FlowIsCorrect()
    {
        var workflowId = Guid.NewGuid().ToString();
        var input = new TestWorkflowInput
        {
            AggregateOneId = Guid.NewGuid().ToString(),
            AggregateTwoId = Guid.NewGuid().ToString(),
            StringToSave = Guid.NewGuid().ToString()
        };

        var result = await Cluster
            .GetTestWorkflow(workflowId)
            .ExecuteAsync(input, CancellationToken.None);

        await Task.Delay(10);

        var aggregateOneState = await GetAggregateStateWrapperAsync(input.AggregateOneId);
        aggregateOneState!.CallerStates.ShouldBeEmpty();

        var aggregateTwoState = await GetAggregateStateWrapperAsync(input.AggregateTwoId);
        aggregateTwoState!.CallerStates.ShouldBeEmpty();

        var state = await GetWorkflowStateAsync(workflowId);

        state!.Completed.ShouldBeTrue();

        await Task.Delay(500);

        var stateAfterCleared = await GetWorkflowStateAsync(workflowId);
        stateAfterCleared.ShouldBeNull();
    }

    private async Task<AggregateStateWrapper?> GetAggregateStateWrapperAsync(string testAggregateId)
    {
        var key = $"{TestAggregateActor.Kind}/{testAggregateId}";

        var stateWrapper = await AggregateStore.GetAsync(key);

        return stateWrapper;
    }

    private async Task<WorkflowState?> GetWorkflowStateAsync(string workflowId)
    {
        var key = $"{TestWorkflowActor.Kind}/{workflowId}";

        var state = await WorkflowStore.GetAsync(key);

        return state;
    }
}