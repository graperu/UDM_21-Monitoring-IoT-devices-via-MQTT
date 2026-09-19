@echo off
setlocal
cd /d "%~dp0"
dotnet build Code/UDM_21.sln -c Release
if errorlevel 1 (
    echo Build that bai. Vui long xem loi ben tren.
    pause
    exit /b 1
)
title UDM_21 MQTT IoT Launcher
echo ===================================================
echo   UDM_21: Dang khoi chay 5 Thiet Bi IoT va Dashboard
echo ===================================================
echo.
echo 1. Dang bat 5 thiet bi gia lap IoT...
start "1. IoT Simulators (5 Devices)" cmd /k "dotnet run --no-build -c Release --project Code/Simulators/Simulators.csproj -- --host broker.emqx.io --port 1883 --topic-root udm21_nhom01"
ping 127.0.0.1 -n 3 >nul
echo.
echo 2. Dang bat giao dien WPF Dashboard...
start "" dotnet run --no-build -c Release --project Code/Dashboard/Dashboard.csproj
echo.
echo [XONG] Da khoi chay xong ca 2 ung dung!
