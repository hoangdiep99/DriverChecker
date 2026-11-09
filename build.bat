@echo off
chcp 65001 >nul
echo ================================================================
echo       BIÊN DỊCH DRIVER CHECKER VỚI .NET CSC CÓ SẴN
echo ================================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [LOI] Khong tim thay csc.exe tren he thong!
    pause
    exit /b 1
)

set REFDIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319

echo [*] Dang bien dich DriverChecker.exe...
"%CSC%" /target:winexe /out:"DriverChecker.exe" /platform:anycpu ^
    /win32manifest:"DriverChecker.exe.manifest" ^
    /win32icon:"app.ico" ^
    /r:"%REFDIR%\System.Management.dll" ^
    /r:"%REFDIR%\System.Windows.Forms.dll" ^
    /r:"%REFDIR%\System.Drawing.dll" ^
    /r:"%REFDIR%\System.ServiceProcess.dll" ^
    /r:"%REFDIR%\Microsoft.CSharp.dll" ^
    /r:"%REFDIR%\System.Core.dll" ^
    /r:"%REFDIR%\System.dll" ^
    DriverChecker.cs

if %errorLevel% equ 0 (
    echo [OK] Bien dich thanh cong! DriverChecker.exe da san sang.
    copy /Y DriverChecker.exe Release\DriverChecker.exe >nul
) else (
    echo [LOI] Bien dich that bai.
)

echo.
pause
