using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Shift operations.
    /// </summary>
    public class ShiftRepository : BaseRepository, IShiftRepository
    {
        public async Task<List<Shift>> GetAllAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var shiftsDict = new Dictionary<int, Shift>();

                // Use LEFT JOIN to fetch shifts and their rules in a single query (fixes N+1 problem)
                var sql = @"SELECT s.shift_id, s.shift_name, s.start_time, s.end_time, sr.expected_time
                           FROM shifts s
                           LEFT JOIN shift_rules sr ON s.shift_id = sr.shift_id_fk
                           ORDER BY s.shift_name, sr.expected_time";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int shiftId = reader.GetInt32(0);

                        // Create shift if not already in dictionary
                        if (!shiftsDict.TryGetValue(shiftId, out var shift))
                        {
                            shift = new Shift
                            {
                                ShiftId = shiftId,
                                ShiftName = reader.GetString(1),
                                StartTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2),
                                EndTime = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetTimeSpan(3),
                                Rules = new List<TimeSpan>()
                            };
                            shiftsDict[shiftId] = shift;
                        }

                        // Add rule if present
                        if (!reader.IsDBNull(4))
                        {
                            shift.Rules.Add(reader.GetTimeSpan(4));
                        }
                    }
                }

                return shiftsDict.Values.ToList();
            });
        }

        public async Task<Shift> GetByIdAsync(int id)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = "SELECT shift_id, shift_name, start_time, end_time FROM shifts WHERE shift_id = @id";
                Shift shift = null;

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            shift = new Shift
                            {
                                ShiftId = reader.GetInt32(0),
                                ShiftName = reader.GetString(1),
                                StartTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2),
                                EndTime = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetTimeSpan(3)
                            };
                        }
                    }
                }

                // Load rules after the reader is closed
                if (shift != null)
                {
                    shift.Rules = await GetShiftRulesAsync(conn, shift.ShiftId);
                }

                return shift;
            });
        }

        public async Task<Shift> GetByNameAsync(string name)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = "SELECT shift_id, shift_name, start_time, end_time FROM shifts WHERE shift_name = @name";
                Shift shift = null;

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("name", name);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            shift = new Shift
                            {
                                ShiftId = reader.GetInt32(0),
                                ShiftName = reader.GetString(1),
                                StartTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2),
                                EndTime = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetTimeSpan(3)
                            };
                        }
                    }
                }

                // Load rules after the reader is closed
                if (shift != null)
                {
                    shift.Rules = await GetShiftRulesAsync(conn, shift.ShiftId);
                }

                return shift;
            });
        }

        public async Task<int> AddAsync(Shift entity)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = "INSERT INTO shifts (shift_name, start_time, end_time) VALUES (@name, @start, @end) RETURNING shift_id";
                int shiftId;

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("name", entity.ShiftName);
                    cmd.Parameters.AddWithValue("start", entity.StartTime);
                    cmd.Parameters.AddWithValue("end", entity.EndTime);
                    shiftId = (int)await cmd.ExecuteScalarAsync();
                }

                // Insert rules
                await SaveShiftRulesAsync(conn, shiftId, entity.Rules);

                return shiftId;
            });
        }

        public async Task UpdateAsync(Shift entity)
        {
            await ExecuteAsync(async conn =>
            {
                var sql = "UPDATE shifts SET shift_name = @name, start_time = @start, end_time = @end WHERE shift_id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("name", entity.ShiftName);
                    cmd.Parameters.AddWithValue("start", entity.StartTime);
                    cmd.Parameters.AddWithValue("end", entity.EndTime);
                    cmd.Parameters.AddWithValue("id", entity.ShiftId);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete existing rules and insert new ones
                using (var delCmd = new NpgsqlCommand("DELETE FROM shift_rules WHERE shift_id_fk = @id", conn))
                {
                    delCmd.Parameters.AddWithValue("id", entity.ShiftId);
                    await delCmd.ExecuteNonQueryAsync();
                }

                await SaveShiftRulesAsync(conn, entity.ShiftId, entity.Rules);
            });
        }

        public async Task DeleteAsync(int id)
        {
            await ExecuteAsync(async conn =>
            {
                // Delete rules first
                using (var rulesCmd = new NpgsqlCommand("DELETE FROM shift_rules WHERE shift_id_fk = @id", conn))
                {
                    rulesCmd.Parameters.AddWithValue("id", id);
                    await rulesCmd.ExecuteNonQueryAsync();
                }

                // Delete shift
                using (var cmd = new NpgsqlCommand("DELETE FROM shifts WHERE shift_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            });
        }

        public async Task<bool> ExistsAsync(string name, int? excludeId = null)
        {
            var sql = excludeId.HasValue
                ? "SELECT COUNT(*) FROM shifts WHERE shift_name = @name AND shift_id != @excludeId"
                : "SELECT COUNT(*) FROM shifts WHERE shift_name = @name";

            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("name", name) };
            if (excludeId.HasValue)
            {
                parameters.Add(new NpgsqlParameter("excludeId", excludeId.Value));
            }

            var count = await ExecuteScalarAsync(sql, parameters.ToArray());
            return (long)count > 0;
        }

        public async Task<List<Shift>> GetActiveShiftsAsync()
        {
            // For now, all shifts are considered active
            return await GetAllAsync();
        }

        private async Task<List<TimeSpan>> GetShiftRulesAsync(NpgsqlConnection conn, int shiftId)
        {
            var rules = new List<TimeSpan>();
            var sql = "SELECT expected_time FROM shift_rules WHERE shift_id_fk = @id ORDER BY expected_time";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", shiftId);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rules.Add(reader.GetTimeSpan(0));
                    }
                }
            }
            return rules;
        }

        private async Task SaveShiftRulesAsync(NpgsqlConnection conn, int shiftId, List<TimeSpan> rules)
        {
            if (rules == null || rules.Count == 0) return;

            var sql = "INSERT INTO shift_rules (shift_id_fk, expected_time) VALUES (@id, @time)";
            foreach (var rule in rules)
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", shiftId);
                    cmd.Parameters.AddWithValue("time", rule);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
