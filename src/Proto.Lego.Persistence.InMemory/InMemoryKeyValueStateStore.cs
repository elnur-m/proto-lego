namespace Proto.Lego.Persistence.InMemory;

public class InMemoryKeyValueStateStore : IKeyValueStateStore
{
    private readonly Dictionary<string, byte[]> _states = new();

    public Task<byte[]?> GetAsync(string key)
    {
        return Task.FromResult(_states.TryGetValue(key, out var value) ? value : null);
    }

    public Task PutAsync(string key, byte[] value)
    {
        _states.Add(key, value);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(string key, byte[] value)
    {
        _states[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _states.Remove(key);
        return Task.CompletedTask;
    }
}