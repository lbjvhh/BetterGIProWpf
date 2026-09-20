"""
Sunshine/Moonlight 实机联调脚本：
1. 检查 Sunshine 进程和端口
2. 启动 Moonlight 连接
3. 输出联调报告
"""
import subprocess, time, socket, sys, os

def check_sunshine_process():
    try:
        r = subprocess.run(["tasklist", "/FI", "IMAGENAME eq sunshine.exe"], capture_output=True, text=True)
        return "sunshine.exe" in r.stdout
    except:
        return False

def check_sunshine_port(host="127.0.0.1", port=47990, timeout=2):
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(timeout)
        s.connect((host, port))
        s.close()
        return True
    except:
        return False

def start_sunshine():
    sunshine_path = r"C:\Program Files\Sunshine\sunshine.exe"
    if not os.path.exists(sunshine_path): return False
    try:
        subprocess.Popen([sunshine_path], creationflags=subprocess.CREATE_NO_WINDOW)
        time.sleep(5)
        return check_sunshine_process()
    except: return False

def start_moonlight(host="127.0.0.1", app="Desktop"):
    moonlight_path = r"C:\Program Files\Moonlight Game Streaming\Moonlight.exe"
    if not os.path.exists(moonlight_path): return False
    try:
        subprocess.Popen([moonlight_path, "stream", host, app])
        time.sleep(3)
        return True
    except: return False

def check_moonlight_process():
    try:
        r = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Moonlight.exe"], capture_output=True, text=True)
        return "Moonlight.exe" in r.stdout
    except: return False

def main():
    print("=" * 60)
    print("Sunshine/Moonlight 联调测试")
    print("=" * 60)
    report = {}
    print("\n[1/5] 检查 Sunshine 进程...")
    report["sunshine_process"] = check_sunshine_process() or start_sunshine()
    print("  ✓" if report["sunshine_process"] else "  ✗")
    print("\n[2/5] 检查 Sunshine 端口 47990...")
    report["sunshine_port"] = check_sunshine_port()
    print("  ✓ web 面板可访问" if report["sunshine_port"] else "  ✗ 首次需初始化")
    print("\n[3/5] 检查 Moonlight 安装...")
    report["moonlight_installed"] = os.path.exists(r"C:\Program Files\Moonlight Game Streaming\Moonlight.exe")
    print("  ✓" if report["moonlight_installed"] else "  ✗")
    print("\n[4/5] 启动 Moonlight...")
    start_moonlight()
    time.sleep(5)
    report["moonlight_running"] = check_moonlight_process()
    print("  ✓" if report["moonlight_running"] else "  ✗")
    print("\n[5/5] 报告:")
    for k, v in report.items(): print(f"  {'✓' if v else '✗'} {k}")
    print(f"\n联调: {'全部通过' if all(report.values()) else '部分失败（首次需在浏览器初始化 Sunshine）'}")

if __name__ == "__main__":
    main()
