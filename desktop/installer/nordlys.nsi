; Nordlys installer (per user, no admin rights).
; Build: makensis /DVERSION=0.2.0 /DSOURCE=..\publish\folder nordlys.nsi
; Installs into %LOCALAPPDATA%\Nordlys, the same folder and uninstall entry the earlier Tauri version used,
; so installing this replaces that version in place. Your data (%APPDATA%\app.nordlys) is never touched,
; by the installer or the uninstaller.

Unicode true
SetCompressor /SOLID lzma
!include "MUI2.nsh"

!ifndef VERSION
  !define VERSION "0.2.0"
!endif
!ifndef SOURCE
  !define SOURCE "..\publish\folder"
!endif
!define APPNAME "Nordlys"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nordlys"

Name "${APPNAME}"
OutFile "..\publish\Nordlys-${VERSION}-setup.exe"
InstallDir "$LOCALAPPDATA\${APPNAME}"
RequestExecutionLevel user
BrandingText "${APPNAME} ${VERSION}"
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "FileDescription" "${APPNAME} installer"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

!define MUI_ICON "..\Nordlys.App\Assets\nordlys.ico"
!define MUI_UNICON "..\Nordlys.App\Assets\nordlys.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\Nordlys.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open Nordlys"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  SetOutPath "$INSTDIR"
  ; Remove the old app files (Tauri version, or a previous Nordlys) but nothing else.
  Delete "$INSTDIR\nordlys.exe"
  RMDir /r "$INSTDIR\runtimes"
  File /r "${SOURCE}\*.*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  CreateShortCut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\Nordlys.exe" "" "$INSTDIR\Nordlys.exe" 0

  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "Elliott"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\Nordlys.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  ; Estimated size in KB, for Settings > Apps.
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" 150000
SectionEnd

Section "Uninstall"
  ; App files only. Lessons and progress in %APPDATA%\app.nordlys stay.
  RMDir /r "$INSTDIR"
  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  DeleteRegKey HKCU "${UNINSTKEY}"
SectionEnd
