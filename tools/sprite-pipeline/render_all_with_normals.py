"""Re-render every in-game 3D enemy with normal maps and copy colour + normal sheets into the game.
Simple enemies come from enemies.json (render_enemy_loop.py), bosses and casters from their rigs
(render_boss_clips.py). Writes Assets/Resources/Art/Dusk/enemy3d_<name>_sheet_<px>.png/.json plus
enemy3d_<name>_sheet_<px>_n.png for each size the game uses.
Usage: python render_all_with_normals.py [only_name ...]"""
import json, os, shutil, subprocess, sys

here = os.path.dirname(os.path.abspath(__file__))
E = os.path.expanduser("~/Code/trellis-mac/enemies")
GAME = os.path.normpath(os.path.join(here, "../../Assets/Resources/Art/Dusk"))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
PY = sys.executable
env = dict(os.environ, SPRITE_NORMALS="1")

# name -> (sizes in game, palette colours per size)
SIMPLE = {"moss": [32, 64, 128], "bat": [32, 64, 128], "pixie": [32, 64, 128], "blob": [64, 128],
          "wolf": [64, 128], "goblin": [64, 128], "mushroom": [64, 128], "mole": [64, 128], "treant": [64, 128]}
# name -> (rig blend, mesh name, emit, style, rest_down, per-clip ms)
BOSSES = {
    "headmaster": ("headmaster", "headmaster", 0.9, "glide", 62, "idle=160,walk=100,cast=75,death=100"),
    "witch": ("witch", "witch", 1.3, "glide", 38, "idle=160,walk=110,cast=75,death=100"),
    "golem": ("golem", "golem", 1.0, "brute", 65, "idle=170,walk=120,cast=80,death=110"),
    "apprentice": ("apprentice", "apprentice", 1.0, "stride", 62, "idle=150,walk=95,cast=70,death=95"),
}
COLORS = {32: 16, 64: 32, 128: 48}
only = set(sys.argv[1:])


def pack(name, frames, sizes, ms):
    for px in sizes:
        base = f"enemy3d_{name}_sheet_{px}"
        subprocess.run([PY, os.path.join(here, "make_enemy_sheet.py"), frames, os.path.join(E, "out"), base, str(px), str(COLORS[px]), str(ms)], check=True)
        subprocess.run([PY, os.path.join(here, "make_enemy_sheet.py"), frames + "_n", os.path.join(E, "out"), base + "_n", str(px), "0", str(ms)], check=True)
        for f in (base + ".png", base + ".json", base + "_n.png"):
            shutil.copy(os.path.join(E, "out", f), os.path.join(GAME, f))
        os.remove(os.path.join(E, "out", base + "_n.json"))


cfg = json.load(open(os.path.join(E, "enemies.json")))
for name, sizes in SIMPLE.items():
    if only and name not in only:
        continue
    c = cfg[name]
    frames = os.path.join(E, "frames", name)
    shutil.rmtree(frames, ignore_errors=True); shutil.rmtree(frames + "_n", ignore_errors=True)
    print("[normals]", name, flush=True)
    subprocess.run([BLENDER, "-b", "-P", os.path.join(here, "render_enemy_loop.py"), "--", os.path.join(E, name + ".glb"), frames,
                    c["mode"], "dirs=8", *[f"{k}={v}" for k, v in c.get("opts", {}).items()]],
                   env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    pack(name, frames, sizes, c.get("ms", 90))

for name, (rig, mesh, emit, style, down, ms) in BOSSES.items():
    if only and name not in only:
        continue
    frames = os.path.join(E, "frames", name + "3d")
    shutil.rmtree(frames, ignore_errors=True); shutil.rmtree(frames + "_n", ignore_errors=True)
    print("[normals]", name, flush=True)
    subprocess.run([BLENDER, "-b", os.path.join(E, "rig", rig + ".blend"), "-P", os.path.join(here, "render_boss_clips.py"), "--",
                    frames, mesh, "256", str(emit), style, str(down)],
                   env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
    pack(name, frames, [64, 128], ms)
print("[normals] all done", flush=True)
