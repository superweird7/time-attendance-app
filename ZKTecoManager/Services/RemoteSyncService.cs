using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZKTecoManager.Data.Repositories;
using ZKTecoManager.Infrastructure;
using ZKTecoManager.Models.Sync;

namespace ZKTecoManager.Services
{
    /// <summary>
    /// Service for synchronizing data from remote locations.
    /// </summary>
    public class RemoteSyncService
    {
        private readonly RemoteLocationRepository _locationRepository;
        private readonly string _localConnectionString;

        public RemoteSyncService()
        {
            _locationRepository = new RemoteLocationRepository();
            _localConnectionString = DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Tests connection to a remote location.
        /// </summary>
        public async Task<bool> TestConnectionAsync(RemoteLocation location)
        {
            try
            {
                using (var conn = new NpgsqlConnection(location.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                    {
                        await cmd.ExecuteScalarAsync();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Fetches pending changes from a remote location without applying them.
        /// </summary>
        public async Task<List<PendingChange>> FetchPendingChangesAsync(RemoteLocation location, DateTime? lastSync)
        {
            var changes = new List<PendingChange>();
            var since = lastSync ?? DateTime.MinValue;

            // Fetch each table with error handling for schema differences
            try { changes.AddRange(await FetchUserChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Users sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchDepartmentChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Departments sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchShiftChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Shifts sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchAttendanceChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Attendance sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchMachineChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Machines sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchExceptionTypesChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Exception types sync error: {ex.Message}"); }

            try { changes.AddRange(await FetchEmployeeExceptionsChangesAsync(location, since)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Employee exceptions sync error: {ex.Message}"); }

            return changes;
        }

        /// <summary>
        /// Applies approved changes to the local database.
        /// </summary>
        public async Task<SyncResult> ApplyChangesAsync(List<PendingChange> changes, int locationId)
        {
            var startTime = DateTime.Now;
            var result = new SyncResult { Success = true };

            using (var localConn = new NpgsqlConnection(_localConnectionString))
            {
                await localConn.OpenAsync();

                foreach (var change in changes)
                {
                    if (!change.IsApproved)
                    {
                        result.RecordsSkipped++;
                        continue;
                    }

                    try
                    {
                        switch (change.TableName)
                        {
                            case "users":
                                await ApplyUserChangeAsync(localConn, change);
                                break;
                            case "departments":
                                await ApplyDepartmentChangeAsync(localConn, change);
                                break;
                            case "shifts":
                                await ApplyShiftChangeAsync(localConn, change);
                                break;
                            case "attendance_logs":
                                await ApplyAttendanceChangeAsync(localConn, change);
                                break;
                            case "machines":
                                await ApplyMachineChangeAsync(localConn, change);
                                break;
                            case "exception_types":
                                await ApplyExceptionTypeChangeAsync(localConn, change);
                                break;
                            case "employee_exceptions":
                                await ApplyEmployeeExceptionChangeAsync(localConn, change);
                                break;
                        }

                        if (change.ChangeType == ChangeType.New)
                            result.RecordsAdded++;
                        else
                            result.RecordsUpdated++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{change.TableName}/{change.RecordKey}: {ex.Message}");
                    }
                }
            }

            result.Duration = DateTime.Now - startTime;

            // Update sync status
            await _locationRepository.UpdateSyncStatusAsync(locationId, result.Success ? "نجاح" : "فشل");
            await _locationRepository.LogSyncAsync(locationId, "Full", result, startTime);

            return result;
        }

        #region Fetch Methods

        private async Task<List<PendingChange>> FetchUserChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                // Only basic columns guaranteed to exist
                var sql = "SELECT user_id, badge_number, name, default_dept_id FROM users";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var badgeNumber = reader.GetString(1);
                        var name = reader.GetString(2);
                        var deptId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                        // Check if exists locally and if data is different
                        var (localExists, isDifferent) = await CheckUserDifferentAsync(badgeNumber, name, deptId);

                        // Only add if new OR if data is actually different
                        if (!localExists || isDifferent)
                        {
                            var change = new PendingChange
                            {
                                TableName = "users",
                                RecordKey = badgeNumber,
                                RecordDescription = $"{name} ({badgeNumber})",
                                RemoteValue = name,
                                ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                RemoteRecord = new
                                {
                                    UserId = reader.GetInt32(0),
                                    BadgeNumber = badgeNumber,
                                    Name = name,
                                    DefaultDeptId = deptId
                                }
                            };
                            changes.Add(change);
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchDepartmentChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                var sql = "SELECT dept_id, dept_name FROM departments";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var deptId = reader.GetInt32(0);
                        var deptName = reader.GetString(1);

                        var (localExists, isDifferent) = await CheckDepartmentDifferentAsync(deptId, deptName);

                        if (!localExists || isDifferent)
                        {
                            var change = new PendingChange
                            {
                                TableName = "departments",
                                RecordKey = deptId.ToString(),
                                RecordDescription = deptName,
                                RemoteValue = deptName,
                                ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                RemoteRecord = new { DeptId = deptId, DeptName = deptName }
                            };
                            changes.Add(change);
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchShiftChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                var sql = "SELECT shift_id, shift_name, start_time, end_time FROM shifts";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var shiftId = reader.GetInt32(0);
                        var shiftName = reader.GetString(1);
                        var startTime = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2);
                        var endTime = reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetTimeSpan(3);

                        var (localExists, isDifferent) = await CheckShiftDifferentAsync(shiftId, shiftName, startTime, endTime);

                        if (!localExists || isDifferent)
                        {
                            var change = new PendingChange
                            {
                                TableName = "shifts",
                                RecordKey = shiftId.ToString(),
                                RecordDescription = shiftName,
                                RemoteValue = shiftName,
                                ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                RemoteRecord = new
                                {
                                    ShiftId = shiftId,
                                    ShiftName = shiftName,
                                    StartTime = startTime,
                                    EndTime = endTime
                                }
                            };
                            changes.Add(change);
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchAttendanceChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                var sql = @"SELECT log_id, user_badge_number, log_time, machine_id
                           FROM attendance_logs WHERE log_time > @since";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                {
                    cmd.Parameters.AddWithValue("since", since == DateTime.MinValue ? new DateTime(2020, 1, 1) : since);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var logId = reader.GetInt32(0);
                            var badge = reader.GetString(1);
                            var logTime = reader.GetDateTime(2);
                            var machineId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                            var change = new PendingChange
                            {
                                TableName = "attendance_logs",
                                RecordKey = $"{badge}_{logTime:yyyyMMddHHmmss}",
                                RecordDescription = $"{badge} @ {logTime:yyyy-MM-dd HH:mm}",
                                RemoteValue = logTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                ChangeType = ChangeType.New,
                                RemoteRecord = new
                                {
                                    LogId = logId,
                                    UserBadgeNumber = badge,
                                    LogTime = logTime,
                                    MachineId = machineId
                                }
                            };

                            var localExists = await CheckAttendanceExistsLocallyAsync(badge, logTime);
                            if (!localExists)
                            {
                                changes.Add(change);
                            }
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchMachineChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                // Use actual column names: id, machine_alias, ip_address
                var sql = "SELECT id, machine_alias, ip_address FROM machines";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var machineId = reader.GetInt32(0);
                        var machineName = reader.GetString(1);
                        var ipAddress = reader.GetString(2);

                        var (localExists, isDifferent) = await CheckMachineDifferentAsync(machineId, machineName, ipAddress);

                        if (!localExists || isDifferent)
                        {
                            var change = new PendingChange
                            {
                                TableName = "machines",
                                RecordKey = machineId.ToString(),
                                RecordDescription = $"{machineName} ({ipAddress})",
                                RemoteValue = machineName,
                                ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                RemoteRecord = new
                                {
                                    MachineId = machineId,
                                    MachineName = machineName,
                                    IpAddress = ipAddress
                                }
                            };
                            changes.Add(change);
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchExceptionTypesChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                var sql = "SELECT exception_type_id, exception_name, description, is_active FROM exception_types";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var typeId = reader.GetInt32(0);
                        var typeName = reader.GetString(1);
                        var description = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        var isActive = reader.IsDBNull(3) ? true : reader.GetBoolean(3);

                        var (localExists, isDifferent) = await CheckExceptionTypeDifferentAsync(typeId, typeName, description, isActive);

                        if (!localExists || isDifferent)
                        {
                            var change = new PendingChange
                            {
                                TableName = "exception_types",
                                RecordKey = typeId.ToString(),
                                RecordDescription = typeName,
                                RemoteValue = typeName,
                                ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                RemoteRecord = new
                                {
                                    ExceptionTypeId = typeId,
                                    ExceptionName = typeName,
                                    Description = description,
                                    IsActive = isActive
                                }
                            };
                            changes.Add(change);
                        }
                    }
                }
            }

            return changes;
        }

        private async Task<List<PendingChange>> FetchEmployeeExceptionsChangesAsync(RemoteLocation location, DateTime since)
        {
            var changes = new List<PendingChange>();

            using (var remoteConn = new NpgsqlConnection(location.GetConnectionString()))
            {
                await remoteConn.OpenAsync();

                // Get exceptions with user name for display, filter by updated_at if available
                var sql = @"SELECT ee.exception_id, ee.user_id_fk, u.name, u.badge_number,
                                   ee.exception_type_id_fk, et.exception_name,
                                   ee.exception_date, ee.notes, ee.clock_in_override, ee.clock_out_override,
                                   ee.updated_at
                            FROM employee_exceptions ee
                            LEFT JOIN users u ON ee.user_id_fk = u.user_id
                            LEFT JOIN exception_types et ON ee.exception_type_id_fk = et.exception_type_id
                            WHERE ee.updated_at > @since OR ee.created_at > @since";

                using (var cmd = new NpgsqlCommand(sql, remoteConn))
                {
                    cmd.Parameters.AddWithValue("since", since == DateTime.MinValue ? new DateTime(2020, 1, 1) : since);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var exceptionId = reader.GetInt32(0);
                            var userId = reader.GetInt32(1);
                            var userName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            var badgeNumber = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            var exceptionTypeId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                            var exceptionName = reader.IsDBNull(5) ? "" : reader.GetString(5);
                            var exceptionDate = reader.GetDateTime(6);
                            var notes = reader.IsDBNull(7) ? "" : reader.GetString(7);
                            var clockInOverride = reader.IsDBNull(8) ? (TimeSpan?)null : reader.GetTimeSpan(8);
                            var clockOutOverride = reader.IsDBNull(9) ? (TimeSpan?)null : reader.GetTimeSpan(9);

                            var (localExists, isDifferent) = await CheckEmployeeExceptionDifferentAsync(
                                badgeNumber, exceptionDate, exceptionTypeId, notes, clockInOverride, clockOutOverride);

                            if (!localExists || isDifferent)
                            {
                                var displayText = $"{userName} - {exceptionDate:yyyy-MM-dd}";
                                if (!string.IsNullOrEmpty(exceptionName))
                                    displayText += $" ({exceptionName})";

                                var change = new PendingChange
                                {
                                    TableName = "employee_exceptions",
                                    RecordKey = $"{badgeNumber}_{exceptionDate:yyyyMMdd}",
                                    RecordDescription = displayText,
                                    RemoteValue = exceptionName,
                                    ChangeType = localExists ? ChangeType.Updated : ChangeType.New,
                                    RemoteRecord = new
                                    {
                                        ExceptionId = exceptionId,
                                        BadgeNumber = badgeNumber,
                                        ExceptionTypeId = exceptionTypeId,
                                        ExceptionDate = exceptionDate,
                                        Notes = notes,
                                        ClockInOverride = clockInOverride,
                                        ClockOutOverride = clockOutOverride
                                    }
                                };
                                changes.Add(change);
                            }
                        }
                    }
                }
            }

            return changes;
        }

        #endregion

        #region Check Exists Methods

        private async Task<(bool exists, bool isDifferent)> CheckUserDifferentAsync(string badgeNumber, string remoteName, int remoteDeptId)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT name, default_dept_id FROM users WHERE badge_number = @badge";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localName = reader.GetString(0);
                            var localDeptId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            bool isDifferent = localName != remoteName || localDeptId != remoteDeptId;
                            return (true, isDifferent);
                        }
                        return (false, false); // Doesn't exist
                    }
                }
            }
        }

        private async Task<(bool exists, bool isDifferent)> CheckDepartmentDifferentAsync(int deptId, string remoteName)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT dept_name FROM departments WHERE dept_id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", deptId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return (true, result.ToString() != remoteName);
                    }
                    return (false, false);
                }
            }
        }

        private async Task<(bool exists, bool isDifferent)> CheckShiftDifferentAsync(int shiftId, string remoteName, TimeSpan remoteStart, TimeSpan remoteEnd)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT shift_name, start_time, end_time FROM shifts WHERE shift_id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", shiftId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localName = reader.GetString(0);
                            var localStart = reader.IsDBNull(1) ? TimeSpan.Zero : reader.GetTimeSpan(1);
                            var localEnd = reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetTimeSpan(2);
                            bool isDifferent = localName != remoteName || localStart != remoteStart || localEnd != remoteEnd;
                            return (true, isDifferent);
                        }
                        return (false, false);
                    }
                }
            }
        }

        private async Task<bool> CheckAttendanceExistsLocallyAsync(string badge, DateTime logTime)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT COUNT(*) FROM attendance_logs WHERE user_badge_number = @badge AND log_time = @time";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badge);
                    cmd.Parameters.AddWithValue("time", logTime);
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }
            }
        }

        private async Task<(bool exists, bool isDifferent)> CheckMachineDifferentAsync(int machineId, string remoteName, string remoteIp)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT machine_alias, ip_address FROM machines WHERE id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", machineId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localName = reader.GetString(0);
                            var localIp = reader.GetString(1);
                            bool isDifferent = localName != remoteName || localIp != remoteIp;
                            return (true, isDifferent);
                        }
                        return (false, false);
                    }
                }
            }
        }

        private async Task<(bool exists, bool isDifferent)> CheckExceptionTypeDifferentAsync(int typeId, string remoteName, string remoteDesc, bool remoteActive)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = "SELECT exception_name, description, is_active FROM exception_types WHERE exception_type_id = @id";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("id", typeId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localName = reader.GetString(0);
                            var localDesc = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            var localActive = reader.IsDBNull(2) ? true : reader.GetBoolean(2);
                            bool isDifferent = localName != remoteName || localDesc != remoteDesc || localActive != remoteActive;
                            return (true, isDifferent);
                        }
                        return (false, false);
                    }
                }
            }
        }

        private async Task<(bool exists, bool isDifferent)> CheckEmployeeExceptionDifferentAsync(
            string badgeNumber, DateTime exceptionDate, int? remoteTypeId, string remoteNotes,
            TimeSpan? remoteClockIn, TimeSpan? remoteClockOut)
        {
            using (var conn = new NpgsqlConnection(_localConnectionString))
            {
                await conn.OpenAsync();
                var sql = @"SELECT ee.exception_type_id_fk, ee.notes, ee.clock_in_override, ee.clock_out_override
                           FROM employee_exceptions ee
                           JOIN users u ON ee.user_id_fk = u.user_id
                           WHERE u.badge_number = @badge AND ee.exception_date = @date";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("badge", badgeNumber);
                    cmd.Parameters.AddWithValue("date", exceptionDate.Date);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var localTypeId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                            var localNotes = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            var localClockIn = reader.IsDBNull(2) ? (TimeSpan?)null : reader.GetTimeSpan(2);
                            var localClockOut = reader.IsDBNull(3) ? (TimeSpan?)null : reader.GetTimeSpan(3);

                            bool isDifferent = localTypeId != remoteTypeId ||
                                               localNotes != remoteNotes ||
                                               localClockIn != remoteClockIn ||
                                               localClockOut != remoteClockOut;
                            return (true, isDifferent);
                        }
                        return (false, false);
                    }
                }
            }
        }

        #endregion

        #region Apply Change Methods

        private async Task ApplyUserChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            var sql = @"INSERT INTO users (badge_number, name, default_dept_id)
                       VALUES (@badge, @name, @dept)
                       ON CONFLICT (badge_number) DO UPDATE SET
                       name = @name, default_dept_id = @dept";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("badge", (string)record.BadgeNumber);
                cmd.Parameters.AddWithValue("name", (string)record.Name);
                cmd.Parameters.AddWithValue("dept", (int)record.DefaultDeptId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyDepartmentChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            var sql = @"INSERT INTO departments (dept_id, dept_name) VALUES (@id, @name)
                       ON CONFLICT (dept_id) DO UPDATE SET dept_name = @name";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", (int)record.DeptId);
                cmd.Parameters.AddWithValue("name", (string)record.DeptName);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyShiftChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            var sql = @"INSERT INTO shifts (shift_id, shift_name, start_time, end_time)
                       VALUES (@id, @name, @start, @end)
                       ON CONFLICT (shift_id) DO UPDATE SET
                       shift_name = @name, start_time = @start, end_time = @end";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", (int)record.ShiftId);
                cmd.Parameters.AddWithValue("name", (string)record.ShiftName);
                cmd.Parameters.AddWithValue("start", (TimeSpan)record.StartTime);
                cmd.Parameters.AddWithValue("end", (TimeSpan)record.EndTime);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyAttendanceChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            // Normalize badge number - remove leading zeros
            string badgeNumber = ((string)record.UserBadgeNumber)?.TrimStart('0') ?? "0";
            if (string.IsNullOrEmpty(badgeNumber)) badgeNumber = "0";

            var sql = @"INSERT INTO attendance_logs (user_badge_number, log_time, machine_id)
                       VALUES (@badge, @time, @machine)
                       ON CONFLICT DO NOTHING";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("badge", badgeNumber);
                cmd.Parameters.AddWithValue("time", (DateTime)record.LogTime);
                cmd.Parameters.AddWithValue("machine", (int)record.MachineId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyMachineChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            var sql = @"INSERT INTO machines (id, machine_alias, ip_address)
                       VALUES (@id, @name, @ip)
                       ON CONFLICT (id) DO UPDATE SET
                       machine_alias = @name, ip_address = @ip";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", (int)record.MachineId);
                cmd.Parameters.AddWithValue("name", (string)record.MachineName);
                cmd.Parameters.AddWithValue("ip", (string)record.IpAddress);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyExceptionTypeChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            var sql = @"INSERT INTO exception_types (exception_type_id, exception_name, description, is_active)
                       VALUES (@id, @name, @desc, @active)
                       ON CONFLICT (exception_type_id) DO UPDATE SET
                       exception_name = @name, description = @desc, is_active = @active";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", (int)record.ExceptionTypeId);
                cmd.Parameters.AddWithValue("name", (string)record.ExceptionName);
                cmd.Parameters.AddWithValue("desc", (object)(string)record.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("active", (bool)record.IsActive);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyEmployeeExceptionChangeAsync(NpgsqlConnection conn, PendingChange change)
        {
            dynamic record = change.RemoteRecord;

            string badgeNumber = (string)record.BadgeNumber;
            DateTime exceptionDate = (DateTime)record.ExceptionDate;
            int? exceptionTypeId = record.ExceptionTypeId;
            string notes = record.Notes;
            TimeSpan? clockInOverride = record.ClockInOverride;
            TimeSpan? clockOutOverride = record.ClockOutOverride;

            // First, get the user_id from badge_number
            var getUserIdSql = "SELECT user_id FROM users WHERE badge_number = @badge";
            int? userId = null;
            using (var cmd = new NpgsqlCommand(getUserIdSql, conn))
            {
                cmd.Parameters.AddWithValue("badge", badgeNumber);
                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    userId = Convert.ToInt32(result);
                }
            }

            if (userId == null)
            {
                throw new Exception($"User with badge {badgeNumber} not found");
            }

            // Delete existing exception for this user/date
            var deleteSql = "DELETE FROM employee_exceptions WHERE user_id_fk = @userId AND exception_date = @date";
            using (var cmd = new NpgsqlCommand(deleteSql, conn))
            {
                cmd.Parameters.AddWithValue("userId", userId.Value);
                cmd.Parameters.AddWithValue("date", exceptionDate.Date);
                await cmd.ExecuteNonQueryAsync();
            }

            // Insert new exception (if there's data to insert)
            if (exceptionTypeId.HasValue || !string.IsNullOrEmpty(notes) || clockInOverride.HasValue || clockOutOverride.HasValue)
            {
                var insertSql = @"INSERT INTO employee_exceptions
                                 (user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override, updated_at)
                                 VALUES (@userId, @typeId, @date, @notes, @clockIn, @clockOut, CURRENT_TIMESTAMP)";
                using (var cmd = new NpgsqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("userId", userId.Value);
                    cmd.Parameters.AddWithValue("typeId", exceptionTypeId.HasValue ? (object)exceptionTypeId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("date", exceptionDate.Date);
                    cmd.Parameters.AddWithValue("notes", (object)notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("clockIn", clockInOverride.HasValue ? (object)clockInOverride.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("clockOut", clockOutOverride.HasValue ? (object)clockOutOverride.Value : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        #endregion
    }
}
