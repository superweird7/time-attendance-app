using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Machine (Device) operations.
    /// </summary>
    public class MachineRepository : BaseRepository, IMachineRepository
    {
        public async Task<List<Machine>> GetAllAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var machines = new List<Machine>();
                var sql = "SELECT id, machine_alias, ip_address, serial_number, location FROM machines ORDER BY COALESCE(location, ''), machine_alias";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        machines.Add(MapMachine(reader));
                    }
                }
                return machines;
            });
        }

        public async Task<Machine> GetByIdAsync(int id)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = "SELECT id, machine_alias, ip_address, serial_number, location FROM machines WHERE id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapMachine(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<Machine> GetByIpAsync(string ipAddress)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = "SELECT id, machine_alias, ip_address, serial_number, location FROM machines WHERE ip_address = @ip";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("ip", ipAddress);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapMachine(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<int> AddAsync(Machine entity)
        {
            var sql = @"
                INSERT INTO machines (machine_alias, ip_address, serial_number, location)
                VALUES (@alias, @ip, @serial, @location)
                RETURNING id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("alias", entity.MachineAlias),
                new NpgsqlParameter("ip", entity.IpAddress),
                new NpgsqlParameter("serial", (object)entity.SerialNumber ?? DBNull.Value),
                new NpgsqlParameter("location", (object)entity.Location ?? DBNull.Value));

            return (int)result;
        }

        public async Task UpdateAsync(Machine entity)
        {
            var sql = @"
                UPDATE machines SET
                    machine_alias = @alias, ip_address = @ip, serial_number = @serial, location = @location
                WHERE id = @id";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("alias", entity.MachineAlias),
                new NpgsqlParameter("ip", entity.IpAddress),
                new NpgsqlParameter("serial", (object)entity.SerialNumber ?? DBNull.Value),
                new NpgsqlParameter("location", (object)entity.Location ?? DBNull.Value),
                new NpgsqlParameter("id", entity.Id));
        }

        public async Task DeleteAsync(int id)
        {
            await ExecuteNonQueryAsync("DELETE FROM machines WHERE id = @id",
                new NpgsqlParameter("id", id));
        }

        public async Task<List<Machine>> GetAccessibleMachinesAsync(int? userId, string userRole)
        {
            if (userRole == "superadmin")
            {
                return await GetAllAsync();
            }

            return await ExecuteAsync(async conn =>
            {
                var machines = new List<Machine>();
                var sql = @"
                    SELECT DISTINCT m.id, m.machine_alias, m.ip_address, m.serial_number, m.location
                    FROM machines m
                    INNER JOIN user_machine_permissions ump ON m.id = ump.machine_id
                    WHERE ump.user_id = @userId
                    ORDER BY COALESCE(m.location, ''), m.machine_alias";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId ?? 0);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            machines.Add(MapMachine(reader));
                        }
                    }
                }
                return machines;
            });
        }

        public async Task UpdateLastSyncAsync(int machineId, DateTime lastSync)
        {
            var sql = "UPDATE machines SET last_sync = @lastSync WHERE id = @id";
            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("lastSync", lastSync),
                new NpgsqlParameter("id", machineId));
        }

        public async Task<List<Machine>> GetByDepartmentPermissionAsync(int departmentId)
        {
            return await ExecuteAsync(async conn =>
            {
                var machines = new List<Machine>();
                var sql = @"
                    SELECT DISTINCT m.id, m.machine_alias, m.ip_address, m.serial_number, m.location
                    FROM machines m
                    INNER JOIN machine_department_permissions mdp ON m.id = mdp.machine_id
                    WHERE mdp.dept_id = @deptId
                    ORDER BY COALESCE(m.location, ''), m.machine_alias";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("deptId", departmentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            machines.Add(MapMachine(reader));
                        }
                    }
                }
                return machines;
            });
        }

        public async Task<bool> IpExistsAsync(string ipAddress, int? excludeId = null)
        {
            var sql = excludeId.HasValue
                ? "SELECT COUNT(*) FROM machines WHERE ip_address = @ip AND id != @excludeId"
                : "SELECT COUNT(*) FROM machines WHERE ip_address = @ip";

            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("ip", ipAddress) };
            if (excludeId.HasValue)
            {
                parameters.Add(new NpgsqlParameter("excludeId", excludeId.Value));
            }

            var count = await ExecuteScalarAsync(sql, parameters.ToArray());
            return (long)count > 0;
        }

        private Machine MapMachine(NpgsqlDataReader reader)
        {
            return new Machine
            {
                Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                MachineAlias = reader.IsDBNull(1) ? "" : reader.GetString(1),
                IpAddress = reader.IsDBNull(2) ? "" : reader.GetString(2),
                SerialNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Location = reader.IsDBNull(4) ? "" : reader.GetString(4)
            };
        }

        public async Task<List<string>> GetAllLocationsAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var locations = new List<string>();
                var sql = "SELECT DISTINCT location FROM machines WHERE location IS NOT NULL AND location != '' ORDER BY location";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        locations.Add(reader.GetString(0));
                    }
                }
                return locations;
            });
        }
    }
}
