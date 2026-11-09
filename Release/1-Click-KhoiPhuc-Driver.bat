@echo off
chcp 65001 >nul
title 1-Click Khoi Phuc Driver
echo ================================================================
echo       CÔNG CỤ TỰ ĐỘNG KHÔI PHỤC DRIVER SAU KHI CÀI WIN
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

if not exist "%BACKUP_DIR%" (
    echo [LOI] Khong tim thay thu muc DriverBackup canh file nay!
    echo Vui long dam bao thu muc chua driver backup da duoc copy vao day.
    pause
    exit /b 1
)

echo [*] Thu muc nguon: %BACKUP_DIR%
echo [*] Dang tien hanh cai dat lai toan bo driver vao may (KHONG CAN MANG)...
echo.

"%~dp0DriverChecker.exe" /restore "%BACKUP_DIR%"

echo.
echo ================================================================
echo [OK] DA HOAN TAT KHOI PHUC DRIVER!
echo Khuyen nghi khoi dong lai may de tat ca driver co hieu luc.
echo ================================================================
echo.
pause
