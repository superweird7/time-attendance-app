using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZKTecoManager.Data.Interfaces;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Pagination;

namespace ZKTecoManager.Data.Repositories
{
    /// <summary>
    /// Repository implementation for User (Employee) operations.
    /// </summary>
    public class UserRepository : BaseRepository, IUserRepository
    {
        public async Task<List<User>> GetAllAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var users = new List<User>();
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    ORDER BY u.name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(MapUser(reader));
                    }
                }
                return users;
            });
        }

        public async Task<PagedResult<User>> GetPagedAsync(PaginationParams pagination)
        {
            return await ExecuteAsync(async conn =>
            {
                var result = new PagedResult<User>
                {
                    Page = pagination.Page,
                    PageSize = pagination.PageSize
                };

                // Build WHERE clause
                var whereClause = new List<string>();

                // Role-based filtering
                if (CurrentUser.Role != "superadmin" && CurrentUser.PermittedDepartmentIds != null)
                {
                    whereClause.Add("u.default_dept_id = ANY(@permittedDepts)");
                }

                if (pagination.DepartmentId.HasValue)
                {
                    whereClause.Add("u.default_dept_id = @deptId");
                }

                if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
                {
                    whereClause.Add("(u.name ILIKE @search OR u.badge_number ILIKE @search)");
                }

                var whereString = whereClause.Count > 0 ? "WHERE " + string.Join(" AND ", whereClause) : "";

                // Get total count - create fresh parameters for count command
                var countSql = $"SELECT COUNT(*) FROM users u {whereString}";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    AddWhereParameters(countCmd, pagination);
                    result.TotalCount = (int)(long)await countCmd.ExecuteScalarAsync();
                }

                // Get paged data - create fresh parameters for data command
                var dataSql = $@"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    {whereString}
                    ORDER BY u.name
                    LIMIT @limit OFFSET @offset";

                using (var cmd = new NpgsqlCommand(dataSql, conn))
                {
                    AddWhereParameters(cmd, pagination);
                    cmd.Parameters.AddWithValue("limit", pagination.PageSize);
                    cmd.Parameters.AddWithValue("offset", pagination.Skip);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Items.Add(MapUser(reader));
                        }
                    }
                }

                return result;
            });
        }

        private void AddWhereParameters(NpgsqlCommand cmd, PaginationParams pagination)
        {
            if (CurrentUser.Role != "superadmin" && CurrentUser.PermittedDepartmentIds != null)
            {
                cmd.Parameters.AddWithValue("permittedDepts", CurrentUser.PermittedDepartmentIds.ToArray());
            }

            if (pagination.DepartmentId.HasValue)
            {
                cmd.Parameters.AddWithValue("deptId", pagination.DepartmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
            {
                cmd.Parameters.AddWithValue("search", $"%{pagination.SearchTerm}%");
            }
        }

        public async Task<int> BulkAssignShiftAsync(List<int> userIds, int shiftId)
        {
            var sql = "UPDATE users SET shift_id = @shiftId WHERE user_id = ANY(@userIds)";
            return await ExecuteAsync(async conn =>
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("shiftId", shiftId);
                    cmd.Parameters.AddWithValue("userIds", userIds.ToArray());
                    return await cmd.ExecuteNonQueryAsync();
                }
            });
        }

        public async Task DeleteMultipleAsync(List<int> userIds)
        {
            var sql = "DELETE FROM users WHERE user_id = ANY(@ids)";
            await ExecuteNonQueryAsync(sql, new NpgsqlParameter("ids", userIds.ToArray()));
        }

        public async Task<User> GetByIdAsync(int id)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE u.user_id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapUser(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<User> GetByBadgeNumberAsync(string badgeNumber)
        {
            return await ExecuteAsync(async conn =>
            {
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE u.badge_number = @badge";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapUser(reader);
                        }
                    }
                }
                return null;
            });
        }

        public async Task<List<User>> GetByDepartmentAsync(int departmentId)
        {
            return await ExecuteAsync(async conn =>
            {
                var users = new List<User>();
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE u.default_dept_id = @deptId
                    ORDER BY u.name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("deptId", departmentId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(MapUser(reader));
                        }
                    }
                }
                return users;
            });
        }

        public async Task<List<User>> GetAccessibleUsersAsync(int? currentUserId, string userRole, List<int> departmentIds = null)
        {
            if (userRole == "superadmin" && departmentIds == null)
            {
                return await GetAllAsync();
            }

            return await ExecuteAsync(async conn =>
            {
                var users = new List<User>();
                string sql;

                if (departmentIds != null && departmentIds.Count > 0)
                {
                    sql = @"
                        SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                               u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                        FROM users u
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        WHERE u.default_dept_id = ANY(@deptIds)
                        ORDER BY u.name";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("deptIds", departmentIds.ToArray());
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(MapUser(reader));
                            }
                        }
                    }
                }
                else
                {
                    sql = @"
                        SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                               u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                        FROM users u
                        LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                        LEFT JOIN shifts s ON u.shift_id = s.shift_id
                        INNER JOIN user_department_permissions udp ON u.default_dept_id = udp.dept_id
                        WHERE udp.user_id = @userId
                        ORDER BY u.name";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("userId", currentUserId ?? 0);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(MapUser(reader));
                            }
                        }
                    }
                }

                return users;
            });
        }

        public async Task<User> ValidateCredentialsAsync(string username, string password)
        {
            return await ExecuteAsync(async conn =>
            {
                // Fetch user by username (badge number) first
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE TRIM(u.badge_number) = @username";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("username", username.Trim());
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var user = MapUser(reader);
                            // Verify password using secure helper (supports hashed and legacy passwords)
                            if (PasswordHelper.VerifyPassword(password, user.Password))
                            {
                                return user;
                            }
                        }
                    }
                }
                return null;
            });
        }

        public async Task<List<User>> GetUsersWithShiftsAsync()
        {
            return await ExecuteAsync(async conn =>
            {
                var users = new List<User>();
                var sql = @"
                    SELECT u.user_id, u.badge_number, u.name, u.default_dept_id, d.dept_name,
                           u.shift_id, s.shift_name, u.password, u.role, u.can_edit_times
                    FROM users u
                    LEFT JOIN departments d ON u.default_dept_id = d.dept_id
                    LEFT JOIN shifts s ON u.shift_id = s.shift_id
                    WHERE u.shift_id IS NOT NULL
                    ORDER BY u.name";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(MapUser(reader));
                    }
                }
                return users;
            });
        }

        public async Task<int> AddAsync(User entity)
        {
            var sql = @"
                INSERT INTO users (badge_number, name, default_dept_id, shift_id, password, role, can_edit_times)
                VALUES (@badge, @name, @deptId, @shiftId, @password, @role, @canEdit)
                RETURNING user_id";

            var result = await ExecuteScalarAsync(sql,
                new NpgsqlParameter("badge", entity.BadgeNumber),
                new NpgsqlParameter("name", entity.Name),
                new NpgsqlParameter("deptId", entity.DefaultDeptId),
                new NpgsqlParameter("shiftId", (object)entity.ShiftId ?? System.DBNull.Value),
                new NpgsqlParameter("password", entity.Password ?? ""),
                new NpgsqlParameter("role", entity.Role ?? ""),
                new NpgsqlParameter("canEdit", entity.CanEditTimes));

            return (int)result;
        }

        public async Task UpdateAsync(User entity)
        {
            var sql = @"
                UPDATE users SET
                    badge_number = @badge, name = @name, default_dept_id = @deptId,
                    shift_id = @shiftId, password = @password,
                    role = @role, can_edit_times = @canEdit
                WHERE user_id = @id";

            await ExecuteNonQueryAsync(sql,
                new NpgsqlParameter("badge", entity.BadgeNumber),
                new NpgsqlParameter("name", entity.Name),
                new NpgsqlParameter("deptId", entity.DefaultDeptId),
                new NpgsqlParameter("shiftId", (object)entity.ShiftId ?? System.DBNull.Value),
                new NpgsqlParameter("password", entity.Password ?? ""),
                new NpgsqlParameter("role", entity.Role ?? ""),
                new NpgsqlParameter("canEdit", entity.CanEditTimes),
                new NpgsqlParameter("id", entity.UserId));
        }

        public async Task DeleteAsync(int id)
        {
            await ExecuteNonQueryAsync("DELETE FROM users WHERE user_id = @id",
                new NpgsqlParameter("id", id));
        }

        public async Task<bool> BadgeExistsAsync(string badgeNumber, int? excludeUserId = null)
        {
            var sql = excludeUserId.HasValue
                ? "SELECT COUNT(*) FROM users WHERE badge_number = @badge AND user_id != @excludeId"
                : "SELECT COUNT(*) FROM users WHERE badge_number = @badge";

            var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("badge", badgeNumber) };
            if (excludeUserId.HasValue)
            {
                parameters.Add(new NpgsqlParameter("excludeId", excludeUserId.Value));
            }

            var count = await ExecuteScalarAsync(sql, parameters.ToArray());
            return (long)count > 0;
        }

        public async Task<int> GetCountAsync(int? departmentId = null)
        {
            var sql = departmentId.HasValue
                ? "SELECT COUNT(*) FROM users WHERE default_dept_id = @deptId"
                : "SELECT COUNT(*) FROM users";

            var parameters = departmentId.HasValue
                ? new[] { new NpgsqlParameter("deptId", departmentId.Value) }
                : new NpgsqlParameter[0];

            var count = await ExecuteScalarAsync(sql, parameters);
            return (int)(long)count;
        }

        private User MapUser(NpgsqlDataReader reader)
        {
            return new User
            {
                UserId = reader.GetInt32(0),
                BadgeNumber = reader.GetString(1),
                Name = reader.GetString(2),
                DefaultDeptId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Departments = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ShiftId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                ShiftName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Password = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Role = reader.IsDBNull(8) ? "" : reader.GetString(8),
                CanEditTimes = !reader.IsDBNull(9) && reader.GetBoolean(9)
            };
        }
    }
}
