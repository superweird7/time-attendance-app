-- =====================================================
-- ZKTeco Manager - New Database Schema
-- Version: 2.0
-- Created: 2025-12-07
-- =====================================================

-- Drop existing tables if they exist (in reverse dependency order)
DROP TABLE IF EXISTS sync_history CASCADE;
DROP TABLE IF EXISTS sync_settings CASCADE;
DROP TABLE IF EXISTS remote_locations CASCADE;
DROP TABLE IF EXISTS user_department_permissions CASCADE;
DROP TABLE IF EXISTS admin_device_mappings CASCADE;
DROP TABLE IF EXISTS admin_department_mappings CASCADE;
DROP TABLE IF EXISTS employee_exceptions CASCADE;
DROP TABLE IF EXISTS attendance_logs CASCADE;
DROP TABLE IF EXISTS biometric_data CASCADE;
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS backup_settings CASCADE;
DROP TABLE IF EXISTS shift_rules CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS machines CASCADE;
DROP TABLE IF EXISTS shifts CASCADE;
DROP TABLE IF EXISTS exception_types CASCADE;
DROP TABLE IF EXISTS departments CASCADE;

-- =====================================================
-- CORE TABLES
-- =====================================================

-- Departments table with hierarchical support
CREATE TABLE departments (
    dept_id SERIAL PRIMARY KEY,
    dept_name VARCHAR(255) NOT NULL,
    parent_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_departments_parent ON departments(parent_dept_id);
CREATE INDEX idx_departments_name ON departments(dept_name);

-- Shifts table
CREATE TABLE shifts (
    shift_id SERIAL PRIMARY KEY,
    shift_name VARCHAR(100) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Shift rules for multiple punch times
CREATE TABLE shift_rules (
    rule_id SERIAL PRIMARY KEY,
    shift_id_fk INTEGER NOT NULL REFERENCES shifts(shift_id) ON DELETE CASCADE,
    expected_time TIME NOT NULL,
    rule_order INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_shift_rules_shift ON shift_rules(shift_id_fk);

-- Exception types (leave types, etc.)
CREATE TABLE exception_types (
    exception_type_id SERIAL PRIMARY KEY,
    exception_name VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Machines (ZKTeco devices)
CREATE TABLE machines (
    id SERIAL PRIMARY KEY,
    machine_alias VARCHAR(255) NOT NULL,
    ip_address VARCHAR(45), -- Supports IPv6
    port INTEGER DEFAULT 4370,
    serial_number VARCHAR(255),
    machine_number INTEGER DEFAULT 1,
    connect_type INTEGER DEFAULT 1, -- 1=TCP/IP, 2=Serial
    serial_port INTEGER,
    baudrate INTEGER DEFAULT 115200,
    is_host BOOLEAN DEFAULT FALSE,
    enabled BOOLEAN DEFAULT TRUE,
    last_sync_time TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_machines_ip ON machines(ip_address);
CREATE INDEX idx_machines_enabled ON machines(enabled);

-- Users table (employees and admins)
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    badge_number VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    default_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
    shift_id INTEGER REFERENCES shifts(shift_id) ON DELETE SET NULL,
    password VARCHAR(255),
    role VARCHAR(50) NOT NULL DEFAULT 'user' CHECK (role IN ('superadmin', 'deptadmin', 'user')),
    can_edit_times BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_badge ON users(badge_number);
CREATE INDEX idx_users_dept ON users(default_dept_id);
CREATE INDEX idx_users_shift ON users(shift_id);
CREATE INDEX idx_users_role ON users(role);
CREATE INDEX idx_users_name ON users(name);

-- =====================================================
-- ATTENDANCE & BIOMETRIC TABLES
-- =====================================================

-- Attendance logs
CREATE TABLE attendance_logs (
    log_id SERIAL PRIMARY KEY,
    user_badge_number VARCHAR(50) NOT NULL,
    log_time TIMESTAMP NOT NULL,
    machine_id INTEGER REFERENCES machines(id) ON DELETE SET NULL,
    verify_type INTEGER DEFAULT 0, -- 0=fingerprint, 1=password, 2=card
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_attendance_log UNIQUE (user_badge_number, log_time, machine_id)
);

CREATE INDEX idx_attendance_badge ON attendance_logs(user_badge_number);
CREATE INDEX idx_attendance_time ON attendance_logs(log_time);
CREATE INDEX idx_attendance_machine ON attendance_logs(machine_id);
CREATE INDEX idx_attendance_date ON attendance_logs(DATE(log_time));

-- Biometric data (fingerprint templates)
CREATE TABLE biometric_data (
    biometric_id SERIAL PRIMARY KEY,
    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    finger_index INTEGER NOT NULL CHECK (finger_index >= 0 AND finger_index <= 9),
    template_data TEXT NOT NULL,
    biometric_type INTEGER NOT NULL DEFAULT 0, -- 0=fingerprint, 1=face, 2=palm
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_user_finger UNIQUE (user_id_fk, finger_index, biometric_type)
);

CREATE INDEX idx_biometric_user ON biometric_data(user_id_fk);

-- =====================================================
-- EXCEPTION & PERMISSION TABLES
-- =====================================================

-- Employee exceptions (leaves, absences, etc.)
CREATE TABLE employee_exceptions (
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
);

CREATE INDEX idx_exceptions_user ON employee_exceptions(user_id_fk);
CREATE INDEX idx_exceptions_date ON employee_exceptions(exception_date);
CREATE INDEX idx_exceptions_type ON employee_exceptions(exception_type_id_fk);

-- Admin to department mappings (for deptadmin role)
CREATE TABLE admin_department_mappings (
    mapping_id SERIAL PRIMARY KEY,
    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    department_id_fk INTEGER NOT NULL REFERENCES departments(dept_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_admin_dept UNIQUE (user_id_fk, department_id_fk)
);

CREATE INDEX idx_admin_dept_user ON admin_department_mappings(user_id_fk);
CREATE INDEX idx_admin_dept_dept ON admin_department_mappings(department_id_fk);

-- Admin to device mappings (for deptadmin role)
CREATE TABLE admin_device_mappings (
    mapping_id SERIAL PRIMARY KEY,
    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    device_id_fk INTEGER NOT NULL REFERENCES machines(id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_admin_device UNIQUE (user_id_fk, device_id_fk)
);

CREATE INDEX idx_admin_device_user ON admin_device_mappings(user_id_fk);
CREATE INDEX idx_admin_device_device ON admin_device_mappings(device_id_fk);

-- User department permissions (alternative permission system)
CREATE TABLE user_department_permissions (
    permission_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    dept_id INTEGER NOT NULL REFERENCES departments(dept_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_user_dept_perm UNIQUE (user_id, dept_id)
);

CREATE INDEX idx_user_dept_perm_user ON user_department_permissions(user_id);
CREATE INDEX idx_user_dept_perm_dept ON user_department_permissions(dept_id);

-- =====================================================
-- AUDIT & SETTINGS TABLES
-- =====================================================

-- Audit logs for tracking user actions
CREATE TABLE audit_logs (
    log_id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(user_id) ON DELETE SET NULL,
    action_type VARCHAR(50) NOT NULL,
    table_name VARCHAR(100),
    record_id INTEGER,
    old_value TEXT,
    new_value TEXT,
    ip_address VARCHAR(50),
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_user ON audit_logs(user_id);
CREATE INDEX idx_audit_action ON audit_logs(action_type);
CREATE INDEX idx_audit_created ON audit_logs(created_at);
CREATE INDEX idx_audit_table ON audit_logs(table_name);

-- Backup settings
CREATE TABLE backup_settings (
    setting_id SERIAL PRIMARY KEY,
    auto_backup_enabled BOOLEAN DEFAULT TRUE,
    backup_time TIME DEFAULT '02:00:00',
    backup_retention_days INTEGER DEFAULT 30,
    backup_path TEXT DEFAULT 'C:\ZKTecoBackups',
    last_backup_date TIMESTAMP,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- REMOTE SYNC TABLES
-- =====================================================

-- Remote locations for multi-site sync
CREATE TABLE remote_locations (
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
);

-- Sync settings
CREATE TABLE sync_settings (
    setting_id SERIAL PRIMARY KEY,
    auto_sync_enabled BOOLEAN DEFAULT FALSE,
    sync_interval_minutes INTEGER DEFAULT 15,
    last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Sync history
CREATE TABLE sync_history (
    sync_id SERIAL PRIMARY KEY,
    location_id INTEGER REFERENCES remote_locations(location_id) ON DELETE CASCADE,
    sync_type VARCHAR(50),
    records_added INTEGER DEFAULT 0,
    records_updated INTEGER DEFAULT 0,
    records_skipped INTEGER DEFAULT 0,
    status VARCHAR(50),
    error_message TEXT,
    started_at TIMESTAMP,
    completed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_sync_history_location ON sync_history(location_id);
CREATE INDEX idx_sync_history_completed ON sync_history(completed_at);

-- =====================================================
-- INSERT DEFAULT DATA
-- =====================================================

-- Insert default backup settings
INSERT INTO backup_settings (auto_backup_enabled, backup_time, backup_retention_days, backup_path)
VALUES (TRUE, '02:00:00', 30, 'C:\ZKTecoBackups');

-- Insert default sync settings
INSERT INTO sync_settings (auto_sync_enabled, sync_interval_minutes)
VALUES (FALSE, 15);

-- =====================================================
-- COMMENTS
-- =====================================================

COMMENT ON TABLE departments IS 'Organizational departments with hierarchical support';
COMMENT ON TABLE shifts IS 'Work shift definitions';
COMMENT ON TABLE shift_rules IS 'Expected punch times for each shift';
COMMENT ON TABLE exception_types IS 'Types of exceptions (leave, absence, etc.)';
COMMENT ON TABLE machines IS 'ZKTeco biometric devices configuration';
COMMENT ON TABLE users IS 'Employees and system administrators';
COMMENT ON TABLE attendance_logs IS 'Punch records from biometric devices';
COMMENT ON TABLE biometric_data IS 'Fingerprint and other biometric templates';
COMMENT ON TABLE employee_exceptions IS 'Leave and absence records for employees';
COMMENT ON TABLE admin_department_mappings IS 'Department permissions for dept admins';
COMMENT ON TABLE admin_device_mappings IS 'Device permissions for dept admins';
COMMENT ON TABLE audit_logs IS 'Audit trail for user actions';
COMMENT ON TABLE backup_settings IS 'Automatic backup configuration';
COMMENT ON TABLE remote_locations IS 'Remote database locations for sync';
COMMENT ON TABLE sync_settings IS 'Sync configuration settings';
COMMENT ON TABLE sync_history IS 'History of sync operations';
