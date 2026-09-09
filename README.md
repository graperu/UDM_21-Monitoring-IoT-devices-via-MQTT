# UDM_21: Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)

> **Mã Đề Tài:** UDM_21  
> **Tên Đề Tài:** Giám sát thiết bị IoT qua giao thức MQTT.  
> **Môn Học:** Lập Trình Mạng (Network Programming) - Học kỳ 2, Năm học 2025–2026.  
> **Nền Tảng Ứng Dụng:** C# .NET 8 WPF Desktop App (Dashboard GUI) & C# Multi-threaded Console App (Giả lập 5+ Thiết bị IoT).  
> **Số Lượng Thành Viên:** 5 Sinh viên.

---


# 👥 1. Danh sách Thành viên Nhóm & Phân công Công việc

| STT | Họ và tên | MSSV | Vai trò / Nhiệm vụ | Thư mục / File đảm nhận |
|-----|-----------|-----------|------------------|-------------------------|
| 1 | Vũ Ngọc Cát Chương | 056206003708 | Thiết kế và lập trình giao diện Dashboard GUI (WPF), xây dựng MainWindow.xaml, App.xaml, DataGrid hiển thị danh sách thiết bị, form gửi lệnh điều khiển và khung Console Log. | `Code/Dashboard/MainWindow.xaml`<br>`Code/Dashboard/App.xaml` |
| 2 | Huỳnh Nữ Huyền Trâm | 051305001244 | Lập trình Logic Dashboard, Event Handler, Controller và Dispatcher. Xử lý Subscribe MQTT, cập nhật dữ liệu thời gian thực lên giao diện, xây dựng Model và Data Binding. | `Code/Dashboard/Controllers/`<br>`Code/Dashboard/Models/`<br>`Code/Dashboard/MainWindow.xaml.cs` |
| 3 | Phan Văn Đình | 054205006039 | Trưởng nhóm. Lập trình hệ thống thiết bị IoT giả lập. Xây dựng DeviceBase, mô phỏng nhiều thiết bị IoT, vòng lặp Telemetry, Last Will & Testament (LWT) và xử lý Command. | `Code/Simulators/DeviceBase.cs`<br>`Code/Simulators/Devices/`<br>`Code/Simulators/Program.cs` |
| 4 | Huỳnh Gia Hợp | 054205008367 | Lập trình giao thức mạng cốt lõi. Xây dựng MQTT Wrapper bằng MQTTnet, xử lý Publish/Subscribe, QoS 0/1, Auto Reconnect, MQTT Logging, JSON Protocol và Command ACK. | `Code/Shared/MqttHelper.cs`<br>`Code/Shared/Protocol.cs`<br>`Code/Shared/Shared.csproj` |
| 5 | Lưu Đình Thuận | 075205010434 | Kiểm thử và báo cáo. Thực hiện Stress Test, đo độ trễ và hiệu năng hệ thống, tổng hợp kết quả kiểm thử, viết báo cáo và slide thuyết trình. | `Extra/scripts/stress_test.py`<br>`DOCX/`<br>`PPTX/` |

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
| **5** | **Lưu Đình Thuận** | `075205010434` | **Kiểm Thử, Đo Đạc Hiệu Năng & Báo Cáo:** Xây dựng `stress_test.py` và `test_runner.py`, đo Throughput/RTT ở hai mức tải, tổng hợp bằng chứng kiểm thử và chuẩn bị nội dung báo cáo. File Word, slide và video chính thức vẫn cần được nhóm hoàn thiện trước khi nộp. | `Extra/scripts/`<br>`Extra/test_results/`<br>`DOCX/`<br>`PPTX/` | **Đang hoàn thiện hồ sơ** |

---

## 📽️ 2. Link Video Demo Hệ Thống
* **URL Video Demo (YouTube / Google Drive Unlisted):** Chưa có. Nhóm phải thay dòng này bằng link video thật trước khi nộp.
* **Nội dung Demo:** Khởi động đồng thời 5 thiết bị, Dashboard nhận dữ liệu Real-time, phát hiện cảnh báo nhiệt độ > 40°C, gửi lệnh bật/tắt đèn và đổi độ sáng tức thì, test ngắt kết nối đột ngột kích hoạt LWT và tự động Reconnect.

---

