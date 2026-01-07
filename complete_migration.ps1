$env:PGPASSWORD = "2001"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
$OLD_DB = "zkteco_db"
$NEW_DB = "zkteco_db_v2"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "ZKTeco Manager - Complete Database Migration" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create new database
Write-Host "Step 1: Creating new database $NEW_DB..." -ForegroundColor Yellow
& $psql -U postgres -h localhost -c "DROP DATABASE IF EXISTS $NEW_DB;"
& $psql -U postgres -h localhost -c "CREATE DATABASE $NEW_DB ENCODING 'UTF8';"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to create database!" -ForegroundColor Red
    exit 1
}
Write-Host "Database created successfully!" -ForegroundColor Green
Write-Host ""

# Step 2: Create schema
Write-Host "Step 2: Creating schema in new database..." -ForegroundColor Yellow
$schemaScript = "C:\Users\Super\Desktop\برنامج البصمة معدل -claude\create_new_database.sql"
& $psql -U postgres -h localhost -d $NEW_DB -f $schemaScript

if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: Some schema operations may have had issues" -ForegroundColor Yellow
}
Write-Host "Schema created!" -ForegroundColor Green
Write-Host ""

# Step 3: Install dblink extension for cross-database queries
Write-Host "Step 3: Installing dblink extension..." -ForegroundColor Yellow
& $psql -U postgres -h localhost -d $NEW_DB -c "CREATE EXTENSION IF NOT EXISTS dblink;"
Write-Host "Extension installed!" -ForegroundColor Green
Write-Host ""

# Step 4: Migrate data
Write-Host "Step 4: Migrating data from $OLD_DB to $NEW_DB..." -ForegroundColor Yellow

$migrationSQL = @"
-- Connect to old database
SELECT dblink_connect('old_db', 'dbname=$OLD_DB user=postgres password=2001');

-- Migrate departments
INSERT INTO departments (dept_id, dept_name, parent_dept_id, created_at)
SELECT * FROM dblink('old_db',
    'SELECT dept_id, dept_name, CASE WHEN parent_dept_id = 0 THEN NULL ELSE parent_dept_id END, CURRENT_TIMESTAMP FROM departments WHERE dept_name IS NOT NULL')
AS t(dept_id INTEGER, dept_name VARCHAR(255), parent_dept_id INTEGER, created_at TIMESTAMP)
ON CONFLICT (dept_id) DO NOTHING;
SELECT setval('departments_dept_id_seq', (SELECT COALESCE(MAX(dept_id), 1) FROM departments));

-- Migrate shifts
INSERT INTO shifts (shift_id, shift_name, start_time, end_time, created_at)
SELECT * FROM dblink('old_db',
    'SELECT shift_id, shift_name, start_time, end_time, CURRENT_TIMESTAMP FROM shifts')
AS t(shift_id INTEGER, shift_name VARCHAR(100), start_time TIME, end_time TIME, created_at TIMESTAMP)
ON CONFLICT (shift_id) DO NOTHING;
SELECT setval('shifts_shift_id_seq', (SELECT COALESCE(MAX(shift_id), 1) FROM shifts));

-- Migrate exception types
INSERT INTO exception_types (exception_type_id, exception_name, created_at)
SELECT * FROM dblink('old_db',
    'SELECT exception_type_id, exception_name, CURRENT_TIMESTAMP FROM exception_types')
AS t(exception_type_id INTEGER, exception_name VARCHAR(255), created_at TIMESTAMP)
ON CONFLICT (exception_type_id) DO NOTHING;
SELECT setval('exception_types_exception_type_id_seq', (SELECT COALESCE(MAX(exception_type_id), 1) FROM exception_types));

-- Migrate machines (keep only unique IPs, first occurrence)
INSERT INTO machines (id, machine_alias, ip_address, port, serial_number, machine_number, connect_type, serial_port, baudrate, is_host, enabled, created_at)
SELECT * FROM dblink('old_db',
    'SELECT DISTINCT ON (ip_address) id, machine_alias, ip_address, COALESCE(port, 4370), serial_number, COALESCE(machine_number, 1), COALESCE(connect_type, 1), serial_port, COALESCE(baudrate, 115200), COALESCE(is_host, FALSE), COALESCE(enabled, TRUE), CURRENT_TIMESTAMP FROM machines ORDER BY ip_address, id')
AS t(id INTEGER, machine_alias VARCHAR(255), ip_address VARCHAR(45), port INTEGER, serial_number VARCHAR(255), machine_number INTEGER, connect_type INTEGER, serial_port INTEGER, baudrate INTEGER, is_host BOOLEAN, enabled BOOLEAN, created_at TIMESTAMP)
ON CONFLICT (id) DO NOTHING;
SELECT setval('machines_id_seq', (SELECT COALESCE(MAX(id), 1) FROM machines));

-- Migrate users
INSERT INTO users (user_id, badge_number, name, default_dept_id, shift_id, password, role, can_edit_times, is_active, created_at)
SELECT * FROM dblink('old_db',
    'SELECT user_id, badge_number, name, default_dept_id, shift_id, password, COALESCE(role, ''user''), COALESCE(can_edit_times, FALSE), TRUE, CURRENT_TIMESTAMP FROM users WHERE badge_number IS NOT NULL')
