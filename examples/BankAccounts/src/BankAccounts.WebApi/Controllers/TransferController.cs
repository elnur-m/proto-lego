using BankAccounts.Workflows;
using BankAccounts.Workflows.TransferFunds;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Proto;
using Proto.Cluster;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransferController : AppControllerBase
{
    private readonly ActorSystem _actorSystem;

    public TransferController(ActorSystem actorSystem, IWorkflowStore workflowStore) : base(workflowStore)
    {
        _actorSystem = actorSystem;
    }

    [HttpPost("TransferFunds")]
    public async Task<ActionResult<WorkflowResult>> TransferFundsAsync([FromBody] TransferFundsRequest request)
    {
        var workflowState = new TransferFundsWorkflowInput
        {
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            Amount = request.Amount
        };

        await _actorSystem.Cluster().RequestAsync<Empty>(
            kind: TransferFundsWorkflow.WorkflowKind,
            identity: request.RequestId,
            message: workflowState,
            ct: CancellationToken.None
        );

        await Task.Delay(100);
        var result = await GetWorkflowResultAsync(TransferFundsWorkflow.WorkflowKind, request.RequestId);

        return Ok(result);
    }
}

public record TransferFundsRequest(string RequestId, string FromAccountId, string ToAccountId, long Amount);