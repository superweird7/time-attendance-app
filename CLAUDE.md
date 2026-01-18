# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ZKTecoManager is a Windows desktop application (WPF) for managing ZKTeco biometric fingerprint attendance devices. Built with C# on .NET Framework 4.7.2.

## Build Commands

```bash
# Build Debug (Any CPU)
msbuild ZKTecoManager.sln /p:Configuration=Debug /p:Platform="Any CPU"

# Build Release (Any CPU)
msbuild ZKTecoManager.sln /p:Configuration=Release /p:Platform="Any CPU"

# Build x86 variants
msbuild ZKTecoManager.sln /p:Configuration=Debug /p:Platform=x86
msbuild ZKTecoManager.sln /p:Configuration=Release /p:Platform=x86

# Restore NuGet packages (if needed)
nuget restore ZKTecoManager.sln
```

Output directories: `bin\Debug\`, `bin\Release\`, `bin\x86\Debug\`, `bin\x86\Release\`

## Architecture

### Technology Stack
- **Framework**: .NET Framework 4.7.2, WPF
- **Database**: PostgreSQL (Npgsql 4.1.14)
- **PDF Generation**: iTextSharp 5.5.13.4
- **Device Communication**: zkemkeeper and ZKFPEngXControl COM libraries

### Application Flow
1. `App.xaml.cs` - Application startup, initializes services and database
2. `LoginWindow.xaml.cs` - Authentication entry point (roles: superadmin, deptadmin)
3. `MainWindow.xaml.cs` - Main navigation hub after login

### Startup Sequence (App.xaml.cs)
1. Load localization resources (English/Arabic)
2. `DatabaseInitializer.Initialize()` - Create DB and schema if needed
3. `AutomaticBackupService.Start()` - Start backup scheduler
4. `AutoDownloadService.Start()` - Start device sync scheduler
5. `CacheManager.Preload()` - Pre-fetch frequently accessed data
6. Show `LoginWindow`

### Core Services (in ZKTecoManager/)
- `AutomaticBackupService.cs` - Hourly backup scheduler, runs at configured time
- `AutoDownloadService.cs` - Background automatic device log download
- `BackupManager.cs` - SQL-based backup/restore (no pg_dump dependency)
- `AuditLogger.cs` - Database audit trail for user actions
- `DataValidator.cs` - Validation for badge numbers, emails, IPs, times

### Infrastructure (in ZKTecoManager/Infrastructure/)
- `DatabaseConfig.cs` - Connection string and pooling config (Min: 5, Max: 20 connections)
- `DatabaseInitializer.cs` - Automatic schema creation on first run
- `ServiceLocator.cs` - Lazy singleton DI for repositories and services
- `BaseWindow.cs` - Base class for WPF windows
- `PasswordHelper.cs` - Password hashing/validation
- `CacheManager.cs` - Application cache with preload on startup

### Data Models (in ZKTecoManager/)
- `User.cs` - Employee/user data
- `Machine.cs` - ZKTeco device configuration
- `Department.cs` - Organizational departments
- `Shift.cs` - Work shift definitions
- `AttendanceLog.cs` - Punch records
- `DailyReportEntry.cs` - Daily attendance summaries
- `EmployeeException.cs` - Leave/absence records
- `CurrentUser.cs` - Logged-in user session state

### Window Organization
- **CRUD Windows**: `AddUserWindow`, `EditUserWindow`, `AddMachineWindow`, `EditMachineWindow`, `AddEditDepartmentWindow`, `AddEditShiftWindow`
- **List/Management Windows**: `EmployeesWindow`, `DevicesWindow`, `DepartmentsWindow`, `ShiftsWindow`, `ManageExceptionsWindow`
- **Sync Windows**: `SyncMenuWindow`, `FromDeviceToPCWindow`, `FromPCToDeviceWindow`, `DeviceSyncWindow`, `SyncDashboardWindow`, `SyncReviewWindow`
- **Reporting**: `ReportsWindow` (generates PDF reports via iTextSharp)
- **Admin**: `AdminPanelWindow`, `BackupWindow`, `ChangePasswordWindow`
- **Bulk Operations**: `BulkAddUsersWindow`, `BulkAssignShiftWindow`, `BulkAssignExceptionWindow`
- **Custom Controls**: `Controls/LoadingOverlay.xaml` - Reusable loading indicator

### Localization
- Resource dictionaries in `Resources/StringResources.ar-IQ.xaml` and `Resources/StringResources.en-US.xaml`
- Language switching via `App.SwitchLanguage()`
- RTL support for Arabic UI

### Database
- PostgreSQL connection: `Host=localhost;Port=5432;Database=zkteco_db`
- Connection string in `App.config`, accessed via `Infrastructure/DatabaseConfig.cs`
- Key tables: users, departments, shifts, machines, attendance_logs, audit_logs, backup_settings, exceptions

## ZKTeco Device Integration

The application uses COM interop for device communication:
- `zkemkeeper` - Main SDK for device operations (connect, read users, read logs, sync)
- `ZKFPEngXControl` - Fingerprint template operations

Device operations are primarily in `FromDeviceToPCWindow.cs` (download) and `FromPCToDeviceWindow.cs` (upload).

## Key Patterns

- **Repository Pattern**: Data access abstracted via interfaces in `Data/Interfaces/` and implementations in `Data/Repositories/`
- **Service Layer**: Business logic in `Services/` (e.g., `DashboardService`, `RemoteSyncService`, `AutoSyncService`, `WebSocketService`, `TelegramNotificationService`)
- **Dependency Injection**: Simple DI via `ServiceLocator` in `Infrastructure/` (lazy singleton initialization)
- **Centralized Configuration**: Database config in `Infrastructure/DatabaseConfig.cs`, connection string in App.config
- **Pagination**: Server-side pagination with `PaginationParams` and `PagedResult<T>` in `Models/Pagination/`
- **Role-based permissions**: superadmin has full access, deptadmin limited to assigned departments/devices
- **Audit logging**: User actions logged to audit_logs table via AuditLogger

## Repository Layer

- `Data/Interfaces/IRepository.cs` - Generic base interface with async CRUD operations
- `Data/Interfaces/IUserRepository.cs` - User/Employee operations with pagination
- `Data/Interfaces/IDepartmentRepository.cs` - Department operations
- `Data/Interfaces/IShiftRepository.cs` - Shift operations
- `Data/Interfaces/IAttendanceRepository.cs` - Attendance log operations
- `Data/Interfaces/IExceptionRepository.cs` - Employee exception operations
- `Data/Interfaces/IMachineRepository.cs` - Machine/device operations
- `Data/Repositories/BaseRepository.cs` - Common database utilities

## Remote Sync System

Multi-location sync functionality in `Services/`:
- `RemoteSyncService.cs` - Fetches and applies changes from remote PostgreSQL databases
- `AutoSyncService.cs` - Background automatic synchronization
- `Models/Sync/RemoteLocation.cs` - Remote database connection configuration
- `Models/Sync/PendingChange.cs` - Represents a change to be synced (New/Updated)
- `Models/Sync/SyncResult.cs` - Sync operation results
- `Data/Repositories/RemoteLocationRepository.cs` - Persists remote location configs
- `Infrastructure/SyncConverters.cs` - Data conversion utilities for sync

## Keyboard Shortcuts

### MainWindow
- `F1` - Open Dashboard
- `F2` - Open Employees
- `F3` - Open Departments
- `F4` - Open Shifts
- `F5` - Open Devices
- `F6` - Open Reports
- `F7` - Open Sync Dashboard
- `Ctrl++` / `Ctrl+-` - Increase/decrease font size
- `Ctrl+0` - Reset font size
- `Escape` - Exit application

### Data Windows (Employees, Departments, Shifts)
- `Ctrl+N` - Add new item
- `Ctrl+E` - Edit selected item
- `Ctrl+F` - Focus search box (Employees only)
- `Delete` - Delete selected item(s)
- `Escape` - Close window

### Dashboard
- `F5` - Refresh data
- `Escape` - Close window
