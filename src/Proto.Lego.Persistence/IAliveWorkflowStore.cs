﻿namespace Proto.Lego.Persistence;

public interface IAliveWorkflowStore
{
    Task PutAsync(string key);

    Task DeleteAsync(string key);

    Task ActOnAllAsync(Func<string, Task> action);
}