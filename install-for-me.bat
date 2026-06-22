@echo off
rem ============================================================================
rem  Builds the add-in and installs it straight into THIS PC's Navisworks
rem  (<Navisworks>\Plugins\NavisworksIfcExporter). Use this to just USE the
rem  add-in yourself. To make a shareable installer, use make-installer.bat.
rem
rem  Requirements: Navisworks 2022+ , Visual Studio 2022 / Build Tools or .NET SDK.
rem  Navisworks is auto-detected; override with:
rem      install-for-me.bat -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2024"
rem ============================================================================
setlocal

rem --- self-elevate (writing under Program Files needs admin) ---
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo Requesting administrator rights...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs -ArgumentList '%*'"
  exit /b
)

cd /d "%~dp0"

echo(
echo === Building and installing Navisworks IFC Exporter ===
echo(

powershell -NoProfile -ExecutionPolicy Bypass -File "build\install-local.ps1" %*
if errorlevel 1 (
  echo(
  echo FAILED. Read the messages above.
) else (
  echo(
  echo Done. Start Navisworks, then:  Tool Add-ins  ^-^>  Export IFC
)
echo(
pause
endlocal