AS t(user_id INTEGER, badge_number VARCHAR(50), name VARCHAR(255), default_dept_id INTEGER, shift_id INTEGER, password VARCHAR(255), role VARCHAR(50), can_edit_times BOOLEAN, is_active BOOLEAN, created_at TIMESTAMP)
ON CONFLICT (user_id) DO NOTHING;
SELECT setval('users_user_id_seq', (SELECT COALESCE(MAX(user_id), 1) FROM users));

-- Migrate attendance logs
INSERT INTO attendance_logs (log_id, user_badge_number, log_time, machine_id, created_at)
SELECT * FROM dblink('old_db',
    'SELECT log_id, user_badge_number, log_time, machine_id, CURRENT_TIMESTAMP FROM attendance_logs')
AS t(log_id INTEGER, user_badge_number VARCHAR(50), log_time TIMESTAMP, machine_id INTEGER, created_at TIMESTAMP)
ON CONFLICT (log_id) DO NOTHING;
SELECT setval('attendance_logs_log_id_seq', (SELECT COALESCE(MAX(log_id), 1) FROM attendance_logs));

-- Migrate employee exceptions
INSERT INTO employee_exceptions (exception_id, user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override, created_at)
SELECT * FROM dblink('old_db',
    'SELECT exception_id, user_id_fk, exception_type_id_fk, exception_date, notes, clock_in_override, clock_out_override, CURRENT_TIMESTAMP FROM employee_exceptions')
AS t(exception_id INTEGER, user_id_fk INTEGER, exception_type_id_fk INTEGER, exception_date DATE, notes TEXT, clock_in_override TIME, clock_out_override TIME, created_at TIMESTAMP)
ON CONFLICT (exception_id) DO NOTHING;
SELECT setval('employee_exceptions_exception_id_seq', (SELECT COALESCE(MAX(exception_id), 1) FROM employee_exceptions));

-- Migrate remote locations
INSERT INTO remote_locations (location_id, location_name, host, port, database_name, username, password, is_active, last_sync_time, last_sync_status, created_at)
SELECT * FROM dblink('old_db',
    'SELECT location_id, location_name, host, COALESCE(port, 5432), database_name, username, password, COALESCE(is_active, TRUE), last_sync_time, last_sync_status, COALESCE(created_at, CURRENT_TIMESTAMP) FROM remote_locations')
AS t(location_id INTEGER, location_name VARCHAR(100), host VARCHAR(255), port INTEGER, database_name VARCHAR(100), username VARCHAR(100), password VARCHAR(255), is_active BOOLEAN, last_sync_time TIMESTAMP, last_sync_status VARCHAR(50), created_at TIMESTAMP)
ON CONFLICT (location_id) DO NOTHING;
SELECT setval('remote_locations_location_id_seq', (SELECT COALESCE(MAX(location_id), 1) FROM remote_locations));

-- Migrate sync history
INSERT INTO sync_history (sync_id, location_id, sync_type, records_added, records_updated, records_skipped, status, error_message, started_at, completed_at)
SELECT * FROM dblink('old_db',
    'SELECT sync_id, location_id, sync_type, COALESCE(records_added, 0), COALESCE(records_updated, 0), COALESCE(records_skipped, 0), status, error_message, started_at, completed_at FROM sync_history')
AS t(sync_id INTEGER, location_id INTEGER, sync_type VARCHAR(50), records_added INTEGER, records_updated INTEGER, records_skipped INTEGER, status VARCHAR(50), error_message TEXT, started_at TIMESTAMP, completed_at TIMESTAMP)
ON CONFLICT (sync_id) DO NOTHING;
SELECT setval('sync_history_sync_id_seq', (SELECT COALESCE(MAX(sync_id), 1) FROM sync_history));

-- Disconnect
SELECT dblink_disconnect('old_db');
"@

& $psql -U postgres -h localhost -d $NEW_DB -c $migrationSQL

Write-Host "Data migration completed!" -ForegroundColor Green
Write-Host ""

# Step 5: Verify migration
Write-Host "Step 5: Verifying migration..." -ForegroundColor Yellow
$verifySQL = @"
SELECT 'Departments' as table_name, COUNT(*) as count FROM departments
UNION ALL SELECT 'Shifts', COUNT(*) FROM shifts
UNION ALL SELECT 'Exception Types', COUNT(*) FROM exception_types
UNION ALL SELECT 'Machines', COUNT(*) FROM machines
UNION ALL SELECT 'Users', COUNT(*) FROM users
UNION ALL SELECT 'Attendance Logs', COUNT(*) FROM attendance_logs
UNION ALL SELECT 'Employee Exceptions', COUNT(*) FROM employee_exceptions
UNION ALL SELECT 'Remote Locations', COUNT(*) FROM remote_locations
ORDER BY table_name;
"@

& $psql -U postgres -h localhost -d $NEW_DB -c $verifySQL

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Migration completed successfully!" -ForegroundColor Green
Write-Host "New database: $NEW_DB" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
