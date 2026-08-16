import os
import sys
import json
import subprocess
import urllib.request
import urllib.error

# Ensure UTF-8 output on Windows consoles
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

REPO = "graperu/UDM_21-Monitoring-IoT-devices-via-MQTT"

ISSUES = [
    {
        "title": "Thiết kế bố cục tổng quan MainWindow.xaml và App.xaml",
        "body": "### Mô tả công việc\n- Thiết kế thanh kết nối MQTT Broker (Host, Port, Nút Connect/Disconnect)\n- Thiết kế DataGrid danh sách thiết bị và trạng thái real-time\n- Thiết kế Form phát lệnh điều khiển thiết bị\n- Thiết kế Khung Console Log với thanh cuộn tùy biến\n\n**Người thực hiện:** Cát Chương\n**Thư mục/File:** `Code/Dashboard/MainWindow.xaml`, `Code/Dashboard/App.xaml`",
        "labels": ["frontend", "ui/ux"],
        "done": True
    },
    {
        "title": "Cấu hình DataBinding giữa GUI và Model DeviceItem",
        "body": "### Mô tả công việc\n- DataBinding hai chiều giữa DataGrid và ObservableCollection<DeviceItem>\n- Hiển thị chấm tròn LED màu tương ứng theo trạng thái: Online (Xanh lá), Cảnh báo (Đỏ cam), Offline (Đỏ)\n\n**Người thực hiện:** Cát Chương\n**Thư mục/File:** `Code/Dashboard/MainWindow.xaml`",
        "labels": ["frontend", "databinding"],
        "done": True
    },
    {
        "title": "Thiết kế giao diện hiển thị Lịch sử 20 bản tin đo gần nhất",
        "body": "### Mô tả công việc\n- Thêm 1 DataGrid phụ (hoặc Tab/Popup) trong MainWindow.xaml để hiển thị lịch sử 20 bản ghi khi click chọn 1 thiết bị\n- Thêm nút 'Xóa lịch sử' cho thiết bị đang chọn\n\n**Người thực hiện:** Cát Chương\n**Thư mục/File:** `Code/Dashboard/MainWindow.xaml`",
        "labels": ["frontend", "ui/ux", "feature"],
        "done": False
    },
    {
        "title": "Xây dựng MqttController quản lý Pub/Sub và kết nối Broker",
        "body": "### Mô tả công việc\n- Khởi tạo MqttHelper với Client ID duy nhất để tránh xung đột\n- Đăng ký nhận tin Wildcard: `iot/+/+/+/telemetry` và `iot/+/+/+/status`\n- Bắt các sự kiện kết nối, ngắt kết nối và điều phối dữ liệu về UI\n\n**Người thực hiện:** Huyền Trâm\n**Thư mục/File:** `Code/Dashboard/Controllers/MqttController.cs`",
        "labels": ["backend", "controller", "mqtt"],
        "done": True
    },
    {
        "title": "Cài đặt Dispatcher và Model DeviceItem chống treo giao diện",
        "body": "### Mô tả công việc\n- Xây dựng Model `DeviceItem.cs` kế thừa `INotifyPropertyChanged`\n- Cập nhật ObservableCollection qua `Dispatcher.Invoke` khi nhận tin từ luồng nền MQTT\n- Logic kiểm tra cảnh báo ngưỡng bất thường: Nhiệt độ > 40°C, Công suất > 3000W, AQI > 150\n\n**Người thực hiện:** Huyền Trâm\n**Thư mục/File:** `Code/Dashboard/Models/DeviceItem.cs`, `Code/Dashboard/MainWindow.xaml.cs`",
        "labels": ["backend", "threading", "wpf"],
        "done": True
    },
    {
        "title": "Xử lý sự kiện gửi Command điều khiển từ GUI",
        "body": "### Mô tả công việc\n- Bắt sự kiện Click nút 'Phát Lệnh Qua MQTT'\n- Validate cú pháp chuỗi JSON tham số trước khi phát tin\n- Phát tin lệnh qua topic `iot/{location}/{deviceType}/{deviceId}/cmd` với QoS 1\n\n**Người thực hiện:** Huyền Trâm\n**Thư mục/File:** `Code/Dashboard/MainWindow.xaml.cs`",
        "labels": ["backend", "controller"],
        "done": True
    },
    {
        "title": "Tích hợp TelemetryHistoryManager hiển thị lịch sử lên UI",
        "body": "### Mô tả công việc\n- Bắt sự kiện `SelectionChanged` của DataGrid danh sách thiết bị\n- Lấy danh sách 20 bản tin từ `_historyManager.GetHistory(deviceId)`\n- Gán danh sách vào ItemsSource của DataGrid Lịch sử trên giao diện\n\n**Người thực hiện:** Huyền Trâm\n**Thư mục/File:** `Code/Dashboard/MainWindow.xaml.cs`, `Code/Dashboard/Services/`",
        "labels": ["backend", "integration", "feature"],
        "done": False
    },
    {
        "title": "Xây dựng lớp trừu tượng DeviceBase và đa luồng Telemetry",
        "body": "### Mô tả công việc\n- Xây dựng vòng lặp phát dữ liệu định kỳ bằng `Task.Delay` và `CancellationToken`\n- Thiết lập Last Will and Testament (LWT) gửi `offline` khi ngắt kết nối đột ngột\n- Lắng nghe topic điều khiển `CmdTopic`\n\n**Người thực hiện:** Phan Đỉnh\n**Thư mục/File:** `Code/Simulators/DeviceBase.cs`, `Code/Simulators/Program.cs`",
        "labels": ["simulator", "core", "multithreading"],
        "done": True
    },
    {
        "title": "Lập trình 5 thiết bị giả lập cảm biến độc lập",
        "body": "### Mô tả công việc\n- Lập trình 5 class thiết bị:\n  1. TempHumidityDevice (lab)\n  2. AirQualityDevice (factory)\n  3. PowerMeterDevice (home)\n  4. SmartLightDevice (home)\n  5. DoorSensorDevice (lab)\n\n**Người thực hiện:** Phan Đỉnh\n**Thư mục/File:** `Code/Simulators/devices/`",
        "labels": ["simulator", "devices"],
        "done": True
    },
    {
        "title": "Xử lý nhận lệnh và phát cảnh báo bất thường trên Simulators",
        "body": "### Mô tả công việc\n- SmartLightDevice xử lý lệnh `TOGGLE_POWER` / `SET_BRIGHTNESS` và phản hồi tức thì bằng gói Telemetry mới\n- TempHumidityDevice giả lập phát giá trị nhiệt độ tăng đột biến > 40°C định kỳ\n\n**Người thực hiện:** Phan Đỉnh\n**Thư mục/File:** `Code/Simulators/devices/SmartLightDevice.cs`, `Code/Simulators/devices/TempHumidityDevice.cs`",
        "labels": ["simulator", "feature"],
        "done": True
    },
    {
        "title": "Chuẩn hóa message_id (GUID) và timestamp ISO trong Telemetry",
        "body": "### Mô tả công việc\n- Bổ sung trường `message_id` dạng GUID duy nhất cho từng bản tin Telemetry\n- Chuẩn hóa trường `timestamp` định dạng ISO-8601 UTC để phục vụ lọc tin trùng và tin đến trễ\n\n**Người thực hiện:** Phan Đỉnh\n**Thư mục/File:** `Code/Shared/Protocol.cs`, `Code/Simulators/DeviceBase.cs`",
        "labels": ["simulator", "protocol", "enhancement"],
        "done": False
    },
    {
        "title": "Xây dựng MqttHelper Wrapper quanh thư viện MQTTnet v4",
        "body": "### Mô tả công việc\n- Wrapper các phương thức bất đồng bộ: `ConnectAsync`, `DisconnectAsync`, `SubscribeAsync`, `PublishAsync`\n- Hỗ trợ QoS 0 (Telemetry) và QoS 1 (Status, Command, LWT)\n\n**Người thực hiện:** Gia Hợp\n**Thư mục/File:** `Code/Shared/MqttHelper.cs`, `Code/Shared/Shared.csproj`",
        "labels": ["network", "mqttnet", "core"],
        "done": True
    },
    {
        "title": "Lập trình cơ chế Tự động kết nối lại (Auto-reconnect Backoff)",
        "body": "### Mô tả công việc\n- Cài đặt vòng lặp thử kết nối lại với Exponential Backoff (2s -> 4s -> 8s -> 16s -> 30s)\n- Tự động subscribe lại toàn bộ danh sách topic đã đăng ký sau khi kết nối lại thành công\n- Tự động phát lại thông điệp trạng thái 'online' với cờ retain\n\n**Người thực hiện:** Gia Hợp\n**Thư mục/File:** `Code/Shared/MqttHelper.cs`",
        "labels": ["network", "reliability"],
        "done": True
    },
    {
        "title": "Định nghĩa Data Contract JSON Protocol (Protocol.cs)",
        "body": "### Mô tả công việc\n- Định nghĩa cấu trúc các đối tượng `TelemetryMessage`, `DeviceStatusMessage`, `CommandMessage`\n- Viết hàm `ToJson()` và `FromJson()` hỗ trợ tuần tự hóa dữ liệu\n\n**Người thực hiện:** Gia Hợp\n**Thư mục/File:** `Code/Shared/Protocol.cs`",
        "labels": ["network", "protocol"],
        "done": True
    },
    {
        "title": "Cài đặt bộ lọc Message Trùng (Deduplication) và Đến Trễ (Out-of-order)",
        "body": "### Mô tả công việc\n- Lọc message trùng: Kiểm tra `message_id` trong danh sách cache (HashSet/LRU Cache 100 tin gần nhất), bỏ qua nếu đã xử lý\n- Lọc message đến trễ: So sánh `timestamp` của bản tin với thời gian bản tin gần nhất của thiết bị, không ghi đè dữ liệu realtime nếu tin bị gửi muộn\n\n**Người thực hiện:** Gia Hợp\n**Thư mục/File:** `Code/Dashboard/Controllers/MqttController.cs`, `Code/Shared/Protocol.cs`",
        "labels": ["network", "protocol", "feature"],
        "done": False
    },
    {
        "title": "Khởi tạo kịch bản Stress Test đo đạc hiệu năng MQTT",
        "body": "### Mô tả công việc\n- Viết script Python `Extra/scripts/stress_test.py` đo số lượng message gửi nhận và thông lượng (throughput) qua MQTT Broker\n\n**Người thực hiện:** Đình Thuận\n**Thư mục/File:** `Extra/scripts/stress_test.py`",
        "labels": ["testing", "performance"],
        "done": True
    },
    {
        "title": "Nâng cấp kịch bản test 2 mức tải & xuất kết quả vào test_results/",
        "body": "### Mô tả công việc\n- Bổ sung 2 kịch bản tải vào `stress_test.py`: Mức 1 (Tải nhẹ 500 msgs) và Mức 2 (Tải nặng 5.000 msgs)\n- Đo Throughput, Latency trung bình, Tỷ lệ xác nhận PUBACK\n- Xuất kết quả kèm thông số cấu hình máy vào thư mục `Extra/test_results/`\n\n**Người thực hiện:** Đình Thuận\n**Thư mục/File:** `Extra/scripts/stress_test.py`, `Extra/test_results/`",
        "labels": ["testing", "benchmark"],
        "done": False
    },
    {
        "title": "Soạn thảo Báo cáo BTL (.docx) và Slide thuyết trình (.pptx)",
        "body": "### Mô tả công việc\n- Viết Báo cáo chi tiết đồ án theo mẫu vào `DOCX/Bao_Cao_UDM_21_Giam_Sat_IoT_MQTT.docx` (mục tiêu, kiến trúc mạng, Topic Tree, phân tích QoS 0/1, Retained Message, Clean Session, LWT, kết quả đo đạc)\n- Soạn slide bảo vệ đồ án vào `PPTX/Slide_Thuyet_Trinh_UDM_21.pptx`\n\n**Người thực hiện:** Đình Thuận (cùng Nhóm)\n**Thư mục/File:** `DOCX/`, `PPTX/`",
        "labels": ["documentation", "report"],
        "done": False
    },
    {
        "title": "Quay Video Demo dự án (3–5 phút) và cập nhật README.md",
        "body": "### Mô tả công việc\n- Quay video demo minh họa các chức năng chạy thực tế (5 thiết bị, Dashboard real-time, gửi lệnh, cảnh báo ngưỡng, test rớt mạng/reconnect)\n- Tải video lên YouTube/Drive (Unlisted) và chèn link vào mục 2 của `README.md`\n- Điền đầy đủ Họ tên, MSSV, GitHub Account của 5 thành viên vào bảng mục 1 của `README.md`\n\n**Người thực hiện:** Đình Thuận (cùng Nhóm)\n**Thư mục/File:** `README.md`",
        "labels": ["documentation", "demo"],
        "done": False
    }
]

