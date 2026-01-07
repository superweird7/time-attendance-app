@echo off
echo ========================================
echo ZKTeco COM Components Registration
echo ========================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

echo Looking for ZKTeco SDK files...
echo.

:: Common locations for zkemkeeper.dll
set FOUND=0

:: Check in app directory
if exist "%~dp0zkemkeeper.dll" (
    echo Found: %~dp0zkemkeeper.dll
    regsvr32 /s "%~dp0zkemkeeper.dll"
    if %errorlevel% equ 0 (
        echo [OK] Registered zkemkeeper.dll from app directory
        set FOUND=1
    ) else (
        echo [ERROR] Failed to register zkemkeeper.dll
    )
)

:: Check in bin\x86\Release
if exist "%~dp0ZKTecoManager\bin\x86\Release\zkemkeeper.dll" (
    echo Found: %~dp0ZKTecoManager\bin\x86\Release\zkemkeeper.dll
    regsvr32 /s "%~dp0ZKTecoManager\bin\x86\Release\zkemkeeper.dll"
    if %errorlevel% equ 0 (
        echo [OK] Registered zkemkeeper.dll from Release folder
        set FOUND=1
    )
)

:: Check in System32
if exist "%SystemRoot%\System32\zkemkeeper.dll" (
    echo Found: %SystemRoot%\System32\zkemkeeper.dll
    regsvr32 /s "%SystemRoot%\System32\zkemkeeper.dll"
    if %errorlevel% equ 0 (
        echo [OK] Registered zkemkeeper.dll from System32
        set FOUND=1
    )
)

:: Check in SysWOW64 (for 32-bit on 64-bit Windows)
if exist "%SystemRoot%\SysWOW64\zkemkeeper.dll" (
    echo Found: %SystemRoot%\SysWOW64\zkemkeeper.dll
    regsvr32 /s "%SystemRoot%\SysWOW64\zkemkeeper.dll"
    if %errorlevel% equ 0 (
        echo [OK] Registered zkemkeeper.dll from SysWOW64
        set FOUND=1
    )
)

:: Check Program Files
if exist "C:\Program Files (x86)\ZKTeco\zkemkeeper.dll" (
    echo Found: C:\Program Files (x86)\ZKTeco\zkemkeeper.dll
    regsvr32 /s "C:\Program Files (x86)\ZKTeco\zkemkeeper.dll"
    if %errorlevel% equ 0 (
        echo [OK] Registered zkemkeeper.dll from Program Files
        set FOUND=1
    )
)

echo.

:: Register ZKFPEngXControl if exists
if exist "%~dp0ZKFPEngXControl.dll" (
    regsvr32 /s "%~dp0ZKFPEngXControl.dll"
    echo [OK] Registered ZKFPEngXControl.dll
)

if exist "%SystemRoot%\SysWOW64\ZKFPEngXControl.dll" (
    regsvr32 /s "%SystemRoot%\SysWOW64\ZKFPEngXControl.dll"
    echo [OK] Registered ZKFPEngXControl.dll from SysWOW64
)

echo.
echo ========================================

if %FOUND% equ 0 (
    echo WARNING: zkemkeeper.dll was not found!
    echo.
    echo Please install the ZKTeco SDK or copy zkemkeeper.dll to:
    echo   - The application folder, or
    echo   - C:\Windows\SysWOW64\
    echo.
    echo You can download ZKTeco SDK from the official ZKTeco website.
) else (
    echo COM registration completed successfully!
    echo.
    echo Please restart the application.
)

echo ========================================
pause
