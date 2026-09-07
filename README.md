# UDM_21: Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)

> **Đề tài UDM_21:** Giám sát thiết bị IoT qua MQTT.  
> **Ứng dụng:** C# .NET 8 WPF Desktop Dashboard App & C# Console App Giả lập 5+ thiết bị IoT.  
> **Môn học:** Lập Trình Mạng (Network Programming).  
> **Số lượng thành viên:** 5 Sinh viên.

---

# 👥 1. Danh sách Thành viên Nhóm & Phân công Công việc

| STT | Họ và tên | MSSV | Vai trò / Nhiệm vụ | Thư mục / File đảm nhận |
|-----|-----------|-----------|------------------|-------------------------|
| 1 | Vũ Ngọc Cát Chương | 056206003708 | Thiết kế và lập trình giao diện Dashboard GUI (WPF), xây dựng MainWindow.xaml, App.xaml, DataGrid hiển thị danh sách thiết bị, form gửi lệnh điều khiển và khung Console Log. | `Code/Dashboard/MainWindow.xaml`<br>`Code/Dashboard/App.xaml` |
| 2 | Huỳnh Nữ Huyền Trâm | 051305001244 | Lập trình Logic Dashboard, Event Handler, Controller và Dispatcher. Xử lý Subscribe MQTT, cập nhật dữ liệu thời gian thực lên giao diện, xây dựng Model và Data Binding. | `Code/Dashboard/Controllers/`<br>`Code/Dashboard/Models/`<br>`Code/Dashboard/MainWindow.xaml.cs` |
| 3 | Phan Văn Đình | 054205006039 | Trưởng nhóm. Lập trình hệ thống thiết bị IoT giả lập. Xây dựng DeviceBase, mô phỏng nhiều thiết bị IoT, vòng lặp Telemetry, Last Will & Testament (LWT) và xử lý Command. | `Code/Simulators/DeviceBase.cs`<br>`Code/Simulators/Devices/`<br>`Code/Simulators/Program.cs` |
| 4 | Huỳnh Gia Hợp | 054205008367 | Lập trình giao thức mạng cốt lõi. Xây dựng MQTT Wrapper bằng MQTTnet, xử lý Publish/Subscribe, QoS 0/1, Auto Reconnect, MQTT Logging, JSON Protocol và Command ACK. | `Code/Shared/MqttHelper.cs`<br>`Code/Shared/Protocol.cs`<br>`Code/Shared/Shared.csproj` |
| 5 | Lưu Đình Thuận | 075205010434 | Kiểm thử và báo cáo. Thực hiện Stress Test, đo độ trễ và hiệu năng hệ thống, tổng hợp kết quả kiểm thử, viết báo cáo và slide thuyết trình. | `Extra/scripts/stress_test.py`<br>`DOCX/`<br>`PPTX/` |

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
---

## ✅ 7. Kết quả đạt được

Dự án đã xây dựng thành công hệ thống giám sát và điều khiển thiết bị IoT thông qua giao thức MQTT bằng ngôn ngữ C# .NET 8.


### Các chức năng đã hoàn thành

✅ Xây dựng Dashboard Desktop bằng WPF hiển thị dữ liệu thiết bị theo thời gian thực.

✅ Mô phỏng đồng thời 5 thiết bị IoT:
- Temperature & Humidity Sensor
- Air Quality Sensor
- Power Meter
- Smart Light
- Door Sensor

✅ Thiết bị gửi dữ liệu Telemetry định kỳ tới MQTT Broker theo mô hình Publish/Subscribe.

✅ Dashboard Subscribe dữ liệu từ nhiều Topic MQTT và cập nhật giao diện theo thời gian thực.

✅ Hiển thị trạng thái Online/Offline của thiết bị thông qua Last Will and Testament (LWT).

✅ Hỗ trợ gửi lệnh điều khiển từ Dashboard tới thiết bị.

✅ Triển khai cơ chế Command ACK giúp xác nhận lệnh điều khiển đã được thiết bị tiếp nhận.

