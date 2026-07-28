# UDM_21: Giám Sát Thiết Bị IoT Qua MQTT (C# .NET 8 WPF)

> **Đề tài:** Giám sát và điều khiển thiết bị IoT thông qua giao thức MQTT.  
> **Ứng dụng:** C# .NET 8 WPF Desktop Dashboard App & C# Console App Giả lập 5+ thiết bị IoT.

---

## 👥 Danh sách thành viên nhóm

| STT | Họ và tên | Mã sinh viên | Vai trò / Phân công | GitHub Account |
|---|---|---|---|---|
| 1 | Nguyễn Văn A | 20000001 | Trưởng nhóm - Thiết kế WPF Dashboard GUI (MVVM) | `@nguyenvana` |
| 2 | Trần Thị B | 20000002 | Phát triển C# Simulators (5+ IoT Devices) | `@tranthib` |
| 3 | Lê Văn C | 20000003 | Xử lý MQTTnet, QoS, Reconnect, LWT | `@levanc` |
| 4 | Phạm Văn D | 20000004 | Kiểm thử (Functional, Stress, Performance) & Báo cáo | `@phamvand` |

---

## 📽️ Link Video Demo
- **URL Video Demo:** [Link Youtube/Drive ở chế độ Public hoặc Unlisted](https://youtube.com/...)

---

## 📐 Kiến trúc Hệ thống & Giao thức

### 1. Mô hình tổng quan (Pub/Sub Pattern via MQTT Broker)
```text
+-----------------------+                +--------------------+                +-----------------------+
|  IoT Simulators (5+)  |  -- Publish -> |    MQTT Broker     |  -- Publish -> | WPF Desktop Dashboard |
|  (C# Console App)     | <- Subscribe - | (Mosquitto / EMQX) | <- Subscribe - |  (C# .NET 8 WPF App)  |
+-----------------------+                +--------------------+                +-----------------------+
```

### 2. Thiết kế Topic (MQTT Topic Design)
Các topic được chuẩn hóa theo cấu trúc: `iot/{location}/{device_type}/{device_id}/{action_or_type}`

- **Dữ liệu cảm biến (Telemetry - Pub from Devices):**
  - `iot/lab/sensor/temp_hum_01/telemetry`
  - `iot/factory/sensor/air_quality_01/telemetry`
  - `iot/home/meter/power_meter_01/telemetry`
  - `iot/home/light/smart_light_01/telemetry`
  - `iot/lab/security/door_sensor_01/telemetry`
  - *Dashboard subscribe bằng Wildcard:* `iot/+/+/+/telemetry` hoặc `iot/#`

- **Trạng thái Online/Offline (Last Will & Testament - LWT / Status):**
  - `iot/{location}/{device_type}/{device_id}/status`  
    *(Payload: `{"device_id": "temp_hum_01", "status": "online"}` with Retained=True)*

- **Lệnh điều khiển (Command - Pub from Dashboard):**
  - `iot/{location}/{device_type}/{device_id}/cmd`  
    *(Payload: `{"command": "TOGGLE_POWER", "params": {"state": "ON"}}`)*

---

## 📁 Cấu trúc Thư mục Repository

```text
UDM_21-Monitoring-IoT-devices-via-MQTT/
├── Code/                          # Mã nguồn C# .NET 8
│   ├── UDM_21.sln                 # Visual Studio Solution File
│   ├── Dashboard/                 # WPF Desktop Dashboard Application
│   │   ├── Controllers/           # MQTT Controller giao tiếp MQTTnet & ViewModel
│   │   ├── Models/                # Data model quản lý danh sách & trạng thái thiết bị
│   │   ├── ViewModels/            # MVVM ViewModel hỗ trợ DataBinding
│   │   ├── Views/                 # WPF UI (MainWindow.xaml)
│   │   └── Dashboard.csproj       # Project file WPF App
│   ├── Simulators/                # C# Console App giả lập 5+ thiết bị IoT
│   │   ├── Devices/               # 5 lớp thiết bị cụ thể (Temp, Air, Power, Light, Door)
│   │   ├── DeviceBase.cs          # Abstract base class cho thiết bị IoT
│   │   ├── Program.cs             # Khởi chạy đồng thời 5 thiết bị
│   │   ├── config.json            # Cấu hình danh sách thiết bị
│   │   └── Simulators.csproj      # Project file Console App
│   ├── Shared/                    # Class Library chứa Mã nguồn dùng chung
│   │   ├── MqttHelper.cs          # Helper wrap thư viện MQTTnet
│   │   ├── Protocol.cs            # Data Contract (Telemetry, Status, Command)
│   │   └── Shared.csproj          # Project file Class Library
│   └── config.example.json        # Cấu hình mẫu cho MQTT Broker
├── DOCX/                          # File báo cáo bài tập lớn (.docx)
├── Extra/                         # Log hệ thống, tài liệu kiểm thử, kịch bản test
│   ├── logs/                      # Lưu file log chạy ứng dụng
│   ├── test_results/              # Lưu báo cáo stress test / performance test
│   └── scripts/                   # Script hỗ trợ đo đạc
├── PPTX/                          # Slide thuyết trình bảo vệ (.pptx)
├── .gitignore                     # Cấu hình Git bỏ qua file Visual Studio build
└── README.md                      # Hướng dẫn chi tiết & thông tin dự án
```

---

## 🛠️ Yêu cầu Môi trường & Cài đặt

1. **Môi trường:** .NET 8.0 SDK (hoặc Visual Studio 2022 v17.8+)
2. **Thư viện NuGet:**
   - `MQTTnet` (v4.x)
   - `Newtonsoft.Json` hoặc `System.Text.Json`
3. **MQTT Broker:** Mosquitto Broker chạy local (port `1883`) hoặc Broker Cloud.

---

## 🚀 Hướng dẫn Khởi chạy (Build & Run)

### Cách 1: Sử dụng Visual Studio 2022
1. Mở file `Code/UDM_21.sln` bằng Visual Studio 2022.
2. Restore NuGet packages (`Build -> Restore NuGet Packages`).
3. Set **Multiple Startup Projects**:
   - `Simulators` -> Start
   - `Dashboard` -> Start
4. Nhấn `F5` để chạy cả Simulators và Dashboard.

### Cách 2: Sử dụng .NET CLI
```bash
# 1. Build Solution
cd Code
dotnet build UDM_21.sln

# 2. Chạy ứng dụng Simulators
dotnet run --project Simulators/Simulators.csproj

# 3. Chạy ứng dụng WPF Dashboard (trên Cửa sổ terminal khác)
dotnet run --project Dashboard/Dashboard.csproj
```
