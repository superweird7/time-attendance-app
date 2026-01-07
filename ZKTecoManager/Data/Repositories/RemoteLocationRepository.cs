using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Sync;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing remote locations.
    /// </summary>
    public class RemoteLocationRepository : BaseRepository
    {
        /// <summary>
        /// Ensures the remote_locations table exists.
        /// </summary>
        public async Task EnsureTableExistsAsync()
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS remote_locations (
                    location_id SERIAL PRIMARY KEY,
                    location_name VARCHAR(100) NOT NULL,
                    host VARCHAR(255) NOT NULL,
                    port INTEGER DEFAULT 5432,
                    database_name VARCHAR(100) NOT NULL,
                    username VARCHAR(100) NOT NULL,
                    password VARCHAR(255) NOT NULL,
                    is_active BOOLEAN DEFAULT true,
                    last_sync_time TIMESTAMP,
                    last_sync_status VARCHAR(50),
                    created_at TIMESTAMP DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS sync_history (
                    sync_id SERIAL PRIMARY KEY,
                    location_id INTEGER REFERENCES remote_locations(location_id),
                    sync_type VARCHAR(50),
                    records_added INTEGER DEFAULT 0,
                    records_updated INTEGER DEFAULT 0,
                    records_skipped INTEGER DEFAULT 0,
                    status VARCHAR(50),
                    error_message TEXT,
                    started_at TIMESTAMP,
                    completed_at TIMESTAMP DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS sync_settings (
                    setting_id SERIAL PRIMARY KEY,
                    auto_sync_enabled BOOLEAN DEFAULT false,
                    sync_interval_minutes INTEGER DEFAULT 15,
                    last_modified TIMESTAMP DEFAULT NOW()
                );

                INSERT INTO sync_settings (auto_sync_enabled, sync_interval_minutes)
                SELECT false, 15
                WHERE NOT EXISTS (SELECT 1 FROM sync_settings);
            ";

            await ExecuteNonQueryAsync(sql);
        }

        /// <summary>
        /// Gets all remote locations.
        /// </summary>
        public async Task<List<RemoteLocation>> GetAllAsync()
        {
            var locations = new List<RemoteLocation>();

            await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT location_id, location_name, host, port, database_name,
                           username, password, is_active, last_sync_time, last_sync_status
                           FROM remote_locations ORDER BY location_name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        locations.Add(new RemoteLocation
                        {
                            LocationId = reader.GetInt32(0),
                            LocationName = reader.GetString(1),
                            Host = reader.GetString(2),
                            Port = reader.GetInt32(3),
                            DatabaseName = reader.GetString(4),
                            Username = reader.GetString(5),
                            Password = reader.GetString(6),
                            IsActive = reader.GetBoolean(7),
                            LastSyncTime = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                            LastSyncStatus = reader.IsDBNull(9) ? null : reader.GetString(9)
                        });
                    }
                }
            });

            return locations;
        }

        /// <summary>
        /// Gets a remote location by ID.
        /// </summary>
        public async Task<RemoteLocation> GetByIdAsync(int locationId)
        {
            RemoteLocation location = null;

            await ExecuteAsync(async conn =>
            {
                var sql = @"SELECT location_id, location_name, host, port, database_name,
                           username, password, is_active, last_sync_time, last_sync_status
                           FROM remote_locations WHERE location_id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", locationId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            location = new RemoteLocation
                            {
                                LocationId = reader.GetInt32(0),
                                LocationName = reader.GetString(1),
                                Host = reader.GetString(2),
                                Port = reader.GetInt32(3),
                                DatabaseName = reader.GetString(4),
                                Username = reader.GetString(5),
                                Password = reader.GetString(6),
                                IsActive = reader.GetBoolean(7),
                                LastSyncTime = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8),
                                LastSyncStatus = reader.IsDBNull(9) ? null : reader.GetString(9)
                            };
                        }
                    }
                }
            });

            return location;
        }

        /// <summary>
        /// Adds a new remote location.
        /// </summary>
        public async Task<int> AddAsync(RemoteLocation location)
        {
            var sql = @"INSERT INTO remote_locations
                       (location_name, host, port, database_name, username, password, is_active)
                       VALUES (@name, @host, @port, @db, @user, @pass, @active)
                       RETURNING location_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("name", location.LocationName),
                new NpgsqlParameter("host", location.Host),
                new NpgsqlParameter("port", location.Port),
                new NpgsqlParameter("db", location.DatabaseName),
                new NpgsqlParameter("user", location.Username),
                new NpgsqlParameter("pass", location.Password),
                new NpgsqlParameter("active", location.IsActive));

            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Updates an existing remote location.
        /// </summary>
        public async Task UpdateAsync(RemoteLocation location)
        {
            var sql = @"UPDATE remote_locations SET
                       location_name = @name,
                       host = @host,
                       port = @port,
                       database_name = @db,
                       username = @user,
                       password = @pass,
                       is_active = @active
                       WHERE location_id = @id";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("id", location.LocationId),
                new NpgsqlParameter("name", location.LocationName),
                new NpgsqlParameter("host", location.Host),
                new NpgsqlParameter("port", location.Port),
                new NpgsqlParameter("db", location.DatabaseName),
                new NpgsqlParameter("user", location.Username),
                new NpgsqlParameter("pass", location.Password),
                new NpgsqlParameter("active", location.IsActive));
        }

        /// <summary>
        /// Deletes a remote location.
        /// </summary>
        public async Task DeleteAsync(int locationId)
        {
            // First delete sync history
            await ExecuteNonQueryAsync("DELETE FROM sync_history WHERE location_id = @id",
                new NpgsqlParameter("id", locationId));

            // Then delete location
            await ExecuteNonQueryAsync("DELETE FROM remote_locations WHERE location_id = @id",
                new NpgsqlParameter("id", locationId));
        }

        /// <summary>
        /// Updates the last sync status for a location.
        /// </summary>
        public async Task UpdateSyncStatusAsync(int locationId, string status)
        {
            // Truncate status to 50 characters to fit column size
            if (!string.IsNullOrEmpty(status) && status.Length > 50)
                status = status.Substring(0, 47) + "...";

            var sql = @"UPDATE remote_locations SET
                       last_sync_time = NOW(),
                       last_sync_status = @status
                       WHERE location_id = @id";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("id", locationId),
                new NpgsqlParameter("status", status ?? (object)DBNull.Value));
        }

        /// <summary>
        /// Logs a sync operation to history.
        /// </summary>
        public async Task LogSyncAsync(int locationId, string syncType, SyncResult result, DateTime startTime)
        {
            var sql = @"INSERT INTO sync_history
                       (location_id, sync_type, records_added, records_updated, records_skipped, status, error_message, started_at, completed_at)
                       VALUES (@locId, @type, @added, @updated, @skipped, @status, @error, @started, NOW())";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("locId", locationId),
                new NpgsqlParameter("type", syncType),
                new NpgsqlParameter("added", result.RecordsAdded),
                new NpgsqlParameter("updated", result.RecordsUpdated),
                new NpgsqlParameter("skipped", result.RecordsSkipped),
                new NpgsqlParameter("status", result.Success ? "Success" : "Failed"),
                new NpgsqlParameter("error", result.Errors.Count > 0 ? string.Join("; ", result.Errors) : (object)DBNull.Value),
                new NpgsqlParameter("started", startTime));
        }

        /// <summary>
        /// Gets sync settings.
        /// </summary>
        public async Task<(bool autoSyncEnabled, int intervalMinutes)> GetSyncSettingsAsync()
        {
            bool enabled = false;
            int interval = 15;

            await ExecuteAsync(async conn =>
            {
                var sql = "SELECT auto_sync_enabled, sync_interval_minutes FROM sync_settings LIMIT 1";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        enabled = reader.GetBoolean(0);
                        interval = reader.GetInt32(1);
                    }
                }
            });

            return (enabled, interval);
        }

        /// <summary>
        /// Updates sync settings.
        /// </summary>
        public async Task UpdateSyncSettingsAsync(bool autoSyncEnabled, int intervalMinutes)
        {
            var sql = @"UPDATE sync_settings SET
                       auto_sync_enabled = @enabled,
                       sync_interval_minutes = @interval,
                       last_modified = NOW()";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("enabled", autoSyncEnabled),
                new NpgsqlParameter("interval", intervalMinutes));
        }
    }
}
