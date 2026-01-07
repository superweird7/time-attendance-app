-- =====================================================
-- ZKTeco Manager - Data Migration Script
-- Migrates data from old zkteco_db to new structure
-- =====================================================

-- =====================================================
-- 1. MIGRATE DEPARTMENTS
-- =====================================================
INSERT INTO departments (dept_id, dept_name, parent_dept_id, created_at)
SELECT
    dept_id,
    dept_name,
    CASE WHEN parent_dept_id = 0 THEN NULL ELSE parent_dept_id END,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.departments
WHERE dept_name NOT LIKE '%ط§ظ%' -- Skip corrupted entries
ON CONFLICT (dept_id) DO NOTHING;

-- Reset sequence
SELECT setval('departments_dept_id_seq', (SELECT COALESCE(MAX(dept_id), 1) FROM departments));

-- =====================================================
-- 2. MIGRATE SHIFTS
-- =====================================================
INSERT INTO shifts (shift_id, shift_name, start_time, end_time, created_at)
SELECT
    shift_id,
    shift_name,
    start_time,
    end_time,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.shifts
ON CONFLICT (shift_id) DO NOTHING;

-- Reset sequence
SELECT setval('shifts_shift_id_seq', (SELECT COALESCE(MAX(shift_id), 1) FROM shifts));

-- =====================================================
-- 3. MIGRATE SHIFT RULES
-- =====================================================
INSERT INTO shift_rules (rule_id, shift_id_fk, expected_time, created_at)
SELECT
    rule_id,
    shift_id_fk,
    expected_time,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.shift_rules
ON CONFLICT (rule_id) DO NOTHING;

-- Reset sequence
SELECT setval('shift_rules_rule_id_seq', (SELECT COALESCE(MAX(rule_id), 1) FROM shift_rules));

-- =====================================================
-- 4. MIGRATE EXCEPTION TYPES
-- =====================================================
INSERT INTO exception_types (exception_type_id, exception_name, created_at)
SELECT
    exception_type_id,
    exception_name,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.exception_types
ON CONFLICT (exception_type_id) DO NOTHING;

-- Reset sequence
SELECT setval('exception_types_exception_type_id_seq', (SELECT COALESCE(MAX(exception_type_id), 1) FROM exception_types));

-- =====================================================
-- 5. MIGRATE MACHINES (remove duplicates)
-- =====================================================
INSERT INTO machines (id, machine_alias, ip_address, port, serial_number, machine_number,
                      connect_type, serial_port, baudrate, is_host, enabled, created_at)
SELECT DISTINCT ON (ip_address)
    id,
    machine_alias,
    ip_address,
    COALESCE(port, 4370),
    serial_number,
    COALESCE(machine_number, 1),
    COALESCE(connect_type, 1),
    serial_port,
    COALESCE(baudrate, 115200),
    COALESCE(is_host, FALSE),
    COALESCE(enabled, TRUE),
    CURRENT_TIMESTAMP
FROM zkteco_db_old.machines
ORDER BY ip_address, id
ON CONFLICT (id) DO NOTHING;

-- Reset sequence
SELECT setval('machines_id_seq', (SELECT COALESCE(MAX(id), 1) FROM machines));

-- =====================================================
-- 6. MIGRATE USERS
-- =====================================================
INSERT INTO users (user_id, badge_number, name, default_dept_id, shift_id,
                   password, role, can_edit_times, is_active, created_at)
SELECT
    user_id,
    badge_number,
    name,
    CASE WHEN default_dept_id IN (SELECT dept_id FROM departments) THEN default_dept_id ELSE NULL END,
    CASE WHEN shift_id IN (SELECT shift_id FROM shifts) THEN shift_id ELSE NULL END,
    password,
    COALESCE(role, 'user'),
    COALESCE(can_edit_times, FALSE),
    TRUE,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.users
WHERE badge_number IS NOT NULL
ON CONFLICT (user_id) DO NOTHING;

