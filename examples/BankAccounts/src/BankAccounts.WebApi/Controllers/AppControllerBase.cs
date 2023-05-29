using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using Proto.Lego.Persistence;
using Proto.Lego.Workflow.Messages;

namespace BankAccounts.WebApi.Controllers;

public abstract class AppControllerBase : ControllerBase
{
    protected readonly IKeyValueStateStore KeyValueStateStore;

    protected AppControllerBase(IKeyValueStateStore keyValueStateStore)
    {
        KeyValueStateStore = keyValueStateStore;
    }

    protected async Task<TState?> GetWorkflowStateAsync<TState>(string kind, string id) where TState : class, IMessage, new()
    {
        var key = $"{kind}/{id}";
        var wrapperBytes = await KeyValueStateStore.GetAsync(key);
        if (wrapperBytes == null)
        {
            return null;
        }

        var wrapper = WorkflowStateWrapper.Parser.ParseFrom(wrapperBytes);
        var state = wrapper.InnerState.Unpack<TState>();

        return state;
    }
}