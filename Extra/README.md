# Thư mục Extra

Thư mục chứa các tài liệu bổ trợ, nhật ký chạy ứng dụng (logs), kịch bản kiểm thử và kết quả kiểm thử.

## Cấu trúc:
- `logs/`: Nơi chứa file log hệ thống trong quá trình thử nghiệm.
- `test_results/`: Nơi lưu kết quả kiểm thử chức năng, stress test, performance test (hình ảnh, log test, dữ liệu CSV/JSON).
- `scripts/`: Nơi lưu các script hỗ trợ đo độ trễ, throughput, sinh dữ liệu tự động.

## Chạy kiểm thử

```bash
dotnet test Code/UDM_21.sln
python Extra/scripts/test_runner.py
python Extra/scripts/stress_test.py
```

- `dotnet test` chạy unit/integration test với MQTT broker cục bộ được dựng tự động.
- `stress_test.py` đo RTT từ `publish()` đến PUBACK của từng message ở 2 mức tải.
- Có thể đổi broker/topic bằng `UDM21_TEST_BROKER`, `UDM21_TEST_PORT` và `UDM21_TEST_TOPIC_ROOT`.
- Cấu hình OS, CPU, RAM, runtime và kết quả E2E mới nhất được lưu trong `test_results/test_report.json`.
