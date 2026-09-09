@echo off
title UDM_21 MQTT IoT Multi-Process Launcher
echo ===================================================
echo   UDM_21: Khoi chay moi thiet bi trong mot tien trinh
echo ===================================================

set "UDM21_BROKER=broker.emqx.io"
set "UDM21_PORT=1883"
set "UDM21_ROOT=udm21_nhom01"

for %%D in (temp_hum_01 air_quality_01 power_meter_01 smart_light_01 door_sensor_01) do (
    start "IoT %%D" cmd /k "dotnet run --project Code/Simulators/Simulators.csproj -- --host %UDM21_BROKER% --port %UDM21_PORT% --topic-root %UDM21_ROOT% --device %%D"
)

ping 127.0.0.1 -n 3 >nul
start "WPF Dashboard" cmd /k "dotnet run --project Code/Dashboard/Dashboard.csproj"

echo [XONG] Da khoi chay 5 tien trinh thiet bi va Dashboard.
