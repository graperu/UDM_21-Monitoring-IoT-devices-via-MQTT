import os
import sys
import time
import json
import uuid
import threading
import subprocess
import platform
import statistics
from importlib.metadata import version as package_version
from datetime import datetime, timezone
import paho.mqtt.client as mqtt

# Force UTF-8 on Windows
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

BROKER = os.environ.get("UDM21_TEST_BROKER", "broker.emqx.io")
PORT = int(os.environ.get("UDM21_TEST_PORT", "1883"))
TOPIC_ROOT = os.environ.get("UDM21_TEST_TOPIC_ROOT", "udm21_nhom01_test")
USE_TLS = os.environ.get("UDM21_TEST_TLS", "false").lower() in ("1", "true", "yes")
MQTT_USERNAME = os.environ.get("UDM21_TEST_USERNAME")
MQTT_PASSWORD = os.environ.get("UDM21_TEST_PASSWORD")
TEST_TIMEOUT = 35

def configure_mqtt_client(client):
    if MQTT_USERNAME:
        client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD or "")
    if USE_TLS:
        client.tls_set()

def get_total_memory_mb():
    if sys.platform != "win32":
        return None

    try:
        import ctypes

        class MemoryStatus(ctypes.Structure):
            _fields_ = [
                ("length", ctypes.c_ulong),
                ("memory_load", ctypes.c_ulong),
                ("total_physical", ctypes.c_ulonglong),
                ("available_physical", ctypes.c_ulonglong),
                ("total_page_file", ctypes.c_ulonglong),
                ("available_page_file", ctypes.c_ulonglong),
                ("total_virtual", ctypes.c_ulonglong),
                ("available_virtual", ctypes.c_ulonglong),
                ("available_extended_virtual", ctypes.c_ulonglong),
            ]

        status = MemoryStatus()
        status.length = ctypes.sizeof(MemoryStatus)
        ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status))
        return round(status.total_physical / 1024 / 1024)
    except Exception:
        return None

def get_test_environment():
    try:
        dotnet_version = subprocess.check_output(
            ["dotnet", "--version"], text=True, timeout=5
        ).strip()
    except Exception:
        dotnet_version = "unavailable"

    try:
        paho_version = package_version("paho-mqtt")
    except Exception:
        paho_version = "unavailable"

    return {
        "machine": platform.node(),
        "os": platform.platform(),
        "processor": platform.processor() or "unavailable",
        "logical_cpu_count": os.cpu_count(),
        "total_memory_mb": get_total_memory_mb(),
        "python_version": platform.python_version(),
        "dotnet_sdk_version": dotnet_version,
        "paho_mqtt_version": paho_version,
    }

def is_valid_telemetry(device_id, payload):
    try:
        uuid.UUID(str(payload["message_id"]))
        timestamp = datetime.fromisoformat(str(payload["timestamp"]).replace("Z", "+00:00"))
        expected = EXPECTED_DEVICES[device_id]
        return (
            timestamp.tzinfo is not None
            and timestamp.utcoffset() == timezone.utc.utcoffset(timestamp)
            and payload.get("device_id") == device_id
            and payload.get("device_type") == expected["type"]
            and payload.get("location") == expected["location"]
            and isinstance(payload.get("data"), dict)
            and len(payload["data"]) > 0
        )
    except (KeyError, TypeError, ValueError):
        return False

EXPECTED_DEVICES = {
    "temp_hum_01": {"type": "sensor", "location": "lab"},
    "air_quality_01": {"type": "sensor", "location": "factory"},
    "power_meter_01": {"type": "meter", "location": "home"},
    "smart_light_01": {"type": "light", "location": "home"},
    "door_sensor_01": {"type": "security", "location": "lab"}
}

test_results = {
    "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
    "broker": f"{BROKER}:{PORT}",
    "topic_root": TOPIC_ROOT,
    "tls": USE_TLS,
    "authenticated": bool(MQTT_USERNAME),
    "environment": get_test_environment(),
    "test_cases": {},
    "summary": {"total": 0, "passed": 0, "failed": 0}
}

received_statuses = {}
online_seen = set()
received_telemetries = {}
alert_detected = []
command_responded = threading.Event()
received_command_telemetry = None
lock = threading.Lock()

