@echo off
setlocal
set NITROGEN_ROOT=C:\Users\as123\Downloads\NitroGen-main\NitroGen-main
set NGPT=C:\better\BetterGIProWpf-App\Models\ng.pt
set PYTHONPATH=%NITROGEN_ROOT%
set HF_HUB_OFFLINE=1
set TRANSFORMERS_OFFLINE=1
if not exist "%NGPT%" (
  set HF_ENDPOINT=https://hf-mirror.com
  python -c "from huggingface_hub import hf_hub_download; hf_hub_download('nvidia/NitroGen','ng.pt',local_dir=r'C:\better\BetterGIProWpf-App\Models')"
)
start "nitrogen-serve" cmd /c "cd /d %NITROGEN_ROOT% && set PYTHONPATH=%NITROGEN_ROOT% && python scripts\serve.py \"%NGPT%\" --port 5555"
timeout /t 25 /nobreak >nul
start "nitrogen-bridge" cmd /c "cd /d C:\better\nitrogen_bridge && python nitrogen_bridge_server.py --zmq-port 5555 --http-port 5003"
echo NitroGen started: ZMQ 5555 + HTTP 5003
endlocal
