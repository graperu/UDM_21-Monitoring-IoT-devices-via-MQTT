import time
import json
import paho.mqtt.client as mqtt
from datetime import datetime
import threading

BROKER = "localhost"
PORT = 1883
TOPIC = "udm21/stress/test"
TOTAL_MESSAGES = 1000
PAYLOAD_SIZE = 64

acked_count = 0
lock = threading.Lock()
all_acked_event = threading.Event()

def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print("Connected to MQTT Broker successfully.")
    else:
        print(f"Connection failed with code {rc}")

def on_publish(client, userdata, mid, reason_code=None, properties=None):
    global acked_count
    with lock:
        acked_count += 1
        if acked_count >= TOTAL_MESSAGES:
            all_acked_event.set()

client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, "StressTestClient")
client.on_connect = on_connect
client.on_publish = on_publish

client.connect(BROKER, PORT, 60)
client.loop_start()
time.sleep(0.3)  # doi CONNACK truoc khi bat dau do

payload_data = "x" * PAYLOAD_SIZE
start_time = time.time()

for i in range(TOTAL_MESSAGES):
    message = json.dumps({"index": i, "data": payload_data, "timestamp": time.time()})
    client.publish(TOPIC, message, qos=1)

# doi den khi TAT CA tin nhan duoc Broker xac nhan (PUBACK), toi da 10s
all_acked_event.wait(timeout=10)
end_time = time.time()

duration = end_time - start_time
throughput = acked_count / duration if duration > 0 else 0

print("\n--- TEST RESULTS ---")
print(f"Messages Acked: {acked_count}/{TOTAL_MESSAGES}")
print(f"Time Taken: {duration:.4f} seconds")
print(f"Throughput (thuc te, da xac nhan): {throughput:.2f} msg/sec")

client.loop_stop()
client.disconnect()
