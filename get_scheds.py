import urllib.request
import json

data = json.dumps({"tool": "aigrav_check_schedules"}).encode("utf-8")
req = urllib.request.Request("http://127.0.0.1:8765/", data=data, headers={"Content-Type": "application/json"})
with urllib.request.urlopen(req, timeout=60) as resp:
    res = json.loads(resp.read().decode("utf-8"))
    with open("C:/AIGRAV/all_schedules.txt", "w", encoding="utf-8") as f:
        f.write(res.get("Result", ""))
print("Wrote all schedules to C:/AIGRAV/all_schedules.txt")
