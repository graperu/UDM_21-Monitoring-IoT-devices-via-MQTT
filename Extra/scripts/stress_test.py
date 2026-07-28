"""
Script skeleton for performing MQTT Stress Test and Performance Test.
Measures latency, throughput, and error rate under high load.
"""

import time
import json
import paho.mqtt.client as mqtt

BROKER = "localhost"
PORT = 1883
NUM_MESSAGES = 1000
TOPIC = "iot/stress_test/sensor/temp_01/telemetry"


def run_stress_test():
    print(f"Starting Stress Test: Sending {NUM_MESSAGES} messages to {BROKER}:{PORT}...")
    client = mqtt.Client(client_id="stress_tester")
    client.connect(BROKER, PORT, 60)
    client.loop_start()

    start_time = time.time()
    sent_count = 0

    for i in range(NUM_MESSAGES):
        payload = json.dumps({"seq": i, "timestamp": time.time(), "val": 36.5})
        client.publish(TOPIC, payload, qos=0)
        sent_count += 1

    elapsed = time.time() - start_time
    throughput = sent_count / elapsed if elapsed > 0 else 0

    print("--- Stress Test Results ---")
    print(f"Total Sent: {sent_count} messages")
    print(f"Total Time: {elapsed:.3f} seconds")
    print(f"Throughput: {throughput:.2f} msg/sec")

    client.loop_stop()
    client.disconnect()


if __name__ == "__main__":
    run_stress_test()