def on_connect(client, userdata, flags, rc, properties=None):
    print(f"[TEST RUNNER] Đã kết nối thành công tới MQTT Broker: {BROKER}:{PORT}")
    client.subscribe(f"{TOPIC_ROOT}/#", qos=1)
    print(f"[TEST RUNNER] Đã subscribe topic wildcard: {TOPIC_ROOT}/#")

def on_message(client, userdata, msg):
    global received_command_telemetry
    topic = msg.topic
    payload_str = msg.payload.decode("utf-8", errors="ignore")
    
    try:
        payload = json.loads(payload_str)
    except Exception:
        return

    with lock:
        if topic.endswith("/status"):
            dev_id = payload.get("device_id")
            status = payload.get("status")
            if dev_id:
                received_statuses[dev_id] = {
                    "status": status,
                    "timestamp": payload.get("timestamp"),
                    "message_id": payload.get("message_id")
                }
                if status == "online":
                    online_seen.add(dev_id)
                print(f"  [STATUS] Nhận trạng thái từ '{dev_id}': {status.upper()}")

        elif topic.endswith("/telemetry"):
            dev_id = payload.get("device_id")
            if dev_id:
                if dev_id not in received_telemetries:
                    received_telemetries[dev_id] = []
                received_telemetries[dev_id].append(payload)
                
                # Check anomaly threshold
                data = payload.get("data", {})
                temp = data.get("temperature")
                if temp and isinstance(temp, (int, float)) and temp > 40.0:
                    alert_detected.append({"device": dev_id, "temp": temp, "payload": payload})
                    print(f"  🚨 [CẢNH BÁO BẤT THƯỜNG] Thiết bị '{dev_id}' phát nhiệt độ cao: {temp}°C (> 40°C)")

                # Check command response for smart_light_01
                if dev_id == "smart_light_01" and data.get("state") == "ON":
                    received_command_telemetry = payload
                    command_responded.set()

