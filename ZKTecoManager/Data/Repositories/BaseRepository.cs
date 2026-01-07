using Npgsql;
using System;
using System.Threading.Tasks;
using ZKTecoManager.Infrastructure;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Base repository class providing common database operations.
    /// </summary>
    public abstract class BaseRepository
    {
        /// <summary>
        /// Gets a new database connection.
        /// </summary>
        protected NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(DatabaseConfig.ConnectionString);
        }

        /// <summary>
        /// Executes an action with a database connection.
        /// </summary>
        protected async Task<T> ExecuteAsync<T>(Func<NpgsqlConnection, Task<T>> action)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync();
                return await action(conn);
            }
        }

        /// <summary>
        /// Executes an action with a database connection (no return value).
        /// </summary>
        protected async Task ExecuteAsync(Func<NpgsqlConnection, Task> action)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync();
                await action(conn);
            }
        }

        /// <summary>
        /// Executes a non-query command and returns the number of affected rows.
        /// </summary>
        protected async Task<int> ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            return await ExecuteAsync(async conn =>
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    return await cmd.ExecuteNonQueryAsync();
                }
            });
        }

        /// <summary>
        /// Executes a scalar command and returns the result.
        /// </summary>
        protected async Task<object> ExecuteScalarAsync(string sql, params NpgsqlParameter[] parameters)
        {
            return await ExecuteAsync(async conn =>
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters);
                    }
                    return await cmd.ExecuteScalarAsync();
                }
            });
        }
    }
}
