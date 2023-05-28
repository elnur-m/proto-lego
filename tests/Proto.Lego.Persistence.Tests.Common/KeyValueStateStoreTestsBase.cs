using Shouldly;
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

        value.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_WhenNewValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var bytes = new byte[5];
        Random.Shared.NextBytes(bytes);

        await _keyValueStateStore.SetAsync(key, bytes);

        var value = await _keyValueStateStore.GetAsync(key);
        value.ShouldBe(bytes);
    }

    [Fact]
    public async Task SetAsync_WhenExistingValue_Succeeds()
    {
        var key = Guid.NewGuid().ToString();
        var bytesOne = new byte[5];
        Random.Shared.NextBytes(bytesOne);
        var bytesTwo = new byte[8];
        Random.Shared.NextBytes(bytesTwo);

        await _keyValueStateStore.SetAsync(key, bytesOne);
        await _keyValueStateStore.SetAsync(key, bytesTwo);

        var value = await _keyValueStateStore.GetAsync(key);
        value.ShouldBe(bytesTwo);
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
        var bytes = new byte[5];
        Random.Shared.NextBytes(bytes);
        await _keyValueStateStore.SetAsync(key, bytes);

        await _keyValueStateStore.DeleteAsync(key);

        var value = await _keyValueStateStore.GetAsync(key);
        value.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenKeyDoesNotExist_Succeeds()
    {
        var key = Guid.NewGuid().ToString();

        await _keyValueStateStore.DeleteAsync(key);
        await _keyValueStateStore.DeleteAsync(key);

        var value = await _keyValueStateStore.GetAsync(key);
        value.ShouldBeNull();
    }
}