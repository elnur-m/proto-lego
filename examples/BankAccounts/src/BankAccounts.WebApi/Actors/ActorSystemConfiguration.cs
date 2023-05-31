using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows;
using BankAccounts.Workflows.AddFunds;
using BankAccounts.Workflows.CreateAccount;
using BankAccounts.Workflows.TransferFunds;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.DependencyInjection;
using Proto.Lego.Aggregate;
using Proto.Lego.Workflow;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace BankAccounts.WebApi.Actors;

public static class ActorSystemConfiguration
{
    public static void AddActorSystem(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(provider =>
        {
            // actor system configuration

            var actorSystemConfig = ActorSystemConfig
                .Setup();

            // remote configuration

            var remoteConfig = GrpcNetRemoteConfig
                    .BindToLocalhost()
                    .WithProtoMessages(AggregateReflection.Descriptor)
                    .WithProtoMessages(WorkflowReflection.Descriptor)
                    .WithProtoMessages(AccountAggregateReflection.Descriptor)
                    .WithProtoMessages(CreateAccountWorkflowReflection.Descriptor)
                    .WithProtoMessages(TransferFundsWorkflowReflection.Descriptor)
                    .WithProtoMessages(AddFundsWorkflowReflection.Descriptor)
                ;

            // cluster configuration

            var clusterConfig = ClusterConfig
                    .Setup(
                        clusterName: "BankAccounts",
                        clusterProvider: new TestProvider(new TestProviderOptions(), new InMemAgent()),
                        identityLookup: new PartitionIdentityLookup()
                    )
                    .WithClusterKind(
                        kind: AccountAggregate.AggregateKind,
                        Props.FromProducer(() => ActivatorUtilities.CreateInstance<AccountAggregate>(provider))
                    )
                    .WithClusterKind(
                        kind: CreateAccountWorkflow.WorkflowKind,
                        Props.FromProducer(() => ActivatorUtilities.CreateInstance<CreateAccountWorkflow>(provider))
                    )
                    .WithClusterKind(
                        kind: AddFundsWorkflow.WorkflowKind,
                        Props.FromProducer(() => ActivatorUtilities.CreateInstance<AddFundsWorkflow>(provider))
                    )
                    .WithClusterKind(
                        kind: TransferFundsWorkflow.WorkflowKind,
                        Props.FromProducer(() => ActivatorUtilities.CreateInstance<TransferFundsWorkflow>(provider))
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