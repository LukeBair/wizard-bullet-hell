# Unity Mac build -> Builds/Mac/Wizard Bullet Hell.app. Run from the repo root (wrap in tools/quiet/q).
set -e
ROOT=$(pwd)
~/.unity/bin/unity --no-banner --non-interactive build --target StandaloneOSX --execute-method BuildMac.Build --log-file "$ROOT-unity-mac.log"
echo "built: $ROOT/Builds/Mac/Wizard Bullet Hell.app"