def run_tests():
    print("=================================================================")
    print(" BẮT ĐẦU KIỂM THỬ TỰ ĐỘNG HỆ THỐNG UDM_21 (MQTT IoT)")
    print("=================================================================\n")

    # 1. Setup MQTT Test Client
    client_id = f"TestRunner_{uuid.uuid4().hex[:6]}"
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id)
    configure_mqtt_client(client)
    client.on_connect = on_connect
    client.on_message = on_message

    print(f"1. Kết nối Test Client ({client_id}) tới Broker...")
    client.connect(BROKER, PORT, 60)
    client.loop_start()
    time.sleep(2)

    # 2. Launch Simulators Process
    sim_exe = os.path.abspath("Code/Simulators/bin/Release/net8.0/Simulators.exe")
    print(f"2. Khởi chạy 5 Thiết bị giả lập từ: {sim_exe}")
    
    simulator_environment = os.environ.copy()
    if MQTT_USERNAME:
        simulator_environment["UDM21_MQTT_USERNAME"] = MQTT_USERNAME
        simulator_environment["UDM21_MQTT_PASSWORD"] = MQTT_PASSWORD or ""
    sim_args = [sim_exe, "--host", BROKER, "--port", str(PORT), "--topic-root", TOPIC_ROOT]
    if USE_TLS:
        sim_args.append("--tls")
    sim_proc = subprocess.Popen(sim_args,
                                stdin=subprocess.PIPE,
                                stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE,
                                text=True,
                                encoding="utf-8",
                                env=simulator_environment)

    print(f"3. Lắng nghe dữ liệu trong {TEST_TIMEOUT} giây...\n")

    # Wait for devices to come online
    time.sleep(6)

    # 4. Send Command Test to smart_light_01
    print("4. [TEST LỆNH ĐIỀU KHIỂN] Phát lệnh TOGGLE_POWER (state: ON) tới smart_light_01...")
    cmd_payload = {
        "message_id": str(uuid.uuid4()),
        "command": "TOGGLE_POWER",
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "params": {"state": "ON"}
    }
    client.publish(f"{TOPIC_ROOT}/home/light/smart_light_01/cmd", json.dumps(cmd_payload), qos=1)

    # Wait for alert and remaining data
    time.sleep(TEST_TIMEOUT - 8)

    # Stop simulators process cleanly
    print("\n5. Dừng tiến trình Simulators...")
    sim_proc.terminate()
    try:
        sim_proc.wait(timeout=3)
    except Exception:
        sim_proc.kill()

    client.loop_stop()
    client.disconnect()

    print("\n=================================================================")
    print(" ĐÁNH GIÁ VÀ TỔNG HỢP KẾT QUẢ TEST CASES")
    print("=================================================================")

    # Test Case 1: All 5 Devices Online Status
    online_count = sum(1 for device_id in EXPECTED_DEVICES if device_id in online_seen)
    tc1_pass = (online_count == 5)
    test_results["test_cases"]["TC1_5_Devices_Online"] = {
        "description": "5/5 Thiết bị IoT gửi trạng thái Online qua LWT Retained topic",
        "passed": tc1_pass,
        "details": f"{online_count}/5 thiết bị đã báo online",
        "devices_found": sorted(online_seen)
    }
    print(f"[*] TC1: Khởi tạo 5 thiết bị Online: {'[PASS]' if tc1_pass else '[FAIL]'} ({online_count}/5)")

    # Test Case 2: Telemetry Data Format & Attributes
    all_telemetry_valid = True
    for dev_id, msgs in received_telemetries.items():
        if not msgs:
            all_telemetry_valid = False
            break
        latest = msgs[-1]
        if not is_valid_telemetry(dev_id, latest):
            all_telemetry_valid = False
            break
    
    tc2_pass = (len(received_telemetries) == 5 and all_telemetry_valid)
    test_results["test_cases"]["TC2_Telemetry_Data_Format"] = {
        "description": "Dữ liệu Telemetry có đầy đủ MessageId GUID, Timestamp UTC và Data JSON",
        "passed": tc2_pass,
        "details": f"Đã nhận telemetry từ {len(received_telemetries)}/5 thiết bị",
        "samples": {d: received_telemetries[d][-1] for d in received_telemetries}
    }
    print(f"[*] TC2: Chuẩn hóa Telemetry JSON (GUID + UTC Time): {'[PASS]' if tc2_pass else '[FAIL]'}")

    # Test Case 3: Command Execution & Instant State Reflection
    tc3_pass = command_responded.is_set()
    test_results["test_cases"]["TC3_Command_Execution_SmartLight"] = {
        "description": "Thiết bị smart_light_01 nhận lệnh TOGGLE_POWER và phản hồi state=ON tức thì",
        "passed": tc3_pass,
        "details": received_command_telemetry if tc3_pass else "Không nhận được phản hồi lệnh kịp thời"
    }
    print(f"[*] TC3: Gửi & Phản hồi lệnh điều khiển Real-time: {'[PASS]' if tc3_pass else '[FAIL]'}")

    # Test Case 4: Anomaly Detection (Temperature > 40°C)
    tc4_pass = (len(alert_detected) > 0)
    test_results["test_cases"]["TC4_Anomaly_Detection_Alert"] = {
        "description": "Thiết bị temp_hum_01 phát số liệu bất thường (> 40°C) để kích hoạt cảnh báo",
        "passed": tc4_pass,
        "details": alert_detected
    }
    print(f"[*] TC4: Giả lập và phát hiện cảnh báo vượt ngưỡng: {'[PASS]' if tc4_pass else '[FAIL]'} ({len(alert_detected)} lần)")

    # 6. Stress Testing (2 Levels)
    print("\n-----------------------------------------------------------------")
    print(" 6. CHẠY KIỂM THỬ TẢI & ĐO ĐẠC HIỆU NĂNG (2 MỨC TẢI)")
    print("-----------------------------------------------------------------")
    
    stress_results = run_stress_test()
    test_results["stress_test"] = stress_results
    test_results["test_cases"]["TC5_Stress_Test_Performance"] = {
        "description": "Đo đạc Throughput và Latency ở 2 mức tải: 500 msgs và 2000 msgs",
        "passed": all(result["passed"] for result in stress_results.values()),
        "results": stress_results
    }

    # Summary
    total_tcs = len(test_results["test_cases"])
    passed_tcs = sum(1 for tc in test_results["test_cases"].values() if tc["passed"])
    test_results["summary"]["total"] = total_tcs
    test_results["summary"]["passed"] = passed_tcs
    test_results["summary"]["failed"] = total_tcs - passed_tcs

    # Save to file
    os.makedirs("Extra/test_results", exist_ok=True)
    report_file = "Extra/test_results/test_report.json"
    with open(report_file, "w", encoding="utf-8") as f:
        json.dump(test_results, f, indent=2, ensure_ascii=False)

    print(f"\n[XONG] Đã lưu báo cáo kết quả kiểm thử vào: {report_file}")
    print(f"TỔNG KẾT: {passed_tcs}/{total_tcs} Test Cases PASSED ({passed_tcs/total_tcs*100:.1f}%)")
    if passed_tcs != total_tcs:
        raise SystemExit(1)

