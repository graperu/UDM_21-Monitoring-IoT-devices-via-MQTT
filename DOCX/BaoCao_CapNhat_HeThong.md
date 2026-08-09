# BÁO CÁO CẬP NHẬT & TỐI ƯU HỆ THỐNG GIÁM SÁT IoT QUA MQTT (UDM_21)

**Dự án:** Giám sát & Điều khiển Thiết bị IoT Qua Giao Thức MQTT (C# .NET 8 WPF)  
**Ngày cập nhật:** 09/08/2026  
**Người thực hiện:** Antigravity AI & Nhóm Phát Triển UDM_21  

---

## 📑 1. TỔNG QUAN CÁC HẠNG MỤC ĐÃ THỰC HIỆN

Trong đợt cập nhật này, hệ thống đã được đồng bộ mã nguồn mới nhất từ GitHub repository, kiểm tra biên dịch toàn bộ solution, đồng thời bổ sung 3 tính năng quan trọng phục vụ cho việc kiểm thử tính năng nâng cao và đảm bảo tính ổn định khi chạy thực tế.

---

## 🛠️ 2. CHI TIẾT CÁC NÂNG CẤP MÃ NGUỒN

### 2.1. Nâng cấp Giả lập Thiết bị & Cấu hình CLI (`Simulators`)
- **Tệp thay đổi:** `Code/Simulators/Program.cs`
- **Nội dung:** Cho phép truyền trực tiếp địa chỉ MQTT Broker (`host`) và cổng (`port`) từ tham số dòng lệnh CLI.
- **Lợi ích:** Dễ dàng chuyển đổi giữa Broker nội bộ (`localhost:1883`) và Broker công cộng (`broker.emqx.io:1883`) khi chạy câu lệnh:
  ```bash
  dotnet run --project Code/Simulators/Simulators.csproj broker.emqx.io 1883
  ```

---

### 2.2. Giả lập Cảnh báo Bất thường & Xử lý Ngưỡng An toàn (`Alert & Anomaly Detection`)
- **Tệp thay đổi:**
  - `Code/Simulators/devices/TempHumidityDevice.cs`
  - `Code/Dashboard/models/DeviceItem.cs`
  - `Code/Dashboard/MainWindow.xaml`
  - `Code/Dashboard/MainWindow.xaml.cs`
- **Nội dung cải tiến:**
  1. **Thiết bị Nhiệt độ (`temp_hum_01`):** Định kỳ mỗi 5 chu kỳ phát dữ liệu (~15 giây), thiết bị sẽ phát 1 giá trị nhiệt độ tăng đột biến ($48.5^\circ\text{C} - 54.5^\circ\text{C} > 40^\circ\text{C}$).
  2. **Dashboard WPF:**
     - Phân tích tự động payload Telemetry nhận được. Nếu `temperature > 40°C`, `power_watt > 3000W` hoặc `aqi > 150`, Dashboard xuất cảnh báo đỏ:
       `🚨 [CẢNH BÁO BẤT THƯỜNG] temp_hum_01: Nhiệt độ vượt ngưỡng: 52.19°C (> 40°C)`
     - Cập nhật trạng thái hiển thị trên DataGrid thành **`⚠️ Cảnh báo`** đi kèm chấm đèn chỉ thị màu đỏ cam (`OrangeRed`).

---

### 2.3. Phản hồi Lệnh Điều khiển Real-Time (`Command ACK & State Reflection`)
- **Tệp thay đổi:** `Code/Simulators/devices/SmartLightDevice.cs`
- **Nội dung cải tiến:**
  - Khi ứng dụng Dashboard gửi lệnh điều khiển (như `TOGGLE_POWER` với `{"state": "ON"}`/`{"state": "OFF"}` hoặc `SET_BRIGHTNESS`), thiết bị `smart_light_01` sẽ:
    1. Cập nhật biến trạng thái thực tế trong bộ nhớ.
    2. Xuất log xác nhận phản hồi lệnh trên Console: `[Device smart_light_01] 💡 PHẢN HỒI LỆNH: Đèn đã thực sự BẬT (Độ sáng: 80%)!`.
    3. Ngay lập tức Publish 1 gói dữ liệu Telemetry cập nhật về MQTT Broker, giúp giao diện Dashboard phản ánh trạng thái mới **tức thì** mà không cần chờ chu kỳ định kỳ 5 giây.

---

### 2.4. Khắc phục Lỗi Xung đột Client ID trên MQTT Broker Công cộng
- **Tệp thay đổi:**
  - `Code/Dashboard/controllers/MqttController.cs`
  - `Code/Simulators/DeviceBase.cs`
- **Nguyên nhân:** Khi chạy trên MQTT Broker dùng chung (`broker.emqx.io`), việc cố định Client ID làm cho Broker từ chối kết nối (`NotAuthorized`) hoặc ngắt kết nối lẫn nhau giữa các phiên làm việc.
- **Giải pháp:** Tự động sinh chuỗi định danh duy nhất chứa GUID ngắn cho cả Dashboard (`WpfDashboard_xxxxxx`) và các thiết bị giả lập (`sim_temp_hum_01_xxxx`).

---

## 📊 3. KẾT QUẢ KIỂM THỬ (VERIFICATION RESULTS)

1. **Biên dịch Dự án (`dotnet build`):**
   - **Thành công:** 0 Lỗi (Error), 0 Cảnh báo (Warning).
2. **Khởi chạy Hệ thống (`dotnet run`):**
   - **Simulators:** 5/5 thiết bị (`temp_hum_01`, `air_quality_01`, `power_meter_01`, `smart_light_01`, `door_sensor_01`) kết nối và phát dữ liệu real-time lên `broker.emqx.io:1883`.
   - **Dashboard WPF:** Khởi chạy cửa sở GUI, tự động kết nối và hiển thị liên tục bảng theo dõi thiết bị kèm nhật ký lệnh/cảnh báo.

---

## 📌 4. HƯỚNG DẪN KHỞI CHẠY LẠI DỰ ÁN

Mở 2 cửa sở Terminal độc lập tại thư mục gốc:

```bash
# Terminal 1: Chạy các thiết bị giả lập IoT (Kết nối Broker công cộng)
dotnet run --project Code/Simulators/Simulators.csproj broker.emqx.io 1883

# Terminal 2: Chạy ứng dụng Giao diện WPF Dashboard
dotnet run --project Code/Dashboard/Dashboard.csproj
```