-- Reset sequence
SELECT setval('users_user_id_seq', (SELECT COALESCE(MAX(user_id), 1) FROM users));

-- =====================================================
-- 7. MIGRATE ATTENDANCE LOGS
-- =====================================================
INSERT INTO attendance_logs (log_id, user_badge_number, log_time, machine_id, created_at)
SELECT
    log_id,
    user_badge_number,
    log_time,
    CASE WHEN machine_id IN (SELECT id FROM machines) THEN machine_id ELSE NULL END,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.attendance_logs
ON CONFLICT (log_id) DO NOTHING;

-- Reset sequence
SELECT setval('attendance_logs_log_id_seq', (SELECT COALESCE(MAX(log_id), 1) FROM attendance_logs));

-- =====================================================
-- 8. MIGRATE EMPLOYEE EXCEPTIONS
-- =====================================================
INSERT INTO employee_exceptions (exception_id, user_id_fk, exception_type_id_fk,
                                  exception_date, notes, clock_in_override, clock_out_override, created_at)
SELECT
    exception_id,
    user_id_fk,
    exception_type_id_fk,
    exception_date,
    notes,
    clock_in_override,
    clock_out_override,
    CURRENT_TIMESTAMP
FROM zkteco_db_old.employee_exceptions
WHERE user_id_fk IN (SELECT user_id FROM users)
ON CONFLICT (exception_id) DO NOTHING;

-- Reset sequence
SELECT setval('employee_exceptions_exception_id_seq', (SELECT COALESCE(MAX(exception_id), 1) FROM employee_exceptions));

-- =====================================================
-- 9. MIGRATE REMOTE LOCATIONS
-- =====================================================
INSERT INTO remote_locations (location_id, location_name, host, port, database_name,
                               username, password, is_active, last_sync_time, last_sync_status, created_at)
SELECT
    location_id,
    location_name,
    host,
    COALESCE(port, 5432),
    database_name,
    username,
    password,
    COALESCE(is_active, TRUE),
    last_sync_time,
    last_sync_status,
    COALESCE(created_at, CURRENT_TIMESTAMP)
FROM zkteco_db_old.remote_locations
ON CONFLICT (location_id) DO NOTHING;

-- Reset sequence
SELECT setval('remote_locations_location_id_seq', (SELECT COALESCE(MAX(location_id), 1) FROM remote_locations));

-- =====================================================
-- 10. MIGRATE SYNC HISTORY
-- =====================================================
INSERT INTO sync_history (sync_id, location_id, sync_type, records_added, records_updated,
                          records_skipped, status, error_message, started_at, completed_at)
SELECT
    sync_id,
    location_id,
    sync_type,
    COALESCE(records_added, 0),
    COALESCE(records_updated, 0),
    COALESCE(records_skipped, 0),
    status,
    error_message,
    started_at,
    completed_at
FROM zkteco_db_old.sync_history
WHERE location_id IN (SELECT location_id FROM remote_locations)
ON CONFLICT (sync_id) DO NOTHING;

-- Reset sequence
SELECT setval('sync_history_sync_id_seq', (SELECT COALESCE(MAX(sync_id), 1) FROM sync_history));

-- =====================================================
-- VERIFICATION QUERIES
-- =====================================================
SELECT 'Departments' as table_name, COUNT(*) as count FROM departments
UNION ALL SELECT 'Shifts', COUNT(*) FROM shifts
UNION ALL SELECT 'Exception Types', COUNT(*) FROM exception_types
UNION ALL SELECT 'Machines', COUNT(*) FROM machines
UNION ALL SELECT 'Users', COUNT(*) FROM users
UNION ALL SELECT 'Attendance Logs', COUNT(*) FROM attendance_logs
UNION ALL SELECT 'Employee Exceptions', COUNT(*) FROM employee_exceptions
UNION ALL SELECT 'Remote Locations', COUNT(*) FROM remote_locations;
