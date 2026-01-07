using Npgsql;
using System;
using System.Windows;

namespace ZKTecoManager.Infrastructure
{
    /// <summary>
    /// Handles database initialization and schema creation for fresh installations.
    /// This ensures the application can be installed on any PC and work immediately.
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string DATABASE_NAME = "zkteco_db";

        /// <summary>
        /// Initializes the database. Creates the database and schema if they don't exist.
        /// </summary>
        /// <returns>True if initialization successful, false otherwise</returns>
        public static bool Initialize()
        {
            try
            {
                // First, check if database exists and create if not
                if (!DatabaseExists())
                {
                    if (!CreateDatabase())
                    {
                        return false;
                    }
                }

                // Then, ensure all tables exist
                EnsureSchema();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
                MessageBox.Show(
                    $"Database initialization failed:\n{ex.Message}\n\nفشل تهيئة قاعدة البيانات",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Checks if the database exists
        /// </summary>
        private static bool DatabaseExists()
        {
            try
            {
                // Connect to postgres database to check
                string adminConnString = "Host=localhost;Port=5432;Username=postgres;Password=2001;Database=postgres";

                using (var conn = new NpgsqlConnection(adminConnString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{DATABASE_NAME}'", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        return result != null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking database existence: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates the database
        /// </summary>
        private static bool CreateDatabase()
        {
            try
            {
                string adminConnString = "Host=localhost;Port=5432;Username=postgres;Password=2001;Database=postgres";

                using (var conn = new NpgsqlConnection(adminConnString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand($"CREATE DATABASE {DATABASE_NAME} ENCODING 'UTF8'", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                System.Diagnostics.Debug.WriteLine("Database created successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating database: {ex.Message}");
                MessageBox.Show(
                    $"Failed to create database:\n{ex.Message}\n\nفشل إنشاء قاعدة البيانات",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Ensures all required tables exist in the database
        /// </summary>
        private static void EnsureSchema()
        {
            using (var conn = new NpgsqlConnection(DatabaseConfig.ConnectionString))
            {
                conn.Open();

                // Create tables in correct order (parent tables first)
                CreateDepartmentsTable(conn);
                CreateShiftsTable(conn);
                CreateExceptionTypesTable(conn);
                CreateMachinesTable(conn);
                CreateBackupSettingsTable(conn);
                CreateSyncSettingsTable(conn);
                CreateRemoteLocationsTable(conn);
                CreateUsersTable(conn);
                CreateShiftRulesTable(conn);
                CreateBiometricDataTable(conn);
                CreateAttendanceLogsTable(conn);
                CreateEmployeeExceptionsTable(conn);
                CreateAdminDepartmentMappingsTable(conn);
                CreateAdminDeviceMappingsTable(conn);
                CreateUserDepartmentPermissionsTable(conn);
                CreateAuditLogsTable(conn);
                CreateSyncHistoryTable(conn);
                CreateSyncTableTrackingTable(conn);
                CreatePasswordPolicySettingsTable(conn);
                CreatePermissionTemplatesTable(conn);
                CreateAutoDownloadSettingsTable(conn);
                CreateWebSessionsTable(conn);
                // Leave Management System - Commented out for future implementation
                //CreateLeaveTypesTable(conn);
                //CreateLeaveBalancesTable(conn);
                //CreateLeaveRequestsTable(conn);
                //CreateLeaveApprovalHistoryTable(conn);
                CreateAnnouncementsTable(conn);
                CreateTelegramTables(conn);

                // Create performance indexes
                CreatePerformanceIndexes(conn);

                // Ensure default data exists
                EnsureDefaultData(conn);
            }
        }

        private static void ExecuteNonQuery(NpgsqlConnection conn, string sql)
        {
            try
            {
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P07") // Table already exists
            {
                // Ignore - table already exists
            }
            catch (PostgresException ex) when (ex.SqlState == "42710") // Object already exists
            {
                // Ignore - index/constraint already exists
            }
        }

        private static void CreateDepartmentsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS departments (
                    dept_id SERIAL PRIMARY KEY,
                    dept_name VARCHAR(255) NOT NULL,
                    parent_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
                    head_user_id INTEGER,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add head_user_id column if it doesn't exist (migration for existing databases)
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'departments' AND column_name = 'head_user_id') THEN
                        ALTER TABLE departments ADD COLUMN head_user_id INTEGER;
                    END IF;
                END $$;");
        }

        private static void CreateShiftsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS shifts (
                    shift_id SERIAL PRIMARY KEY,
                    shift_name VARCHAR(100) NOT NULL,
                    start_time TIME NOT NULL,
                    end_time TIME NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateShiftRulesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS shift_rules (
                    rule_id SERIAL PRIMARY KEY,
                    shift_id_fk INTEGER NOT NULL REFERENCES shifts(shift_id) ON DELETE CASCADE,
                    expected_time TIME NOT NULL,
                    rule_order INTEGER DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateExceptionTypesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS exception_types (
                    exception_type_id SERIAL PRIMARY KEY,
                    exception_name VARCHAR(255) NOT NULL UNIQUE,
                    description TEXT,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateMachinesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS machines (
                    id SERIAL PRIMARY KEY,
                    machine_alias VARCHAR(255) NOT NULL,
                    ip_address VARCHAR(45),
                    port INTEGER DEFAULT 4370,
                    serial_number VARCHAR(255),
                    machine_number INTEGER DEFAULT 1,
                    connect_type INTEGER DEFAULT 1,
                    serial_port INTEGER,
                    baudrate INTEGER DEFAULT 115200,
                    is_host BOOLEAN DEFAULT FALSE,
                    enabled BOOLEAN DEFAULT TRUE,
                    location VARCHAR(255),
                    last_sync_time TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add location column if it doesn't exist (migration for existing databases)
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'machines' AND column_name = 'location') THEN
                        ALTER TABLE machines ADD COLUMN location VARCHAR(255);
                    END IF;
                END $$;");
        }

        private static void CreateUsersTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS users (
                    user_id SERIAL PRIMARY KEY,
                    badge_number VARCHAR(50) NOT NULL UNIQUE,
                    name VARCHAR(255) NOT NULL,
                    default_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
                    shift_id INTEGER REFERENCES shifts(shift_id) ON DELETE SET NULL,
                    password VARCHAR(255),
                    role VARCHAR(50) NOT NULL DEFAULT 'user',
                    can_edit_times BOOLEAN DEFAULT FALSE,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add system_access_type column if it doesn't exist (migration for existing databases)
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'users' AND column_name = 'system_access_type') THEN
                        ALTER TABLE users ADD COLUMN system_access_type VARCHAR(20) DEFAULT 'full_access'
                            CHECK (system_access_type IN ('full_access', 'leave_only'));
                    END IF;
                END $$;");

            // Add index for system_access_type
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_users_system_access_type ON users(system_access_type)");

            // Ensure existing users have system_access_type set
            ExecuteNonQuery(conn, "UPDATE users SET system_access_type = 'full_access' WHERE system_access_type IS NULL");
        }

        private static void CreateAttendanceLogsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS attendance_logs (
                    log_id SERIAL PRIMARY KEY,
                    user_badge_number VARCHAR(50) NOT NULL,
                    log_time TIMESTAMP NOT NULL,
                    machine_id INTEGER REFERENCES machines(id) ON DELETE SET NULL,
                    verify_type INTEGER DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_attendance_log UNIQUE (user_badge_number, log_time, machine_id)
                )");
        }

        private static void CreateBiometricDataTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS biometric_data (
                    biometric_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    finger_index INTEGER NOT NULL,
                    template_data TEXT NOT NULL,
                    biometric_type INTEGER NOT NULL DEFAULT 0,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_user_finger UNIQUE (user_id_fk, finger_index, biometric_type)
                )");
        }

        private static void CreateEmployeeExceptionsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS employee_exceptions (
                    exception_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    exception_type_id_fk INTEGER REFERENCES exception_types(exception_type_id) ON DELETE SET NULL,
                    exception_date DATE NOT NULL,
                    notes TEXT,
                    clock_in_override TIME,
                    clock_out_override TIME,
                    created_by INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateAdminDepartmentMappingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS admin_department_mappings (
                    mapping_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    department_id_fk INTEGER NOT NULL REFERENCES departments(dept_id) ON DELETE CASCADE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_admin_dept UNIQUE (user_id_fk, department_id_fk)
                )");
        }

        private static void CreateAdminDeviceMappingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS admin_device_mappings (
                    mapping_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    device_id_fk INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_admin_device UNIQUE (user_id_fk, device_id_fk)
                )");
        }

        private static void CreateUserDepartmentPermissionsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS user_department_permissions (
                    permission_id SERIAL PRIMARY KEY,
                    user_id INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    dept_id INTEGER NOT NULL REFERENCES departments(dept_id) ON DELETE CASCADE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_user_dept_perm UNIQUE (user_id, dept_id)
                )");
        }

        private static void CreateAuditLogsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS audit_logs (
                    log_id SERIAL PRIMARY KEY,
                    user_id INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
                    action_type VARCHAR(50) NOT NULL,
                    table_name VARCHAR(100),
                    record_id INTEGER,
                    old_value TEXT,
                    new_value TEXT,
                    ip_address VARCHAR(50),
                    description TEXT,
                    is_synced BOOLEAN DEFAULT FALSE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add is_synced column if it doesn't exist (for existing databases)
            try
            {
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS is_synced BOOLEAN DEFAULT FALSE", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* Column might already exist */ }
        }

        private static void CreateBackupSettingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS backup_settings (
                    setting_id SERIAL PRIMARY KEY,
                    auto_backup_enabled BOOLEAN DEFAULT TRUE,
                    backup_time TIME DEFAULT '02:00:00',
                    backup_retention_days INTEGER DEFAULT 30,
                    backup_path TEXT DEFAULT 'C:\ZKTecoBackups',
                    last_backup_date TIMESTAMP,
                    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    server_backup_enabled BOOLEAN DEFAULT FALSE,
                    server_backup_path TEXT,
                    server_backup_interval_days INTEGER DEFAULT 10,
                    last_server_backup_date TIMESTAMP
                )");

            // Add new server backup columns if they don't exist (for existing databases)
            try
            {
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE backup_settings ADD COLUMN IF NOT EXISTS server_backup_enabled BOOLEAN DEFAULT FALSE", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE backup_settings ADD COLUMN IF NOT EXISTS server_backup_path TEXT", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE backup_settings ADD COLUMN IF NOT EXISTS server_backup_interval_days INTEGER DEFAULT 10", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new NpgsqlCommand(
                    "ALTER TABLE backup_settings ADD COLUMN IF NOT EXISTS last_server_backup_date TIMESTAMP", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* Columns might already exist */ }
        }

        private static void CreateRemoteLocationsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS remote_locations (
                    location_id SERIAL PRIMARY KEY,
                    location_name VARCHAR(100) NOT NULL,
                    host VARCHAR(255) NOT NULL,
                    port INTEGER DEFAULT 5432,
                    database_name VARCHAR(100) NOT NULL,
                    username VARCHAR(100) NOT NULL,
                    password VARCHAR(255) NOT NULL,
                    is_active BOOLEAN DEFAULT TRUE,
                    last_sync_time TIMESTAMP,
                    last_sync_status VARCHAR(50),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateSyncSettingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS sync_settings (
                    setting_id SERIAL PRIMARY KEY,
                    auto_sync_enabled BOOLEAN DEFAULT FALSE,
                    sync_interval_minutes INTEGER DEFAULT 15,
                    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");
        }

        private static void CreateSyncHistoryTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS sync_history (
                    sync_id SERIAL PRIMARY KEY,
                    location_id INTEGER REFERENCES remote_locations(location_id) ON DELETE CASCADE,
                    sync_type VARCHAR(50),
                    records_added INTEGER DEFAULT 0,
                    records_updated INTEGER DEFAULT 0,
                    records_skipped INTEGER DEFAULT 0,
                    records_failed INTEGER DEFAULT 0,
                    status VARCHAR(50),
                    error_message TEXT,
                    tables_synced TEXT,
                    data_size_bytes BIGINT DEFAULT 0,
                    duration_seconds INTEGER DEFAULT 0,
                    is_incremental BOOLEAN DEFAULT TRUE,
                    started_at TIMESTAMP,
                    completed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add new columns if they don't exist
            try
            {
                ExecuteNonQuery(conn, "ALTER TABLE sync_history ADD COLUMN IF NOT EXISTS records_failed INTEGER DEFAULT 0");
                ExecuteNonQuery(conn, "ALTER TABLE sync_history ADD COLUMN IF NOT EXISTS tables_synced TEXT");
                ExecuteNonQuery(conn, "ALTER TABLE sync_history ADD COLUMN IF NOT EXISTS data_size_bytes BIGINT DEFAULT 0");
                ExecuteNonQuery(conn, "ALTER TABLE sync_history ADD COLUMN IF NOT EXISTS duration_seconds INTEGER DEFAULT 0");
                ExecuteNonQuery(conn, "ALTER TABLE sync_history ADD COLUMN IF NOT EXISTS is_incremental BOOLEAN DEFAULT TRUE");
            }
            catch { /* Columns might already exist */ }
        }

        private static void CreateSyncTableTrackingTable(NpgsqlConnection conn)
        {
            // Track last sync time per table per location for incremental sync
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS sync_table_tracking (
                    tracking_id SERIAL PRIMARY KEY,
                    location_id INTEGER REFERENCES remote_locations(location_id) ON DELETE CASCADE,
                    table_name VARCHAR(100) NOT NULL,
                    last_sync_time TIMESTAMP,
                    last_record_id BIGINT DEFAULT 0,
                    is_enabled BOOLEAN DEFAULT TRUE,
                    CONSTRAINT unique_location_table UNIQUE (location_id, table_name)
                )");
        }

        private static void CreatePasswordPolicySettingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS password_policy_settings (
                    setting_id INTEGER PRIMARY KEY DEFAULT 1,
                    min_length INTEGER DEFAULT 6,
                    require_uppercase BOOLEAN DEFAULT FALSE,
                    require_lowercase BOOLEAN DEFAULT FALSE,
                    require_numbers BOOLEAN DEFAULT FALSE,
                    require_special BOOLEAN DEFAULT FALSE
                )");

            // Ensure default row exists
            ExecuteNonQuery(conn, @"
                INSERT INTO password_policy_settings (setting_id) VALUES (1)
                ON CONFLICT (setting_id) DO NOTHING");
        }

        private static void CreatePermissionTemplatesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS permission_templates (
                    template_id SERIAL PRIMARY KEY,
                    template_name VARCHAR(100) NOT NULL,
                    description TEXT,
                    department_ids TEXT,
                    device_ids TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Insert default templates if none exist
            ExecuteNonQuery(conn, @"
                INSERT INTO permission_templates (template_name, description, department_ids, device_ids)
                SELECT 'مدير موارد بشرية', 'وصول كامل لجميع الأقسام والأجهزة', 'ALL', 'ALL'
                WHERE NOT EXISTS (SELECT 1 FROM permission_templates WHERE template_name = 'مدير موارد بشرية')");

            ExecuteNonQuery(conn, @"
                INSERT INTO permission_templates (template_name, description, department_ids, device_ids)
                SELECT 'مشرف حضور', 'عرض فقط - بدون تعديل', '', ''
                WHERE NOT EXISTS (SELECT 1 FROM permission_templates WHERE template_name = 'مشرف حضور')");
        }

        private static void CreateAutoDownloadSettingsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS auto_download_settings (
                    setting_id INTEGER PRIMARY KEY DEFAULT 1,
                    enabled BOOLEAN DEFAULT FALSE,
                    interval_minutes INTEGER DEFAULT 60,
                    download_logs BOOLEAN DEFAULT TRUE,
                    last_download TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Ensure default row exists
            ExecuteNonQuery(conn, @"
                INSERT INTO auto_download_settings (setting_id) VALUES (1)
                ON CONFLICT (setting_id) DO NOTHING");
        }

        private static void CreateWebSessionsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS web_sessions (
                    session_id VARCHAR(64) PRIMARY KEY,
                    user_id INTEGER REFERENCES users(user_id) ON DELETE CASCADE,
                    user_type VARCHAR(20) DEFAULT 'employee',
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    expires_at TIMESTAMP,
                    ip_address VARCHAR(45),
                    user_agent TEXT
                )");

            // Index for fast session lookups
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_web_sessions_expires ON web_sessions(expires_at)");
        }

        private static void CreateLeaveTypesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS leave_types (
                    leave_type_id SERIAL PRIMARY KEY,
                    leave_type_name VARCHAR(100) NOT NULL UNIQUE,
                    default_annual_days INTEGER DEFAULT 0,
                    requires_approval BOOLEAN DEFAULT TRUE,
                    deducts_from_balance BOOLEAN DEFAULT TRUE,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add index
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_types_active ON leave_types(is_active)");

            // Insert default leave types
            ExecuteNonQuery(conn, @"
                INSERT INTO leave_types (leave_type_name, default_annual_days, requires_approval, deducts_from_balance)
                SELECT 'إجازة سنوية - Annual Leave', 30, TRUE, TRUE WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE leave_type_name = 'إجازة سنوية - Annual Leave')");

            ExecuteNonQuery(conn, @"
                INSERT INTO leave_types (leave_type_name, default_annual_days, requires_approval, deducts_from_balance)
                SELECT 'إجازة مرضية - Sick Leave', 15, TRUE, TRUE WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE leave_type_name = 'إجازة مرضية - Sick Leave')");

            ExecuteNonQuery(conn, @"
                INSERT INTO leave_types (leave_type_name, default_annual_days, requires_approval, deducts_from_balance)
                SELECT 'إجازة طارئة - Emergency Leave', 5, TRUE, TRUE WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE leave_type_name = 'إجازة طارئة - Emergency Leave')");

            ExecuteNonQuery(conn, @"
                INSERT INTO leave_types (leave_type_name, default_annual_days, requires_approval, deducts_from_balance)
                SELECT 'إجازة بدون راتب - Unpaid Leave', 0, TRUE, FALSE WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE leave_type_name = 'إجازة بدون راتب - Unpaid Leave')");

            ExecuteNonQuery(conn, @"
                INSERT INTO leave_types (leave_type_name, default_annual_days, requires_approval, deducts_from_balance)
                SELECT 'إجازة زواج - Marriage Leave', 3, TRUE, TRUE WHERE NOT EXISTS (SELECT 1 FROM leave_types WHERE leave_type_name = 'إجازة زواج - Marriage Leave')");
        }

        private static void CreateLeaveBalancesTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS leave_balances (
                    balance_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    leave_type_id_fk INTEGER NOT NULL REFERENCES leave_types(leave_type_id) ON DELETE CASCADE,
                    year INTEGER NOT NULL,
                    total_days DECIMAL(5,2) DEFAULT 0,
                    used_days DECIMAL(5,2) DEFAULT 0,
                    remaining_days DECIMAL(5,2) GENERATED ALWAYS AS (total_days - used_days) STORED,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT unique_user_leave_type_year UNIQUE (user_id_fk, leave_type_id_fk, year)
                )");

            // Add indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_balances_user ON leave_balances(user_id_fk)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_balances_year ON leave_balances(year)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_balances_user_year ON leave_balances(user_id_fk, year)");
        }

        private static void CreateLeaveRequestsTable(NpgsqlConnection conn)
        {
            // Drop old leave_requests table if it exists (from previous version)
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'leave_requests' AND column_name = 'request_id') THEN
                        DROP TABLE IF EXISTS leave_requests CASCADE;
                    END IF;
                END $$;");

            // Create new enhanced leave_requests table
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS leave_requests (
                    leave_request_id SERIAL PRIMARY KEY,
                    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
                    leave_type_id_fk INTEGER NOT NULL REFERENCES leave_types(leave_type_id),
                    start_date DATE NOT NULL,
                    end_date DATE NOT NULL,
                    total_days DECIMAL(5,2) NOT NULL,
                    reason TEXT,
                    status VARCHAR(20) DEFAULT 'pending'
                        CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
                    requested_by INTEGER REFERENCES users(user_id),
                    requested_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    approved_by INTEGER REFERENCES users(user_id),
                    approved_at TIMESTAMP,
                    rejection_reason TEXT,
                    notes TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_requests_user ON leave_requests(user_id_fk)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_requests_status ON leave_requests(status)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_requests_dates ON leave_requests(start_date, end_date)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_requests_user_dates ON leave_requests(user_id_fk, start_date, end_date)");
        }

        private static void CreateLeaveApprovalHistoryTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS leave_approval_history (
                    history_id SERIAL PRIMARY KEY,
                    leave_request_id_fk INTEGER NOT NULL REFERENCES leave_requests(leave_request_id) ON DELETE CASCADE,
                    action VARCHAR(20) NOT NULL CHECK (action IN ('submitted', 'approved', 'rejected', 'cancelled', 'modified')),
                    performed_by INTEGER REFERENCES users(user_id),
                    performed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    previous_status VARCHAR(20),
                    new_status VARCHAR(20),
                    comments TEXT
                )");

            // Add indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_approval_history_request ON leave_approval_history(leave_request_id_fk)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_leave_approval_history_date ON leave_approval_history(performed_at)");
        }

        private static void CreateAnnouncementsTable(NpgsqlConnection conn)
        {
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS announcements (
                    announcement_id SERIAL PRIMARY KEY,
                    title VARCHAR(200) NOT NULL,
                    content TEXT,
                    start_date DATE DEFAULT CURRENT_DATE,
                    end_date DATE,
                    is_active BOOLEAN DEFAULT TRUE,
                    priority INTEGER DEFAULT 0,
                    created_by INTEGER REFERENCES users(user_id),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Add is_manager column to users table for manager portal
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'users' AND column_name = 'is_manager') THEN
                        ALTER TABLE users ADD COLUMN is_manager BOOLEAN DEFAULT FALSE;
                    END IF;
                END $$;");

            // Add managed_dept_ids column for managers
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'users' AND column_name = 'managed_dept_ids') THEN
                        ALTER TABLE users ADD COLUMN managed_dept_ids INTEGER[];
                    END IF;
                END $$;");

            // Add birthday column to users table for kiosk
            ExecuteNonQuery(conn, @"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_name = 'users' AND column_name = 'birth_date') THEN
                        ALTER TABLE users ADD COLUMN birth_date DATE;
                    END IF;
                END $$;");
        }

        private static void CreateTelegramTables(NpgsqlConnection conn)
        {
            // Telegram bot settings
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS telegram_settings (
                    setting_id INTEGER PRIMARY KEY DEFAULT 1,
                    bot_token VARCHAR(100),
                    is_enabled BOOLEAN DEFAULT FALSE,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                )");

            // Telegram subscriptions for notifications
            ExecuteNonQuery(conn, @"
                CREATE TABLE IF NOT EXISTS telegram_subscriptions (
                    subscription_id SERIAL PRIMARY KEY,
                    chat_id VARCHAR(50) NOT NULL,
                    notification_type VARCHAR(50) NOT NULL,
                    subscribed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE(chat_id, notification_type)
                )");

            // Create index for faster lookups
            ExecuteNonQuery(conn, @"
                CREATE INDEX IF NOT EXISTS idx_telegram_subscriptions_type
                ON telegram_subscriptions(notification_type)");
        }

        /// <summary>
        /// Creates performance indexes for faster queries
        /// </summary>
        private static void CreatePerformanceIndexes(NpgsqlConnection conn)
        {
            // Attendance logs indexes - critical for report generation
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_attendance_logs_badge ON attendance_logs(user_badge_number)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_attendance_logs_time ON attendance_logs(log_time)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_attendance_logs_badge_time ON attendance_logs(user_badge_number, log_time)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_attendance_logs_date ON attendance_logs(DATE(log_time))");

            // Users indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_users_dept ON users(default_dept_id)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_users_shift ON users(shift_id)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_users_role ON users(role)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_users_active ON users(is_active)");

            // Employee exceptions indexes - for report filtering
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_employee_exceptions_user ON employee_exceptions(user_id_fk)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_employee_exceptions_date ON employee_exceptions(exception_date)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_employee_exceptions_user_date ON employee_exceptions(user_id_fk, exception_date)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_employee_exceptions_type ON employee_exceptions(exception_type_id_fk)");

            // Audit logs indexes - for sync
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_audit_logs_created ON audit_logs(created_at)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_audit_logs_synced ON audit_logs(is_synced)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_audit_logs_table ON audit_logs(table_name)");

            // Sync history indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_sync_history_location ON sync_history(location_id)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_sync_history_completed ON sync_history(completed_at)");

            // Department and shift indexes
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_departments_name ON departments(dept_name)");
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_shifts_name ON shifts(shift_name)");

            System.Diagnostics.Debug.WriteLine("Performance indexes created/verified");
        }

        private static void EnsureDefaultData(NpgsqlConnection conn)
        {
            // Ensure backup_settings has at least one row
            ExecuteNonQuery(conn, @"
                INSERT INTO backup_settings (auto_backup_enabled, backup_time, backup_retention_days, backup_path)
                SELECT TRUE, '02:00:00', 30, 'C:\ZKTecoBackups'
                WHERE NOT EXISTS (SELECT 1 FROM backup_settings)");

            // Ensure sync_settings has at least one row
            ExecuteNonQuery(conn, @"
                INSERT INTO sync_settings (auto_sync_enabled, sync_interval_minutes)
                SELECT FALSE, 15
                WHERE NOT EXISTS (SELECT 1 FROM sync_settings)");

            // Ensure admin user exists
            ExecuteNonQuery(conn, @"
                INSERT INTO users (badge_number, name, password, role, can_edit_times)
                SELECT 'admin', 'System Admin', 'admin', 'superadmin', true
                WHERE NOT EXISTS (SELECT 1 FROM users WHERE badge_number = 'admin')");
        }
    }
}
