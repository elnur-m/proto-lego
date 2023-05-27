using Xunit;

namespace Proto.Lego.Persistence.Tests.Common;

public abstract class KeyValueStateStoreTestsBase
{
    private readonly IKeyValueStateStore _keyValueStateStore;

    protected KeyValueStateStoreTestsBase(IKeyValueStateStore keyValueStateStore)
    {
        _keyValueStateStore = keyValueStateStore;
    }

    [Fact]
    public async Task GetAsync_WhenValueDoesNotExist_ReturnsNull()
    {
        var key = Guid.NewGuid().ToString();
        var value = await _keyValueStateStore.GetAsync(key);
        Assert.Null(value);
    }

    [Fact]
    public async Task PutAsync_WhenNewValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.PutAsync(key, Array.Empty<byte>());
    }

    [Fact]
    public async Task PutAsync_WhenExistingValue_Fails()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.PutAsync(key, Array.Empty<byte>());
        await Assert.ThrowsAnyAsync<Exception>(async () => await _keyValueStateStore.PutAsync(key, Array.Empty<byte>()));
    }

    [Fact]
    public async Task PutThenGetAsync_ReturnsPutValue()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.PutAsync(key, Array.Empty<byte>());
        var val = await _keyValueStateStore.GetAsync(key);
        Assert.NotNull(val);
        Assert.Empty(val);
    }

    [Fact]
    public async Task UpdateAsync_WhenValueDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.UpdateAsync(key, Array.Empty<byte>());
    }

    [Fact]
    public async Task UpdateAsync_WhenValueExists_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.UpdateAsync(key, Array.Empty<byte>());
        await _keyValueStateStore.UpdateAsync(key, Array.Empty<byte>());
    }

    [Fact]
    public async Task UpdateAsync_SavesValue()
    {
        var key = Guid.NewGuid().ToString();
        var value = new byte[] { 1, 2, 3 };
        await _keyValueStateStore.UpdateAsync(key, value);
        var valueFromDb = await _keyValueStateStore.GetAsync(key);
        Assert.NotNull(valueFromDb);
        Assert.Equal(value, valueFromDb);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.DeleteAsync(key);
    }

    [Fact]
    public async Task DeleteAsync_RemovesValue()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.PutAsync(key, Array.Empty<byte>());
        await _keyValueStateStore.DeleteAsync(key);
        var value = await _keyValueStateStore.GetAsync(key);
        Assert.Null(value);
    }

    [Fact]
    public async Task DeleteAsync_WhenValueExists_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        await _keyValueStateStore.DeleteAsync(key);
        await _keyValueStateStore.DeleteAsync(key);
    }
}