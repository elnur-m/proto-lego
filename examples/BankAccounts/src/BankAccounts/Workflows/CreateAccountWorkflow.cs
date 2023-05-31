using BankAccounts.Aggregates;
using BankAccounts.Aggregates.Account;
using BankAccounts.Workflows.CreateAccount;
using Microsoft.Extensions.Logging;
using Proto.Lego;
using Proto.Lego.Persistence;

namespace BankAccounts.Workflows;

public class CreateAccountWorkflow : Workflow<CreateAccountWorkflowInput>
{
    public const string WorkflowKind = nameof(CreateAccountWorkflow);

    public CreateAccountWorkflow(IWorkflowStore store, ILogger<Workflow<CreateAccountWorkflowInput>> logger
    ) : base(store, logger)
    {
        Kind = WorkflowKind;
    }

    protected override async Task ExecuteWorkflowAsync(CreateAccountWorkflowInput input)
    {
        Logger.LogInformation($"Entering Execute for {Key}");

        var result = await ExecuteAsync(AccountAggregate.AggregateKind, input.AccountId, new Create());

        if (result.Success)
        {
            State!.Result.Succeeded = true;
        }
        else
        {
            State!.Result.ErrorMessages.Add(result.ErrorMessage);
        }
    }

    protected override async Task BeforeCleanUpAsync()
    {
        await Task.Delay(5000);
    }
}