## 📐 3. Tổng Quan Kiến Trúc & Thiết Kế Giao Thức Mạng

### 3.1. Mô hình Kiến trúc Publish / Subscribe qua MQTT Broker
Hệ thống tuân thủ mô hình mạng phân tán chuẩn, giao tiếp hoàn toàn qua giao thức TCP/IP MQTT (port cấu hình được; hỗ trợ kết nối thường và TLS), không gọi hàm nội bộ giữa các tiến trình:

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
        │                         MQTT Broker (TCP 1883 hoặc TLS theo cấu hình)                            │
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
   * Dashboard gửi `SUBSCRIBE` với Topic Wildcard `udm21_nhom01/+/+/+/telemetry` và `udm21_nhom01/+/+/+/status`.
   * Broker trả lời `SUBACK`.
4. **Kết thúc kết nối:**
   * *Ngắt chủ động:* Gửi gói `DISCONNECT` và đóng Socket an toàn.
   * *Ngắt đột ngột (rớt mạng, tắt nguồn):* Broker phát hiện quá hạn KeepAlive và thay mặt Client Publish bản tin LWT `status: offline` tới Dashboard.

---

## 📡 4. Thiết Kế Cấu Trúc Topic & Data Contract JSON

### 4.1. Cấu trúc Topic Phân cấp Chuẩn
Quy tắc phân cấp: `{topic_root}/{location}/{device_type}/{device_id}/{action_or_type}`. Giá trị mặc định `udm21_nhom01` tách dữ liệu của nhóm khỏi các topic `iot/...` công cộng và có thể đổi đồng thời trên Dashboard/Simulator.

| Loại Topic | Mẫu Topic Cụ Thể | QoS | Retain | Chức Năng |
|:---|:---|:---:|:---:|:---|
| **Telemetry** | `udm21_nhom01/lab/sensor/temp_hum_01/telemetry`<br>`udm21_nhom01/factory/sensor/air_quality_01/telemetry`<br>`udm21_nhom01/home/meter/power_meter_01/telemetry`<br>`udm21_nhom01/home/light/smart_light_01/telemetry`<br>`udm21_nhom01/lab/security/door_sensor_01/telemetry` | `0` | `false` | Thiết bị phát số liệu cảm biến định kỳ (2s - 5s). Dashboard subscribe bằng wildcard: `udm21_nhom01/+/+/+/telemetry`. |
| **Status (LWT)**| `udm21_nhom01/{location}/{device_type}/{device_id}/status` | `1` | `true` | Báo trạng thái `online` khi kết nối và `offline` khi ngắt mạng (sử dụng Retain Flag để Dashboard nhận ngay khi mở lên). |
| **Command** | `udm21_nhom01/{location}/{device_type}/{device_id}/cmd` | `1` | `false` | Dashboard gửi lệnh điều khiển (bật/tắt, chỉnh độ sáng) tới thiết bị đích. |

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
   * Không hard-code mật khẩu/private key trong mã nguồn; host, port, topic root, TLS và username cấu hình qua GUI/CLI. Simulator đọc password từ biến môi trường `UDM21_MQTT_PASSWORD` hoặc tên biến truyền qua `--password-env`.
   * Khi bật TLS, MQTTnet dùng kho chứng chỉ tin cậy của hệ điều hành và từ chối chứng chỉ broker không hợp lệ.
   * Toàn bộ luồng nền `Task.Delay` đều liên kết với `CancellationTokenSource` và giải phóng Socket/Client sạch sẽ khi ứng dụng dừng (`Dispose`/`DisconnectAsync`).
7. **Lịch sử bền vững:**
   * Dashboard lưu telemetry vào SQLite `Extra/data/telemetry.db`; khóa chính `message_id` loại bản ghi trùng cả sau khi khởi động lại.
   * GUI hiển thị 20 bản tin gần nhất và database tự giới hạn tối đa 10.000 bản tin cho mỗi thiết bị.

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
│   │   ├── Services/TelemetryHistoryManager.cs # SQLite, hiển thị 20 và lưu tối đa 10.000 bản tin/device
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
├── DOCX/                                  # Báo cáo Markdown; file .docx chưa được thêm
│   ├── BaoCao_CapNhat_HeThong.md
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
├── PPTX/                                  # File .pptx chính thức chưa được thêm
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
dotnet run --project Code/Simulators/Simulators.csproj -- --host broker.emqx.io --port 1883 --topic-root udm21_nhom01

