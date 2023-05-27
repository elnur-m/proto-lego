using Microsoft.Extensions.DependencyInjection;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.DependencyInjection;
using Proto.Lego.Aggregate.Messages;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace Proto.Lego.Aggregate.Tests.Setup;

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
                .WithProtoMessages(MessagesReflection.Descriptor)
                .WithProtoMessages(TestAggregate.TestAggregateReflection.Descriptor)
                ;

            // cluster configuration

            var clusterConfig = ClusterConfig
                .Setup(
                    clusterName: clusterName,
                    clusterProvider: new TestProvider(new TestProviderOptions(), new InMemAgent()),
                    identityLookup: new PartitionIdentityLookup()
                )
                .WithClusterKind(
                    kind: TestAggregate.TestAggregate.AggregateKind,
                    Props.FromProducer(() => ActivatorUtilities.CreateInstance<TestAggregate.TestAggregate>(provider))
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