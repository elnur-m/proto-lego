using Proto.Lego.Persistence.Tests.Common;

namespace Proto.Lego.Persistence.InMemory.Tests;

public class InMemoryAliveWorkflowState : AliveWorkflowStoreTestsBase, IClassFixture<InMemoryAliveWorkflowStore>
{
    public InMemoryAliveWorkflowState(InMemoryAliveWorkflowStore aliveWorkflowStore) : base(aliveWorkflowStore)
    {
    }
}