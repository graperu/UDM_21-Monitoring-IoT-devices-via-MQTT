# BÁO CÁO KẾT QUẢ KIỂM THỬ HỆ THỐNG UDM_21
**Đề tài:** Giám Sát & Điều Khiển Thiết Bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)  
**Thời gian thực hiện:** 07/09/2026

**Môi trường kiểm thử:** Public MQTT Broker `broker.emqx.io:1883` với topic root riêng `udm21_nhom01_test` và MQTTnet broker cục bộ cho integration test

**Máy kiểm thử:** Windows 11 build 26200, Intel64 Family 6 Model 141, 12 logical CPU, 16.122 MB RAM

**Runtime:** .NET SDK 10.0.100, Python 3.14.0, paho-mqtt 2.1.0

---

## 📊 1. BẢNG TỔNG HỢP KẾT QUẢ KIỂM THỬ (TEST SUMMARY)

| Mã Test Case | Hạng Mục Kiểm Thử | Tiêu Chuẩn Đánh Giá | Kết Quả Thực Tế | Trạng Thái |
| :---: | :--- | :--- | :--- | :---: |
| **TC-01** | Khởi chạy 5 Thiết bị IoT độc lập | 5/5 thiết bị kết nối và gửi trạng thái `online` (LWT Retained) | 5/5 thiết bị (`temp_hum_01`, `air_quality_01`, `power_meter_01`, `smart_light_01`, `door_sensor_01`) online thành công | **✅ PASS** |
| **TC-02** | Chuẩn hóa JSON Protocol | Telemetry chứa đầy đủ `message_id` (GUID), `timestamp` (UTC ISO-8601) và `data` | Đã nhận và parse thành công 100% bản tin từ 5 thiết bị | **✅ PASS** |
| **TC-03** | Gửi lệnh & Phản hồi Real-time | Gửi `TOGGLE_POWER` (ON) tới `smart_light_01`, đèn đổi trạng thái tức thì | Đèn `smart_light_01` đổi `state: ON`, `brightness: 80` và phản hồi telemetry ngay lập tức | **✅ PASS** |
| **TC-04** | Phát hiện Cảnh báo bất thường | `temp_hum_01` sinh nhiệt độ > 40°C định kỳ, hệ thống bắt được cảnh báo | Bắt được 2 lần nhiệt độ bất thường ($48.70^\circ\text{C}$ và $53.90^\circ\text{C} > 40^\circ\text{C}$) | **✅ PASS** |
| **TC-05** | Stress Test 2 Mức Tải (QoS 1) | Gửi 500 và 2.000 message, đo throughput cùng RTT PUBACK từng message | Đạt tỷ lệ xác nhận 100.0% (500/500 và 2000/2000 PUBACK) | **✅ PASS** |
| **TC-06** | Validation, duplicate và late message | Chạy unit test với malformed JSON, GUID/topic/command sai, message trùng và timestamp cũ | 11/11 unit test PASS | **✅ PASS** |
| **TC-07** | Authentication, LWT, broker disconnect và reconnect | Dựng MQTTnet broker cục bộ; kiểm tra đúng/sai tài khoản, đóng client đột ngột, dừng/khởi động lại broker và kiểm tra re-subscribe | 3/3 integration test PASS | **✅ PASS** |
| **TC-08** | SQLite và cấu hình MQTT | Khởi động lại history manager, gửi duplicate, giới hạn 20 bản hiển thị, kiểm tra topic root và thông tin đăng nhập | 5/5 unit test PASS | **✅ PASS** |
| **TC-09** | MQTT qua TLS thực tế | Chạy một `temp_hum_01` riêng với `--tls` trên public broker, publish status/telemetry rồi đóng sạch | Kết nối thành công tới `mqtts://broker.emqx.io:8883` ngày 07/09/2026 | **✅ PASS** |

---

## ⚡ 2. KẾT QUẢ ĐO ĐẠC HIỆU NĂNG & ĐỘ TRỄ (PERFORMANCE BENCHMARK)

| Mức Tải Kiểm Thử | Số Message | QoS | Tỷ Lệ ACK | Thời Gian | Throughput | RTT trung bình | RTT P95 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Mức 1 (Tải nhẹ)** | 500 | 1 | **100% (500/500)** | 7.91 s | **63.22 msg/s** | **4,024.83 ms** | **7,413.53 ms** |
| **Mức 2 (Tải nặng)** | 2.000 | 1 | **100% (2000/2000)** | 27.65 s | **72.35 msg/s** | **14,397.31 ms** | **26,357.61 ms** |

**Cách đo:** lưu thời điểm trước mỗi `publish()` và tính RTT khi callback PUBACK tương ứng được gọi. Test chỉ PASS khi nhận đủ ACK trước timeout hữu hạn theo tải. RTT bao gồm thời gian xếp hàng phía client/public broker nên không được xem là độ trễ đường truyền thuần.

---

## 🔍 3. CHI TIẾT CÁC BẢN TIN DỮ LIỆU ĐÃ KIỂM CHỨNG

### 3.1. Bản tin Dữ liệu Cảm biến Nhiệt độ / Độ ẩm (`temp_hum_01`)
```json
{
  "message_id": "e85ff714-b6ad-4c16-8646-d651b8c36a1a",
  "device_id": "temp_hum_01",
  "device_type": "sensor",
  "location": "lab",
  "timestamp": "2026-09-06T07:13:16.8260620Z",
  "data": {
    "temperature": 29.18,
    "humidity": 64.6,
    "unit_temp": "°C",
    "unit_hum": "%"
  }
}
```

### 3.2. Bản tin Phản hồi Lệnh Bật Đèn Thông Minh (`smart_light_01`)
```json
{
  "message_id": "44980dd6-e7f3-46cd-8c2d-efffdf358bc4",
  "device_id": "smart_light_01",
  "device_type": "light",
  "location": "home",
  "timestamp": "2026-09-06T07:13:15.3548768Z",
  "data": {
    "state": "ON",
    "brightness": 80,
    "power_draw_w": 12.0
  }
}
```

### 3.3. Bản tin Cảnh báo Ngưỡng Nhiệt Độ Cao
```json
{
  "message_id": "341ecbfb-321a-4681-bc5b-79bda3bdc94b",
  "device_id": "temp_hum_01",
  "timestamp": "2026-09-06T07:12:58.7798203Z",
  "data": {
    "temperature": 52.14,
    "humidity": 67.27
  }
}
```

---

## 🎯 4. KẾT LUẬN & ĐÁNH GIÁ ĐỒ ÁN
1. **Chức năng:** 5 thiết bị, command và cảnh báo đã có bằng chứng end-to-end từ lần chạy ngày 07/09/2026.
2. **Reliability:** 19/19 test C# xác nhận validation, deduplication, out-of-order, SQLite persistence, topic isolation, authentication, LWT và reconnect/re-subscribe trên broker cục bộ.
3. **Hiệu năng:** Hai mức tải đều nhận 100% PUBACK; mức 2 có RTT hàng đợi cao và được công bố đúng trong báo cáo thay vì quy đổi từ tổng thời gian.
4. **Mã nguồn:** Solution biên dịch 0 lỗi, 0 cảnh báo trong lần kiểm tra sau cập nhật.
5. **Dependency:** `dotnet list package --vulnerable --include-transitive` không phát hiện package có advisory sau khi nâng Microsoft.Data.Sqlite lên bản vá 8.0.30.
