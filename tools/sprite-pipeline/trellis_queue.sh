#!/bin/bash
# Run TRELLIS on every image in a folder that doesn't have a model yet, one at a time.
# Usage: trellis_queue.sh [inputs_dir] [out_dir]   (defaults: ~/Code/trellis-mac/enemies/inputs, ../)
# Each run logs to <out_dir>/<name>.log, which trellis_tracker.py shows. Images added while the queue
# runs are picked up before it exits.
T=~/Code/trellis-mac
IN=${1:-$T/enemies/inputs}; OUT=${2:-$T/enemies}
cd "$T" && source .venv/bin/activate
while :; do
  next=""
  for img in "$IN"/*.png; do
    n=$(basename "$img" .png)
    [ -f "$OUT/$n.glb" ] || [ -f "$OUT/$n.log" ] && continue
    next=$img; break
  done
  [ -z "$next" ] && break
  n=$(basename "$next" .png)
  echo "[queue] $n"
  python -u generate.py "$next" --output "$OUT/$n" --pipeline-type 1024_cascade --texture-size 2048 > "$OUT/$n.log" 2>&1
done
echo "[queue] empty"
