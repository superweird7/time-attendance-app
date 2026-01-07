@echo off
chcp 65001 >nul

echo ======================================
echo   ZKTecoManager Database Setup
echo ======================================
echo.

REM Set PostgreSQL password from argument or default
if "%~1"=="" (
    set PGPASSWORD=2001
) else (
    set PGPASSWORD=%~1
)

REM Find PostgreSQL installation
set PGPATH=

if exist "C:\Program Files\PostgreSQL\18\bin\psql.exe" set "PGPATH=C:\Program Files\PostgreSQL\18\bin"
if exist "C:\Program Files\PostgreSQL\17\bin\psql.exe" set "PGPATH=C:\Program Files\PostgreSQL\17\bin"
if exist "C:\Program Files\PostgreSQL\16\bin\psql.exe" set "PGPATH=C:\Program Files\PostgreSQL\16\bin"
if exist "C:\Program Files\PostgreSQL\15\bin\psql.exe" set "PGPATH=C:\Program Files\PostgreSQL\15\bin"
if exist "C:\Program Files\PostgreSQL\14\bin\psql.exe" set "PGPATH=C:\Program Files\PostgreSQL\14\bin"

if "%PGPATH%"=="" (
    echo [ERROR] PostgreSQL not found!
    echo Please install PostgreSQL first.
    pause
    exit /b 1
)

echo [OK] Found PostgreSQL at: %PGPATH%
echo.

REM Create database if not exists
echo Creating database 'zkteco_db'...
"%PGPATH%\createdb.exe" -U postgres -h localhost zkteco_db 2>nul
echo [OK] Database ready.

REM Run schema script
echo.
echo Initializing database schema...
"%PGPATH%\psql.exe" -U postgres -h localhost -d zkteco_db -f "%~dp0schema.sql" >nul 2>&1
echo [OK] Database schema initialized.

echo.
echo ======================================
echo   Database Setup Complete!
echo ======================================
echo.
echo Default login:
echo   Username: admin
echo   Password: admin
echo.

exit /b 0
