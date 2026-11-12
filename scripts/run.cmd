@echo off
rem AquaTrack - one click: apply pending database changes, then start the app.
rem Stop the app in Visual Studio first (Shift+F5), otherwise the port is busy.
title AquaTrack
cd /d "%~dp0.."

if not exist "src\AquaTrack.Infrastructure\Persistence\Migrations\*_Day6Payments.cs" (
    powershell -ExecutionPolicy Bypass -File "%~dp0migrate.ps1" -Name Day6Payments || goto :fail
)
if not exist "src\AquaTrack.Infrastructure\Persistence\Migrations\*_Day6WalletNames.cs" (
    powershell -ExecutionPolicy Bypass -File "%~dp0migrate.ps1" -Name Day6WalletNames || goto :fail
)

echo.
echo ==^> Starting AquaTrack on https://localhost:7180  (close this window to stop)
echo.
start "" cmd /c "timeout /t 15 >nul & start https://localhost:7180"
dotnet run --project src\AquaTrack.Web --launch-profile https
echo.
echo AquaTrack stopped.
pause
exit /b 0

:fail
echo.
echo FAILED. Take a screenshot of this window and send it to Claude.
pause
exit /b 1
