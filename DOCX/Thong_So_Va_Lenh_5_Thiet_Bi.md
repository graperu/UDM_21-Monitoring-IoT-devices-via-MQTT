# TÀI LIỆU TẬP LỆNH ĐIỀU KHIỂN & DỮ LIỆU TELEMETRY 5 THIẾT BỊ IOT (UDM_21)

Hệ thống đã hỗ trợ **tương tác và điều khiển 2 chiều trên toàn bộ 5 thiết bị IoT**. Khi người dùng gửi lệnh từ Dashboard, thiết bị mô phỏng (Simulator) sẽ nhận lệnh, thay đổi trạng thái hoạt động thực tế ngay lập tức và phát lại bản tin Telemetry phản hồi tức thì lên Dashboard.

---

## 1. Cấu Trúc Topic MQTT

- **Lệnh điều khiển (Command):** `{topic_root}/{location}/{device_type}/{device_id}/cmd`
- **Dữ liệu cảm biến (Telemetry):** `{topic_root}/{location}/{device_type}/{device_id}/telemetry`
- **Trạng thái LWT (Status):** `{topic_root}/{location}/{device_type}/{device_id}/status`

*(Mặc định `topic_root` = `udm21_nhom01`)*

---

## 2. Danh Mục Lệnh & Tham Số Chi Tiết Theo Từng Thiết Bị

### 💡 1. Thiết Bị Đèn Thông Minh (`smart_light_01`)
- **Loại (`DeviceType`):** `light` | **Vị trí:** `home`
- **Topic nhận lệnh:** `udm21_nhom01/home/light/smart_light_01/cmd`
- **Hiệu ứng thị giác:** Biểu tượng 💡 (Bật)/🌑 (Tắt), công suất điện tăng/giảm theo độ sáng.

| Tên Lệnh (`Command`) | Tham Số JSON (`Params`) | Hành Vi & Phản Hồi |
| :--- | :--- | :--- |
| `TOGGLE_POWER` | `{"state": "ON"}` | Bật sáng đèn, công suất tiêu thụ ~12.0W |
| `TOGGLE_POWER` | `{"state": "OFF"}` | Tắt đèn, công suất giảm về ~0.5W |
| `TOGGLE_POWER` | `{}` | Đảo trạng thái hiện tại giữa Bật và Tắt |
| `SET_BRIGHTNESS` | `{"brightness": 100}` | Đặt độ sáng 100% (Cực đại) |
| `SET_BRIGHTNESS` | `{"brightness": 50}` | Đặt độ sáng 50% |
| `SET_BRIGHTNESS` | `{"brightness": 0}` | Đặt độ sáng 0% (Tự động chuyển về trạng thái Tắt) |

---

### 🚪 2. Cảm Biến Cửa An Ninh (`door_sensor_01`)
- **Loại (`DeviceType`):** `security` | **Vị trí:** `lab`
- **Topic nhận lệnh:** `udm21_nhom01/lab/security/door_sensor_01/cmd`
- **Hiệu ứng thị giác:** Biểu tượng 🔓 (Đang Mở)/🚪 (Đã Đóng), chuyển trạng thái text và cảnh báo cạy phá.

| Tên Lệnh (`Command`) | Tham Số JSON (`Params`) | Hành Vi & Phản Hồi |
| :--- | :--- | :--- |
| `OPEN_DOOR` | `{}` | Mở cửa ngay lập tức (`door_state = "OPEN"`) |
| `CLOSE_DOOR` | `{}` | Đóng cửa lại (`door_state = "CLOSED"`) |
| `TOGGLE_DOOR` | `{}` | Đảo trạng thái Đóng <-> Mở |
| `TRIGGER_ALARM` | `{}` | Kích hoạt cảnh báo cạy phá (`tamper_alert = true`) |
| `CLEAR_ALARM` | `{}` | Hủy trạng thái cảnh báo an toàn (`tamper_alert = false`) |

---

### 🌡️ 3. Cảm Biến Môi Trường Nhiệt Độ & Độ Ẩm (`temp_hum_01`)
- **Loại (`DeviceType`):** `sensor` | **Vị trí:** `lab`
- **Topic nhận lệnh:** `udm21_nhom01/lab/sensor/temp_hum_01/cmd`
- **Hiệu ứng thị giác:** Nhiệt độ hiển thị số to trên Card, đổi sang màu cam đỏ `⚠️ Cảnh báo` khi nhiệt độ > 40°C.

