using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.CreateAccount;
using Microsoft.Extensions.Logging;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.Workflows;

public class CreateAccountWorkflow : Workflow<CreateAccountWorkflowState>
{
    public const string WorkflowKind = nameof(CreateAccountWorkflow);

    public CreateAccountWorkflow(
        IKeyValueStateStore stateStore,
        IAliveWorkflowStore aliveWorkflowStore,
        ILogger<Workflow<CreateAccountWorkflowState>> logger
    ) : base(stateStore, aliveWorkflowStore, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync()
    {
        var result = await ExecuteAsync(AccountAggregate.AggregateKind, InnerState!.AccountId, new Create());

        if (result.Success)
        {
            InnerState.Succeeded = true;
        }
        else
        {
            InnerState.ErrorMessage = result.ErrorMessage;
        }
    }

    protected override async Task CleanUpAsync()
    {
        await base.CleanUpAsync();
        await RemoveFromAliveWorkflowStoreAsync();
    }
}