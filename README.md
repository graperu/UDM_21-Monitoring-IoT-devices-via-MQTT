# UDM_21: Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)

> **Rà soát ngày 15/09/2026:** Xem [báo cáo sửa lỗi và đối chiếu đề bài](DOCX/RaSoat_ToiUu_UDM21.md).
> Bản chỉnh sửa này chưa được build/chạy WPF hoặc xUnit trong môi trường rà soát (không có .NET SDK).
> Các số liệu PASS/benchmark bên dưới là thông tin của bản gốc, không xác nhận cho bản hiện tại.


> **Mã Đề Tài:** UDM_21  
> **Tên Đề Tài:** Giám sát thiết bị IoT qua giao thức MQTT.  
> **Môn Học:** Lập Trình Mạng (Network Programming) - Học kỳ 2, Năm học 2025–2026.  
> **Nền Tảng:** C# .NET 8 WPF Desktop App (Dashboard GUI) & C# Multi-threaded Console App (Giả lập 5 Thiết bị IoT).  
> **Số Lượng Thành Viên:** 5 Sinh viên.

---

## 📑 MỤC LỤC
1. [Danh sách Thành viên Nhóm & Phân công Công việc](#-1-danh-sách-thành-viên-nhóm--phân-công-công-việc)
2. [Tổng Quan Kiến Trúc & Thiết Kế Giao Thức Mạng](#-2-tổng-quan-kiến-trúc--thiết-kế-giao-thức-mạng)
3. [Thiết Kế Cấu Trúc Topic & Data Contract JSON](#-3-thiết-kế-cấu-trúc-topic--data-contract-json)
4. [Các Điểm Nổi Bật Về Giao Diện & Trải Nghiệm Người Dùng (UI/UX)](#-4-các-điểm-nổi-bật-về-giao-diện--trải-nghiệm-người-dùng-uiux)
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
| **1** | **Vũ Ngọc Cát Chương** | `056206003708` | **Thiết kế & Lập trình Giao diện (WPF GUI):** Xây dựng `MainWindow.xaml`, `App.xaml`, thiết kế bố cục 3 phân vùng hiện đại, hệ thống thẻ trực quan 5 thiết bị, bảng điều khiển lệnh nhanh và cửa sổ lịch sử đo SQLite (`TelemetryHistoryWindow.xaml`). | `Code/Dashboard/MainWindow.xaml`<br>`Code/Dashboard/App.xaml`<br>`Code/Dashboard/Resources/` | **100% (Hoàn thành)** |
| **2** | **Huỳnh Nữ Huyền Trâm** | `051305001244` | **Lập trình Logic Dashboard & Controller:** Viết `MqttController.cs`, xử lý luồng Dispatcher cập nhật an toàn lên UI, triển khai mô hình dữ liệu `DeviceItem`, `TelemetryHistoryDisplayItem` và bộ lọc lịch sử đo SQLite. | `Code/Dashboard/controllers/`<br>`Code/Dashboard/models/`<br>`Code/Dashboard/services/` | **100% (Hoàn thành)** |
| **3** | **Phan Văn Đỉnh** | `054205006039` | **Trưởng nhóm - Lập trình Thiết bị Giả lập IoT:** Xây dựng `DeviceBase.cs` đa luồng (`Task.Delay` + `CancellationToken`), lập trình 5 thiết bị giả lập cảm biến độc lập, cấu hình LWT Online/Offline, chuẩn hóa `message_id` GUID và `timestamp` ISO-8601 UTC. | `Code/Simulators/DeviceBase.cs`<br>`Code/Simulators/devices/`<br>`Code/Simulators/Program.cs` | **100% (Hoàn thành)** |
| **4** | **Huỳnh Gia Hợp** | `054205008367` | **Giao thức Mạng Cốt lõi & Reliability:** Xây dựng `MqttHelper.cs` (Wrapper MQTTnet v4), cấu hình QoS 0/1, Auto-reconnect Exponential Backoff (2s → 30s), định nghĩa JSON Data Contract trong `Protocol.cs`, cài đặt bộ lọc Message Trùng (Deduplication) và Đến Trễ (Out-of-order). | `Code/Shared/MqttHelper.cs`<br>`Code/Shared/Protocol.cs`<br>`Code/Shared/Shared.csproj` | **100% (Hoàn thành)** |
| **5** | **Lưu Đình Thuận** | `075205010434` | **Kiểm Thử Tự Động, Stress Test & Báo Cáo:** Xây dựng bộ kiểm thử xUnit (32 bài test tự động), viết kịch bản `stress_test.py` đo Throughput/RTT qua 2 mức tải, tổng hợp bằng chứng kiểm thử, hoàn thiện báo cáo Word và Slide thuyết trình. | `Code/Tests/`<br>`Extra/scripts/`<br>`DOCX/` & `PPTX/` | **100% (Hoàn thành)** |

---

## 📐 2. Tổng Quan Kiến Trúc & Thiết Kế Giao Thức Mạng

### 2.1. Mô hình Kiến trúc Publish / Subscribe qua MQTT Broker
Hệ thống tuân thủ kiến trúc mạng phân tán chuẩn, giao tiếp hoàn toàn qua giao thức TCP/IP MQTT (hỗ trợ cả broker nội bộ Mosquitto và public broker EMQX, hỗ trợ xác thực tài khoản và mã hóa TLS):

```text
┌─────────────────────────────────────────┐                     ┌─────────────────────────────────────────┐
│         IoT Simulators (5 Thiết Bị)     │                     │          WPF Desktop Dashboard          │
│            (C# .NET 8 Console)          │                     │            (C# .NET 8 Desktop)          │
│                                         │                     │                                         │
│  • temp_hum_01       (Nhiệt độ/Độ ẩm)   │                     │  • Thanh trạng thái kết nối & Broker    │
│  • air_quality_01    (Chất lượng KK)    │                     │  • Bảng 5 thẻ thiết bị trực quan        │
│  • power_meter_01    (Công tơ điện)     │                     │  • Cụm điều khiển lệnh & đo lường nhanh │
│  • smart_light_01    (Đèn thông minh)   │                     │  • Tab Lịch sử SQLite lọc theo thiết bị │
│  • door_sensor_01    (Cảm biến cửa)     │                     │  • Bộ lọc trùng tin & Out-of-order      │
└──────────────────┬──────────────────────┘                     └──────────────────▲──────────────────────┘
                   │                                                               │
                   │  1. Publish Telemetry (QoS 0)                                 │  3. Push Telemetry (QoS 0)
                   │  2. Publish Status LWT (QoS 1, Retain)                        │  4. Push Status (QoS 1, Retain)
                   │  5. Subscribe Command (QoS 1)                                 │  6. Publish Command (QoS 1)
                   │                                                               │
                   ▼                                                               │
        ┌──────────────────────────────────────────────────────────────────────────┴──────────────────────┐
        │                         MQTT Broker (TCP 1883 hoặc TLS 8883)                                    │
        │                        (Public: broker.emqx.io | Local: Eclipse Mosquitto)                      │
        │                                                                                                 │
        │  • Quản lý Client Sessions, Routing bản tin theo Topic Tree và Wildcard                         │
        │  • Lưu trữ Retained Message trạng thái Online/Offline cho thiết bị                              │
        │  • Tự động kích hoạt Last Will & Testament (LWT) khi thiết bị mất kết nối đột ngột              │
        └─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.2. Danh sách 5 Thiết Bị Giả Lập IoT

| Mã Thiết Bị | Loại Thiết Bị | Vị Trí | Dữ Liệu Đo Lường Chính (Telemetry) | Lệnh Điều Khiển (Command) |
|:---|:---|:---|:---|:---|
| **`smart_light_01`** | Đèn chiếu sáng | `home` | Trạng thái (ON/OFF), Độ sáng (0–100%), Công suất (W) | `TOGGLE_POWER`, `SET_BRIGHTNESS` |
| **`door_sensor_01`** | An ninh cửa | `lab` | Trạng thái cửa (OPEN/CLOSED), Pin (%), Cảnh báo cạy cửa | `TRIGGER_ALARM`, `CLEAR_ALARM` |
| **`temp_hum_01`** | Môi trường khí hậu | `lab` | Nhiệt độ (°C), Độ ẩm (%) | `SET_TEMPERATURE`, `TRIGGER_HEAT_ALERT`, `CALIBRATE` |
| **`air_quality_01`** | Chất lượng không khí | `factory` | Chỉ số AQI, Nồng độ CO₂ (ppm) | `SET_AQI`, `PURIFY_AIR`, `TRIGGER_POLLUTION_ALERT` |
| **`power_meter_01`** | Đo lường điện năng | `home` | Điện áp (V), Dòng điện (A), Công suất (W), Tổng kWh | `SET_LOAD`, `TRIGGER_OVERLOAD`, `RESET_KWH` |

---

## 📡 3. Thiết Kế Cấu Trúc Topic & Data Contract JSON

### 3.1. Cấu trúc Topic Phân cấp Chuẩn
Quy tắc phân cấp: `{topic_root}/{location}/{device_type}/{device_id}/{action_or_type}`  
*(Mặc định `topic_root` là `udm21_nhom01`, có thể thay đổi linh hoạt trên giao diện).*

| Loại Topic | Mẫu Topic Cụ Thể | QoS | Retain | Ý Nghĩa Kỹ Thuật |
|:---|:---|:---:|:---:|:---|
| **Telemetry** | `udm21_nhom01/{location}/{device_type}/{device_id}/telemetry` | `0` | `false` | Bản tin đo lường cảm biến gửi định kỳ (2s - 5s). Dashboard lắng nghe bằng Wildcard: `udm21_nhom01/+/+/+/telemetry`. |
| **Status (LWT)**| `udm21_nhom01/{location}/{device_type}/{device_id}/status` | `1` | `true` | Báo trạng thái `online` khi mở và `offline` khi ngắt mạng (có cờ Retained để Dashboard nhận ngay khi vừa mở lên). |
| **Command** | `udm21_nhom01/{location}/{device_type}/{device_id}/cmd` | `1` | `false` | Dashboard phát lệnh điều khiển gửi đích danh tới thiết bị cụ thể. |

### 3.2. Định dạng JSON Data Contract

#### 1. Dữ liệu Đo Lường (`TelemetryMessage`)
```json
{
  "message_id": "e85ff714-b6ad-4c16-8646-d651b8c36a1a",
  "device_id": "temp_hum_01",
  "device_type": "sensor",
  "location": "lab",
  "timestamp": "2026-09-14T07:13:16.826Z",
  "data": {
    "temperature": 29.18,
    "humidity": 64.60,
    "unit_temp": "°C",
    "unit_hum": "%"
  }
}
```

#### 2. Trạng thái Thiết Bị (`DeviceStatusMessage`)
```json
{
  "message_id": "54a39df7-2ef6-4a11-b472-358fe2467d30",
  "device_id": "temp_hum_01",
  "status": "online",
  "timestamp": "2026-09-14T07:12:44.112Z"
}
```

#### 3. Lệnh Điều Khiển (`CommandMessage`)
```json
{
  "message_id": "44980dd6-e7f3-46cd-8c2d-efffdf358bc4",
  "command": "TOGGLE_POWER",
  "timestamp": "2026-09-14T07:13:15.354Z",
  "params": {
    "state": "ON",
    "brightness": 80
  }
}
```

---

## 🎨 4. Các Điểm Nổi Bật Về Giao Diện & Trải Nghiệm Người Dùng (UI/UX)

Giao diện WPF Dashboard được thiết kế theo phong cách ứng dụng kỹ thuật máy tính để bàn (Windows Desktop Engineering), thân thiện và tiện dụng cho cả người dùng thông thường lẫn kỹ sư vận hành:

1. **Thanh Trạng Thái Đỉnh (Top Bar):**
   - Nút bật/ngắt kết nối một chạm (`[Kết Nối]` / `[Ngắt Kết Nối]`).
   - Huy hiệu chỉ thị trạng thái mạng rõ ràng (`● Đã kết nối` màu xanh lục, `○ Chưa kết nối` màu xám).
   - Ngăn kéo cài đặt kết nối (`⚙ Cài đặt`) có thể thu gọn, giấu kín các thông số kỹ thuật (Broker, Port, TLS, Username, Password) khỏi mắt người dùng thông thường.
2. **Bố Cục 3 Phân Vùng Rõ Ràng (Main View):**
   - **Vùng Trái (Danh Sách Thiết Bị):** 5 thẻ thiết bị trực quan, hiển thị tên tiếng Việt thân thiện, vị trí lắp đặt, số đo cốt lõi và nhãn cảnh báo (Bình thường / Quá nhiệt / Quá tải / Ô nhiễm / Cạy cửa).
   - **Vùng Giữa (Chi Tiết & Điều Khiển Nhanh):** Khi người dùng nhấp chọn thiết bị ở danh sách, khu vực điều khiển riêng biệt sẽ xuất hiện tương ứng (Thanh trượt chỉnh độ sáng đèn, nút Đóng/Mở cửa, nút Lọc khí sạch, nút Cài đặt nhiệt độ...).
   - **Vùng Đáy (Nhật Ký Hệ Thống & Lịch Sử):**
     - Tab **Tổng quan**: Theo dõi thời gian thực các sự kiện mạng, thông báo kết nối, gửi nhận lệnh.
     - Tab **Lịch sử đo (SQLite)**: Cho phép lọc theo từng thiết bị hoặc xem toàn bộ, hiển thị chỉ số chính, chỉ số phụ và trạng thái đã phân tích thân thiện, không làm tràn ngập dữ liệu JSON thô.
     - Tab **Kỹ thuật MQTT**: Dành riêng cho giáo viên/kỹ sư kiểm tra chi tiết các trường giao thức.
3. **Chuẩn Độ Tương Phản Màu Sắc (WCAG AAA):**
   - Tất cả các nút bấm hành động (`SuccessButtonStyle`, `DangerButtonStyle`, `PrimaryButtonStyle`) đều có `ControlTemplate` riêng với trạng thái rê chuột (`IsMouseOver`) và nhấn chuột (`IsPressed`) sắc nét, đảm bảo chữ không bao giờ bị chìm mờ.

---

## 📚 5. Giải Thích Các Khái Niệm Mạng Cốt Lõi (Lập Trình Mạng)

* **Quality of Service (QoS):**
  * **QoS 0 (At most once):** Áp dụng cho dữ liệu cảm biến định kỳ (nhiệt độ, công suất, chất lượng khí). Tối đa hóa thông lượng mạng và giảm thiểu độ trễ; nếu mất một gói tin, gói tin chu kỳ tiếp theo sẽ bù đắp ngay lập tức.
  * **QoS 1 (At least once - Có xác nhận PUBACK):** Áp dụng cho bản tin trạng thái (Online/Offline LWT) và các lệnh điều khiển thiết bị (Bật/Tắt đèn, Báo động). Bắt buộc phải có xác nhận nhận tin từ Broker.
* **Retained Message:**
  * Áp dụng cho Topic trạng thái (`status`). Broker giữ lại bản tin trạng thái cuối cùng. Khi Dashboard kết nối và Subscribe, Broker lập tức gửi ngay bản tin này mà không cần chờ thiết bị phát tin mới.
* **Last Will and Testament (LWT):**
  * Thiết bị gửi trước "di chúc" `{"status": "offline"}` trong gói `CONNECT`. Nếu thiết bị mất kết nối đột ngột (mất điện, đứt cáp), Broker tự động phát bản tin LWT này thay cho thiết bị.
* **Clean Session vs Persistent Session:**
  * Simulators dùng `CleanSession = true` để khởi động nhanh và xóa hàng đợi cũ. Dashboard quản lý bộ nhớ đệm và tự động Re-subscribe lại toàn bộ Topic khi kết nối lại mạng.

---

## 🛡️ 6. Xử Lý Lỗi, Độ Tin Cậy & Bảo Mật

1. **An Toàn Luồng Giao Diện (WPF Dispatcher Thread-Safety):**
   - Luồng mạng nền của `MQTTnet` cập nhật dữ liệu lên giao diện qua `Dispatcher.BeginInvoke`, bảo đảm cập nhật control đúng luồng; truy vấn lịch sử tự động được gom nhịp và chạy nền.
2. **Bộ Lọc Bản Tin Trùng Lặp (Deduplication Filter):**
   - `MessageDeduplicator` sử dụng `HashSet<string>` kết hợp `Queue<string>` theo dõi 100 `message_id` gần nhất trong bộ nhớ, tự động loại bỏ bản tin bị lặp do cơ chế truyền lại của QoS 1.
3. **Bộ Lọc Bản Tin Đến Trễ (Out-of-Order Message Filter):**
   - So sánh mốc thời gian UTC (`timestamp`) của bản tin mới với bản tin đã xử lý gần nhất; từ chối ghi đè dữ liệu cảm biến cũ nếu gói tin đến muộn do độ trễ mạng.
4. **Tự Động Kết Nối Lại (Auto-Reconnect với Exponential Backoff):**
   - Khi mạng bị gián đoạn, `MqttHelper` tự động kích hoạt thuật toán lùi thời gian thử lại: $2\text{s} \rightarrow 4\text{s} \rightarrow 8\text{s} \rightarrow 16\text{s} \rightarrow 30\text{s}$, tránh làm nghẽn Broker.
5. **Cảnh Báo Vượt Ngưỡng Tức Thì (Real-time Threshold Alerts):**
   - Nhiệt độ $> 40^\circ\text{C}$ $\rightarrow$ Quá nhiệt cảnh báo đỏ.
   - Công suất $> 3000\text{W}$ $\rightarrow$ Quá tải điện.
   - Chỉ số AQI $> 100$ $\rightarrow$ Không khí kém / nguy hại.
   - Cạy mở cửa bất thường $\rightarrow$ Báo động an ninh.
6. **Lưu Trữ Lịch Sử Bền Vững (SQLite Persistence):**
   - Telemetry được ghi vào SQLite (`Extra/data/telemetry.db`); sử dụng `message_id` làm khóa chính ngăn trùng lặp dữ liệu, giới hạn 10.000 bản ghi/thiết bị để tối ưu dung lượng đĩa.

---

## 📁 7. Cấu Trúc Thư Mục Repository Chuẩn

```text
UDM_21-Monitoring-IoT-devices-via-MQTT/
├── Code/                                  # Toàn bộ mã nguồn (.NET 8 C#)
│   ├── UDM_21.sln                         # Solution chính chứa 4 Projects
│   ├── Dashboard/                         # Ứng dụng giám sát WPF Desktop
│   │   ├── controllers/MqttController.cs  # Điều phối MQTTnet, Deduplication & Dispatcher
│   │   ├── models/                        # Models: DeviceItem, MetricTile, LogEntry, History
│   │   ├── services/                      # SQLite TelemetryHistoryManager, DisplayNameResolver
│   │   ├── Resources/                     # ResourceDictionary, XAML Template thẻ thiết bị
│   │   ├── App.xaml / App.xaml.cs         # Style toàn cục, bảng màu & ControlTemplate nút bấm
│   │   ├── MainWindow.xaml / .cs          # Màn hình chính (Bố cục 3 phân vùng)
│   │   └── TelemetryHistoryWindow.xaml/.cs # Cửa sổ phụ xem lịch sử đo đạc
│   ├── Simulators/                        # Console App giả lập 5 thiết bị IoT
│   │   ├── devices/                       # 5 loại cảm biến: Đèn, Cửa, Nhiệt độ, Khí, Điện
│   │   ├── DeviceBase.cs                  # Lớp cơ sở đa luồng, vòng lặp phát tin & xử lý lệnh
│   │   └── Program.cs                     # Điểm khởi động 5 thiết bị
│   ├── Shared/                            # Thư viện dùng chung giữa Dashboard & Simulator
│   │   ├── MqttHelper.cs                  # MQTTnet v4 wrapper, kết nối, subscribe, backoff
│   │   ├── Protocol.cs                    # JSON Data Contracts & hằng số chuẩn
│   │   └── MessageValidation.cs           # Bộ lọc trùng lặp & kiểm tra thứ tự bản tin
│   └── Tests/                             # Dự án kiểm thử tự động xUnit (32 bài test)
│       ├── MqttIntegrationTests.cs        # Kiểm thử tích hợp kết nối Broker & QoS
│       ├── TelemetryHistoryManagerTests.cs # Kiểm thử SQLite & logic lọc bản ghi
│       └── DashboardPresentationTests.cs  # Kiểm thử phân tích số liệu & hiển thị tiếng Việt
├── DOCX/                                  # Thư mục chứa tài liệu báo cáo đề tài
├── Extra/                                 # Tài liệu kiểm thử hiệu năng & script bổ trợ
│   ├── logs/                              # Nhật ký chạy của broker / thiết bị
│   ├── mosquitto/                         # File cấu hình Mosquitto local broker
│   ├── scripts/                           # Script Python stress test & runner
│   │   ├── stress_test.py                 # Đo Throughput & Latency 2 mức tải
│   │   └── test_runner.py                 # Kiểm thử tự động end-to-end kịch bản
│   └── test_results/                      # Báo cáo kết quả đo đạc hiệu năng JSON
│       └── stress_report.json
├── PPTX/                                  # Thư mục chứa slide thuyết trình
├── run.bat                                # Script 1-Click khởi chạy toàn bộ hệ thống
├── run-multiprocess.bat                   # Script khởi chạy từng thiết bị trên từng cửa sổ riêng
└── README.md                              # Tài liệu hướng dẫn & báo cáo dự án
```

---

## 🚀 8. Hướng Dẫn Cài Đặt & Khởi Chạy Dự Án

### Yêu cầu môi trường:
* Hệ điều hành: Windows 10 / 11 (64-bit).
* **.NET SDK 8.0 trở lên** (Tải tại [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)).
* Visual Studio 2022 (với Workload *.NET Desktop Development*) hoặc VS Code / Rider.
* Python 3.8+ (nếu muốn chạy các script kiểm thử stress test bổ trợ).

---

### Cách 1: Khởi chạy 1-Click bằng file `run.bat` (Khuyên dùng)
1. Mở thư mục dự án trên máy tính.
2. Nhấp đúp chuột vào file **`run.bat`**.
3. Hệ thống sẽ tự động khởi chạy đồng thời:
   * **Cửa sổ 1:** 5 Thiết bị giả lập IoT (kết nối `broker.emqx.io:1883`).
   * **Cửa sổ 2:** Giao diện WPF Dashboard tự động kết nối và nhận dữ liệu thời gian thực.

---

### Cách 2: Khởi chạy thủ công từ dòng lệnh Terminal

Mở 2 cửa sổ Terminal độc lập tại thư mục gốc của dự án:

```powershell
# Terminal 1: Chạy 5 Thiết bị giả lập IoT
dotnet run --project Code/Simulators/Simulators.csproj

# Terminal 2: Chạy Giao diện giám sát WPF Dashboard
dotnet run --project Code/Dashboard/Dashboard.csproj
```

> **Tùy chọn chạy thiết bị đơn lẻ:**
> ```powershell
> dotnet run --project Code/Simulators/Simulators.csproj -- --device smart_light_01
> ```

---

## 📊 9. Kết Quả Kiểm Thử Chức Năng & Stress Test Hiệu Năng

### 9.1. Bộ Kiểm Thử Tự Động xUnit (`Code/Tests`)
Solution tích hợp sẵn bộ kiểm thử tự động toàn diện với **32 bài test tự động** trong project `Code/Tests`.

Chạy toàn bộ bài test:
```powershell
dotnet test Code/Tests/Tests.csproj
```

**Kết quả kiểm thử:**
```text
Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32, Duration: 4 s - Tests.dll (net8.0)
```

| Nhóm Kiểm Thử | Số Bài Test | Mục Tiêu Đánh Giá | Kết Quả |
|:---|:---:|:---|:---:|
| **Giao thức & Hợp đồng JSON** | 8 | Tính toàn vẹn của GUID, ISO-8601 Timestamp, phân cấp Topic, Payload hợp lệ | **32/32 PASS** |
| **Độ tin cậy mạng (Reliability)** | 10 | Bộ lọc trùng lặp bản tin, lọc bản tin đến trễ, dọn dẹp hàng đợi kết nối | **PASS** |
| **Quản lý Lịch sử SQLite** | 7 | Ghi nhận telemetry, chống trùng ID, lọc theo từng thiết bị và tất cả thiết bị | **PASS** |
| **Giao diện Dashboard** | 7 | Phân tích số liệu cảm biến sang định dạng tiếng Việt, đánh giá ngưỡng cảnh báo | **PASS** |

---

### 9.2. Đo Đạc Hiệu Năng & Thông Lượng Mạng (Stress Test Benchmark)
Kịch bản kiểm thử đo thông lượng bằng Python (`Extra/scripts/stress_test.py`) gửi gói tin QoS 1 qua Public Broker `broker.emqx.io:1883` trên Topic cô lập `udm21_nhom01_test`:

| Mức Tải Kiểm Thử | Số Lượng Gói Tin | QoS | Tỷ Lệ Nhận ACK | Thời Gian Chạy | Throughput (Thông Lượng) | Độ Trễ RTT Trung Bình | Độ Trễ RTT P95 |
|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Mức 1 (Tải nhẹ)** | 500 msgs | 1 | **100.0% (500/500)** | 6.70 s | **74.60 msg/s** | **3,397.23 ms** | **6,286.00 ms** |
| **Mức 2 (Tải nặng)** | 2.000 msgs | 1 | **100.0% (2000/2000)** | 26.83 s | **74.55 msg/s** | **13,447.77 ms** | **25,532.83 ms** |

> **Nhận xét:**
> - Hệ thống đạt **100% tỷ lệ xác nhận gói tin (PUBACK)** ở cả hai mức tải, không xảy ra hiện tượng mất mát dữ liệu hoặc rớt kết nối.
> - Độ trễ ở mức tải 2.000 gói phản ánh hàng đợi xử lý của Public Broker khi chịu tải dồn dập, phép đo PUBACK này không đo hiệu năng hay độ mượt của Dashboard WPF.

---

## 🎯 10. Giới Hạn Sản Phẩm & Hướng Phát Triển

### 10.1. Giới hạn sản phẩm:
* Dự án tập trung vào lập trình mạng tầng ứng dụng qua giao thức MQTT; các thiết bị IoT được mô phỏng phần mềm đa luồng trên C# .NET thay vì mạch vi điều khiển ESP32/Arduino vật lý.
* Cơ sở dữ liệu SQLite cục bộ được tối ưu lưu trữ 10.000 bản ghi gần nhất cho mỗi thiết bị, phù hợp với phạm vi đồ án môn học.
* Cấu hình mặc định sử dụng Public Broker EMQX để thuận tiện trình diễn; khi triển khai sản phẩm thực tế cần cấu hình Broker nội bộ có xác thực tài khoản và TLS.

### 10.2. Hướng phát triển tương lai:
* Kết nối trực tiếp với phần cứng IoT thực tế (ESP32, Raspberry Pi) qua Wi-Fi / Ethernet.
* Tích hợp cơ sở dữ liệu chuỗi thời gian (InfluxDB / TimescaleDB) để vẽ biểu đồ trực quan xu hướng nhiệt độ và công suất theo thời gian.
* Bổ sung cơ chế bảo mật xác thực 2 chiều (Mutual TLS / X.509 Client Certificates) và phân quyền Access Control List (ACL) cho từng cảm biến.
* Mở rộng ứng dụng di động (.NET MAUI) hỗ trợ nhận thông báo đẩy khi phát hiện cảnh báo nguy hại từ xa.
