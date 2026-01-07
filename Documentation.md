# برنامج البصمة - Documentation

## Overview

**برنامج البصمة** is a comprehensive Windows desktop application for managing ZKTeco biometric fingerprint attendance devices. Built with modern WPF technology, it provides a complete solution for employee attendance tracking, reporting, and multi-location synchronization.

---

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Framework | .NET Framework | 4.7.2 |
| UI | WPF (Windows Presentation Foundation) | - |
| Database | PostgreSQL | 12+ |
| Database Driver | Npgsql | 4.1.14 |
| PDF Generation | iTextSharp | 5.5.13.4 |
| Device SDK | zkemkeeper COM | 1.0 |
| Fingerprint SDK | ZKFPEngXControl COM | 4.0 |

---

## Features

### Core Features

#### 1. Employee Management
- Add, edit, delete employees
- Bulk import from CSV or device
- Assign departments and shifts
- Badge number management
- Server-side pagination (50 records/page)

#### 2. Department Management
- Create organizational structure
- Assign employees to departments
- Department-based filtering

#### 3. Shift Management
- Define work schedules
- Set start/end times
- Bulk assign shifts to employees

#### 4. Device Management
- Connect to ZKTeco fingerprint devices
- Download attendance logs
- Upload employee data to devices
- Sync fingerprint templates

#### 5. Attendance Reports
- Daily/Monthly/Custom date range reports
- PDF export with iTextSharp
- Department and employee filtering
- Manual time override capability

#### 6. Exception Management
- Leave types (sick, vacation, etc.)
- Bulk exception assignment
- Date range exceptions

---

### Advanced Features

#### 7. Multi-Location Sync System
- **AutoSyncService**: Background synchronization (15/30/60 min intervals)
- **RemoteSyncService**: Connect to multiple PostgreSQL databases
- **SyncDashboardWindow**: Central management for all locations
- **SyncReviewWindow**: Review and approve/reject changes
- **SyncLogger**: Bilingual logging (Arabic/English)
- **Conflict Detection**: Identifies and handles data conflicts

#### 8. Backup System
- **AutomaticBackupService**: Scheduled hourly backups
- **Dual Backup**: Local + network share backup
- **BackupManager**: SQL-based backup (no pg_dump dependency)
- **BackupPreviewWindow**: Preview before restore
- **Schema Versioning**: Compatibility checking
- **Auto-Cleanup**: Delete old backups based on retention policy

#### 9. Dashboard & Analytics
- **Real-time KPIs**: Present, Absent, Late, On Leave counts
- **Attendance Rate**: Percentage calculation
- **Department Filtering**: Filter by department
- **Auto-Refresh**: Updates every 5 minutes

#### 10. Performance Optimization
- **CacheManager**: In-memory caching (80% fewer DB queries)
- **Server-side Pagination**: 50 records per page
- **Cache Preloading**: Departments, shifts, exception types
- **Lazy Loading**: Data loaded only when needed

#### 11. Security
- **Role-based Access**: Superadmin and DeptAdmin roles
- **PBKDF2 Password Hashing**: SHA256 with 10,000 iterations
- **Audit Logging**: All user actions logged
- **Timing-Attack Protection**: Constant-time comparison

#### 12. Localization
- **Arabic (ar-IQ)**: Full RTL support
- **English (en-US)**: Complete translation
- **Dynamic Switching**: Change language without restart

---

## Architecture

### Project Structure

```
ZKTecoManager/
├── App.xaml(.cs)                 # Application entry point
├── LoginWindow.xaml(.cs)         # Authentication
├── MainWindow.xaml(.cs)          # Main navigation hub
│
├── Controls/
│   └── LoadingOverlay.xaml(.cs)  # Reusable loading indicator
│
├── Data/
│   ├── Interfaces/               # Repository interfaces
│   │   ├── IRepository.cs
│   │   ├── IUserRepository.cs
│   │   ├── IDepartmentRepository.cs
│   │   ├── IShiftRepository.cs
│   │   ├── IAttendanceRepository.cs
│   │   ├── IExceptionRepository.cs
│   │   └── IMachineRepository.cs
│   │
│   └── Repositories/             # Repository implementations
│       ├── BaseRepository.cs
│       ├── UserRepository.cs
│       ├── DepartmentRepository.cs
│       ├── ShiftRepository.cs
│       ├── AttendanceRepository.cs
│       ├── ExceptionRepository.cs
│       ├── MachineRepository.cs
│       └── RemoteLocationRepository.cs
│
├── Infrastructure/
│   ├── DatabaseConfig.cs         # Connection string management
│   ├── DatabaseInitializer.cs    # Schema initialization
│   ├── ServiceLocator.cs         # Dependency injection
│   ├── CacheManager.cs           # In-memory caching
│   ├── PasswordHelper.cs         # Password hashing
│   ├── BaseWindow.cs             # Common window functionality
│   └── SyncConverters.cs         # Sync data conversion
│
├── Models/
│   ├── Dashboard/
│   │   ├── AttendanceKpiData.cs
│   │   ├── AbsenteeInfo.cs
│   │   └── LateArrivalInfo.cs
│   │
│   ├── Pagination/
│   │   ├── PaginationParams.cs
│   │   └── PagedResult.cs
│   │
│   └── Sync/
│       ├── RemoteLocation.cs
│       ├── PendingChange.cs
│       └── SyncResult.cs
│
├── Services/
│   ├── DashboardService.cs       # KPI calculations
│   ├── AutoSyncService.cs        # Background sync
│   └── RemoteSyncService.cs      # Multi-location sync
│
├── Resources/
│   ├── StringResources.ar-IQ.xaml  # Arabic translations
│   └── StringResources.en-US.xaml  # English translations
│
└── [Windows]                     # All XAML windows
    ├── EmployeesWindow.xaml
    ├── DepartmentsWindow.xaml
    ├── ShiftsWindow.xaml
    ├── DevicesWindow.xaml
    ├── ReportsWindow.xaml
    ├── DashboardWindow.xaml
    ├── SyncDashboardWindow.xaml
    ├── BackupWindow.xaml
    └── ... (other windows)
```

