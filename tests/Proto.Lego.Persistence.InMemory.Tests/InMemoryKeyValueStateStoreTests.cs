using Proto.Lego.Persistence.Tests.Common;

namespace Proto.Lego.Persistence.InMemory.Tests;

public class InMemoryKeyValueStateStoreTests : KeyValueStateStoreTestsBase, IClassFixture<InMemoryKeyValueStateStore>
{
    public InMemoryKeyValueStateStoreTests(InMemoryKeyValueStateStore keyValueStore) : base(keyValueStore)
    {
    }
}