def run_stress_test():
    results = {}
    
    for level, count in [("Muc_1_Tai_Nhe_500_msgs", 500), ("Muc_2_Tai_Nang_2000_msgs", 2000)]:
        print(f"-> Đang chạy kiểm thử: {level} ({count} messages, QoS 1)...")
        acked = 0
        ack_lock = threading.Lock()
        done_event = threading.Event()
        send_times = {}
        latencies_ms = []

        def on_pub(cli, userdata, mid, reason_code=None, properties=None):
            nonlocal acked
            with ack_lock:
                acked += 1
                sent_at = send_times.pop(mid, None)
                if sent_at is not None:
                    latencies_ms.append((time.perf_counter() - sent_at) * 1000)
                if acked >= count:
                    done_event.set()

        stress_cli = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, f"StressCli_{level}_{uuid.uuid4().hex[:4]}")
        configure_mqtt_client(stress_cli)
        stress_cli.on_publish = on_pub
        stress_cli.connect(BROKER, PORT, 60)
        stress_cli.loop_start()
        time.sleep(0.5)

        start_time = time.perf_counter()
        for i in range(count):
            msg_data = {"id": i, "timestamp": time.time(), "data": "x" * 64}
            with ack_lock:
                sent_at = time.perf_counter()
                info = stress_cli.publish(f"{TOPIC_ROOT}/test/stress", json.dumps(msg_data), qos=1)
                send_times[info.mid] = sent_at

        # Timeout tăng theo tải nhưng luôn hữu hạn; public broker có thể throttle QoS 1.
        timeout_seconds = min(60, max(15, count / 40))
        completed = done_event.wait(timeout=timeout_seconds)
        duration = time.perf_counter() - start_time

        stress_cli.loop_stop()
        stress_cli.disconnect()

        with ack_lock:
            acked_snapshot = acked
            latency_snapshot = list(latencies_ms)

        throughput = acked_snapshot / duration if duration > 0 else 0
        avg_latency_ms = statistics.fmean(latency_snapshot) if latency_snapshot else 0
        sorted_latencies = sorted(latency_snapshot)
        p95_index = max(0, int(len(sorted_latencies) * 0.95) - 1)
        p95_latency_ms = sorted_latencies[p95_index] if sorted_latencies else 0
        passed = completed and acked_snapshot == count

        res_info = {
            "total_sent": count,
            "acked_count": acked_snapshot,
            "duration_sec": round(duration, 3),
            "throughput_msg_sec": round(throughput, 2),
            "avg_latency_ms": round(avg_latency_ms, 2),
            "p95_latency_ms": round(p95_latency_ms, 2),
            "success_rate_pct": round(acked_snapshot / count * 100, 2),
            "timeout_sec": timeout_seconds,
            "passed": passed,
        }
        results[level] = res_info
        print(f"   [KẾT QUẢ] Acked: {acked_snapshot}/{count} ({res_info['success_rate_pct']}%) | Time: {duration:.2f}s | Throughput: {throughput:.2f} msg/s | Avg RTT: {avg_latency_ms:.2f}ms | P95 RTT: {p95_latency_ms:.2f}ms | {'PASS' if passed else 'FAIL'}")

    return results

if __name__ == "__main__":
    run_tests()