### Design Patterns

1. **Repository Pattern**: Data access abstraction
2. **Service Layer**: Business logic separation
3. **Dependency Injection**: ServiceLocator for loose coupling
4. **Singleton Pattern**: CacheManager, AutoSyncService
5. **Observer Pattern**: Event-driven sync notifications

---

## Database Schema

### Core Tables

| Table | Description |
|-------|-------------|
| `users` | Employee/user data |
| `departments` | Organizational departments |
| `shifts` | Work shift definitions |
| `machines` | ZKTeco device configuration |
| `attendance_logs` | Punch records |
| `employee_exceptions` | Leave/absence records |
| `exception_types` | Types of exceptions |

### System Tables

| Table | Description |
|-------|-------------|
| `audit_logs` | User action audit trail |
| `backup_settings` | Backup configuration |
| `remote_locations` | Remote sync locations |
| `sync_settings` | Auto-sync configuration |
| `changes_log` | Unsynced changes tracking |

---

## Keyboard Shortcuts

### Main Window
| Key | Action |
|-----|--------|
| F1 | Dashboard |
| F2 | Employees |
| F3 | Departments |
| F4 | Shifts |
| F5 | Devices |
| F6 | Reports |
| F7 | Sync Dashboard |
| F8 | Changes Log |
| Escape | Exit |

### Data Windows
| Key | Action |
|-----|--------|
| Ctrl+N | Add new |
| Ctrl+E | Edit selected |
| Ctrl+F | Focus search |
| Delete | Delete selected |
| Escape | Close window |

---

## User Roles

### Superadmin
- Full system access
- User management
- Backup/restore
- Admin panel access
- All departments visible

### DeptAdmin (Department Admin)
- Limited to assigned departments
- Cannot access admin panel
- Cannot manage other users
- Filtered device access

---

## API/Services

### AutomaticBackupService
```csharp
// Start backup scheduler
AutomaticBackupService.Start();

// Stop backup scheduler
AutomaticBackupService.Stop();
```

### CacheManager
```csharp
// Get cached departments
var departments = CacheManager.GetDepartments();

// Invalidate cache
CacheManager.Invalidate(CacheManager.KEY_DEPARTMENTS);

// Preload all caches
CacheManager.Preload();
```

### AutoSyncService
```csharp
// Initialize and start
await AutoSyncService.Instance.InitializeAsync();

// Update settings
await AutoSyncService.Instance.UpdateSettingsAsync(enabled: true, intervalMinutes: 30);

// Events
AutoSyncService.Instance.SyncCompleted += OnSyncCompleted;
AutoSyncService.Instance.ConflictsDetected += OnConflictsDetected;
```

---

## Configuration

### App.config
```xml
<connectionStrings>
  <add name="PostgreSQL"
       connectionString="Host=localhost;Port=5432;Database=zkteco_db;Username=postgres;Password=YOUR_PASSWORD" />
</connectionStrings>
```

### Backup Settings (Database)
| Setting | Description |
|---------|-------------|
| `auto_backup_enabled` | Enable/disable auto backup |
| `backup_time` | Time to run backup (e.g., 10:00) |
| `backup_retention_days` | Days to keep old backups |
| `server_backup_enabled` | Enable network backup |
| `server_backup_path` | Network share path |
| `server_backup_interval_days` | Days between server backups |

---

## Build Commands

```bash
# Debug Build (Any CPU)
msbuild ZKTecoManager.sln /p:Configuration=Debug /p:Platform="Any CPU"

# Release Build (Any CPU)
msbuild ZKTecoManager.sln /p:Configuration=Release /p:Platform="Any CPU"

# x86 Debug
msbuild ZKTecoManager.sln /p:Configuration=Debug /p:Platform=x86

# x86 Release
msbuild ZKTecoManager.sln /p:Configuration=Release /p:Platform=x86
```

---

## Output Directories

| Configuration | Path |
|---------------|------|
| Debug (Any CPU) | `bin\Debug\` |
| Release (Any CPU) | `bin\Release\` |
| Debug (x86) | `bin\x86\Debug\` |
| Release (x86) | `bin\x86\Release\` |

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025 | Initial release with all features |

---

## Support

For issues or questions, please contact the development team.

---

**Copyright © 2025 - برنامج البصمة**
