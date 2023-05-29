using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows;
using BankAccounts.Workflows.AddFunds;
using BankAccounts.Workflows.CreateAccount;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using Proto;
using Proto.Cluster;
using Proto.Lego.Aggregate.Messages;
using Proto.Lego.Persistence;

namespace BankAccounts.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : AppControllerBase
{
    private readonly ActorSystem _actorSystem;

    public AccountController(ActorSystem actorSystem, IKeyValueStateStore keyValueStateStore) : base(keyValueStateStore)
    {
        _actorSystem = actorSystem;
    }

    [HttpPost("CreateAccount")]
    public async Task<ActionResult<CreateAccountWorkflowState>> CreateAccountAsync([FromBody] CreateAccountRequest request)
    {
        var workflowState = new CreateAccountWorkflowState
        {
            AccountId = Guid.NewGuid().ToString()
        };

        await _actorSystem.Cluster().RequestAsync<Empty>(
            kind: CreateAccountWorkflow.WorkflowKind,
            identity: request.RequestId,
            message: workflowState,
            ct: CancellationToken.None
        );

        await Task.Delay(100);
        var state = await GetWorkflowStateAsync<CreateAccountWorkflowState>(CreateAccountWorkflow.WorkflowKind, request.RequestId);

        return Ok(state);
    }

    [HttpPost("AddFunds")]
    public async Task<ActionResult<AddFundsWorkflowState>> AddFundsAsync([FromBody] AddFundsRequest request)
    {
        var workflowState = new AddFundsWorkflowState
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
        var state = await GetWorkflowStateAsync<AddFundsWorkflowState>(AddFundsWorkflow.WorkflowKind, request.RequestId);

        return Ok(state);
    }

    [HttpGet("Get/{accountId}")]
    public async Task<ActionResult<AccountAggregateState>> GetAsync(string accountId)
    {
        var key = $"{AccountAggregate.AggregateKind}/{accountId}";
        var stateWrapperBytes = await KeyValueStateStore.GetAsync(key);

        if (stateWrapperBytes == null)
        {
            return NotFound();
        }

        var stateWrapper = AggregateStateWrapper.Parser.ParseFrom(stateWrapperBytes);
        var state = stateWrapper.InnerState.Unpack<AccountAggregateState>();

        return Ok(state);
    }
}

public record CreateAccountRequest(string RequestId);
public record AddFundsRequest(string RequestId, string AccountId, long Amount);