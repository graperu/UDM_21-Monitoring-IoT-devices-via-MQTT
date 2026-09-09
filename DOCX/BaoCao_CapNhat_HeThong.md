# BÁO CÁO CẬP NHẬT HỆ THỐNG UDM_21

**Đề tài:** Giám sát và điều khiển thiết bị IoT qua MQTT

**Nền tảng:** C# .NET 8, WPF, MQTTnet, SQLite

**Ngày kiểm tra:** 07/09/2026

## 1. Phạm vi cập nhật

Đợt cập nhật hoàn thiện bốn hạn chế của phiên bản trước:

1. Lịch sử telemetry không còn mất khi đóng Dashboard.
2. Kết nối MQTT hỗ trợ TLS và username/password cấu hình được.
3. Topic có namespace riêng, tránh nhận nhầm message trên broker công cộng.
4. Simulator có thể chạy cả năm thiết bị trong một tiến trình hoặc mỗi thiết bị trong tiến trình riêng.

## 2. Nội dung thực hiện

### 2.1. Topic và bảo mật kết nối

- Topic mặc định: `udm21_nhom01/{location}/{device_type}/{device_id}/{kind}`.
- Dashboard subscribe bằng wildcard dưới đúng `topic_root` đã cấu hình.
- Dashboard có ô Host, Port, Topic Root, TLS, Username và Password.
- MQTTnet xác thực chứng chỉ broker bằng trust store của hệ điều hành.
- Simulator nhận password qua biến môi trường, không ghi secret vào source hoặc log.
- Có cấu hình Mosquitto local chỉ lắng nghe `127.0.0.1` để demo không phụ thuộc Internet.

Commit triển khai: `9ce37e9 feat: isolate mqtt topics and secure broker connections`.

### 2.2. Chế độ Simulator đa tiến trình

- `--device all`: chạy năm thiết bị trong một tiến trình như trước.
- `--device <device_id>`: chỉ chạy thiết bị được chọn.
- Hỗ trợ CLI cho host, port, topic root, TLS, username và tên biến môi trường chứa password.
- `run-multiprocess.bat` mở năm tiến trình thiết bị riêng và một Dashboard.

Commit triển khai: `8d3c554 feat: support per-device simulator processes`.

### 2.3. Lịch sử SQLite

- Database mặc định: `Extra/data/telemetry.db`.
- `message_id` là khóa chính để chống lưu trùng sau khi Dashboard restart.
- Lưu tối đa 10.000 telemetry cho mỗi thiết bị; GUI hiển thị 20 bản gần nhất.
- Khi khởi động lại, Dashboard nạp thiết bị và telemetry cuối từ database, tạm đánh dấu Offline cho tới khi nhận status mới.
- Nút xóa lịch sử xóa dữ liệu tương ứng trong SQLite.

Commit triển khai: `477411f feat: persist telemetry history with sqlite`.

## 3. Kết quả kiểm thử

### 3.1. Build và kiểm thử tự động

| Hạng mục | Kết quả |
|---|---:|
| Release build | 0 lỗi, 0 cảnh báo |
| C# unit/integration tests | 19/19 PASS |
| End-to-end MQTT | 5/5 PASS |
| Dependency vulnerability scan | Không phát hiện advisory |

Các test C# bao phủ validation JSON/topic/command, duplicate, out-of-order, authentication đúng/sai, LWT, broker disconnect, reconnect/re-subscribe, SQLite restart, duplicate database và giới hạn 20 bản hiển thị.

### 3.2. Smoke test thực tế

- Kết nối TLS tới `mqtts://broker.emqx.io:8883`: PASS.
- Chạy riêng `temp_hum_01`/`door_sensor_01` bằng `--device`: PASS.
- Kết nối, publish online/telemetry, publish offline và đóng tiến trình sạch: PASS.

### 3.3. Performance test qua broker công cộng

| Mức tải | PUBACK | Thời gian | Throughput | RTT trung bình | RTT P95 |
|---|---:|---:|---:|---:|---:|
| 500 message QoS 1 | 500/500 | 7,91 giây | 63,22 msg/s | 4.024,83 ms | 7.413,53 ms |
| 2.000 message QoS 1 | 2.000/2.000 | 27,65 giây | 72,35 msg/s | 14.397,31 ms | 26.357,61 ms |

RTT bao gồm thời gian chờ trong hàng đợi publish QoS 1, không phải chỉ là độ trễ mạng. Dữ liệu gốc và cấu hình máy nằm trong `Extra/test_results/test_report.json`.

## 4. Hướng dẫn chạy

### Chế độ thông thường

```powershell
run.bat
```

### Mỗi thiết bị một tiến trình

```powershell
run-multiprocess.bat
```

### Broker local

```powershell
mosquitto -c Extra/mosquitto/mosquitto.local.conf -v
```

Sau đó nhập `localhost`, port `1883`, TLS tắt và cùng Topic Root trên Dashboard/Simulator.

## 5. Hạn chế còn lại trước khi nộp môn

- Chưa có file báo cáo Word `.docx` chính thức.
- Chưa có slide `.pptx` chính thức.
- Chưa có link video demo thật trong README.
- TLS/authentication cần broker và tài khoản phù hợp; cấu hình mặc định vẫn dùng dữ liệu giả qua public broker.

Ba mục hồ sơ đầu tiên cần được nhóm hoàn thành và kiểm tra thủ công trước khi nộp.
