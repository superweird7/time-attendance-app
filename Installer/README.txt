================================================================================
                    ZKTecoManager Installer Build Guide
================================================================================

PREREQUISITES
-------------
1. Inno Setup 6.x (Free) - Download from: https://jrsoftware.org/isdl.php
2. PostgreSQL installer (optional to bundle)
3. Visual Studio with .NET Framework 4.7.2

================================================================================

STEP-BY-STEP INSTRUCTIONS
-------------------------

STEP 1: Install Inno Setup
   - Download Inno Setup from: https://jrsoftware.org/isdl.php
   - Run the installer and install with default options
   - This will install the Inno Setup Compiler

STEP 2: Build Release Version (Already Done)
   - The Release build is located in: ZKTecoManager\bin\Release\
   - If you need to rebuild:
     Open Command Prompt and run:
     msbuild ZKTecoManager.sln /p:Configuration=Release /p:Platform="Any CPU"

STEP 3: Compile the Installer
   - Open the file: Installer\ZKTecoManager_Setup.iss
   - Right-click and select "Compile" (or open with Inno Setup Compiler)
   - Or: Open Inno Setup Compiler, File > Open, select ZKTecoManager_Setup.iss
   - Press F9 or Build > Compile
   - The installer will be created in: Installer\Output\ZKTecoManager_Setup_1.0.0.exe

STEP 4: Test the Installer
   - Run the generated .exe file
   - Follow the installation wizard
   - The installer will:
     * Check if PostgreSQL is installed
     * Copy application files
     * Create database and tables automatically
     * Create desktop shortcut

================================================================================

WHAT THE INSTALLER DOES
-----------------------
1. Checks if PostgreSQL is installed (warns if not)
2. Asks for PostgreSQL password (default: 2001)
3. Installs application to Program Files\ZKTecoManager
4. Runs database setup script automatically
5. Creates desktop and Start Menu shortcuts
6. Launches the application

================================================================================

FOR NEW INSTALLATIONS (USER PCs)
--------------------------------
Before running the installer on a new PC:

1. Install PostgreSQL 14 or higher:
   - Download from: https://www.postgresql.org/download/windows/
   - During installation, set password to: 2001 (or your preferred password)
   - Keep default port: 5432
   - Complete the installation

2. Run ZKTecoManager_Setup_1.0.0.exe
   - Enter the PostgreSQL password when prompted
   - Complete the installation
   - The database will be created automatically

3. Login with default credentials:
   - Username: admin
   - Password: admin
   - (Change password after first login!)

================================================================================

OPTIONAL: Bundle PostgreSQL with Installer
------------------------------------------
To include PostgreSQL installer (makes installer ~200MB larger):

1. Download PostgreSQL Windows installer from:
   https://www.postgresql.org/download/windows/

2. Place the file (e.g., postgresql-18-windows-x64.exe) in the Installer folder

3. Edit ZKTecoManager_Setup.iss and uncomment this line:
   ; Source: "postgresql-18-windows-x64.exe"; DestDir: "{tmp}"; ...

4. Add a [Run] section to install PostgreSQL silently:
   Filename: "{tmp}\postgresql-18-windows-x64.exe"; Parameters: "--mode unattended --superpassword 2001"; Check: not IsPostgreSQLInstalled

================================================================================

CUSTOMIZATION
-------------
Edit ZKTecoManager_Setup.iss to customize:

- MyAppName      : Application name
- MyAppVersion   : Version number (update for new releases)
- MyAppPublisher : Your company name
- MyAppURL       : Your website
- DefaultDirName : Installation directory
- SetupIconFile  : Add your own .ico file for the installer

================================================================================

TROUBLESHOOTING
---------------
Problem: "PostgreSQL not found" error during database setup
Solution: Install PostgreSQL first, then run the installer

Problem: Database connection failed
Solution: Check PostgreSQL service is running:
          - Open Services (services.msc)
          - Find "postgresql-x64-XX" service
          - Make sure it's running

Problem: Login fails with "admin" user
Solution: The default password hash may not match. Create a new admin:
          - Use pgAdmin or psql to connect to zkteco_db
          - Run: UPDATE users SET password = '' WHERE badge_number = 'admin';
          - Login with empty password, then change it

================================================================================

FILES IN THIS FOLDER
--------------------
- ZKTecoManager_Setup.iss  : Inno Setup script (main installer config)
- schema.sql               : Database schema (creates all tables)
- setup_database.bat       : Batch script to initialize database
- README.txt               : This file

================================================================================
                              Happy Deploying!
================================================================================
