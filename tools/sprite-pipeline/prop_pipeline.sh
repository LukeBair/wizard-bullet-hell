#!/bin/bash
# Grok image -> TRELLIS model -> Blender renders (6 angles, normals, shadow) -> game art, for static props.
# Usage: prop_pipeline.sh [-n] name=height_px [name=height_px ...]
#   name         prop file stem, e.g. caves_crystal_cluster -> Resources/Art/Dusk/prop_caves_crystal_cluster_<k>_<hi|lo>.png
#   height_px    in-game "hi" height (forest: tree 192, boulders 112, shrine 144; lo is half)
#   -n           dry run: print what would run
# Input image: ~/Code/trellis-mac/props/inputs/<name>.png, or ~/Downloads/grok-prop-<name>.png (copied in).
# Each step is skipped when its output exists, so re-running only does what is missing. Delete
# props/<name>.glb to redo the model, props/renders/<name>_* to redo the renders.
# Wrap in tools/quiet/q:  tools/quiet/q -n props -- tools/sprite-pipeline/prop_pipeline.sh caves_crystal_cluster=160
set -e
DRY=""; [ "$1" = "-n" ] && { DRY=echo; shift; }
[ $# -eq 0 ] && { sed -n 2,11p "$0"; exit 1; }
HERE=$(cd "$(dirname "$0")" && pwd); ART="$HERE/../../Assets/Resources/Art/Dusk"
T=~/Code/trellis-mac; P=$T/props; BLENDER=${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}
mkdir -p "$P/inputs" "$P/renders"
for spec in "$@"; do
  name=${spec%%=*}; height=${spec#*=}; [ "$height" = "$spec" ] && height=128
  echo "== $name (hi height $height)"
  img=$P/inputs/$name.png
  if [ ! -f "$img" ]; then
    g=~/Downloads/grok-prop-$name.png
    [ -f "$g" ] && $DRY cp "$g" "$img" || { echo "  no input image: $img (or $g)"; exit 1; }
  fi
  if [ ! -f "$P/$name.glb" ]; then
    echo "  TRELLIS (about 7 min)"
    $DRY bash -c "cd $T && source .venv/bin/activate && python -u generate.py '$img' --output '$P/$name' --pipeline-type 1024_cascade --texture-size 2048 > '$P/$name.log' 2>&1"
  fi
  if [ -z "$(ls "$P/renders/${name}_0.png" 2>/dev/null)" ]; then
    echo "  Blender renders"
    $DRY "$BLENDER" -b -P "$HERE/render_prop.py" -- "$P/$name.glb" "$P/renders" "$name" > "$P/renders/$name.log" 2>&1
  fi
  echo "  trim into game art"
  $DRY "$T/.venv/bin/python" "$HERE/trim_props.py" "$P/renders" "$ART" "$name=$height"
done
echo "done. Unity makes .meta files on import; run tools/artgen/make_metas.sh for the ASTC import settings"
