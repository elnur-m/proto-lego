using Proto.Lego.Persistence.Tests.Common;

namespace Proto.Lego.Persistence.InMemory.Tests;

public class InMemoryWorkflowStoreTests : WorkflowStoreTestsBase, IClassFixture<InMemoryWorkflowStore>
{
    public InMemoryWorkflowStoreTests(InMemoryWorkflowStore workflowStore) : base(workflowStore)
    {
    }
}