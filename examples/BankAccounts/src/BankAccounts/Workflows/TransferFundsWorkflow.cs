using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.TransferFunds;
using Microsoft.Extensions.Logging;
using Proto.Lego;
using Proto.Lego.Persistence;

namespace BankAccounts.Workflows;

public class TransferFundsWorkflow : Workflow<TransferFundsWorkflowInput>
{
    public const string WorkflowKind = nameof(TransferFundsWorkflow);

    public TransferFundsWorkflow(IWorkflowStore store, ILogger<Workflow<TransferFundsWorkflowInput>> logger
    ) : base(store, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync(TransferFundsWorkflowInput input)
    {
        var subtract = new Subtract
        {
            Amount = input.Amount
        };

        var prepareSubtractResult = await PrepareAsync(AccountAggregate.AggregateKind, input.FromAccountId, subtract);

        if (!prepareSubtractResult.Success)
        {
            State!.Result.Succeeded = false;
            State.Result.ErrorMessages.Add(prepareSubtractResult.ErrorMessage);

            return;
        }

        var add = new Add
        {
            Amount = input.Amount
        };

        var prepareAddResult = await PrepareAsync(AccountAggregate.AggregateKind, input.ToAccountId, add);

        if (!prepareAddResult.Success)
        {
            State!.Result.Succeeded = false;
            State.Result.ErrorMessages.Add(prepareAddResult.ErrorMessage);

            await CancelAsync(AccountAggregate.AggregateKind, input.FromAccountId, subtract);

            return;
        }

        await ConfirmAsync(AccountAggregate.AggregateKind, input.FromAccountId, subtract);
        await ConfirmAsync(AccountAggregate.AggregateKind, input.ToAccountId, add);

        State!.Result.Succeeded = true;
    }

    protected override async Task BeforeCleanUpAsync()
    {
        await Task.Delay(5000);
    }
}