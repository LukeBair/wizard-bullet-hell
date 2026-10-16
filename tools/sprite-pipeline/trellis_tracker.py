"""Live progress page for TRELLIS generations, plus a viewer for what came out of them.
Reads the logs that `python generate.py ... > x.log 2>&1` writes and shows each run's stage, step, speed
and an overall estimate. Images waiting in enemies/inputs (trellis_queue.sh) show as queued. Open a card
to orbit the GLB in 3D and play the sprite sheets made from it (enemies/out/enemy_<name>_sheet*.json).
Usage: python trellis_tracker.py [log_dir ...] [--port 8765]
Default log dirs: ~/Code/trellis-mac and ~/Code/trellis-mac/enemies (any *.log that TRELLIS wrote)."""
import glob, http.server, json, os, re, sys, time, urllib.parse

args = sys.argv[1:]
port = 8765
if "--port" in args:
    i = args.index("--port"); port = int(args[i + 1]); del args[i:i + 2]
home = os.path.expanduser("~/Code/trellis-mac")
DIRS = [os.path.abspath(os.path.expanduser(d)) for d in args] or [home, os.path.join(home, "enemies")]

# Stages in log order with their rough share of the whole run (from an M4 Pro, 1024_cascade).
STAGES = [
    ("load", "Loading model", 0.25),
    ("sparse", "Sparse structure", 0.10),
    ("shape1", "Shape, pass 1", 0.08),
    ("shape2", "Shape, pass 2", 0.08),
    ("shape3", "Shape, pass 3", 0.09),
    ("texture", "Texture", 0.25),
    ("bake", "Baking textures", 0.15),
]
TQDM = re.compile(r"(Loading weights|Sampling sparse structure|Sampling shape SLat|Sampling texture SLat)"
                  r":\s+(\d+)%\|[^|]*\|\s*(\d+)/(\d+) \[([\d:]+)<([\d:?]+),\s*([\d.?]+)(s/it|it/s)")


def parse(path):
    raw = open(path, errors="replace").read()
    queued_input = os.path.join(INPUTS, os.path.basename(path)[:-4] + ".png")
    if "Loading pipeline" not in raw[:4000] and not os.path.isfile(queued_input):
        return None
    lines = raw.replace("\r", "\n").split("\n")
    st = os.stat(path)
    started = getattr(st, "st_birthtime", st.st_ctime)
    info = {"name": os.path.basename(path)[:-4], "path": path, "started": started, "updated": st.st_mtime,
            "input": None, "stage": "load", "step": None, "total": None, "rate": None, "eta": None,
            "state": "running", "mesh": None, "gen_time": None, "bake_time": None, "output": None}
    if os.path.isfile(queued_input):
        info["input"] = queued_input
    shape_seen, last_shape_step = 0, None
    for ln in lines:
        m = re.search(r"Input: (\S+)", ln)
        if m:
            info["input"] = m.group(1)
        if "Sampling sparse structure" in ln:
            info["stage"] = "sparse"
        if "Sampling shape SLat" in ln:
            m = TQDM.search(ln)
            step = int(m.group(3)) if m else None
            if step == 0 and last_shape_step != 0:                  # a progress bar restarted
                shape_seen += 1
            last_shape_step = step if step is not None else last_shape_step
            info["stage"] = f"shape{min(max(shape_seen, 1), 3)}"
        if "Sampling texture SLat" in ln:
            info["stage"] = "texture"
        if ln.startswith("Mesh:"):
            info["mesh"] = ln[5:].strip()
        m = re.search(r"Generation time: ([\d.]+)s", ln)
        if m:
            info["gen_time"] = float(m.group(1))
        if "Baking PBR" in ln:
            info["stage"] = "bake"; info["step"] = info["total"] = None
        m = re.search(r"Bake time: ([\d.]+)s", ln)
        if m:
            info["bake_time"] = float(m.group(1))
        m = re.search(r"Saved: (\S+\.glb)", ln)
        if m:
            info["output"] = m.group(1)
        m = TQDM.search(ln)
        if m and info["stage"] != "bake":
            info["step"], info["total"] = int(m.group(3)), int(m.group(4))
            r = m.group(7)
            if r != "?":
                info["rate"] = f"{r}{m.group(8)}"
            info["eta"] = m.group(6)
    tail = "\n".join(lines[-40:])
    if re.search(r"Saved: \S+\.obj", raw) or ("Total time" in raw):
        info["state"] = "done"; info["stage"] = "done"
    elif "Traceback" in tail or "Error" in tail.split("Saved")[-1][-600:]:
        info["state"] = "failed"
        info["error"] = [l for l in lines if l.strip()][-1][:300]
    elif time.time() - st.st_mtime > 300:
        info["state"] = "stalled"

    # overall fraction
    keys = [s[0] for s in STAGES]
    if info["state"] == "done":
        frac = 1.0
    else:
        cur = keys.index(info["stage"]) if info["stage"] in keys else 0
        frac = sum(s[2] for s in STAGES[:cur])
        w = STAGES[cur][2]
        if info["step"] is not None and info["total"]:
            frac += w * info["step"] / info["total"]
        elif info["stage"] == "load":
            frac += w * min(1.0, (time.time() - started) / 90)      # weights take ~90s
        elif info["stage"] == "bake":
            frac += w * 0.5
    info["fraction"] = round(frac, 4)
    info["stage_label"] = dict((s[0], s[1]) for s in STAGES).get(info["stage"], "Done")
    elapsed = (info["updated"] if info["state"] == "done" else time.time()) - started
    info["elapsed"] = elapsed
    info["eta_total"] = elapsed * (1 - frac) / frac if 0.05 < frac < 1 and info["state"] == "running" else None
    return info



