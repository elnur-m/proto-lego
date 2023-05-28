using Npgsql;

namespace Proto.Lego.Persistence.Npgsql;

public class NpgsqlAliveWorkflowStore : IAliveWorkflowStore
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public NpgsqlAliveWorkflowStore(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }

    public async Task SetAsync(string key)
    {
        await ExecuteNonQueryAsync(
            $@"INSERT INTO {_tableName} (key) VALUES (@Key)
                    ON CONFLICT (key)
                    DO UPDATE SET key=excluded.key",
            new NpgsqlParameter<string>("Key", key)
        );
    }

    public async Task DeleteAsync(string key)
    {
        await ExecuteNonQueryAsync(
            $"DELETE FROM {_tableName} WHERE key=@Key",
            new NpgsqlParameter<string>("Key", key)
        );
    }

    public async Task ActOnAllAsync(Func<string, Task> action)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await using var command = new NpgsqlCommand($"SELECT key FROM {_tableName}", connection);

        await connection.OpenAsync();

        var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var key = (string)reader["key"];
            await action(key);
        }
    }

    private async Task ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await using var command = new NpgsqlCommand(sql, connection);

        await connection.OpenAsync();

        await using var tx = await connection.BeginTransactionAsync();

        command.Transaction = tx;

        if (parameters.Length > 0) command.Parameters.AddRange(parameters);

        await command.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }
}