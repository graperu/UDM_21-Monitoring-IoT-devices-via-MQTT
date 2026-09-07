# UDM_21: Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)

> **Mã Đề Tài:** UDM_21  
> **Tên Đề Tài:** Giám sát thiết bị IoT qua giao thức MQTT.  
> **Môn Học:** Lập Trình Mạng (Network Programming) - Học kỳ 2, Năm học 2025–2026.  
> **Nền Tảng Ứng Dụng:** C# .NET 8 WPF Desktop App (Dashboard GUI) & C# Multi-threaded Console App (Giả lập 5+ Thiết bị IoT).  
> **Số Lượng Thành Viên:** 5 Sinh viên.

---

## 📑 MỤC LỤC
1. [Danh sách Thành viên Nhóm & Phân công Công việc](#-1-danh-sách-thành-viên-nhóm--phân-công-công-việc)
2. [Link Video Demo Hệ Thống](#-2-link-video-demo-hệ-thống)
3. [Tổng Quan Kiến Trúc & Thiết Kế Giao Thức Mạng](#-3-tổng-quan-kiến-trúc--thiết-kế-giao-thức-mạng)
4. [Thiết Kế Cấu Trúc Topic & Data Contract JSON](#-4-thiết-kế-cấu-trúc-topic--data-contract-json)
5. [Giải Thích Các Khái Niệm Mạng Cốt Lõi (Lập Trình Mạng)](#-5-giải-thích-các-khái-niệm-mạng-cốt-lõi-lập-trình-mạng)
6. [Xử Lý Lỗi, Độ Tin Cậy & Bảo Mật](#-6-xử-lý-lỗi-độ-tin-cậy--bảo-mật)
7. [Cấu Trúc Thư Mục Repository Chuẩn](#-7-cấu-trúc-thư-mục-repository-chuẩn)
8. [Hướng Dẫn Cài Đặt & Khởi Chạy Dự Án](#-8-hướng-dẫn-cài-đặt--khởi-chạy-dự-án)
9. [Kết Quả Kiểm Thử Chức Năng & Stress Test Hiệu Năng](#-9-kết-quả-kiểm-thử-chức-năng--stress-test-hiệu-năng)
10. [Giới Hạn Sản Phẩm & Hướng Phát Triển](#-10-giới-hạn-sản-phẩm--hướng-phát-triển)

---

## 👥 1. Danh sách Thành viên Nhóm & Phân công Công việc

| STT | Họ và Tên | MSSV | Vai Trò / Trách Nhiệm Chi Tiết | Thư Mục / File Đảm Nhận | Mức Độ Hoàn Thành |
|:---:|:---|:---:|:---|:---|:---:|
| **1** | **Vũ Ngọc Cát Chương** | `056206003708` | **Thiết kế & Lập trình Giao diện (WPF GUI):** Xây dựng `MainWindow.xaml`, `App.xaml`, thiết kế bố cục DataGrid, Box gửi lệnh điều khiển, Log Console tùy biến thanh cuộn, cửa sổ hiển thị lịch sử 20 bản tin đo gần nhất (`TelemetryHistoryWindow.xaml`). | `Code/Dashboard/MainWindow.xaml`<br>`Code/Dashboard/App.xaml`<br>`Code/Dashboard/TelemetryHistoryWindow.xaml` | **100% (Hoàn thành)** |
| **2** | **Huỳnh Nữ Huyền Trâm** | `051305001244` | **Lập trình Logic Dashboard & Controller:** Viết `MqttController.cs`, xử lý `Dispatcher.BeginInvoke` để cập nhật UI bất đồng bộ, triển khai `INotifyPropertyChanged` trong `DeviceItem.cs`, xử lý sự kiện gửi lệnh và tích hợp `TelemetryHistoryManager`. | `Code/Dashboard/Controllers/`<br>`Code/Dashboard/Models/`<br>`Code/Dashboard/MainWindow.xaml.cs` | **100% (Hoàn thành)** |
| **3** | **Phan Văn Đỉnh** | `054205003603` | **Trưởng nhóm - Lập trình Thiết bị Giả lập IoT:** Xây dựng `DeviceBase.cs` đa luồng (`Task.Delay` + `CancellationToken`), lập trình 5 thiết bị giả lập cảm biến độc lập, cấu hình LWT Online/Offline, chuẩn hóa `message_id` GUID và `timestamp` ISO-8601 UTC. | `Code/Simulators/DeviceBase.cs`<br>`Code/Simulators/Devices/`<br>`Code/Simulators/Program.cs` | **100% (Hoàn thành)** |
| **4** | **Huỳnh Gia Hợp** | `054205008367` | **Giao thức Mạng Cốt lõi & Reliability:** Xây dựng `MqttHelper.cs` (Wrapper MQTTnet v4), cấu hình QoS 0/1, Auto-reconnect Exponential Backoff (2s → 30s), định nghĩa JSON Data Contract trong `Protocol.cs`, cài đặt bộ lọc Message Trùng (Deduplication) và Đến Trễ (Out-of-order). | `Code/Shared/MqttHelper.cs`<br>`Code/Shared/Protocol.cs`<br>`Code/Shared/Shared.csproj` | **100% (Hoàn thành)** |
| **5** | **Lưu Đình Thuận** | `075205010434` | **Kiểm Thử, Đo Đạc Hiệu Năng & Báo Cáo:** Xây dựng `stress_test.py` và `test_runner.py`, đo đạc Throughput và Latency ở 2 mức tải (500 msgs và 2.000 msgs), tổng hợp dữ liệu kiểm thử, biên soạn tài liệu Báo cáo Word (`DOCX/`) và Slide bảo vệ (`PPTX/`). | `Extra/scripts/`<br>`Extra/test_results/`<br>`DOCX/`<br>`PPTX/` | **100% (Hoàn thành)** |

---

## 📽️ 2. Link Video Demo Hệ Thống
* **URL Video Demo (YouTube / Google Drive Unlisted):** [https://youtu.be/udm21-demo-iot-mqtt](https://youtube.com/) *(Đang cập nhật link video thuyết minh)*
* **Nội dung Demo:** Khởi động đồng thời 5 thiết bị, Dashboard nhận dữ liệu Real-time, phát hiện cảnh báo nhiệt độ > 40°C, gửi lệnh bật/tắt đèn và đổi độ sáng tức thì, test ngắt kết nối đột ngột kích hoạt LWT và tự động Reconnect.

---

## 📐 3. Tổng Quan Kiến Trúc & Thiết Kế Giao Thức Mạng

### 3.1. Mô hình Kiến trúc Publish / Subscribe qua MQTT Broker
Hệ thống tuân thủ mô hình mạng phân tán chuẩn, giao tiếp hoàn toàn qua giao thức TCP/IP MQTT (Port 1883), không gọi hàm nội bộ giữa các tiến trình:

```text
┌─────────────────────────────────────────┐                     ┌─────────────────────────────────────────┐
│         IoT Simulators (5 Thiết Bị)     │                     │          WPF Desktop Dashboard          │
│            (C# .NET 8 Console)          │                     │            (C# .NET 8 Desktop)          │
│                                         │                     │                                         │
│  • temp_hum_01       (Nhiệt độ/Độ ẩm)   │                     │  • Bảng DataGrid hiển thị Real-time     │
│  • air_quality_01    (Chất lượng KK)    │                     │  • Đèn LED chỉ thị: Online/Warn/Offline │
│  • power_meter_01    (Công tơ điện)     │                     │  • Form phát lệnh điều khiển thiết bị   │
│  • smart_light_01    (Đèn thông minh)   │                     │  • Console Log + Popup Lịch sử đo       │
│  • door_sensor_01    (Cảm biến cửa)     │                     │  • Bộ lọc trùng & chống treo GUI        │
└──────────────────┬──────────────────────┘                     └──────────────────▲──────────────────────┘
                   │                                                               │
                   │  1. Publish Telemetry (QoS 0)                                 │  3. Push Telemetry (QoS 0)
                   │  2. Publish Status LWT (QoS 1, Retain)                        │  4. Push Status (QoS 1, Retain)
                   │  5. Subscribe Command (QoS 1)                                 │  6. Publish Command (QoS 1)
                   │                                                               │
                   ▼                                                               │
        ┌──────────────────────────────────────────────────────────────────────────┴──────────────────────┐
        │                                  MQTT Broker (Port 1883)                                        │
        │                        (Public: broker.emqx.io | Local: Eclipse Mosquitto)                      │
        │                                                                                                 │
        │  • Quản lý Client Sessions, Routing bản tin theo Topic Tree và Wildcard                         │
        │  • Lưu trữ Retained Message trạng thái Online/Offline cho thiết bị                              │
        │  • Tự động kích hoạt Last Will & Testament (LWT) khi thiết bị mất kết nối đột ngột              │
        └─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2. Quy trình Kết nối, Duy trì và Ngắt kết nối Mạng
1. **Thiết lập kết nối (Handshake):**
   * Client gửi gói `CONNECT` kèm Unique Client ID (`sim_xxx` hoặc `WpfDashboard_xxx`), `CleanSession = true`, `KeepAlive = 30s` và thông số LWT Payload (`status: offline`).
   * Broker phản hồi gói `CONNACK (Return Code 0: Connection Accepted)`.
2. **Duy trì kết nối (Heartbeat):**
   * Định kỳ gửi gói `PINGREQ` và nhận `PINGRESP` mỗi 30 giây để duy trì Socket TCP.
3. **Đăng ký nhận tin (Subscription):**
   * Dashboard gửi `SUBSCRIBE` với Topic Wildcard `iot/+/+/+/telemetry` và `iot/+/+/+/status`.
   * Broker trả lời `SUBACK`.
4. **Kết thúc kết nối:**
   * *Ngắt chủ động:* Gửi gói `DISCONNECT` và đóng Socket an toàn.
   * *Ngắt đột ngột (rớt mạng, tắt nguồn):* Broker phát hiện quá hạn KeepAlive và thay mặt Client Publish bản tin LWT `status: offline` tới Dashboard.

---

## 📡 4. Thiết Kế Cấu Trúc Topic & Data Contract JSON

### 4.1. Cấu trúc Topic Phân cấp Chuẩn
Quy tắc phân cấp: `iot/{location}/{device_type}/{device_id}/{action_or_type}`

| Loại Topic | Mẫu Topic Cụ Thể | QoS | Retain | Chức Năng |
|:---|:---|:---:|:---:|:---|
| **Telemetry** | `iot/lab/sensor/temp_hum_01/telemetry`<br>`iot/factory/sensor/air_quality_01/telemetry`<br>`iot/home/meter/power_meter_01/telemetry`<br>`iot/home/light/smart_light_01/telemetry`<br>`iot/lab/security/door_sensor_01/telemetry` | `0` | `false` | Thiết bị phát số liệu cảm biến định kỳ (2s - 5s). Dashboard subscribe bằng wildcard: `iot/+/+/+/telemetry`. |
| **Status (LWT)**| `iot/{location}/{device_type}/{device_id}/status` | `1` | `true` | Báo trạng thái `online` khi kết nối và `offline` khi ngắt mạng (sử dụng Retain Flag để Dashboard nhận ngay khi mở lên). |
| **Command** | `iot/{location}/{device_type}/{device_id}/cmd` | `1` | `false` | Dashboard gửi lệnh điều khiển (bật/tắt, chỉnh độ sáng) tới thiết bị đích. |

---

### 4.2. Định dạng JSON Data Contract

#### 1. Dữ liệu Cảm biến (`TelemetryMessage`)
```json
{
  "message_id": "e85ff714-b6ad-4c16-8646-d651b8c36a1a",
  "device_id": "temp_hum_01",
  "device_type": "sensor",
  "location": "lab",
  "timestamp": "2026-09-06T07:13:16.8260620Z",
  "data": {
    "temperature": 29.18,
    "humidity": 64.60,
    "unit_temp": "°C",
    "unit_hum": "%"
  }
}
```

#### 2. Trạng thái Thiết bị (`DeviceStatusMessage`)
```json
{
  "message_id": "54a39df7-2ef6-4a11-b472-358fe2467d30",
  "device_id": "temp_hum_01",
  "status": "online",
  "timestamp": "2026-09-06T07:12:44.1120000Z"
}
```

#### 3. Lệnh Điều Khiển (`CommandMessage`)
```json
{
  "message_id": "44980dd6-e7f3-46cd-8c2d-efffdf358bc4",
  "command": "TOGGLE_POWER",
  "timestamp": "2026-09-06T07:13:15.3548768Z",
  "params": {
    "state": "ON",
    "brightness": 80
  }
}
```

---

## 📚 5. Giải Thích Các Khái Niệm Mạng Cốt Lõi (Lập Trình Mạng)

* **Quality of Service (QoS):**
  * **QoS 0 (At most once - Bắn và quên):** Áp dụng cho dữ liệu cảm biến gửi liên tục (nhiệt độ, công suất). Ưu tiên tốc độ cao, độ trễ thấp; nếu mất 1 gói tin thì gói tin chu kỳ sau sẽ bù đắp ngay lập tức.
  * **QoS 1 (At least once - Có xác nhận PUBACK):** Áp dụng cho bản tin trạng thái (Online/Offline) và lệnh điều khiển thiết bị (Bật/Tắt đèn). Đảm bảo tin nhắn bắt buộc phải đến đích thành công.
* **Retained Message:**
  * Được áp dụng cho Topic `status`. Broker lưu trữ lại bản tin trạng thái cuối cùng. Khi Dashboard khởi động sau thiết bị và vừa Subscribe vào Topic, Broker sẽ gửi lại ngay trạng thái mới nhất mà không cần chờ thiết bị phát tin mới.
* **Last Will and Testament (LWT):**
  * Khi thiết bị gửi gói `CONNECT`, nó đính kèm sẵn một "di chúc" `{"status": "offline"}`. Nếu thiết bị bị đứt cáp mạng hoặc mất nguồn đột ngột, Broker sẽ tự động phát bản tin LWT này thay cho thiết bị để báo cho Dashboard biết thiết bị đã Offline.
* **Clean Session vs Persistent Session:**
  * Simulators sử dụng `CleanSession = true` để kết nối nhanh và dọn dẹp hàng đợi cũ. Dashboard hỗ trợ tự động Re-subscribe lại toàn bộ Topic sau khi có mạng lại.

---

## 🛡️ 6. Xử Lý Lỗi, Độ Tin Cậy & Bảo Mật

1. **Chống treo giao diện người dùng (WPF UI Thread Safety):**
   * Mọi sự kiện nhận tin từ luồng nền MQTTnet đều được đưa bất đồng bộ vào UI Thread thông qua `Dispatcher.BeginInvoke`, tránh chặn luồng nhận MQTT khi GUI đang bận.
2. **Bộ lọc Message Trùng (Deduplication):**
   * Sử dụng `HashSet<string>` kết hợp `Queue<string>` lưu trữ 100 `message_id` gần nhất trong bộ nhớ. Bỏ qua các bản tin trùng lặp do cơ chế truyền lại của QoS 1.
3. **Bộ lọc Message Đến Trễ (Out-of-order Message Filtering):**
   * So sánh trường `timestamp` (UTC) của bản tin nhận được với `lastProcessedTimestamp` của thiết bị. Nếu nhận được tin nhắn cũ đến sau do độ trễ mạng thì không ghi đè dữ liệu Real-time.
4. **Cơ chế Tự động Kết nối lại (Auto-reconnect với Exponential Backoff):**
   * Khi mất kết nối ngoài ý muốn (Broker sập hoặc rớt mạng), `MqttHelper` kích hoạt vòng lặp thử lại với thời gian tăng dần: $2s \rightarrow 4s \rightarrow 8s \rightarrow 16s \rightarrow 30s$ nhằm tránh nghẽn mạng.
5. **Cảnh báo Ngưỡng Bất Thường (Threshold Alerting):**
   * Dashboard tự động phát hiện các chỉ số vượt ngưỡng an toàn:
     * Nhiệt độ $> 40^\circ\text{C}$ $\rightarrow$ Báo động quá nhiệt.
     * Công suất $> 3000\text{W}$ $\rightarrow$ Báo động quá tải điện.
     * Chỉ số AQI $> 150$ $\rightarrow$ Báo động chất lượng không khí nguy hại.
   * Đổi màu đèn LED chỉ thị sang màu **Đỏ cam (`OrangeRed`)** và xuất cảnh báo trên Console Log.
6. **Bảo mật & Quản lý Tài nguyên:**
   * Không hard-code mật khẩu/private key trong mã nguồn; tham số kết nối IP/Port hoàn toàn cấu hình động qua GUI và CLI.
   * Toàn bộ luồng nền `Task.Delay` đều liên kết với `CancellationTokenSource` và giải phóng Socket/Client sạch sẽ khi ứng dụng dừng (`Dispose`/`DisconnectAsync`).

---

## 📁 7. Cấu Trúc Thư Mục Repository Chuẩn

```text
UDM_21-Monitoring-IoT-devices-via-MQTT/
├── .vscode/                               # Cấu hình F5 Debug & Build tự động cho Visual Studio Code
│   ├── launch.json                        # Cấu hình Debug đồng thời Dashboard & Simulators
│   ├── tasks.json                         # Tasks tự động dotnet build
│   └── settings.json
├── Code/                                  # Mã nguồn chính (.NET 8 C#)
│   ├── UDM_21.sln                         # Solution chứa 3 Projects
│   ├── Dashboard/                         # WPF Desktop Dashboard Application
│   │   ├── Controllers/MqttController.cs  # Điều phối MQTTnet, Deduplication & Dispatcher
│   │   ├── Models/DeviceItem.cs           # Model implement INotifyPropertyChanged
│   │   ├── Services/TelemetryHistoryManager.cs # Quản lý 20 bản tin lịch sử gần nhất
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MainWindow.xaml / .cs          # Giao diện chính (DataGrid, Form lệnh, Console)
│   │   └── TelemetryHistoryWindow.xaml/.cs # Cửa sổ xem lịch sử chi tiết 20 bản ghi
│   ├── Simulators/                        # C# Console App Giả lập 5+ Thiết bị IoT
│   │   ├── Devices/                       # 5 loại thiết bị cảm biến độc lập
│   │   │   ├── TempHumidityDevice.cs      # Nhiệt độ, độ ẩm (giả lập tăng vọt > 40°C)
│   │   │   ├── AirQualityDevice.cs        # AQI, CO2 ppm
│   │   │   ├── PowerMeterDevice.cs        # Điện áp, dòng điện, công suất, tổng kWh
│   │   │   ├── SmartLightDevice.cs        # Đèn thông minh (Bật/Tắt, độ sáng, phản hồi tức thì)
│   │   │   └── DoorSensorDevice.cs        # Cảm biến cửa, mức pin
│   │   ├── DeviceBase.cs                  # Lớp trừu tượng đa luồng, LWT, Telemetry Loop
│   │   └── Program.cs                     # Điểm khởi chạy 5 thiết bị
│   └── Shared/                            # Class Library dùng chung
│       ├── MqttHelper.cs                  # Wrapper MQTTnet v4, QoS 0/1, Auto-reconnect Backoff
│       └── Protocol.cs                    # JSON Data Contract (Telemetry, Status, Command)
├── DOCX/                                  # Báo cáo đồ án chính thức
│   ├── Bao_Cao_UDM_21_Giam_Sat_IoT_MQTT.docx
│   └── README.md
├── Extra/                                 # Tài liệu kiểm thử & công cụ bổ trợ
│   ├── logs/                              # Nhật ký chạy hệ thống
│   ├── scripts/                           # Script Python kiểm thử
│   │   ├── test_runner.py                 # Script test tự động toàn diện 5 Test Cases
│   │   ├── stress_test.py                 # Script đo Throughput & Latency 2 mức tải
│   │   └── create_github_issues.py        # Script tự động đồng bộ 19 Issues lên GitHub
│   └── test_results/                      # Kết quả đo đạc kiểm thử
│       ├── test_report.json               # Dữ liệu kiểm thử JSON
│       └── Bao_Cao_Kiem_Thu_He_Thong.md   # Báo cáo kiểm thử chi tiết Markdown
├── PPTX/                                  # Slide thuyết trình bảo vệ đồ án
│   ├── Slide_Thuyet_Trinh_UDM_21.pptx
│   └── README.md
├── run.bat                                # Script khởi chạy 1-Click toàn bộ hệ thống
├── .gitignore                             # Loại trừ build artifacts (bin, obj, .vs)
└── README.md                              # Tài liệu hướng dẫn & Báo cáo tổng quan dự án
```

---

## 🚀 8. Hướng Dẫn Cài Đặt & Khởi Chạy Dự Án

### Yêu cầu môi trường:
* Hệ điều hành: Windows 10/11 (64-bit).
* **.NET SDK 8.0 trở lên** (Tải tại [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)).
* Visual Studio 2022 (với Workload *.NET Desktop Development*) hoặc Visual Studio Code (với Extension *C# Dev Kit*).
* Python 3.8+ (để chạy script kiểm thử hiệu năng).

---

### Cách 1: Khởi chạy 1-Click bằng file `run.bat` (Khuyên dùng)
1. Mở thư mục dự án trên máy.
2. Nhấp đúp chuột vào file **`run.bat`**.
3. Hệ thống sẽ tự động khởi động đồng thời:
   * **Cửa sổ 1:** 5 Thiết bị giả lập IoT (kết nối `broker.emqx.io:1883`).
   * **Cửa sổ 2:** Giao diện WPF Dashboard tự động kết nối và nhận dữ liệu thời gian thực.

---

### Cách 2: Sử dụng .NET CLI từ Terminal
Mở 2 cửa sổ Terminal độc lập tại thư mục gốc của dự án:

```bash
# Terminal 1: Chạy 5 Thiết bị giả lập IoT
dotnet run --project Code/Simulators/Simulators.csproj broker.emqx.io 1883

# Terminal 2: Chạy Giao diện WPF Dashboard
dotnet run --project Code/Dashboard/Dashboard.csproj
```

> **Mẹo sử dụng Broker Nội Bộ (Local Broker):**
> Nếu muốn chạy hoàn toàn Offline bằng Mosquitto nội bộ, bạn chỉ cần gõ:
> `dotnet run --project Code/Simulators/Simulators.csproj localhost 1883`

---

## 📊 9. Kết Quả Kiểm Thử Chức Năng & Stress Test Hiệu Năng

Hệ thống có script end-to-end `Extra/scripts/test_runner.py` kết nối trực tiếp với Public Broker `broker.emqx.io:1883`.

Ngoài ra, solution có project `Code/Tests` với **13 test tự động**. Các integration test dựng MQTT broker cục bộ bằng MQTTnet để kiểm tra dữ liệu sai, message trùng, message đến trễ, LWT khi client mất đột ngột, phát hiện broker ngắt và tự động reconnect/re-subscribe:

```bash
dotnet test Code/UDM_21.sln
```

### 9.1. Bảng Tổng Hợp Kiểm Thử Chức Năng (Functional Test)

| Mã TC | Test Case | Tiêu Chuẩn Đánh Giá | Kết Quả Thực Tế | Trạng Thái |
|:---:|:---|:---|:---|:---:|
| **TC-01** | **Khởi tạo 5 Thiết bị Online** | 5/5 thiết bị gửi trạng thái `online` có cờ `Retain = true` và thiết lập LWT khi kết nối. | Nhận đủ 5 trạng thái Online từ `temp_hum_01`, `air_quality_01`, `power_meter_01`, `smart_light_01`, `door_sensor_01`. | **✅ PASS** |
| **TC-02** | **Chuẩn hóa JSON Protocol** | Mỗi bản tin dữ liệu cảm biến phải có `message_id` (GUID duy nhất) và `timestamp` (UTC ISO-8601). | 100% bản tin nhận về đều có GUID hợp lệ và thời gian UTC chuẩn xác. | **✅ PASS** |
| **TC-03** | **Phát Lệnh & Phản Hồi Real-time** | Gửi lệnh `TOGGLE_POWER` (`state: ON`) tới đèn thông minh `smart_light_01`. | Thiết bị `smart_light_01` nhận lệnh, đổi trạng thái sang `ON`, công suất `12W` và phát ngay Telemetry phản hồi tức thì. | **✅ PASS** |
| **TC-04** | **Cảnh Báo Vượt Ngưỡng** | `temp_hum_01` định kỳ phát giá trị nhiệt độ đột biến > 40°C để kích hoạt cảnh báo. | Bắt thành công **2 lần** nhiệt độ cao bất thường ($52.14^\circ\text{C}$ và $51.89^\circ\text{C}$) kích hoạt đèn cảnh báo đỏ cam. | **✅ PASS** |
| **TC-05** | **Stress Test 2 Mức Tải (QoS 1)** | Gửi 500 msgs (Tải nhẹ) & 2.000 msgs (Tải nặng), đo throughput và RTT PUBACK của từng message. | Tỷ lệ xác nhận gói tin đạt **100.0%**; test tự đánh dấu FAIL nếu thiếu ACK hoặc timeout. | **✅ PASS** |

---

### 9.2. Đo Đạc Hiệu Năng & Thông Lượng (Performance Benchmark)

| Mức Tải | Số Lượng Message | QoS | Tỷ Lệ ACK | Tổng Thời Gian | Throughput | RTT trung bình | RTT P95 |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Mức 1 (Tải nhẹ)** | 500 | 1 | **100% (500/500)** | 6.29 s | **79.46 msg/s** | **3,219.04 ms** | **5,898.41 ms** |
| **Mức 2 (Tải nặng)** | 2.000 | 1 | **100% (2000/2000)** | 26.02 s | **76.88 msg/s** | **12,953.73 ms** | **24,512.80 ms** |

Kết quả trên được đo ngày 06/09/2026 qua public broker. RTT cao ở mức tải nặng phản ánh cả thời gian chờ trong hàng đợi QoS 1, không phải chỉ độ trễ đường truyền. Cấu hình máy, phiên bản runtime, phương pháp đo và dữ liệu gốc nằm trong `Extra/test_results/stress_report.json`.

---

## 🎯 10. Giới Hạn Sản Phẩm & Hướng Phát Triển

### Giới hạn sản phẩm:
* Dự án tập trung vào lập trình mạng tầng ứng dụng qua MQTT; các thiết bị phần cứng cảm biến được giả lập trên tiến trình phần mềm C# đa luồng thay vì mạch vi điều khiển ESP32/Arduino vật lý.
* Dữ liệu lịch sử 20 bản tin đo gần nhất được lưu trữ tạm thời trong bộ nhớ RAM (`TelemetryHistoryManager`), chưa tích hợp cơ sở dữ liệu quan hệ (SQL/NoSQL) để lưu trữ dài hạn nhiều tháng.

### Hướng phát triển tương lai:
* Tích hợp cơ sở dữ liệu thời gian thực (InfluxDB / TimescaleDB) để vẽ biểu đồ đường (Line Chart) trực quan hóa xu hướng nhiệt độ và công suất tiêu thụ theo thời gian.
* Tích hợp giao thức bảo mật Transport Layer Security (MQTT qua TLS/SSL Port 8883) với chứng chỉ số X.509 để mã hóa gói tin trên đường truyền.
* Phát triển ứng dụng Mobile (.NET MAUI) để quản trị viên có thể nhận thông báo đẩy (Push Notification) khi có cảnh báo cháy hoặc rò rỉ khí độc từ xa.
