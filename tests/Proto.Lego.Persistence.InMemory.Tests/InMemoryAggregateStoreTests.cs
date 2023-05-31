using Proto.Lego.Persistence.Tests.Common;

namespace Proto.Lego.Persistence.InMemory.Tests;

public class InMemoryAggregateStoreTests : AggregateStoreTestsBase, IClassFixture<InMemoryAggregateStore>
{
    public InMemoryAggregateStoreTests(InMemoryAggregateStore aggregateStore) : base(aggregateStore)
    {
    }
}