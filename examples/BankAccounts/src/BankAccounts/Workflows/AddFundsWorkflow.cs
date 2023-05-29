using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.AddFunds;
using Microsoft.Extensions.Logging;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.Workflows;

public class AddFundsWorkflow : Workflow<AddFundsWorkflowState>
{
    public const string WorkflowKind = nameof(AddFundsWorkflow);

    public AddFundsWorkflow(
        IKeyValueStateStore stateStore,
        IAliveWorkflowStore aliveWorkflowStore,
        ILogger<Workflow<AddFundsWorkflowState>> logger
    ) : base(stateStore, aliveWorkflowStore, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync()
    {
        var addFunds = new Add
        {
            Amount = InnerState!.Amount
        };

        var prepareResult = await PrepareAsync(AccountAggregate.AggregateKind, InnerState.AccountId, addFunds);

        if (!prepareResult.Success)
        {
            InnerState.ErrorMessage = prepareResult.ErrorMessage;
            return;
        }

        await ConfirmAsync(AccountAggregate.AggregateKind, InnerState.AccountId, addFunds);
        InnerState.Succeeded = true;
    }

    protected override async Task CleanUpAsync()
    {
        await base.CleanUpAsync();
        await RemoveFromAliveWorkflowStoreAsync();
    }
}