def create_via_gh():
    print("=== TẠO ISSUES QUA GITHUB CLI (gh) ===")
    for item in ISSUES:
        title = item["title"]
        body = item["body"]
        done = item["done"]

        cmd = ["gh", "issue", "create", "--repo", REPO, "--title", title, "--body", body]
        res = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
        if res.returncode == 0:
            issue_url = res.stdout.strip()
            print(f"[CREATED] {title} -> {issue_url}")
            if done:
                subprocess.run(["gh", "issue", "close", issue_url, "--comment", "Task đã hoàn thành trong mã nguồn hiện tại."], capture_output=True, text=True, encoding="utf-8")
                print(f"   └── [CLOSED / DONE]")
        else:
            print(f"[ERROR] Không thể tạo '{title}': {res.stderr.strip()}")

def create_via_token(token):
    print("=== TẠO ISSUES QUA GITHUB REST API ===")
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json",
        "User-Agent": "UDM21-Issue-Creator"
    }

    url = f"https://api.github.com/repos/{REPO}/issues"

    for item in ISSUES:
        title = item["title"]
        body = item["body"]
        labels = item["labels"]
        done = item["done"]

        data = {
            "title": title,
            "body": body,
            "labels": labels
        }
        
        req = urllib.request.Request(url, data=json.dumps(data).encode("utf-8"), headers=headers, method="POST")
        try:
            with urllib.request.urlopen(req) as response:
                resp_json = json.loads(response.read().decode("utf-8"))
                issue_number = resp_json["number"]
                issue_url = resp_json["html_url"]
                print(f"[CREATED #{issue_number}] {title} -> {issue_url}")

                if done:
                    patch_url = f"https://api.github.com/repos/{REPO}/issues/{issue_number}"
                    patch_data = {"state": "closed"}
                    patch_req = urllib.request.Request(patch_url, data=json.dumps(patch_data).encode("utf-8"), headers=headers, method="PATCH")
                    with urllib.request.urlopen(patch_req) as patch_resp:
                        print(f"   └── [CLOSED / DONE]")
        except urllib.error.HTTPError as e:
            print(f"[ERROR #{e.code}] {title}: {e.read().decode('utf-8')}")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1].startswith("ghp_"):
        create_via_token(sys.argv[1])
    else:
        # Check if gh is authenticated
        auth_check = subprocess.run(["gh", "auth", "status"], capture_output=True, text=True, encoding="utf-8")
        if auth_check.returncode == 0:
            create_via_gh()
        else:
            print("=================================================================")
            print("CHƯA ĐĂNG NHẬP GITHUB CLI!")
            print("Vui lòng chọn 1 trong 2 cách sau để đẩy 19 Issues lên GitHub:")
            print("-----------------------------------------------------------------")
            print("CÁCH 1 (Khuyên dùng - Đăng nhập GitHub CLI):")
            print("  1. Chạy lệnh:  gh auth login")
            print("  2. Chạy lệnh:  python Extra/scripts/create_github_issues.py")
            print("-----------------------------------------------------------------")
            print("CÁCH 2 (Sử dụng GitHub Personal Access Token):")
            print("  1. Tạo token tại: https://github.com/settings/tokens (quyền 'repo')")
            print("  2. Chạy lệnh:  python Extra/scripts/create_github_issues.py YOUR_TOKEN")
            print("=================================================================")
