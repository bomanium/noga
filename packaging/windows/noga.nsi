; noga's Windows installer (docs/decisions/2026-11-02-installers.md), built on Linux by tools/publish.sh with NSIS's
; makensis (zlib licence; its LZMA module is CPL-1.0 with an exception for what it compresses: nsis-COPYING.txt):
;
;   makensis -DVERSION=0.1.0-alpha.2 -DVERSION4=0.1.0.0 -DSOURCE=folder -DFILES=files.nsh -DOUTFILE=setup.exe noga.nsi
;
; SOURCE is the win-x64 release folder (the zip's), FILES the list of its files that the uninstaller deletes (publish.sh
; writes it: Delete and RMDir lines, deepest first), so it removes what it installed and nothing else. Per user, no
; administrator: %LOCALAPPDATA%\Programs\noga, a Start-menu shortcut to the desktop app, a desktop one if chosen,
; noga on the user's PATH if chosen, and an entry in Settings, Apps. Unsigned for now (backlog: code signing).

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user
ManifestDPIAware true

!include MUI2.nsh
!include LogicLib.nsh
!include WinMessages.nsh

!define PRODUCT "noga"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\noga"

Name "${PRODUCT} ${VERSION}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\noga"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"
BrandingText "${PRODUCT} ${VERSION}"

VIProductVersion "${VERSION4}"
VIAddVersionKey "ProductName" "${PRODUCT}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${PRODUCT} setup: file-based quality control of video deliveries"
VIAddVersionKey "LegalCopyright" "${PRODUCT}"

!define MUI_ICON "..\..\src\Noga.App\Assets\icon.ico"
!define MUI_UNICON "..\..\src\Noga.App\Assets\icon.ico"
!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_NODESC
!define MUI_FINISHPAGE_RUN "$INSTDIR\noga-app.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open noga"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Show the README (the command line, the licences)"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

; The user's PATH, changed through the registry as it is (an expandable string keeps its %VARIABLES%; NSIS's own
; strings stop at 1024 characters, which a long PATH passes), by PowerShell, which every supported Windows has. The
; folder goes in through an environment variable, so no quoting of it can go wrong.
!macro EditPath ADD
  System::Call 'Kernel32::SetEnvironmentVariable(t "NOGA_DIR", t "$INSTDIR")'
  nsExec::ExecToLog `"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$$d = $$env:NOGA_DIR; $$k = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment'); $$p = $$k.GetValue('Path', '', 'DoNotExpandEnvironmentNames'); $$kind = if ($$p) { $$k.GetValueKind('Path') } else { 'ExpandString' }; $$parts = @($$p -split ';' | Where-Object { $$_ -and $$_.TrimEnd('\') -ne $$d.TrimEnd('\') }); if (${ADD}) { $$parts += $$d }; $$k.SetValue('Path', ($$parts -join ';'), $$kind)"`
  Pop $0
  SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" /TIMEOUT=1000
!macroend

Section "noga and its desktop app" SecMain
  SectionIn RO
  ; An earlier install in the same folder: its own uninstaller removes its files first (they may differ from these),
  ; and it stays where it is while it runs (_?=).
  ${If} ${FileExists} "$INSTDIR\uninstall.exe"
    ExecWait '"$INSTDIR\uninstall.exe" /S _?=$INSTDIR'
    Delete "$INSTDIR\uninstall.exe"
  ${EndIf}

  SetOutPath "$INSTDIR"
  File /r "${SOURCE}\*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  CreateShortcut "$SMPROGRAMS\noga.lnk" "$INSTDIR\noga-app.exe" "" "$INSTDIR\noga-app.exe" 0

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${PRODUCT}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\noga-app.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  SectionGetSize ${SecMain} $0
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" $0
SectionEnd

Section "Desktop shortcut" SecDesktop
  CreateShortcut "$DESKTOP\noga.lnk" "$INSTDIR\noga-app.exe" "" "$INSTDIR\noga-app.exe" 0
SectionEnd

Section "Add noga to PATH (the command line)" SecPath
  !insertmacro EditPath "$$true"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "OnPath" 1
SectionEnd

Section "Uninstall"
  ReadRegDWORD $1 HKCU "${UNINSTALL_KEY}" "OnPath"
  ${If} $1 = 1
    !insertmacro EditPath "$$false"
  ${EndIf}
  Delete "$SMPROGRAMS\noga.lnk"
  Delete "$DESKTOP\noga.lnk"
  !include "${FILES}"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
SectionEnd
