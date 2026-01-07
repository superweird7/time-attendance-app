using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Department operations.
    /// </summary>
    public class DepartmentRepository : BaseRepository, IDepartmentRepository
    {
        public async Task<List<Department>> GetAllAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var departments = new List<Department>();
                var sql = @"
                    SELECT d.dept_id, d.dept_name,
                           COUNT(u.user_id) as employee_count,
                           d.head_user_id,
                           h.name as head_name
                    FROM departments d
                    LEFT JOIN users u ON d.dept_id = u.default_dept_id
                    LEFT JOIN users h ON d.head_user_id = h.user_id
                    GROUP BY d.dept_id, d.dept_name, d.head_user_id, h.name
                    ORDER BY d.dept_name";
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        departments.Add(new Department
                        {
                            DeptId = reader.GetInt32(0),
                            DeptName = reader.GetString(1),
                            EmployeeCount = reader.GetInt32(2),
                            HeadUserId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                            HeadName = reader.IsDBNull(4) ? null : reader.GetString(4)
                        });
                    }
                }
                return departments;
            });
        }

        public async Task<Department> GetByIdAsync(int id)
        {
            return await ExecuteAsync(async conn =>
            {
                using (var cmd = new NpgsqlCommand("SELECT dept_id, dept_name FROM departments WHERE dept_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            };
                        }
                    }
                }
                return null;
            });
        }

        public async Task<Department> GetByNameAsync(string name)
        {
            return await ExecuteAsync(async conn =>
            {
                using (var cmd = new NpgsqlCommand("SELECT dept_id, dept_name FROM departments WHERE dept_name = @name", conn))
                {
                    cmd.Parameters.AddWithValue("name", name);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            };
                        }
                    }
                }
                return null;
            });
        }

        public async Task<int> AddAsync(Department entity)
        {
            var sql = "INSERT INTO departments (dept_name, head_user_id) VALUES (@name, @headUserId) RETURNING dept_id";
            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("name", entity.DeptName),
                new NpgsqlParameter("headUserId", (object)entity.HeadUserId ?? DBNull.Value));
            return (int)result;
        }

        public async Task UpdateAsync(Department entity)
        {
            var sql = "UPDATE departments SET dept_name = @name, head_user_id = @headUserId WHERE dept_id = @id";
            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("name", entity.DeptName),
                new NpgsqlParameter("headUserId", (object)entity.HeadUserId ?? DBNull.Value),
                new NpgsqlParameter("id", entity.DeptId));
        }

        public async Task DeleteAsync(int id)
        {
            await ExecuteNonQueryAsync("DELETE FROM departments WHERE dept_id = @id",
                new NpgsqlParameter("id", id));
        }

        public async Task<List<Department>> GetAccessibleDepartmentsAsync(int? userId, string userRole)
        {
            if (userRole == "superadmin")
            {
                return await GetAllAsync();
            }

            return await ExecuteAsync(async conn =>
            {
                var departments = new List<Department>();
                var sql = @"
                    SELECT DISTINCT d.dept_id, d.dept_name
                    FROM departments d
                    INNER JOIN user_department_permissions udp ON d.dept_id = udp.dept_id
                    WHERE udp.user_id = @userId
                    ORDER BY d.dept_name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId ?? 0);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            });
                        }
                    }
                }
                return departments;
            });
        }

        public async Task<List<Department>> GetAccessibleDepartmentsAsync(List<int> departmentIds)
        {
            if (departmentIds == null || departmentIds.Count == 0)
            {
                return new List<Department>();
            }

            return await ExecuteAsync(async conn =>
            {
                var departments = new List<Department>();
                var sql = "SELECT dept_id, dept_name FROM departments WHERE dept_id = ANY(@ids) ORDER BY dept_name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("ids", departmentIds.ToArray());
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            departments.Add(new Department
                            {
                                DeptId = reader.GetInt32(0),
                                DeptName = reader.GetString(1)
                            });
                        }
                    }
                }
                return departments;
            });
        }

        public async Task<bool> ExistsAsync(string name, int? excludeId = null)
        {
            var sql = excludeId.HasValue
                ? "SELECT COUNT(*) FROM departments WHERE dept_name = @name AND dept_id != @excludeId"
                : "SELECT COUNT(*) FROM departments WHERE dept_name = @name";

            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("name", name) };
            if (excludeId.HasValue)
            {
                parameters.Add(new NpgsqlParameter("excludeId", excludeId.Value));
            }

            var count = await ExecuteScalarAsync(sql, parameters.ToArray());
            return (long)count > 0;
        }
    }
}