# Terminal 2: Chạy Giao diện WPF Dashboard
dotnet run --project Code/Dashboard/Dashboard.csproj
```

> **Mẹo sử dụng Broker Nội Bộ (Local Broker):**
> Khởi động broker bằng `mosquitto -c Extra/mosquitto/mosquitto.local.conf -v`, nhập `localhost:1883` trên Dashboard rồi chạy Simulator với `--host localhost`.

### Chạy mỗi thiết bị trong một tiến trình riêng

Nhấp đúp `run-multiprocess.bat`, hoặc chạy riêng từng thiết bị:

```powershell
dotnet run --project Code/Simulators/Simulators.csproj -- --device temp_hum_01
dotnet run --project Code/Simulators/Simulators.csproj -- --device smart_light_01
```

### TLS và tài khoản MQTT

Dashboard có sẵn ô TLS, Username và Password. Simulator nhận cấu hình tương đương mà không đưa password vào command line:

```powershell
$env:UDM21_MQTT_PASSWORD = "mat-khau-demo"
dotnet run --project Code/Simulators/Simulators.csproj -- --host mqtt.example.com --port 8883 --tls --username demo --password-env UDM21_MQTT_PASSWORD
```

---

## 📊 9. Kết Quả Kiểm Thử Chức Năng & Stress Test Hiệu Năng

Hệ thống có script end-to-end `Extra/scripts/test_runner.py` kết nối trực tiếp với Public Broker `broker.emqx.io:1883`.

Ngoài ra, solution có project `Code/Tests` với **19 test tự động**. Các test kiểm tra dữ liệu sai, message trùng, message đến trễ, topic root, authentication, SQLite còn dữ liệu sau restart, LWT khi client mất đột ngột, phát hiện broker ngắt và tự động reconnect/re-subscribe:

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
| **Mức 1 (Tải nhẹ)** | 500 | 1 | **100% (500/500)** | 7.91 s | **63.22 msg/s** | **4,024.83 ms** | **7,413.53 ms** |
| **Mức 2 (Tải nặng)** | 2.000 | 1 | **100% (2000/2000)** | 27.65 s | **72.35 msg/s** | **14,397.31 ms** | **26,357.61 ms** |

Kết quả trên được đo ngày 07/09/2026 qua public broker với namespace cô lập `udm21_nhom01_test`. RTT cao ở mức tải nặng phản ánh cả thời gian chờ trong hàng đợi QoS 1, không phải chỉ độ trễ đường truyền. Cấu hình máy, phiên bản runtime, phương pháp đo và dữ liệu gốc nằm trong `Extra/test_results/test_report.json`.

---

## 🎯 10. Giới Hạn Sản Phẩm & Hướng Phát Triển

### Giới hạn sản phẩm:
* Dự án tập trung vào lập trình mạng tầng ứng dụng qua MQTT; các thiết bị phần cứng cảm biến được giả lập trên tiến trình phần mềm C# đa luồng thay vì mạch vi điều khiển ESP32/Arduino vật lý.
* SQLite giới hạn 10.000 bản tin gần nhất cho mỗi thiết bị; hệ thống chưa được thiết kế để lưu dữ liệu nhiều tháng hoặc phân tích time-series quy mô lớn.
* TLS và authentication phụ thuộc broker được chọn và thông tin tài khoản hợp lệ; cấu hình demo mặc định vẫn dùng MQTT không mã hóa với dữ liệu giả lập.
* Hồ sơ nộp môn hiện chưa có file Word `.docx`, slide `.pptx` và link video demo thật.

### Hướng phát triển tương lai:
* Tích hợp cơ sở dữ liệu thời gian thực (InfluxDB / TimescaleDB) để vẽ biểu đồ đường (Line Chart) trực quan hóa xu hướng nhiệt độ và công suất tiêu thụ theo thời gian.
* Bổ sung mutual TLS với chứng chỉ client và cơ chế phân quyền ACL riêng cho từng thiết bị.
* Phát triển ứng dụng Mobile (.NET MAUI) để quản trị viên có thể nhận thông báo đẩy (Push Notification) khi có cảnh báo cháy hoặc rò rỉ khí độc từ xa.
