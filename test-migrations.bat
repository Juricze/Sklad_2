@echo off
echo ================================
echo   MIGRATION TESTING SCRIPT
echo ================================
echo.

set DB_PATH=%LOCALAPPDATA%\Sklad_2_Data\sklad.db
set BACKUP_PATH=%LOCALAPPDATA%\Sklad_2_Data\sklad_backup.db

echo Database path: %DB_PATH%
echo.

:MENU
echo ================================
echo   SELECT TEST SCENARIO:
echo ================================
echo 1. Test NEW database (delete existing)
echo 2. Test UPGRADE (backup current, simulate old DB)
echo 3. Restore original database
echo 4. Check current schema version
echo 5. Exit
echo.
set /p choice="Enter your choice (1-5): "

if "%choice%"=="1" goto TEST_NEW
if "%choice%"=="2" goto TEST_UPGRADE  
if "%choice%"=="3" goto RESTORE
if "%choice%"=="4" goto CHECK_VERSION
if "%choice%"=="5" goto EXIT
goto MENU

:TEST_NEW
echo.
echo === TEST 1: New Database ===
echo Backing up current database...
if exist "%DB_PATH%" (
    copy "%DB_PATH%" "%BACKUP_PATH%" >nul
    echo Current database backed up to sklad_backup.db
)
echo.
echo Deleting current database...
if exist "%DB_PATH%" del "%DB_PATH%"
echo Database deleted.
echo.
echo NOW RUN THE APPLICATION!
echo Expected result:
echo - New database created with schema version 2
echo - All discount features work immediately
echo.
pause
goto MENU

:TEST_UPGRADE
echo.
echo === TEST 2: Database Upgrade ===
echo Backing up current database...
if exist "%DB_PATH%" (
    copy "%DB_PATH%" "%BACKUP_PATH%" >nul
    echo Current database backed up to sklad_backup.db
)
echo.
echo Creating simulated OLD database (without discount fields)...
echo This requires SQLite command line tool...
echo.
echo Please follow manual steps in the console output above.
echo.
pause
goto MENU

:RESTORE
echo.
echo === RESTORE: Original Database ===
if exist "%BACKUP_PATH%" (
    echo Restoring original database from backup...
    copy "%BACKUP_PATH%" "%DB_PATH%" >nul
    echo Database restored from sklad_backup.db
) else (
    echo No backup found at %BACKUP_PATH%
)
echo.
pause
goto MENU

:CHECK_VERSION
echo.
echo === CHECK: Current Schema Version ===
echo Database path: %DB_PATH%
if exist "%DB_PATH%" (
    echo Database file exists.
    echo To check schema version, run this SQLite query:
    echo   SELECT MAX(version) FROM schema_versions;
    echo.
    echo Opening database with SQLite (if available)...
    where sqlite3 >nul 2>nul
    if %errorlevel%==0 (
        sqlite3 "%DB_PATH%" "SELECT 'Current schema version: ' || COALESCE(MAX(version), 'No version table') FROM sqlite_master LEFT JOIN schema_versions WHERE sqlite_master.name = 'schema_versions';"
    ) else (
        echo SQLite3 command not found in PATH.
        echo Install SQLite or check manually with database browser.
    )
) else (
    echo Database file does not exist.
)
echo.
pause
goto MENU

:EXIT
echo.
echo Exiting...
exit /b 0