✅ Hỗ trợ MQTT Logging để theo dõi toàn bộ hoạt động Publish, Subscribe và Command.

✅ Tự động kết nối lại (Auto Reconnect) khi Broker hoặc Client bị mất kết nối.

✅ Quản lý lịch sử Telemetry thông qua TelemetryHistoryManager.

✅ Hỗ trợ cảnh báo dữ liệu bất thường (Anomaly Alert) phục vụ giám sát hệ thống.

✅ Sử dụng Unique Client ID cho từng thiết bị nhằm tránh xung đột kết nối MQTT.

✅ Thực hiện Stress Test nhằm đánh giá độ ổn định và khả năng xử lý nhiều thông điệp MQTT liên tục.

✅ Hoàn thiện báo cáo và slide thuyết trình .



### Kết quả kiểm thử

- Dashboard nhận dữ liệu Telemetry ổn định từ nhiều thiết bị đồng thời.
- Thiết bị tự động cập nhật trạng thái Online/Offline khi kết nối thay đổi.
- Lệnh điều khiển được gửi và xác nhận thành công thông qua ACK Protocol.
- Hệ thống duy trì hoạt động ổn định khi thực hiện Stress Test với số lượng lớn thông điệp MQTT.
- Chức năng Auto Reconnect hoạt động đúng khi Broker bị ngắt kết nối tạm thời.

### Công nghệ sử dụng

- C# .NET 8
- WPF
- MQTTnet
- MQTT Broker (Mosquitto / EMQX)
- JSON Serialization
- Git & GitHub

---

## 📊 8. Đánh giá kết quả

Hệ thống hoạt động ổn định với nhiều thiết bị gửi dữ liệu đồng thời.

Dashboard có khả năng cập nhật dữ liệu theo thời gian thực và hiển thị trạng thái thiết bị chính xác.

Chức năng Auto Reconnect giúp hệ thống duy trì hoạt động khi Broker bị ngắt kết nối tạm thời.

Command ACK giúp xác nhận việc thực thi lệnh điều khiển từ Dashboard tới thiết bị.

MQTT Logging hỗ trợ theo dõi và xử lý lỗi trong quá trình vận hành hệ thống.

Dự án đáp ứng đầy đủ các yêu cầu của đề tài môn Lập Trình Mạng.

---

## 🤝 9. Đánh giá đóng góp thành viên

| Thành viên | Vai trò | Mức độ hoàn thành |
|------------|----------|------------------|
| Vũ Ngọc Cát Chương | Giao diện Dashboard WPF | Hoàn thành tốt |
| Huỳnh Nữ Huyền Trâm | Logic Dashboard | Hoàn thành tốt |
| Phan Văn Đình | Thiết bị IoT giả lập | Hoàn thành tốt |
| Huỳnh Gia Hợp | MQTT Protocol, Logging, Auto Reconnect, ACK Protocol | Hoàn thành tốt |
| Lưu Đình Thuận | Kiểm thử, Báo cáo, Slide | Hoàn thành tốt |

Tất cả thành viên đều hoàn thành nhiệm vụ được phân công đúng tiến độ, phối hợp hiệu quả trong quá trình phát triển dự án và không phát sinh mâu thuẫn trong quá trình làm việc nhóm.

---

## 🎯 Kết luận

Dự án đã áp dụng thành công giao thức MQTT để xây dựng hệ thống giám sát và điều khiển thiết bị IoT theo mô hình Publish/Subscribe.

Thông qua dự án, nhóm đã hiểu rõ hơn về lập trình mạng, giao thức MQTT, cơ chế QoS, Last Will & Testament (LWT), Auto Reconnect, Command ACK và việc xây dựng hệ thống IoT theo thời gian thực.

Hệ thống có thể tiếp tục mở rộng trong tương lai bằng cách tích hợp cơ sở dữ liệu, nền tảng Cloud hoặc các thiết bị IoT thực tế.
