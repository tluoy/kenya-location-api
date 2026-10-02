using System.Data;
using Npgsql;

namespace KenyaLocation.Api.Database;

public static class DbQuery
{
    public static async Task<List<Dictionary<string, object?>>> QueryRows(
        NpgsqlDataSource db,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var command = db.CreateCommand(sql);

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] =
                    await reader.IsDBNullAsync(i)
                        ? null
                        : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }
}