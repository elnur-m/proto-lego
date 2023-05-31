using Microsoft.AspNetCore.Mvc;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow;

namespace BankAccounts.WebApi.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    protected readonly IWorkflowStore WorkflowStore;

    protected AppControllerBase(IWorkflowStore workflowStore)
    {
        WorkflowStore = workflowStore;
    }

    protected async Task<WorkflowResult?> GetWorkflowResultAsync(string kind, string id)
    {
        var key = $"{kind}/{id}";
        var state = await WorkflowStore.GetAsync(key);
        if (state == null)
        {
            return null;
        }

        var result = state.Result;

        return result;
    }
}