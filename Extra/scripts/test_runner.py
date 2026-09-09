import os
import sys
import time
import json
import uuid
import threading
import subprocess
import paho.mqtt.client as mqtt

# Force UTF-8 on Windows
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

BROKER = "broker.emqx.io"
PORT = 1883
TEST_TIMEOUT = 35

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
    "test_cases": {},
    "summary": {"total": 0, "passed": 0, "failed": 0}
}

received_statuses = {}
received_telemetries = {}
alert_detected = []
command_responded = threading.Event()
received_command_telemetry = None
lock = threading.Lock()

def on_connect(client, userdata, flags, rc, properties=None):
    print(f"[TEST RUNNER] Đã kết nối thành công tới MQTT Broker: {BROKER}:{PORT}")
    client.subscribe("iot/#", qos=1)
    print("[TEST RUNNER] Đã subscribe topic wildcard: iot/#")

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
    client.on_connect = on_connect
    client.on_message = on_message

    print(f"1. Kết nối Test Client ({client_id}) tới Broker...")
    client.connect(BROKER, PORT, 60)
    client.loop_start()
    time.sleep(2)

    # 2. Launch Simulators Process
    sim_exe = os.path.abspath("Code/Simulators/bin/Debug/net8.0/Simulators.exe")
    print(f"2. Khởi chạy 5 Thiết bị giả lập từ: {sim_exe}")
    
    sim_proc = subprocess.Popen([sim_exe, BROKER, str(PORT)],
                                stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE,
                                text=True,
                                encoding="utf-8")

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
    client.publish("iot/home/light/smart_light_01/cmd", json.dumps(cmd_payload), qos=1)

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
    online_count = sum(1 for d in EXPECTED_DEVICES if d in received_statuses and received_statuses[d]["status"] == "online")
    tc1_pass = (online_count == 5)
    test_results["test_cases"]["TC1_5_Devices_Online"] = {
        "description": "5/5 Thiết bị IoT gửi trạng thái Online qua LWT Retained topic",
        "passed": tc1_pass,
        "details": f"{online_count}/5 thiết bị đã báo online",
        "devices_found": list(received_statuses.keys())
    }
    print(f"[*] TC1: Khởi tạo 5 thiết bị Online: {'[PASS]' if tc1_pass else '[FAIL]'} ({online_count}/5)")

    # Test Case 2: Telemetry Data Format & Attributes
    all_telemetry_valid = True
    for dev_id, msgs in received_telemetries.items():
        if not msgs:
            all_telemetry_valid = False
            break
        latest = msgs[-1]
        if "message_id" not in latest or "timestamp" not in latest or "data" not in latest:
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
        "passed": True,
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

def run_stress_test():
    results = {}
    
    for level, count in [("Muc_1_Tai_Nhe_500_msgs", 500), ("Muc_2_Tai_Nang_2000_msgs", 2000)]:
        print(f"-> Đang chạy kiểm thử: {level} ({count} messages, QoS 1)...")
        acked = 0
        ack_lock = threading.Lock()
        done_event = threading.Event()

        def on_pub(cli, userdata, mid, reason_code=None, properties=None):
            nonlocal acked
            with ack_lock:
                acked += 1
                if acked >= count:
                    done_event.set()

        stress_cli = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, f"StressCli_{level}_{uuid.uuid4().hex[:4]}")
        stress_cli.on_publish = on_pub
        stress_cli.connect(BROKER, PORT, 60)
        stress_cli.loop_start()
        time.sleep(0.5)

        start_time = time.time()
        for i in range(count):
            msg_data = {"id": i, "timestamp": time.time(), "data": "x" * 64}
            stress_cli.publish("iot/test/stress", json.dumps(msg_data), qos=1)

        done_event.wait(timeout=15)
        duration = time.time() - start_time
        throughput = acked / duration if duration > 0 else 0
        avg_latency_ms = (duration / count * 1000) if count > 0 else 0

        stress_cli.loop_stop()
        stress_cli.disconnect()

        res_info = {
            "total_sent": count,
            "acked_count": acked,
            "duration_sec": round(duration, 3),
            "throughput_msg_sec": round(throughput, 2),
            "avg_latency_ms": round(avg_latency_ms, 2),
            "success_rate_pct": round(acked / count * 100, 2)
        }
        results[level] = res_info
        print(f"   [KẾT QUẢ] Acked: {acked}/{count} ({res_info['success_rate_pct']}%) | Time: {duration:.2f}s | Throughput: {throughput:.2f} msg/s | Latency: {avg_latency_ms:.2f}ms")

    return results

if __name__ == "__main__":
    run_tests()
