"""Parse inference.yml -> character dict txt."""
import urllib.request
url = "https://hf-mirror.com/PaddlePaddle/PP-OCRv5_server_rec_onnx/raw/main/inference.yml"
req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
yml = urllib.request.urlopen(req, timeout=60).read().decode("utf-8")
in_dict = False; chars = []
for line in yml.splitlines():
    if line.strip().startswith("character_dict:"): in_dict = True; continue
    if in_dict:
        if line.startswith("  - "): chars.append(line[4:])
        elif line and not line.startswith(" "): break
out = r"C:\better\BetterGIProWpf-App\Models\ppocrv5\rec\ppocr_keys_v1.txt"
open(out,"w",encoding="utf-8").write("\n".join(chars)+"\n")
print(f"Wrote {len(chars)} chars -> {out}")
