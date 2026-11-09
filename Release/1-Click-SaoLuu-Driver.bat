@echo off
chcp 65001 >nul
title 1-Click Sao Luu Driver
echo ================================================================
echo         CÔNG CỤ TỰ ĐỘNG SAO LƯU DRIVER (OFFLINE)
echo ================================================================
echo.

:: Kiem tra quyen Admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Vui long chay file nay bang quyen Run as Administrator!
    pause
    exit /b 1
)

set BACKUP_DIR=%~dp0DriverBackup
echo [*] Thu muc sao luu: %BACKUP_DIR%
echo [*] Dang sao luu toan bo driver OEM cua may...
echo.

"%~dp0DriverChecker.exe" /backup "%BACKUP_DIR%"

if %errorLevel% equ 0 (
    echo.
    echo ================================================================
    echo [OK] DA SAO LUU DRIVER THANH CONG!
    echo Toan bo driver da duoc luu tai thu muc DriverBackup canh file nay.
    echo Copy toan bo thu muc nay sang USB de dung sau khi cai lai Win.
    echo ================================================================
) else (
    echo.
    echo [LOI] Sao luu that bai! Ma loi: %errorLevel%
)

echo.
pause
