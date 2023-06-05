using Microsoft.AspNetCore.Mvc;
using Proto;
using Proto.Cluster;
using Proto.Lego.Workflow;

namespace BankAccounts.WebApi.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    protected readonly ActorSystem ActorSystem;

    protected AppControllerBase(ActorSystem actorSystem)
    {
        ActorSystem = actorSystem;
    }

    protected async Task<WorkflowResult?> GetWorkflowResultAsync(string kind, string id)
    {
        var state = await ActorSystem.Cluster().RequestAsync<WorkflowState>(
            kind: kind,
            identity: id,
            message: new GetStateWhenCompleted(),
            ct: CancellationToken.None
        );

        var result = state.Result;

        return result;
    }
}