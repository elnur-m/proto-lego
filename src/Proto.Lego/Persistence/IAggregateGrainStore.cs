using Proto.Lego.AggregateGrain;

namespace Proto.Lego.Persistence;

public interface IAggregateGrainStore
{
    Task<AggregateGrainStateWrapper?> GetAsync(string key);

    Task SetAsync(string key, AggregateGrainStateWrapper state);

    Task DeleteAsync(string key);
}