@echo off
rem ============================================================================
rem  Builds the shareable installer (setup.exe) for the Navisworks IFC Exporter.
rem
rem  Requirements on THIS Windows PC:
rem    - Navisworks 2022+ installed (provides the API DLLs)
rem    - Visual Studio 2022 (or "Build Tools for Visual Studio") OR the .NET SDK
rem    - Inno Setup 6   ->  https://jrsoftware.org/isdl.php
rem
rem  Output:  artifacts\NavisworksIfcExporter-Setup-0.1.0.exe
rem
rem  If your Navisworks is not 2022 at the default path, pass it through, e.g.:
rem    make-installer.bat -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2024"
rem  To sign it (publisher CHAN):
rem    make-installer.bat -SignPfx installer\CHAN-codesign.pfx -SignPassword "pw"
rem ============================================================================
setlocal
cd /d "%~dp0"

echo(
echo === Building Navisworks IFC Exporter installer ===
echo(

powershell -NoProfile -ExecutionPolicy Bypass -File "build\build-installer.ps1" %*
set RC=%ERRORLEVEL%

echo(
if "%RC%"=="0" (
  echo Done. Your installer is in the "artifacts" folder:
  echo     artifacts\NavisworksIfcExporter-Setup-0.1.0.exe
  echo Share that single .exe - users just double-click it.
) else (
  echo BUILD FAILED. Read the messages above.
  echo Most common cause: Inno Setup 6 or a build tool is not installed.
)
echo(
pause
endlocal