SHEETS = os.path.join(home, "enemies", "out")
INPUTS = os.path.join(home, "enemies", "inputs")


def sheets_for(name):
    out = []
    for j in sorted(glob.glob(os.path.join(SHEETS, f"enemy_{name}_sheet*.json"))):
        png = j[:-5] + ".png"
        if not os.path.isfile(png):
            continue
        d = json.load(open(j))
        px = d["frames"][0]["frame"]["w"]
        clips = {t["name"]: [d["frames"][i]["frame"] for i in range(t["from"], t["to"] + 1)] for t in d["meta"]["frameTags"]}
        out.append({"px": px, "png": png, "json": j, "ms": d["frames"][0]["duration"], "clips": clips,
                    "size": d["meta"]["size"]})
    return sorted(out, key=lambda s: s["px"])


def runs():
    seen, out = set(), []
    for d in DIRS:
        for p in glob.glob(os.path.join(d, "*.log")):
            if p in seen:
                continue
            seen.add(p)
            try:
                r = parse(p)
            except OSError:
                r = None
            if r:
                out.append(r)
    names = {r["name"] for r in out}
    for img in sorted(glob.glob(os.path.join(INPUTS, "*.png"))):         # waiting in the queue
        n = os.path.basename(img)[:-4]
        if n not in names and not os.path.isfile(os.path.join(home, "enemies", n + ".glb")):
            out.append({"name": n, "path": img, "input": img, "state": "queued", "stage": "queued",
                        "stage_label": "Queued", "fraction": 0, "started": 0, "elapsed": None})
            names.add(n)
    for j in glob.glob(os.path.join(SHEETS, "enemy_*_sheet*.json")):     # sheets with no TRELLIS run (2D)
        n = re.match(r"enemy_(.+?)_sheet", os.path.basename(j)).group(1)
        if n not in names:
            out.append({"name": n, "path": j, "input": None, "state": "2d", "stage": "done", "stage_label": "2D only",
                        "fraction": 1, "started": 0, "elapsed": None})
            names.add(n)
    for r in out:
        g = r.get("output")
        if g:
            g = g if os.path.isabs(g) else os.path.join(home, g)
            r["glb"] = g if os.path.isfile(g) else None
        r["sheets"] = sheets_for(r["name"])
    order = {"running": 0, "queued": 1, "stalled": 2, "failed": 2, "done": 3, "2d": 4}
    out.sort(key=lambda r: (order.get(r["state"], 5), -r["started"] if r["state"] != "queued" else r["name"]))
    return out


def allowed(p):
    p = os.path.realpath(p)
    roots = [os.path.realpath(home), os.path.realpath(os.path.expanduser("~/Downloads"))]   # inputs can live in Downloads
    return any(p.startswith(r + os.sep) for r in roots) and p.lower().endswith((".glb", ".png", ".jpg", ".json"))


