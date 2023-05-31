using Proto.Lego.Aggregate;

namespace Proto.Lego.Persistence;

public interface IAggregateStore
{
    Task<AggregateStateWrapper?> GetAsync(string key);

    Task SetAsync(string key, AggregateStateWrapper state);

    Task DeleteAsync(string key);
}