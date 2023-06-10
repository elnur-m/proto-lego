using Microsoft.Extensions.DependencyInjection;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.DependencyInjection;
using Proto.Lego.AggregateGrain;
using Proto.Lego.CodeGen.Tests.Aggregates;
using Proto.Lego.CodeGen.Tests.Workflows;
using Proto.Lego.Workflow;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace Proto.Lego.CodeGen.Tests.Setup;

public static class ActorSystemConfiguration
{
    public static void AddActorSystem(this IServiceCollection serviceCollection, string clusterName)
    {
        serviceCollection.AddSingleton(provider =>
        {
            // actor system configuration

            var actorSystemConfig = ActorSystemConfig
                .Setup();

            // remote configuration

            var remoteConfig = GrpcNetRemoteConfig
                    .BindToLocalhost()
                    .WithProtoMessages(AggregateGrainReflection.Descriptor)
                    .WithProtoMessages(WorkflowReflection.Descriptor)
                    .WithProtoMessages(TestAggregateReflection.Descriptor)
                    .WithProtoMessages(TestWorkflowReflection.Descriptor)
                ;

            // cluster configuration

            var clusterConfig = ClusterConfig
                    .Setup(
                        clusterName: clusterName,
                        clusterProvider: new TestProvider(new TestProviderOptions(), new InMemAgent()),
                        identityLookup: new PartitionIdentityLookup()
                    )
                    .WithClusterKind(
                        kind: TestAggregateActor.Kind,
                        Props.FromProducer(() => new TestAggregateActor((context, identity) =>
                            ActivatorUtilities.CreateInstance<TestAggregate>(provider, context, identity)))
                    )
                    .WithClusterKind(
                        kind: TestWorkflowActor.Kind,
                        Props.FromProducer(() => new TestWorkflowActor((context, identity) =>
                            ActivatorUtilities.CreateInstance<TestWorkflow>(provider, context, identity)))
                    )
                ;

            // create the actor system

            return new ActorSystem(actorSystemConfig)
                .WithServiceProvider(provider)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);
        });
    }
}