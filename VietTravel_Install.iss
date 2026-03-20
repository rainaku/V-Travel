#define MyAppName "Viet Travel"
#define MyAppVersion "1.0"
#define MyAppPublisher "Viet Travel"
#define MyAppExeName "VietTravel.UI.exe"

[Setup]
; AppId is required and must be unique. You can generate a new one if you like.
AppId={{5C170B61-0CD5-4045-8C79-4A82F2ACB5DA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; If you have an icon, uncomment the next line and provide path
SetupIconFile=VietTravel.UI\UI\Assets\logo.ico
OutputBaseFilename=VietTravel_Setup
; Output directory where the final exe will be placed
OutputDir=.\InnoSetupOutput
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; InnoSetup no longer ships with Vietnamese natively in newer standard installs, 
; but you can add custom if you have it. Removed Vietnamese to prevent compile error in standard Inno Setup.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; IMPORTANT: Run 'dotnet publish VietTravel.UI/VietTravel.UI.csproj -c Release -r win-x64 --self-contained false -p:CopyOutputSymbolsToPublishDirectory=false -p:DebugType=none' first.
Source: "VietTravel.UI\bin\Release\net10.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "VietTravel.UI\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: ".env"; DestDir: "{app}"; Flags: ignoreversion
Source: "VietTravel.UI\Fonts\SF-Pro-Display-Bold.otf"; DestDir: "{fonts}"; FontInstall: "SF Pro Display Bold"; Flags: onlyifdestfileexists uninsneveruninstall
; Exclude tmpbuild or other unneeded directories if any

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
