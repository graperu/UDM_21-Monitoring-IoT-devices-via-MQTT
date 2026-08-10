# UDM_21: Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)

> **Đề tài UDM_21:** Giám sát thiết bị IoT qua MQTT.  
> **Ứng dụng:** C# .NET 8 WPF Desktop Dashboard App & C# Console App Giả lập 5+ thiết bị IoT.  
> **Môn học:** Lập Trình Mạng (Network Programming).  
> **Số lượng thành viên:** 5 Sinh viên.

---

## 👥 1. Danh sách Thành viên Nhóm & Phân công Công việc (5 Thành Viên)

| STT | Họ và tên | Mã Sinh Viên | Vai Trò / Nhiệm Vụ Phân Công | Thư Mục / File Đảm Nhận | GitHub Account |
|---|---|---|---|---|---|
| 1 | [Họ tên SV 1] | [MSSV 1] | **Trưởng nhóm:** Thiết kế & Lập trình Giao diện Dashboard GUI (WPF XAML, DataBinding, Layout) | `Code/Dashboard/MainWindow.xaml`<br>`Code/Dashboard/App.xaml` | `@account1` |
| 2 | [Họ tên SV 2] | [MSSV 2] | **Lập trình Logic Dashboard:** Event Handler, Controller & Dispatcher không làm treo GUI | `Code/Dashboard/Controllers/`<br>`Code/Dashboard/Models/`<br>`Code/Dashboard/MainWindow.xaml.cs` | `@account2` |
| 3 | [Họ tên SV 3] | [MSSV 3] | **Lập trình Thiết bị Giả lập:** Multi-threading 5 IoT Devices, Vòng lặp Telemetry, LWT Status | `Code/Simulators/DeviceBase.cs`<br>`Code/Simulators/Devices/`<br>`Code/Simulators/Program.cs` | `@account3` |
| 4 | [Họ tên SV 4] | [MSSV 4] | **Lập trình Giao thức Mạng Cốt lõi:** Wrapper MQTTnet, Pub/Sub, QoS 0/1, Reconnect, JSON Protocol | `Code/Shared/MqttHelper.cs`<br>`Code/Shared/Protocol.cs`<br>`Code/Shared/Shared.csproj` | `@account4` |
| 5 | [Họ tên SV 5] | [MSSV 5] | **Kiểm thử & Báo cáo:** Stress Test, Performance Test, Đo độ trễ, Viết Báo cáo (.docx) & Slide (.pptx) | `Extra/scripts/stress_test.py`<br>`DOCX/`<br>`PPTX/` | `@account5` |

---

