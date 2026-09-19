"""HTTP(5003)<->ZMQ(5555) bridge to NitroGen serve.py."""
import argparse, base64, io, json, threading, pickle
import numpy as np, zmq
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer

ZMQ_PORT=5555; HTTP_PORT=5003; _ctx=None; _sock=None; _lock=threading.Lock()

def ensure():
    global _ctx,_sock
    if _sock is not None: return True
    try:
        _ctx=zmq.Context(); _sock=_ctx.socket(zmq.REQ); _sock.RCVTIMEO=300000
        _sock.connect(f"tcp://127.0.0.1:{ZMQ_PORT}"); return True
    except Exception: return False

def predict(img):
    with _lock:
        if not ensure(): raise RuntimeError("serve.py unreachable")
        _sock.send(pickle.dumps({"type":"predict","image":img}))
        r=pickle.loads(_sock.recv())
        if r.get("status")!="ok": raise RuntimeError(r.get("message","?"))
        return r["pred"]

def flatten(pred):
    out=[]
    for k in ("j_left","j_right","buttons"):
        a=pred.get(k)
        if a is None: continue
        out.extend(float(x) for x in np.asarray(a,dtype=np.float32).reshape(-1))
    while len(out)<336: out.append(0.0)
    return out[:336]

class H(BaseHTTPRequestHandler):
    def log_message(self,*a): pass
    def _j(self,c,o): b=json.dumps(o).encode(); self.send_response(c); self.send_header("Content-Type","application/json"); self.send_header("Content-Length",str(len(b))); self.end_headers(); self.wfile.write(b)
    def do_GET(self):
        if self.path in ("/health","/"):
            ok=ensure(); self._j(200 if ok else 503,{"status":"ok" if ok else "unreachable"})
        else: self._j(404,{"error":"?"})
    def do_POST(self):
        if self.path!="/predict": self._j(404,{"error":"?"}); return
        try:
            n=int(self.headers.get("Content-Length",0)); raw=json.loads(self.rfile.read(n).decode())
            im=Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")
            pred=predict(np.asarray(im,dtype=np.uint8))
            self._j(200,{"action":flatten(pred),"confidence":0.6,"source":"nitrogen-zmq"})
        except Exception as e: self._j(502,{"error":str(e)})

if __name__=="__main__":
    ap=argparse.ArgumentParser(); ap.add_argument("--zmq-port",type=int,default=5555); ap.add_argument("--http-port",type=int,default=5003)
    a=ap.parse_args(); ZMQ_PORT=a.zmq_port; HTTP_PORT=a.http_port; ensure()
    print(f"[bridge] HTTP {HTTP_PORT} -> ZMQ {ZMQ_PORT}",flush=True)
    HTTPServer(("127.0.0.1",HTTP_PORT),H).serve_forever()
