namespace Proto.Lego.Persistence;

public interface IKeyValueStateStore
{
    Task<byte[]?> GetAsync(string key);

    Task SetAsync(string key, byte[] value);

    Task DeleteAsync(string key);
}