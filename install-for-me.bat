@echo off
rem ============================================================================
rem  Builds the add-in and installs it straight into THIS PC's Navisworks.
rem  Use this if you just want to USE the add-in yourself (no Inno Setup needed,
rem  no setup.exe produced). To make a shareable installer, use make-installer.bat.
rem
rem  Requirements on this Windows PC:
rem    - Navisworks 2022+ installed
rem    - Visual Studio 2022 / Build Tools  OR  the .NET SDK
rem
rem  For a different Navisworks version/path:
rem    install-for-me.bat -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2024"
rem ============================================================================
setlocal
cd /d "%~dp0"

echo(
echo === Building Navisworks IFC Exporter ===
echo(

powershell -NoProfile -ExecutionPolicy Bypass -File "build\pack-bundle.ps1" %*
if errorlevel 1 (
  echo(
  echo BUILD FAILED. Read the messages above.
  pause
  endlocal
  exit /b 1
)

set "DEST=%APPDATA%\Autodesk\ApplicationPlugins\NavisworksIfcExporter.bundle"
echo(
echo Installing to: %DEST%
if exist "%DEST%" rmdir /s /q "%DEST%"
xcopy /e /i /y "artifacts\NavisworksIfcExporter.bundle" "%DEST%" >nul

echo(
echo Done. Start Navisworks, then:  Tool Add-ins  ->  Export IFC
echo(
pause
endlocal
