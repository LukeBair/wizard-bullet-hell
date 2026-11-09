#!/bin/bash
# Give every new (untracked) PNG in Resources/Art/Dusk a .meta copied from the matching forest file (new guid),
# so it gets point filtering and the ASTC overrides without opening Unity first.
cd "$(dirname "$0")/../../Assets/Resources/Art/Dusk" || exit 1
for f in $(git ls-files -o --exclude-standard . | grep '\.png$' | xargs -n1 basename); do
  [ -f "$f.meta" ] && continue
  case $f in
    ground_*_base.png) t=ground_forest_base.png;; ground_*patch*) t=ground_forest_patch_0_0.png;;
    prop_*_hi_n.png) t=prop_tree_oak_0_hi_n.png;; prop_*_lo_n.png) t=prop_tree_oak_0_lo_n.png;;
    prop_*_hi_shadow.png) t=prop_tree_oak_0_hi_shadow.png;; prop_*_lo_shadow.png) t=prop_tree_oak_0_lo_shadow.png;;
    prop_*_hi.png) t=prop_tree_oak_0_hi.png;; prop_*_lo.png) t=prop_tree_oak_0_lo.png;;
    *) echo "skip $f"; continue;;
  esac
  sed "s/^guid: .*/guid: $(uuidgen | tr -d - | tr A-Z a-z)/" "$t.meta" > "$f.meta"
done