## 📽️ 2. Link Video Demo Dự án
- **URL Video Demo:** [Link Youtube/Google Drive ở chế độ Public hoặc Unlisted](https://youtube.com/...)

---

## 📐 3. Kiến trúc Hệ thống & Thiết kế Giao thức Mạng

### 3.1. Mô hình Tổng quan (Pub/Sub Pattern via MQTT Broker)
```text
+-----------------------+                +--------------------+                +-----------------------+
|  IoT Simulators (5+)  |  -- Publish -> |    MQTT Broker     |  -- Publish -> | WPF Desktop Dashboard |
|  (C# Console App)     | <- Subscribe - | (Mosquitto / EMQX) | <- Subscribe - |  (C# .NET 8 WPF App)  |
+-----------------------+                +--------------------+                +-----------------------+
```

### 3.2. Thiết kế Topic (MQTT Topic Design)
Các topic được thiết kế phân cấp chuẩn: `iot/{location}/{device_type}/{device_id}/{action_or_type}`

1. **Dữ liệu Cảm biến (Telemetry - Published by Devices):**
   - `iot/lab/sensor/temp_hum_01/telemetry`
   - `iot/factory/sensor/air_quality_01/telemetry`
   - `iot/home/meter/power_meter_01/telemetry`
   - `iot/home/light/smart_light_01/telemetry`
   - `iot/lab/security/door_sensor_01/telemetry`
   - *Wildcard Subscription từ Dashboard:* `iot/+/+/+/telemetry` hoặc `iot/#`

2. **Trạng thái Trực tuyến / Ngoại tuyến (Status / Last Will & Testament - LWT):**
   - `iot/{location}/{device_type}/{device_id}/status`
   - Payload: `{"device_id": "temp_hum_01", "status": "online"}` (Pub với `Retain = true`)

3. **Lệnh Điều khiển (Command - Published by Dashboard):**
   - `iot/{location}/{device_type}/{device_id}/cmd`
   - Payload: `{"message_id": "...", "command": "TOGGLE_POWER", "params": {"state": "ON"}}`

---

## 📚 4. Giải Thích Các Khái Niệm Mạng Cốt Lõi (Lập Trình Mạng)

- **Quality of Service (QoS):**
  - **QoS 0 (At most once):** Áp dụng cho telemetry gửi liên tục (nhiệt độ, công suất). Nếu mất gói tin, gói tiếp theo sẽ cập nhật ngay.
  - **QoS 1 (At least once):** Áp dụng cho thông điệp trạng thái (Online/Offline) và lệnh điều khiển quan trọng (bật/tắt thiết bị) để đảm bảo không bị thất lạc tin nhắn.
- **Retained Message:**
  - Được dùng trên các Topic `status`. Khi Dashboard vừa khởi động và Subscribe, MQTT Broker sẽ lập tức gửi lại trạng thái mới nhất của các thiết bị mà không cần chờ thiết bị phát tin mới.
- **Last Will and Testament (LWT):**
  - Cấu hình cho Broker biết: nếu thiết bị IoT bị ngắt kết nối đột ngột (mất mạng, rớt cáp, tắt nguồn), Broker sẽ thay mặt thiết bị Publish tin nhắn `status: offline` đến Dashboard.
- **Clean Session / Persistent Session:**
  - Simulators sử dụng Clean Session để kết nối nhanh. Dashboard hỗ trợ nhận lại dữ liệu lệnh/cảnh báo nhỡ khi bị mất kết nối tạm thời.

---

## 📁 5. Cấu trúc Thư mục Repository & Phân chia Code

```text
UDM_21-Monitoring-IoT-devices-via-MQTT/
├── .vscode/                       # Cấu hình F5 Debug & Build tự động cho Visual Studio Code
│   ├── launch.json                # Cấu hình F5 Debug cả Dashboard & Simulators
│   ├── tasks.json                 # Cấu hình dotnet build & dotnet run
│   └── settings.json              # Chỉ định UDM_21.sln mặc định
├── Code/                          # Mã nguồn chính C# .NET 8
│   ├── UDM_21.sln                 # Visual Studio Solution File
│   ├── Dashboard/                 # WPF Desktop Dashboard App (Thành viên 1 & Thành viên 2)
│   │   ├── Controllers/           # MqttController (Giao tiếp MQTTnet & WPF Dispatcher) - TV 2
│   │   ├── Models/                # DeviceItem (INotifyPropertyChanged DataBinding) - TV 2
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml        # Giao diện WPF (DataGrid, Form phát lệnh, Console Log) - TV 1
│   │   └── MainWindow.xaml.cs     # Event Handler (Xử lý không treo GUI) - TV 2
│   ├── Simulators/                # C# Console App Giả lập 5+ Thiết bị (Thành viên 3)
│   │   ├── Devices/               # 5 loại thiết bị cảm biến & công tơ điện - TV 3
│   │   ├── DeviceBase.cs          # Abstract class thiết bị IoT (Multi-threading, LWT) - TV 3
│   │   ├── Program.cs             # Khởi chạy đồng thời 5 thiết bị - TV 3
│   │   └── config.json            # Cấu hình các thiết bị giả lập - TV 3
│   ├── Shared/                    # Class Library dùng chung (Thành viên 4)
│   │   ├── MqttHelper.cs          # Wrapper gói thư viện MQTTnet v4.x - TV 4
│   │   └── Protocol.cs            # Data Contract JSON (Telemetry, Status, Command) - TV 4
│   └── config.example.json        # Cấu hình MQTT Broker mẫu
├── DOCX/                          # Nơi lưu báo cáo bài tập lớn (.docx) (Thành viên 5)
│   └── README.md
├── Extra/                         # Thư mục logs & kết quả kiểm thử (Thành viên 5)
│   ├── logs/                      # Nhật ký hệ thống - TV 5
│   ├── test_results/              # Kết quả stress test / performance test - TV 5
│   └── scripts/                   # Script kiểm thử độ trễ & throughput - TV 5
├── PPTX/                          # Slide thuyết trình bảo vệ (.pptx) (Thành viên 5)
│   └── README.md
├── .gitignore                     # Gitignore chuẩn C# / Visual Studio (.NET)
└── README.md                      # Tài liệu hướng dẫn & thông tin dự án
```

---

## 🚀 6. Hướng dẫn Khởi chạy Dự án

### Cách 1: Sử dụng Visual Studio Code (Khuyên dùng)
1. Cài đặt Extension **C# Dev Kit** trên VS Code.
2. Mở thư mục dự án trong VS Code.
3. Nhấn `Ctrl + Shift + D`, chọn configuration: **`🚀 Debug All (Simulators + Dashboard)`**.
4. Nhấn **`F5`** để chạy tự động cả 5 Thiết bị giả lập và WPF Dashboard!

### Cách 2: Sử dụng Visual Studio 2022
1. Mở file `Code/UDM_21.sln`.
2. Phải chuột vào Solution -> chọn **Set Startup Projects...** -> chọn **Multiple startup projects**:
   - `Simulators` -> **Start**
   - `Dashboard` -> **Start**
3. Nhấn **`F5`** để khởi chạy.

### Cách 3: Sử dụng .NET CLI
```bash
# Terminal 1: Chạy các thiết bị giả lập
dotnet run --project Code/Simulators/Simulators.csproj

# Terminal 2: Chạy ứng dụng Dashboard WPF
dotnet run --project Code/Dashboard/Dashboard.csproj
```
// HGiaHop test
