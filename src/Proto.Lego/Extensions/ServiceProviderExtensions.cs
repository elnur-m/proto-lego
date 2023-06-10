using Microsoft.Extensions.DependencyInjection;
using Proto.Cluster;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace Proto.Lego.Extensions;

public static class ServiceProviderExtensions
{
    public static void UseWorkflowTriggering(this IServiceProvider provider)
    {
        var workflowStore = provider.GetRequiredService<IWorkflowStore>();
        var actorSystem = provider.GetRequiredService<ActorSystem>();
        var cluster = actorSystem.Cluster();

        actorSystem.EventStream.Subscribe<ClusterTopology>(async topology =>
        {
            if (!topology.Members.Any())
            {
                return;
            }

            if (topology.Members.OrderBy(x => x.ToString()).First().Id != actorSystem.Id)
            {
                return;
            }

            await workflowStore.ActOnAllAsync(async (key, state) =>
            {
                var split = key.Split('/');
                var kind = string.Join('/', split.Take(split.Length - 1));
                var identity = split.Last();
                await cluster.RequestAsync<WorkflowResult>(identity, kind, new Trigger(), CancellationToken.None);
            });
        });
    }
}