using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Employee Exception operations.
    /// </summary>
    public class ExceptionRepository : BaseRepository, IExceptionRepository
    {
        public async Task<EmployeeException> GetByUserAndDateAsync(int userId, DateTime date)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"
                    SELECT e.exception_id, e.user_id_fk, e.exception_type_id_fk, e.exception_date,
                           e.notes, et.exception_name, e.clock_in_override, e.clock_out_override
                    FROM employee_exceptions e
                    INNER JOIN exception_types et ON e.exception_type_id_fk = et.exception_type_id
                    WHERE e.user_id_fk = @userId AND e.exception_date = @date";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("date", date.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapException(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<List<EmployeeException>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, List<int> departmentIds = null)
        {
            return await ExecuteAsync(async conn =>
            {
                var exceptions = new List<EmployeeException>();
                var sql = @"
                    SELECT e.exception_id, e.user_id_fk, e.exception_type_id_fk, e.exception_date,
                           e.notes, et.exception_name, e.clock_in_override, e.clock_out_override
                    FROM employee_exceptions e
                    INNER JOIN exception_types et ON e.exception_type_id_fk = et.exception_type_id
                    INNER JOIN users u ON e.user_id_fk = u.user_id
                    WHERE e.exception_date >= @startDate AND e.exception_date <= @endDate";

                if (departmentIds != null && departmentIds.Count > 0)
                {
                    sql += " AND u.default_dept_id = ANY(@deptIds)";
                }

                sql += " ORDER BY e.exception_date, u.name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("startDate", startDate.Date);
                    cmd.Parameters.AddWithValue("endDate", endDate.Date);

                    if (departmentIds != null && departmentIds.Count > 0)
                    {
                        cmd.Parameters.AddWithValue("deptIds", departmentIds.ToArray());
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            exceptions.Add(MapException(reader));
                        }
                    }
                }
                return exceptions;
            });
        }

        public async Task<List<EmployeeException>> GetByUserAndDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
        {
            return await ExecuteAsync(async conn =>
            {
                var exceptions = new List<EmployeeException>();
                var sql = @"
                    SELECT e.exception_id, e.user_id_fk, e.exception_type_id_fk, e.exception_date,
                           e.notes, et.exception_name, e.clock_in_override, e.clock_out_override
                    FROM employee_exceptions e
                    INNER JOIN exception_types et ON e.exception_type_id_fk = et.exception_type_id
                    WHERE e.user_id_fk = @userId
                      AND e.exception_date >= @startDate AND e.exception_date <= @endDate
                    ORDER BY e.exception_date";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId);
                    cmd.Parameters.AddWithValue("startDate", startDate.Date);
                    cmd.Parameters.AddWithValue("endDate", endDate.Date);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            exceptions.Add(MapException(reader));
                        }
                    }
                }
                return exceptions;
            });
        }

        public async Task<int> AddAsync(EmployeeException exception)
        {
            var sql = @"
                INSERT INTO employee_exceptions (user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override)
                VALUES (@userId, @typeId, @date, @notes, @clockIn, @clockOut)
                RETURNING exception_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("userId", exception.UserId),
                new NpgsqlParameter("typeId", exception.ExceptionTypeId),
                new NpgsqlParameter("date", exception.ExceptionDate.Date),
                new NpgsqlParameter("notes", (object)exception.Notes ?? DBNull.Value),
                new NpgsqlParameter("clockIn", (object)exception.ClockInOverride ?? DBNull.Value),
                new NpgsqlParameter("clockOut", (object)exception.ClockOutOverride ?? DBNull.Value));

            return (int)result;
        }

        public async Task<int> AddBulkAsync(List<EmployeeException> exceptions)
        {
            if (exceptions == null || exceptions.Count == 0) return 0;

            return await ExecuteAsync(async conn =>
            {
                int count = 0;
                var sql = @"
                    INSERT INTO employee_exceptions (user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override)
                    VALUES (@userId, @typeId, @date, @notes, @clockIn, @clockOut)
                    ON CONFLICT (user_id_fk, exception_date) DO UPDATE
                    SET exception_type_id_fk = EXCLUDED.exception_type_id_fk,
                        notes = EXCLUDED.notes,
                        clock_in_override = EXCLUDED.clock_in_override,
                        clock_out_override = EXCLUDED.clock_out_override";

                foreach (var exc in exceptions)
                {
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", exc.UserId);
                        cmd.Parameters.AddWithValue("typeId", exc.ExceptionTypeId);
                        cmd.Parameters.AddWithValue("date", exc.ExceptionDate.Date);
                        cmd.Parameters.AddWithValue("notes", (object)exc.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("clockIn", (object)exc.ClockInOverride ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("clockOut", (object)exc.ClockOutOverride ?? DBNull.Value);
                        count += await cmd.ExecuteNonQueryAsync();
                    }
                }
                return count;
            });
        }

        public async Task UpdateAsync(EmployeeException exception)
        {
            var sql = @"
                UPDATE employee_exceptions SET
                    exception_type_id_fk = @typeId, exception_date = @date, notes = @notes,
                    clock_in_override = @clockIn, clock_out_override = @clockOut
                WHERE exception_id = @id";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("typeId", exception.ExceptionTypeId),
                new NpgsqlParameter("date", exception.ExceptionDate.Date),
                new NpgsqlParameter("notes", (object)exception.Notes ?? DBNull.Value),
                new NpgsqlParameter("clockIn", (object)exception.ClockInOverride ?? DBNull.Value),
                new NpgsqlParameter("clockOut", (object)exception.ClockOutOverride ?? DBNull.Value),
                new NpgsqlParameter("id", exception.ExceptionId));
        }

        public async Task DeleteAsync(int id)
        {
            await ExecuteNonQueryAsync("DELETE FROM employee_exceptions WHERE exception_id = @id",
                new NpgsqlParameter("id", id));
        }

        public async Task<int> GetOnLeaveCountAsync(DateTime date, List<int> departmentIds = null)
        {
            var sql = @"
                SELECT COUNT(DISTINCT e.user_id_fk)
                FROM employee_exceptions e
                INNER JOIN users u ON e.user_id_fk = u.user_id
                WHERE e.exception_date = @date";

            if (departmentIds != null && departmentIds.Count > 0)
            {
                sql += " AND u.default_dept_id = ANY(@deptIds)";
            }

            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("date", date.Date) };
            if (departmentIds != null && departmentIds.Count > 0)
            {
                parameters.Add(new NpgsqlParameter("deptIds", departmentIds.ToArray()));
            }

            var count = await ExecuteScalarAsync(sql, parameters.ToArray());
            return (int)(long)count;
        }

        public async Task<List<ExceptionType>> GetExceptionTypesAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var types = new List<ExceptionType>();
                var sql = "SELECT exception_type_id, exception_name FROM exception_types ORDER BY exception_name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        types.Add(new ExceptionType
                        {
                            ExceptionTypeId = reader.GetInt32(0),
                            ExceptionName = reader.GetString(1)
                        });
                    }
                }
                return types;
            });
        }

        private EmployeeException MapException(NpgsqlDataReader reader)
        {
            return new EmployeeException
            {
                ExceptionId = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                ExceptionTypeId = reader.GetInt32(2),
                ExceptionDate = reader.GetDateTime(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                ExceptionName = reader.GetString(5),
                ClockInOverride = reader.IsDBNull(6) ? (TimeSpan?)null : reader.GetTimeSpan(6),
                ClockOutOverride = reader.IsDBNull(7) ? (TimeSpan?)null : reader.GetTimeSpan(7)
            };
        }
    }
}
