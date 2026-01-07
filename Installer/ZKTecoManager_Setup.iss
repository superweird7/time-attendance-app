; ZKTecoManager Installer Script
; Created with Inno Setup

#define MyAppName "ZKTecoManager"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
#define MyAppExeName "ZKTecoManager.exe"
#define MyAppURL "https://yourcompany.com"

[Setup]
; Application info
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=Output
OutputBaseFilename=ZKTecoManager_Setup_{#MyAppVersion}
; SetupIconFile=app.ico  ; Uncomment and add your icon file here
Compression=lzma2/ultra64
SolidCompression=yes
; UI settings
WizardStyle=modern
DisableProgramGroupPage=yes
; Privileges
PrivilegesRequired=admin
; Language
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files (from Release build)
Source: "..\ZKTecoManager\bin\Release\ZKTecoManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ZKTecoManager\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ZKTecoManager\bin\Release\*.config"; DestDir: "{app}"; Flags: ignoreversion

; Database setup files
Source: "schema.sql"; DestDir: "{app}\Database"; Flags: ignoreversion
Source: "setup_database.bat"; DestDir: "{app}\Database"; Flags: ignoreversion

; PostgreSQL installer (optional - place postgresql-xx-windows-x64.exe in Installer folder)
; Uncomment the next line if you want to bundle PostgreSQL:
; Source: "postgresql-18-windows-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsPostgreSQLInstalled

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Run database setup after installation
Filename: "{app}\Database\setup_database.bat"; Parameters: "2001"; WorkingDir: "{app}\Database"; Flags: runhidden waituntilterminated; StatusMsg: "Setting up database..."
; Launch application after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if PostgreSQL is installed
function IsPostgreSQLInstalled: Boolean;
var
  PostgresPaths: array[0..6] of String;
  i: Integer;
begin
  Result := False;
  PostgresPaths[0] := 'C:\Program Files\PostgreSQL\18\bin\psql.exe';
  PostgresPaths[1] := 'C:\Program Files\PostgreSQL\17\bin\psql.exe';
  PostgresPaths[2] := 'C:\Program Files\PostgreSQL\16\bin\psql.exe';
  PostgresPaths[3] := 'C:\Program Files\PostgreSQL\15\bin\psql.exe';
  PostgresPaths[4] := 'C:\Program Files\PostgreSQL\14\bin\psql.exe';
  PostgresPaths[5] := 'C:\Program Files (x86)\PostgreSQL\18\bin\psql.exe';
  PostgresPaths[6] := 'C:\Program Files (x86)\PostgreSQL\17\bin\psql.exe';

  for i := 0 to 6 do
  begin
    if FileExists(PostgresPaths[i]) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

// Show warning if PostgreSQL is not installed
function InitializeSetup: Boolean;
begin
  Result := True;

  if not IsPostgreSQLInstalled then
  begin
    if MsgBox('PostgreSQL is not installed on this computer.' + #13#10 + #13#10 +
              'ZKTecoManager requires PostgreSQL to work.' + #13#10 + #13#10 +
              'Please install PostgreSQL first, then run this installer again.' + #13#10 + #13#10 +
              'Download PostgreSQL from: https://www.postgresql.org/download/windows/' + #13#10 + #13#10 +
              'Do you want to continue anyway?',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

// Custom page for database password
var
  DBPasswordPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  DBPasswordPage := CreateInputQueryPage(wpSelectDir,
    'Database Configuration', 'Configure PostgreSQL connection',
    'Enter the PostgreSQL password (default: 2001)');
  DBPasswordPage.Add('PostgreSQL Password:', True);
  DBPasswordPage.Values[0] := '2001';
end;

// Pass password to setup script
function GetDBPassword(Param: String): String;
begin
  Result := DBPasswordPage.Values[0];
end;
