using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows;
using BankAccounts.Workflows.AddFunds;
using BankAccounts.Workflows.CreateAccount;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Proto;
using Proto.Cluster;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : AppControllerBase
{
    private readonly ActorSystem _actorSystem;
    private readonly IAggregateStore _aggregateStore;

    public AccountController(ActorSystem actorSystem, IWorkflowStore workflowStore, IAggregateStore aggregateStore) : base(workflowStore)
    {
        _actorSystem = actorSystem;
        _aggregateStore = aggregateStore;
    }

    [HttpPost("CreateAccount")]
    public async Task<ActionResult<WorkflowResult>> CreateAccountAsync([FromBody] CreateAccountRequest request)
    {
        var workflowState = new CreateAccountWorkflowInput
        {
            AccountId = request.AccountId
        };

        await _actorSystem.Cluster().RequestAsync<Empty>(
            kind: CreateAccountWorkflow.WorkflowKind,
            identity: request.RequestId,
            message: workflowState,
            ct: CancellationToken.None
        );

        await Task.Delay(100);
        var state = await GetWorkflowResultAsync(CreateAccountWorkflow.WorkflowKind, request.RequestId);

        return Ok(state);
    }

    [HttpPost("AddFunds")]
    public async Task<ActionResult<AddFundsWorkflowInput>> AddFundsAsync([FromBody] AddFundsRequest request)
    {
        var workflowState = new AddFundsWorkflowInput
        {
            AccountId = request.AccountId,
            Amount = request.Amount
        };

        await _actorSystem.Cluster().RequestAsync<Empty>(
            kind: AddFundsWorkflow.WorkflowKind,
            identity: request.RequestId,
            message: workflowState,
            ct: CancellationToken.None
        );

        await Task.Delay(100);
        var state = await GetWorkflowResultAsync(AddFundsWorkflow.WorkflowKind, request.RequestId);

        return Ok(state);
    }

    [HttpGet("Get/{accountId}")]
    public async Task<ActionResult<AccountAggregateState>> GetAsync(string accountId)
    {
        var key = $"{AccountAggregate.AggregateKind}/{accountId}";
        var stateWrapper = await _aggregateStore.GetAsync(key);

        if (stateWrapper == null)
        {
            return NotFound();
        }
        var state = stateWrapper.InnerState.Unpack<AccountAggregateState>();

        return Ok(state);
    }
}

public record CreateAccountRequest(string RequestId, string AccountId);
public record AddFundsRequest(string RequestId, string AccountId, long Amount);