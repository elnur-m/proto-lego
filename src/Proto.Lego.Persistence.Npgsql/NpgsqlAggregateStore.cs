﻿using Google.Protobuf;
using Npgsql;
using Proto.Lego.Aggregate;

namespace Proto.Lego.Persistence.Npgsql;

public class NpgsqlAggregateStore : IAggregateStore
{
    private readonly string _connectionString;

    private readonly string _tableName;

    public NpgsqlAggregateStore(string connectionString, string tableName)
    {
        _connectionString = connectionString;
        _tableName = tableName;
    }

    public async Task<AggregateStateWrapper?> GetAsync(string key)
    {
        await using var connection = new NpgsqlConnection(_connectionString);

        await using var command = new NpgsqlCommand($"SELECT value FROM {_tableName} WHERE key = @Key", connection);

        await connection.OpenAsync();

        command.Parameters.AddRange(
            new NpgsqlParameter[]
            {
                new NpgsqlParameter<string>("Key", key),
            }
        );

        var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return Deserialize((byte[])reader["value"]);
        }

        return null;
    }

    public async Task SetAsync(string key, AggregateStateWrapper state)
    {
        var value = Serialize(state);

        await ExecuteNonQueryAsync(
            $@"INSERT INTO {_tableName} (key, value) VALUES(@Key,@Value)
                    ON CONFLICT (key)
                    DO UPDATE SET value=excluded.value",
            new NpgsqlParameter<string>("Key", key),
            new NpgsqlParameter<byte[]>("Value", value)
        );
    }

    public async Task DeleteAsync(string key)
    {
        await ExecuteNonQueryAsync(
            $"DELETE FROM {_tableName} WHERE key=@Key",
            new NpgsqlParameter<string>("Key", key)
        );
    }

    private static byte[] Serialize(AggregateStateWrapper wrapper)
    {
        return wrapper.ToByteArray();
    }

    private static AggregateStateWrapper Deserialize(byte[] bytes)
    {
        return AggregateStateWrapper.Parser.ParseFrom(bytes);
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