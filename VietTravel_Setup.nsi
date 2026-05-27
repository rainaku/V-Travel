; ============================================================
; NSIS Installer Script - Viet Travel
; ============================================================
; Build command: makensis VietTravel_Setup.nsi
; Requires: NSIS 3.x installed
; ============================================================

!include "MUI2.nsh"
!include "FileFunc.nsh"

; ---- General ----
Name "Viet Travel"
OutFile "InnoSetupOutput\VietTravel_Setup.exe"
InstallDir "$LOCALAPPDATA\VietTravel"
InstallDirRegKey HKCU "Software\VietTravel" "InstallDir"
RequestExecutionLevel user
Unicode True

; ---- Version Info ----
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "Viet Travel"
VIAddVersionKey "CompanyName" "Viet Travel"
VIAddVersionKey "FileDescription" "Viet Travel - He Thong Quan Ly Tour Du Lich"
VIAddVersionKey "FileVersion" "1.0.0.0"
VIAddVersionKey "LegalCopyright" "2026 Viet Travel"

; ---- Compression ----
SetCompressor /SOLID lzma
SetCompressorDictSize 64

; ---- Icon ----
!define MUI_ICON "VietTravel.UI\UI\Assets\logo.ico"
!define MUI_UNICON "VietTravel.UI\UI\Assets\logo.ico"

; ---- MUI Settings ----
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Chao mung ban den voi Viet Travel"
!define MUI_WELCOMEPAGE_TEXT "Trinh cai dat se huong dan ban cai dat Viet Travel - He Thong Quan Ly Tour Du Lich len may tinh.$\r$\n$\r$\nVui long dong tat ca ung dung truoc khi tiep tuc.$\r$\n$\r$\nNhan Next de tiep tuc."
!define MUI_FINISHPAGE_RUN "$INSTDIR\VietTravel.UI.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Khoi dong Viet Travel ngay"

; ---- Pages ----
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; ---- Language ----
!insertmacro MUI_LANGUAGE "English"

; ============================================================
; INSTALLER SECTION
; ============================================================
Section "Install" SecInstall
    SetOutPath "$INSTDIR"
    
    ; Copy main application file
    File "build-output\VietTravel.UI.exe"
    
    ; Copy UI subfolder if exists (resources like .mov files)
    SetOutPath "$INSTDIR\UI"
    File /nonfatal /r "build-output\UI\*.*"
    
    SetOutPath "$INSTDIR"
    
    ; Create Start Menu shortcuts
    CreateDirectory "$SMPROGRAMS\Viet Travel"
    CreateShortcut "$SMPROGRAMS\Viet Travel\Viet Travel.lnk" "$INSTDIR\VietTravel.UI.exe" "" "$INSTDIR\VietTravel.UI.exe" 0
    CreateShortcut "$SMPROGRAMS\Viet Travel\Go Cai Dat.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
    
    ; Create Desktop shortcut
    CreateShortcut "$DESKTOP\Viet Travel.lnk" "$INSTDIR\VietTravel.UI.exe" "" "$INSTDIR\VietTravel.UI.exe" 0
    
    ; Write uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Write registry keys for Add/Remove Programs
    WriteRegStr HKCU "Software\VietTravel" "InstallDir" "$INSTDIR"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "DisplayName" "Viet Travel"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "DisplayIcon" "$INSTDIR\VietTravel.UI.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "Publisher" "Viet Travel"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "DisplayVersion" "1.0.0"
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "NoModify" 1
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "NoRepair" 1
    
    ; Get installed size
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel" "EstimatedSize" "$0"
SectionEnd

; ============================================================
; UNINSTALLER SECTION
; ============================================================
Section "Uninstall"
    ; Kill running process
    nsExec::ExecToLog 'taskkill /f /im VietTravel.UI.exe'
    Sleep 1000
    
    ; Remove files
    Delete "$INSTDIR\VietTravel.UI.exe"
    Delete "$INSTDIR\VietTravel.UI.pdb"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir /r "$INSTDIR\UI"
    RMDir "$INSTDIR"
    
    ; Remove shortcuts
    Delete "$DESKTOP\Viet Travel.lnk"
    Delete "$SMPROGRAMS\Viet Travel\Viet Travel.lnk"
    Delete "$SMPROGRAMS\Viet Travel\Go Cai Dat.lnk"
    RMDir "$SMPROGRAMS\Viet Travel"
    
    ; Remove registry keys
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\VietTravel"
    DeleteRegKey HKCU "Software\VietTravel"
SectionEnd