| Tên Lệnh (`Command`) | Tham Số JSON (`Params`) | Hành Vi & Phản Hồi |
| :--- | :--- | :--- |
| `SET_TEMPERATURE` | `{"temperature": 22.0}` | Đặt nhiệt độ môi trường mát mẻ (22.0°C) |
| `SET_TEMPERATURE` | `{"temperature": 28.0}` | Đặt nhiệt độ phòng chuẩn (28.0°C) |
| `SET_TEMPERATURE` | `{"temperature": 52.0}` | Giả lập quá nhiệt 52°C (Kích hoạt cảnh báo hệ thống) |
| `TRIGGER_HEAT_ALERT` | `{}` | Kích hoạt chu kỳ nhiệt độ cao tức thì (>48°C) |
| `CALIBRATE` | `{}` | Hiệu chuẩn lại cảm biến về nhiệt độ chuẩn (26.0°C, 60% độ ẩm) |

---

### 🍃 4. Cảm Biến & Thiết Bị Lọc Không Khí (`air_quality_01`)
- **Loại (`DeviceType`):** `sensor` | **Vị trí:** `factory`
- **Topic nhận lệnh:** `udm21_nhom01/factory/sensor/air_quality_01/cmd`
- **Hiệu ứng thị giác:** Hiển thị chỉ số AQI và nồng độ CO2 ppm, kích hoạt cảnh báo khi AQI > 150.

| Tên Lệnh (`Command`) | Tham Số JSON (`Params`) | Hành Vi & Phản Hồi |
| :--- | :--- | :--- |
| `PURIFY_AIR` | `{}` | Bật chế độ lọc không khí sạch (AQI giảm về 35 - GOOD) |
| `SET_AQI` | `{"aqi": 85.0}` | Giả lập chỉ số AQI mức trung bình (85.0) |
| `SET_AQI` | `{"aqi": 180.0}` | Giả lập ô nhiễm nặng (AQI 180 - POOR, kích hoạt cảnh báo) |
| `TRIGGER_POLLUTION_ALERT` | `{}` | Kích hoạt khói bụi ô nhiễm đột ngột |

---

### ⚡ 5. Đồng Hồ Đo Điện Năng (`power_meter_01`)
- **Loại (`DeviceType`):** `meter` | **Vị trí:** `home`
- **Topic nhận lệnh:** `udm21_nhom01/home/meter/power_meter_01/cmd`
- **Hiệu ứng thị giác:** Cập nhật công suất điện W, dòng điện A, số kWh tích lũy. Cảnh báo quá tải khi > 3000W.

| Tên Lệnh (`Command`) | Tham Số JSON (`Params`) | Hành Vi & Phản Hồi |
| :--- | :--- | :--- |
| `SET_LOAD` | `{"power_watt": 500.0}` | Đặt phụ tải bình thường (500W, ~2.27A) |
| `SET_LOAD` | `{"power_watt": 2000.0}` | Đặt phụ tải cao (2000W, ~9.09A) |
| `TRIGGER_OVERLOAD` | `{}` | Giả lập quá tải lưới điện (>3500W, kích hoạt cảnh báo) |
| `RESET_ENERGY` | `{}` | Đặt lại chỉ số điện năng tiêu thụ về 0.00 kWh |

---

## 3. Tính Năng Tự Động Trên Giao Diện WPF Dashboard

1. **Preset Commands (Mẫu Lệnh Sẵn Có):**
   - Khi chọn bất kỳ thiết bị nào ở ô **Chọn Thiết Bị** hoặc nhấp chuột vào Card thiết bị, dropdown **Mẫu Lệnh Sẵn Có** sẽ tự động liệt kê tất cả các lệnh của riêng thiết bị đó kèm tham số JSON mẫu chuẩn xác 100%.
2. **Nút Tác Vụ Nhanh (Quick Action):**
   - Từng Card thiết bị đều có sẵn 1 nút bấm trực tiếp tương ứng:
     - `smart_light_01`: **Bật/Tắt Đèn**
     - `door_sensor_01`: **Đóng/Mở Cửa**
     - `temp_hum_01`: **Cảnh Báo Nhiệt**
     - `air_quality_01`: **Lọc Không Khí**
     - `power_meter_01`: **Reset Điện**