PAGE = r"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>TRELLIS Tracker</title>
<style>
:root{--bg:#17131f;--card:#221c2e;--line:#3a3150;--text:#ece6f5;--dim:#9d93b3;--acc:#b58cff;--ok:#7fd69a;--bad:#ff7a85;--warn:#ffc46b}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.45 -apple-system,system-ui,sans-serif}
main{max-width:900px;margin:0 auto;padding:24px 16px 60px}h1{font-size:20px;margin:0 0 4px}
.sub{color:var(--dim);font-size:13px;margin-bottom:20px}
.card{display:flex;gap:16px;background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px;margin-bottom:12px}
.card.open-able{cursor:pointer}.card.open-able:hover{border-color:var(--acc)}
.thumb{width:84px;height:84px;flex:none;border-radius:8px;background:#0e0b14 center/contain no-repeat;image-rendering:auto}
.body{flex:1;min-width:0}.top{display:flex;justify-content:space-between;gap:8px;align-items:baseline}
.name{font-weight:600;font-size:16px;overflow:hidden;text-overflow:ellipsis}.pill{font-size:12px;padding:2px 8px;border-radius:99px;border:1px solid currentColor;white-space:nowrap}
.running{color:var(--acc)}.done,.2d{color:var(--ok)}.failed{color:var(--bad)}.stalled{color:var(--warn)}.queued{color:var(--dim)}
.card .name,.card .meta b{color:var(--text)}
.bar{height:10px;background:#0e0b14;border-radius:99px;overflow:hidden;margin:10px 0 6px}
.fill{height:100%;background:var(--acc)}.card.done .fill,.card.\32 d .fill{background:var(--ok)}
.meta{display:flex;flex-wrap:wrap;gap:4px 16px;color:var(--dim);font-size:13px}.meta b{font-weight:500}
.stages{display:flex;flex-wrap:wrap;gap:6px;margin-top:8px}.st{font-size:11px;padding:1px 7px;border-radius:6px;background:#0e0b14;color:var(--dim)}
.st.on{background:var(--acc);color:#17131f}.st.past{color:var(--ok)}.empty{color:var(--dim);text-align:center;padding:40px}
.err{color:var(--bad);font-size:12px;margin-top:6px;word-break:break-all}
#detail{position:fixed;inset:0;background:var(--bg);overflow:auto;display:none;z-index:5}
#detail.show{display:block}.dh{display:flex;align-items:center;gap:12px;padding:14px 16px;border-bottom:1px solid var(--line);position:sticky;top:0;background:var(--bg);z-index:2}
.dh h2{margin:0;font-size:18px;flex:1}button,select{background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:8px;padding:6px 12px;font:inherit;cursor:pointer}
button.on{background:var(--acc);color:#17131f;border-color:var(--acc)}
.dgrid{display:grid;grid-template-columns:1fr 1fr;gap:16px;padding:16px;max-width:1300px;margin:0 auto}
@media(max-width:800px){.dgrid{grid-template-columns:1fr}}
.panel{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px;min-width:0}
.panel h3{margin:0 0 10px;font-size:14px;color:var(--dim);font-weight:500}
#view3d{width:100%;aspect-ratio:1;border-radius:8px;background:#0e0b14;display:block;touch-action:none}
.ctl{display:flex;flex-wrap:wrap;gap:6px;align-items:center;margin:8px 0}.ctl label{color:var(--dim);font-size:13px}
.compass{display:grid;grid-template-columns:repeat(3,1fr);gap:4px;background:#0e0b14;border-radius:8px;padding:6px}
.compass canvas{width:100%;image-rendering:pixelated;background:#2a2238;border-radius:4px}
.compass .mid{display:flex;align-items:center;justify-content:center;color:var(--dim);font-size:12px}
.big canvas{width:100%;max-width:384px;image-rendering:pixelated;background:#2a2238;border-radius:8px;display:block;margin:0 auto}
.sheetimg{width:100%;image-rendering:pixelated;background:#2a2238 repeating-conic-gradient(#2a2238 0 25%,#241d31 0 50%) 0 0/16px 16px;border-radius:8px;display:block}
.hint{color:var(--dim);font-size:12px;margin-top:6px}
</style></head><body><main><h1>TRELLIS Tracker</h1><div class="sub" id="sub">Watching…</div><div id="list"></div></main>
<div id="detail"><div class="dh"><button id="back">← Back</button><h2 id="dname"></h2><span id="dstate" class="pill"></span></div>
<div class="dgrid">
 <div class="panel"><h3>Model</h3><canvas id="view3d"></canvas>
  <div class="ctl"><button id="spin" class="on">Auto-rotate</button><button id="tilt">Game camera</button><label>Light <input id="light" type="range" min="0" max="4" step="0.1" value="1.6"></label></div>
  <div class="hint" id="mhint">Drag to orbit, scroll or pinch to zoom.</div></div>
 <div class="panel"><h3>Animation</h3>
  <div class="ctl"><label>Size</label><span id="sizes"></span><label style="margin-left:8px">Speed</label><select id="spd"><option value="0.5">0.5×</option><option value="1" selected>1×</option><option value="2">2×</option></select></div>
  <div class="compass" id="compass"></div>
  <div class="hint">All eight facings, playing at in-game timing. Click one to enlarge it.</div>
  <div class="big" id="big" style="margin-top:10px"></div></div>
 <div class="panel" style="grid-column:1/-1"><h3>Sprite sheet</h3><img id="sheetimg" class="sheetimg" alt="sheet"><div class="hint" id="sheetinfo"></div></div>
</div></div>
<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js","three/addons/":"https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/"}}</script>
<script type="module">
import * as THREE from "three";
import {OrbitControls} from "three/addons/controls/OrbitControls.js";
import {GLTFLoader} from "three/addons/loaders/GLTFLoader.js";
const ST=[["load","Load"],["sparse","Sparse"],["shape1","Shape 1"],["shape2","Shape 2"],["shape3","Shape 3"],["texture","Texture"],["bake","Bake"]];
const fmt=s=>{if(s==null)return"–";s=Math.round(s);const m=Math.floor(s/60);return m?`${m}m ${String(s%60).padStart(2,"0")}s`:`${s}s`};
const esc=s=>String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const F=p=>"/file?p="+encodeURIComponent(p);
let RUNS=[],openName=null;
async function tick(){
  let d;try{d=await (await fetch("/api/runs")).json()}catch(e){document.getElementById("sub").textContent="Tracker server not reachable";return}
  RUNS=d.runs;const c=s=>RUNS.filter(r=>r.state==s).length;
  document.getElementById("sub").textContent=`${c("running")} running · ${c("queued")} queued · ${c("done")} modelled · ${RUNS.filter(r=>r.sheets.length).length} with sprite sheets`;
  const L=document.getElementById("list");
  if(!RUNS.length){L.innerHTML='<div class="empty">No TRELLIS runs yet.</div>';return}
  L.innerHTML=RUNS.map((r,ix)=>{
    const idx=ST.findIndex(s=>s[0]==r.stage);const pct=Math.round(r.fraction*100);
    const img=r.input?F(r.input.startsWith("/")?r.input:"__home__/"+r.input):(r.sheets[0]?F(r.sheets[0].png):"");
    const thumb=img?`style="background-image:url('${img}')"`:"";
    const stages=(r.state=="2d"||r.state=="queued")?"":ST.map((s,i)=>`<span class="st ${r.state=="done"||i<idx?"past":i==idx?"on":""}">${s[1]}</span>`).join("");
    const step=r.step!=null?` <b>${r.step}/${r.total}</b>`:"";
    const can=r.glb||r.sheets.length;
    const pill=r.state=="running"?pct+"%":r.state=="2d"?"2D":r.state;
    return `<div class="card ${r.state} ${can?"open-able":""}" data-i="${ix}"><div class="thumb" ${thumb}></div><div class="body">
      <div class="top"><span class="name">${esc(r.name)}</span><span class="pill ${r.state}">${pill}</span></div>
      ${r.state=="2d"?"":`<div class="bar"><div class="fill" style="width:${pct}%"></div></div>`}
      <div class="meta"><span>Stage <b>${esc(r.stage_label)}</b>${step}</span>
        ${r.rate&&r.state=="running"?`<span>Speed <b>${esc(r.rate)}</b></span>`:""}
        ${r.elapsed!=null?`<span>Elapsed <b>${fmt(r.elapsed)}</b></span>`:""}
        ${r.eta_total?`<span>About <b>${fmt(r.eta_total)}</b> left</span>`:""}
        ${r.gen_time?`<span>Generation <b>${fmt(r.gen_time)}</b></span>`:""}
        ${r.glb?`<span><b>3D model</b></span>`:""}
        ${r.sheets.length?`<span>Sheets <b>${r.sheets.map(s=>s.px+"px").join(", ")}</b></span>`:""}
        ${can?`<span style="color:var(--acc)">Open →</span>`:""}</div>
      ${stages?`<div class="stages">${stages}</div>`:""}${r.error?`<div class="err">${esc(r.error)}</div>`:""}</div></div>`}).join("");
  L.querySelectorAll(".card.open-able").forEach(el=>el.onclick=()=>openRun(RUNS[+el.dataset.i]));
}
tick();setInterval(()=>{if(!openName)tick()},2000);

// ---------- 3D viewer ----------
const cv=document.getElementById("view3d");
const renderer=new THREE.WebGLRenderer({canvas:cv,antialias:true,alpha:true});
renderer.outputColorSpace=THREE.SRGBColorSpace;
const scene=new THREE.Scene();
const cam=new THREE.PerspectiveCamera(35,1,0.01,100);
const hemi=new THREE.HemisphereLight(0xdde4ff,0x302040,1.6);scene.add(hemi);
const sun=new THREE.DirectionalLight(0xffffff,1.6);sun.position.set(2,3,2);scene.add(sun);
const controls=new OrbitControls(cam,cv);controls.enableDamping=true;controls.autoRotate=true;controls.autoRotateSpeed=2;
let model=null;const loader=new GLTFLoader();
function resize(){const w=cv.clientWidth;if(w&&cv.width!==Math.round(w*devicePixelRatio)){renderer.setPixelRatio(devicePixelRatio);renderer.setSize(w,w,false);cam.aspect=1;cam.updateProjectionMatrix()}}
function loop(){requestAnimationFrame(loop);if(!openName)return;resize();controls.update();renderer.render(scene,cam)}loop();
document.getElementById("spin").onclick=e=>{controls.autoRotate=!controls.autoRotate;e.target.classList.toggle("on",controls.autoRotate)};
document.getElementById("light").oninput=e=>{hemi.intensity=sun.intensity=+e.target.value};
let fitR=1;
document.getElementById("tilt").onclick=()=>{const d=fitR*3.2,t=25*Math.PI/180;cam.position.set(0,Math.sin(t)*d,Math.cos(t)*d);controls.target.set(0,0,0);controls.autoRotate=false;document.getElementById("spin").classList.remove("on")};
function loadModel(url){
  if(model){scene.remove(model);model=null}
  const hint=document.getElementById("mhint");
  if(!url){hint.textContent="No 3D model for this one (2D only, or still generating).";return}
  hint.textContent="Loading model…";
  loader.load(url,g=>{
    model=g.scene;const box=new THREE.Box3().setFromObject(model);const c=box.getCenter(new THREE.Vector3());
    model.position.sub(c);const s=box.getSize(new THREE.Vector3());fitR=Math.max(s.x,s.y,s.z)/2;
    scene.add(model);cam.position.set(fitR*1.2,fitR*0.8,fitR*3);controls.target.set(0,0,0);controls.update();
    hint.textContent="Drag to orbit, scroll or pinch to zoom. Game camera snaps to the 25° sprite angle.";
  },ev=>{if(ev.total)hint.textContent=`Loading model… ${Math.round(ev.loaded/ev.total*100)}%`},()=>hint.textContent="Couldn't load the model.");
}

// ---------- sheet player ----------
const ORDER=["nw","n","ne","w",null,"e","sw","s","se"];
let cur=null,img=null,t0=performance.now(),bigFacing="e";
function setSheet(sh){
  cur=sh;document.querySelectorAll("#sizes button").forEach(b=>b.classList.toggle("on",+b.dataset.px==sh.px));
  img=new Image();img.src=F(sh.png);document.getElementById("sheetimg").src=img.src;
  const n=Object.keys(sh.clips).length;
  document.getElementById("sheetinfo").textContent=`${sh.size.w}×${sh.size.h} · ${sh.px}px frames · ${sh.ms} ms per frame · clips: ${Object.keys(sh.clips).join(", ")}`;
  const comp=document.getElementById("compass");
  comp.innerHTML=ORDER.map(f=>f?`<canvas data-f="${f}" width="${sh.px}" height="${sh.px}" title="${f}"></canvas>`:`<div class="mid">${sh.px}px</div>`).join("");
  comp.querySelectorAll("canvas").forEach(c=>c.onclick=()=>{bigFacing=c.dataset.f});
  document.getElementById("big").innerHTML=`<canvas width="${sh.px}" height="${sh.px}"></canvas>`;
}
function clipFor(f){if(!cur)return null;return cur.clips["walk_"+f]||(f=="e"?cur.clips.walk:null)||(f=="w"&&cur.clips.walk?{flip:true,fr:cur.clips.walk}:null)}
function draw(c,f,k){
  const ctx=c.getContext("2d");ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,c.width,c.height);
  let cl=clipFor(f);if(!cl||!img||!img.complete)return;let flip=false;if(cl.flip){flip=true;cl=cl.fr}
  const fr=cl[k%cl.length];ctx.save();if(flip){ctx.translate(c.width,0);ctx.scale(-1,1)}
  ctx.drawImage(img,fr.x,fr.y,fr.w,fr.h,0,0,c.width,c.height);ctx.restore();
}
function anim(){requestAnimationFrame(anim);if(!openName||!cur)return;
  const spd=+document.getElementById("spd").value;const k=Math.floor((performance.now()-t0)*spd/cur.ms);
  document.querySelectorAll("#compass canvas").forEach(c=>draw(c,c.dataset.f,k));
  const b=document.querySelector("#big canvas");if(b)draw(b,bigFacing,k)}anim();

function openRun(r){
  openName=r.name;document.getElementById("dname").textContent=r.name;
  const p=document.getElementById("dstate");p.textContent=r.state=="2d"?"2D":r.state;p.className="pill "+r.state;
  document.getElementById("detail").classList.add("show");
  loadModel(r.glb?F(r.glb):null);
  const sz=document.getElementById("sizes");
  sz.innerHTML=r.sheets.map(s=>`<button data-px="${s.px}">${s.px}</button>`).join("")||'<span class="hint">No sheets yet</span>';
  sz.querySelectorAll("button").forEach((b,i)=>b.onclick=()=>setSheet(r.sheets[i]));
  cur=null;document.getElementById("compass").innerHTML="";document.getElementById("big").innerHTML="";document.getElementById("sheetimg").removeAttribute("src");document.getElementById("sheetinfo").textContent="";
  if(r.sheets.length)setSheet(r.sheets.find(s=>s.px==64)||r.sheets[0]);
  history.replaceState(null,"","#"+encodeURIComponent(r.name));
}
document.getElementById("back").onclick=()=>{openName=null;document.getElementById("detail").classList.remove("show");history.replaceState(null,"","#");tick()};
(async()=>{await tick();const h=decodeURIComponent(location.hash.slice(1));const r=RUNS.find(r=>r.name==h);if(r)openRun(r)})();
</script></body></html>"""


class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, code, body, ctype):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        if u.path == "/":
            return self.send(200, PAGE.encode(), "text/html; charset=utf-8")
        if u.path == "/api/runs":
            return self.send(200, json.dumps({"dirs": DIRS, "runs": runs()}).encode(), "application/json")
        if u.path == "/file":
            p = urllib.parse.parse_qs(u.query).get("p", [""])[0].replace("__home__", home)
            if allowed(p) and os.path.isfile(p):
                ext = p.rsplit(".", 1)[-1].lower()
                ctype = {"glb": "model/gltf-binary", "json": "application/json", "jpg": "image/jpeg"}.get(ext, "image/png")
                return self.send(200, open(p, "rb").read(), ctype)
        self.send(404, b"not found", "text/plain")


if __name__ == "__main__":
    print(f"TRELLIS tracker on http://localhost:{port}  watching {', '.join(DIRS)}")
    http.server.ThreadingHTTPServer(("127.0.0.1", port), H).serve_forever()
