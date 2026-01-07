@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

echo =====================================================
echo ZKTeco Manager - Database Migration Tool
echo =====================================================
echo.

set PGPASSWORD=2001
set PSQL="C:\Program Files\PostgreSQL\18\bin\psql.exe"
set OLD_DB=zkteco_db
set NEW_DB=zkteco_db_v2
set PG_USER=postgres
set PG_HOST=localhost
set PG_PORT=5432

echo Step 1: Creating new database %NEW_DB%...
%PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -c "DROP DATABASE IF EXISTS %NEW_DB%;" 2>nul
%PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -c "CREATE DATABASE %NEW_DB% ENCODING 'UTF8' LC_COLLATE='en_US.UTF-8' LC_CTYPE='en_US.UTF-8' TEMPLATE template0;"

if %ERRORLEVEL% NEQ 0 (
    echo Failed to create database. Trying with default template...
    %PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -c "CREATE DATABASE %NEW_DB%;"
)

echo.
echo Step 2: Creating schema in new database...
%PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -d %NEW_DB% -f "%~dp0create_new_database.sql"

echo.
echo Step 3: Migrating data from %OLD_DB% to %NEW_DB%...
%PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -d %NEW_DB% -c "CREATE SCHEMA IF NOT EXISTS zkteco_db_old;"
%PSQL% -U %PG_USER% -h %PG_HOST% -p %PG_PORT% -d %NEW_DB% -c "IMPORT FOREIGN SCHEMA public FROM SERVER %OLD_DB%_server INTO zkteco_db_old;" 2>nul

echo.
echo Migration script will be run separately. Please wait...
echo.

pause
