@echo off
REM ============================================================
REM Sklad_2 Standalone Updater - Windows Batch Launcher
REM ============================================================
REM Tento BAT soubor spouští PowerShell updater s obejitím
REM Execution Policy (obchází Microsoft digital signature check)
REM
REM Použití: Dvojklik na tento soubor
REM ============================================================

echo.
echo ========================================
echo   SKLAD_2 STANDALONE UPDATER
echo ========================================
echo.
echo Spoustim PowerShell updater...
echo.

REM Získej cestu k adresáři, kde je tento BAT soubor
set "SCRIPT_DIR=%~dp0"

REM Spusť PowerShell script s bypass execution policy
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT_DIR%StandaloneUpdater.ps1"

REM Počkej na stisk klávesy pokud script selhal
if errorlevel 1 (
    echo.
    echo ========================================
    echo   CHYBA PRI AKTUALIZACI
    echo ========================================
    echo.
    pause
)

exit /b %ERRORLEVEL%
