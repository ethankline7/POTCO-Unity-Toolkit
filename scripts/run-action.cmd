@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "PROJECT_ROOT=%%~fI"

call :resolve_powershell
if errorlevel 1 exit /b 1

if "%~1"=="" goto :help_error

set "ACTION=%~1"
shift /1

if /I "%ACTION%"=="open" goto :open_unity
if /I "%ACTION%"=="primary-checks" goto :primary_checks
if /I "%ACTION%"=="parser-regression" goto :parser_regression
if /I "%ACTION%"=="smoke" goto :smoke
if /I "%ACTION%"=="dna-demo" goto :dna_demo
if /I "%ACTION%"=="setup-resources" goto :setup_resources
if /I "%ACTION%"=="import-dna-assets" goto :import_dna_assets
if /I "%ACTION%"=="help" goto :help_ok

echo Unknown action: %ACTION%
echo.
goto :help_error

:open_unity
call :resolve_unity
if errorlevel 1 exit /b 1
echo Launching Unity project from:
echo   %PROJECT_ROOT%
echo Using editor:
echo   %UNITY_EXE%
start "" "%UNITY_EXE%" -projectPath "%PROJECT_ROOT%" %*
exit /b 0

:primary_checks
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%primary-checks.ps1" %*
exit /b %ERRORLEVEL%

:parser_regression
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-toontown-parser-regression.ps1" %*
exit /b %ERRORLEVEL%

:smoke
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-toontown-smoke.ps1" %*
exit /b %ERRORLEVEL%

:dna_demo
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-toontown-dna-demo.ps1" %*
exit /b %ERRORLEVEL%

:setup_resources
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%setup-toontown-resources.ps1" %*
exit /b %ERRORLEVEL%

:import_dna_assets
call "%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%import-toontown-dna-assets.ps1" %*
exit /b %ERRORLEVEL%

:resolve_powershell
if defined POWERSHELL_EXE exit /b 0

where pwsh >nul 2>nul
if not errorlevel 1 (
  set "POWERSHELL_EXE=pwsh"
  exit /b 0
)

where powershell >nul 2>nul
if not errorlevel 1 (
  set "POWERSHELL_EXE=powershell"
  exit /b 0
)

echo Could not find PowerShell. Install PowerShell 7 (pwsh) or Windows PowerShell.
exit /b 1

:resolve_unity
if defined UNITY_EDITOR_PATH (
  if exist "%UNITY_EDITOR_PATH%" (
    set "UNITY_EXE=%UNITY_EDITOR_PATH%"
    exit /b 0
  )
)

set "CANDIDATE=C:\Program Files\Unity\Hub\Editor\6000.1.11f1\Editor\Unity.exe"
if exist "%CANDIDATE%" (
  set "UNITY_EXE=%CANDIDATE%"
  exit /b 0
)

set "CANDIDATE=C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe"
if exist "%CANDIDATE%" (
  set "UNITY_EXE=%CANDIDATE%"
  exit /b 0
)

set "CANDIDATE=C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe"
if exist "%CANDIDATE%" (
  set "UNITY_EXE=%CANDIDATE%"
  exit /b 0
)

echo Unity editor not found.
echo Set UNITY_EDITOR_PATH to your Unity.exe path, then rerun:
echo   set UNITY_EDITOR_PATH=C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe
exit /b 1

:help_ok
call :print_help
exit /b 0

:help_error
call :print_help
exit /b 1

:print_help
echo Toontown Toolkit cmd runner
echo.
echo Usage:
echo   scripts\run-action.cmd ^<action^> [args]
echo.
echo Actions:
echo   open                 Launch Unity Editor with this project
echo   primary-checks       Run scripts\primary-checks.ps1
echo   parser-regression    Run scripts\run-toontown-parser-regression.ps1
echo   smoke                Run scripts\run-toontown-smoke.ps1
echo   dna-demo             Run scripts\run-toontown-dna-demo.ps1
echo   setup-resources      Run scripts\setup-toontown-resources.ps1
echo   import-dna-assets    Run scripts\import-toontown-dna-assets.ps1
echo.
echo Examples:
echo   scripts\run-action.cmd open
echo   scripts\run-action.cmd primary-checks
echo   scripts\run-action.cmd parser-regression -LogPath "Temp\toontown-parser-regression.log"
echo   scripts\run-action.cmd smoke -LogPath "Temp\toontown-smoke.log"
echo   scripts\run-action.cmd dna-demo -SkipResourceSetup
echo.
exit /b 0
