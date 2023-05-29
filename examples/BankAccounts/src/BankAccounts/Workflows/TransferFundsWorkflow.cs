using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.TransferFunds;
using Microsoft.Extensions.Logging;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.Workflows;

public class TransferFundsWorkflow : Workflow<TransferFundsWorkflowState>
{
    public const string WorkflowKind = nameof(TransferFundsWorkflow);

    public TransferFundsWorkflow(
        IKeyValueStateStore stateStore,
        IAliveWorkflowStore aliveWorkflowStore,
        ILogger<Workflow<TransferFundsWorkflowState>> logger
    ) : base(stateStore, aliveWorkflowStore, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync()
    {
        var subtract = new Subtract
        {
            Amount = InnerState!.Amount
        };

        var prepareSubtractResult = await PrepareAsync(AccountAggregate.AggregateKind, InnerState.FromAccountId, subtract);

        if (!prepareSubtractResult.Success)
        {
            InnerState.Succeeded = false;
            InnerState.ErrorMessage = prepareSubtractResult.ErrorMessage;

            return;
        }

        var add = new Add
        {
            Amount = InnerState.Amount
        };

        var prepareAddResult = await PrepareAsync(AccountAggregate.AggregateKind, InnerState.ToAccountId, add);

        if (!prepareAddResult.Success)
        {
            InnerState.Succeeded = false;
            InnerState.ErrorMessage = prepareSubtractResult.ErrorMessage;

            await CancelAsync(AccountAggregate.AggregateKind, InnerState.FromAccountId, subtract);

            return;
        }

        await ConfirmAsync(AccountAggregate.AggregateKind, InnerState.FromAccountId, subtract);
        await ConfirmAsync(AccountAggregate.AggregateKind, InnerState.ToAccountId, add);

        InnerState.Succeeded = true;
    }

    protected override async Task CleanUpAsync()
    {
        await base.CleanUpAsync();
        await RemoveFromAliveWorkflowStoreAsync();
    }
}