using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.AddFunds;
using Microsoft.Extensions.Logging;
using Proto.Lego;
using Proto.Lego.Persistence;

namespace BankAccounts.Workflows;

public class AddFundsWorkflow : Workflow<AddFundsWorkflowInput>
{
    public const string WorkflowKind = nameof(AddFundsWorkflow);

    public AddFundsWorkflow(IWorkflowStore store, ILogger<Workflow<AddFundsWorkflowInput>> logger
    ) : base(store, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync(AddFundsWorkflowInput input)
    {
        var addFunds = new Add
        {
            Amount = input.Amount
        };

        var prepareResult = await PrepareAsync(AccountAggregate.AggregateKind, input.AccountId, addFunds);

        if (!prepareResult.Success)
        {
            State!.Result.ErrorMessages.Add(prepareResult.ErrorMessage);
            return;
        }

        await ConfirmAsync(AccountAggregate.AggregateKind, input.AccountId, addFunds);
        State!.Result.Succeeded = true;
    }
}