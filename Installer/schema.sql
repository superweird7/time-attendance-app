-- ZKTecoManager Database Schema
-- This script creates all necessary tables for the application

-- Create departments table
CREATE TABLE IF NOT EXISTS departments (
    dept_id SERIAL PRIMARY KEY,
    dept_name VARCHAR(255) NOT NULL,
    parent_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create shifts table
CREATE TABLE IF NOT EXISTS shifts (
    shift_id SERIAL PRIMARY KEY,
    shift_name VARCHAR(255) NOT NULL,
    start_time TIME,
    end_time TIME,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create shift_rules table
CREATE TABLE IF NOT EXISTS shift_rules (
    rule_id SERIAL PRIMARY KEY,
    shift_id_fk INTEGER NOT NULL REFERENCES shifts(shift_id) ON DELETE CASCADE,
    expected_time TIME NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create users table
CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    badge_number VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    default_dept_id INTEGER REFERENCES departments(dept_id) ON DELETE SET NULL,
    shift_id INTEGER REFERENCES shifts(shift_id) ON DELETE SET NULL,
    password VARCHAR(255),
    role VARCHAR(50) DEFAULT '',
    can_edit_times BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create machines table
CREATE TABLE IF NOT EXISTS machines (
    id SERIAL PRIMARY KEY,
    machine_alias VARCHAR(255) NOT NULL,
    ip_address VARCHAR(50) NOT NULL,
    serial_number VARCHAR(100),
    port INTEGER DEFAULT 4370,
    enabled BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create attendance_logs table
CREATE TABLE IF NOT EXISTS attendance_logs (
    log_id SERIAL PRIMARY KEY,
    user_badge_number VARCHAR(50) NOT NULL,
    log_time TIMESTAMP NOT NULL,
    machine_id INTEGER REFERENCES machines(id) ON DELETE SET NULL,
    verify_mode INTEGER DEFAULT 0,
    in_out_mode INTEGER DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_badge_number, log_time, machine_id)
);

-- Create exception_types table
CREATE TABLE IF NOT EXISTS exception_types (
    exception_type_id SERIAL PRIMARY KEY,
    exception_name VARCHAR(255) NOT NULL,
    description TEXT,
    is_paid BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create employee_exceptions table
CREATE TABLE IF NOT EXISTS employee_exceptions (
    exception_id SERIAL PRIMARY KEY,
    user_id_fk INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    exception_type_id INTEGER REFERENCES exception_types(exception_type_id) ON DELETE SET NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create biometric_data table
CREATE TABLE IF NOT EXISTS biometric_data (
    biometric_id SERIAL PRIMARY KEY,
    user_badge_number VARCHAR(50) NOT NULL,
    finger_index INTEGER NOT NULL,
    template_data TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_badge_number, finger_index)
);

-- Create backup_settings table
CREATE TABLE IF NOT EXISTS backup_settings (
    setting_id SERIAL PRIMARY KEY,
    backup_path VARCHAR(500),
    backup_time TIME DEFAULT '02:00:00',
    is_enabled BOOLEAN DEFAULT TRUE,
    last_backup TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create audit_logs table
CREATE TABLE IF NOT EXISTS audit_logs (
    log_id SERIAL PRIMARY KEY,
    user_id INTEGER,
    action_type VARCHAR(50) NOT NULL,
    table_name VARCHAR(100),
    record_id INTEGER,
    old_value TEXT,
    new_value TEXT,
    ip_address VARCHAR(50),
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create user_department_permissions table (for deptadmin access control)
CREATE TABLE IF NOT EXISTS user_department_permissions (
    permission_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    dept_id INTEGER NOT NULL REFERENCES departments(dept_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, dept_id)
);

-- Create remote_locations table (for multi-location sync)
CREATE TABLE IF NOT EXISTS remote_locations (
    location_id SERIAL PRIMARY KEY,
    location_name VARCHAR(255) NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INTEGER DEFAULT 5432,
    database_name VARCHAR(100) NOT NULL,
    username VARCHAR(100) NOT NULL,
    password VARCHAR(255),
    is_enabled BOOLEAN DEFAULT TRUE,
    last_sync TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_users_badge ON users(badge_number);
CREATE INDEX IF NOT EXISTS idx_users_dept ON users(default_dept_id);
CREATE INDEX IF NOT EXISTS idx_attendance_badge ON attendance_logs(user_badge_number);
CREATE INDEX IF NOT EXISTS idx_attendance_time ON attendance_logs(log_time);
CREATE INDEX IF NOT EXISTS idx_attendance_machine ON attendance_logs(machine_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user ON audit_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_action ON audit_logs(action_type);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created ON audit_logs(created_at);

-- Insert default data
INSERT INTO departments (dept_name) VALUES ('القسم الرئيسي') ON CONFLICT DO NOTHING;

INSERT INTO exception_types (exception_name, description, is_paid) VALUES
    ('إجازة سنوية', 'إجازة سنوية مدفوعة', TRUE),
    ('إجازة مرضية', 'إجازة مرضية', TRUE),
    ('إجازة بدون راتب', 'إجازة غير مدفوعة', FALSE),
    ('مأمورية', 'مأمورية عمل خارجية', TRUE),
    ('غياب', 'غياب بدون إذن', FALSE)
ON CONFLICT DO NOTHING;

-- Insert default admin user (password: admin)
INSERT INTO users (badge_number, name, password, role, default_dept_id)
VALUES ('admin', 'مدير النظام', 'admin', 'superadmin', 1)
ON CONFLICT (badge_number) DO NOTHING;

-- Insert default backup settings
INSERT INTO backup_settings (backup_path, backup_time, is_enabled)
VALUES ('C:\ZKTecoBackups', '02:00:00', TRUE)
ON CONFLICT DO NOTHING;
