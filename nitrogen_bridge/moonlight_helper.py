"""Moonlight window discovery + preview. Usage: python moonlight_helper.py"""
import sys, base64, requests
BRIDGE = "http://127.0.0.1:5005"
def find_moonlight():
    r = requests.get(f"{BRIDGE}/windows", timeout=5).json()
    return [w for w in r["windows"] if any(k in w["title"].lower() for k in ["moonlight","sunshine","genshin"])]
if __name__ == "__main__":
    cands = find_moonlight()
    if not cands:
        print("No Moonlight window found"); sys.exit(1)
    hwnd = cands[0]["hwnd"]
    r = requests.post(f"{BRIDGE}/grab", json={"hwnd": hwnd}, timeout=10).json()
    open("preview.jpg","wb").write(base64.b64decode(r["frame_b64"]))
    print(f"OK {r['width']}x{r['height']} {r['ms']}ms")
