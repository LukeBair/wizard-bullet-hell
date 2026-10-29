"""Turn every finished TRELLIS model into enemy sprite sheets, waiting for models still being generated.
For each <name>.glb in the enemies folder with an entry in enemies.json and no sheets yet, renders the
8-facing loop with render_enemy_loop.py and packs 48, 64 and 128px sheets with make_enemy_sheet.py.
Usage: python render_queue.py [enemies_dir] [--once]
enemies.json: {"name": {"mode": "quad", "ms": 90, "opts": {"emit": 0.6, ...}}, ...}"""
import json, os, subprocess, sys, time

here = os.path.dirname(os.path.abspath(__file__))
args = [a for a in sys.argv[1:] if not a.startswith("--")]
E = os.path.expanduser(args[0] if args else "~/Code/trellis-mac/enemies")
once = "--once" in sys.argv
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
SIZES = [(48, 24, ""), (64, 32, "_64"), (128, 48, "_128")]


def pending():
    cfg = json.load(open(os.path.join(E, "enemies.json")))
    todo, waiting = [], []
    for n, c in cfg.items():
        if os.path.isfile(os.path.join(E, "out", f"enemy_{n}_sheet_128.png")):
            continue
        glb = os.path.join(E, n + ".glb")
        (todo if os.path.isfile(glb) else waiting).append((n, c))
    return todo, waiting


while True:
    todo, waiting = pending()
    for n, c in todo:
        frames = os.path.join(E, "frames", n)
        opts = [f"{k}={v}" for k, v in c.get("opts", {}).items()]
        print(f"[render] {n} ({c['mode']})", flush=True)
        with open(os.path.join(E, f"{n}_render.txt"), "w") as log:
            subprocess.run([BLENDER, "-b", "-P", os.path.join(here, "render_enemy_loop.py"), "--",
                            os.path.join(E, n + ".glb"), frames, c["mode"], "dirs=8", *opts],
                           stdout=log, stderr=subprocess.STDOUT, check=True)
        for px, colors, suf in SIZES:
            subprocess.run([sys.executable, os.path.join(here, "make_enemy_sheet.py"), frames, os.path.join(E, "out"),
                            f"enemy_{n}_sheet{suf}", str(px), str(colors), str(c.get("ms", 90))], check=True)
    if once or (not waiting and not todo):
        break
    if not todo:
        time.sleep(30)
print("[render] all done" if not pending()[1] else "[render] stopped", flush=True)
