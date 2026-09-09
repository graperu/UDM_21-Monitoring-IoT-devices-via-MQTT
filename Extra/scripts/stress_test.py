import json
import os
import sys
import time

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

from test_runner import BROKER, PORT, TOPIC_ROOT, USE_TLS, MQTT_USERNAME, get_test_environment, run_stress_test


def main():
    print("===============================================================")
    print(" UDM_21 - STRESS/PERFORMANCE TEST MQTT QoS 1")
    print(f" Broker: {BROKER}:{PORT}")
    print("===============================================================")

    results = run_stress_test()
    report = {
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "broker": f"{BROKER}:{PORT}",
        "topic_root": TOPIC_ROOT,
        "tls": USE_TLS,
        "authenticated": bool(MQTT_USERNAME),
        "environment": get_test_environment(),
        "method": {
            "latency": "RTT từ lúc publish() đến callback PUBACK cho từng message",
            "throughput": "Số PUBACK nhận được chia cho tổng thời gian test",
            "pass_condition": "Nhận đủ PUBACK trước timeout hữu hạn tăng theo mức tải (tối đa 60 giây)",
        },
        "results": results,
        "passed": all(result["passed"] for result in results.values()),
    }

    os.makedirs("Extra/test_results", exist_ok=True)
    output_path = "Extra/test_results/stress_report.json"
    with open(output_path, "w", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)

    print(f"\nĐã lưu bằng chứng cấu hình máy và kết quả vào {output_path}")
    if not report["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
