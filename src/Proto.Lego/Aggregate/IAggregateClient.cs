using Google.Protobuf.WellKnownTypes;

namespace Proto.Lego.Aggregate;

public interface IAggregateClient<TClient> : IAggregateClient
{
    static abstract TClient Create(Cluster.Cluster cluster, string identity, string caller);
}

public interface IAggregateClient
{
    Task<Empty?> ClearAsync(CancellationToken cancellationToken);
}