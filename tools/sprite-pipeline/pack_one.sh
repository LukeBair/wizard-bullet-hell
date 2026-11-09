#!/bin/bash
# Pack already-rendered frames (colour + _n normals) for one rigged enemy into the game and add feet.
# Usage: pack_one.sh name frames_dir ms   (frames_dir and frames_dir_n from render_boss_clips.py, SPRITE_NORMALS=1)
set -e
here="$(cd "$(dirname "$0")" && pwd)"; E=~/Code/trellis-mac/enemies; GAME="$here/../../Assets/Resources/Art/Dusk"
PY=~/Code/trellis-mac/.venv/bin/python; n=$1; f=$2; ms=$3
for px in 64 128; do
  c=32; [ $px = 128 ] && c=48; base=enemy3d_${n}_sheet_$px
  $PY "$here/make_enemy_sheet.py" "$f" $E/out $base $px $c "$ms"
  $PY "$here/make_enemy_sheet.py" "${f}_n" $E/out ${base}_n $px 0 "$ms"
  cp $E/out/$base.png $E/out/$base.json $E/out/${base}_n.png "$GAME/"; rm -f $E/out/${base}_n.json
  $PY "$here/add_feet.py" "$GAME/$base.json"
  # the tracker page (trellis_tracker.py) plays enemies/out/enemy_<name>_sheet*.json
  cp "$GAME/$base.png" $E/out/enemy_${n}_sheet_$px.png; cp "$GAME/$base.json" $E/out/enemy_${n}_sheet_$px.json
done
