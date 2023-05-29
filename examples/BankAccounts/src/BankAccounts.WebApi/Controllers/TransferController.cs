using BankAccounts.Workflows;
using BankAccounts.Workflows.AddFunds;
using BankAccounts.Workflows.TransferFunds;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Proto;
using Proto.Cluster;
using Proto.Lego.Persistence;

namespace BankAccounts.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransferController : AppControllerBase
{
    private readonly ActorSystem _actorSystem;

    public TransferController(ActorSystem actorSystem, IKeyValueStateStore stateStore) : base(stateStore)
    {
        _actorSystem = actorSystem;
    }

    [HttpPost("TransferFunds")]
    public async Task<ActionResult<TransferFundsWorkflowState>> TransferFundsAsync([FromBody] TransferFundsRequest request)
    {
        var workflowState = new TransferFundsWorkflowState
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
        var state = await GetWorkflowStateAsync<TransferFundsWorkflowState>(TransferFundsWorkflow.WorkflowKind, request.RequestId);

        return Ok(state);
    }
}

public record TransferFundsRequest(string RequestId, string FromAccountId, string ToAccountId, long Amount);