# Image to 3D model to sprite sheet

How the 3D wizard sprite sheets (`player_sheet_64`, `player_sheet_128`) were made: one T-pose
image becomes a 3D model (TRELLIS.2), gets rigged and posed in Blender, and every animation frame
is rendered and packed into the game's sheet layout. All of it runs locally on Apple Silicon.

`TRELLIS` below means a clone of https://github.com/shivampkumar/trellis-mac (here
`~/Code/trellis-mac`). Blender is `/Applications/Blender.app/Contents/MacOS/Blender`.

## One-time setup

1. Clone trellis-mac, run `bash setup.sh`, and log in to Hugging Face (`hf auth login`) with access
   to `facebook/dinov3-vitl16-pretrain-lvd1689m`. Read DINOv3's license before shipping anything.
2. The Metal texture-baking packages fail to build as shipped (PyTorch's headers need C++20). Without
   them the textures come out noisy. Fix, from the trellis-mac folder:
   ```bash
   sed -i '' 's/-std=c++17/-std=c++20/g' deps/trellis2-apple/o-voxel/setup.py deps/mtlbvh/setup.py \
       deps/mtldiffrast/setup.py deps/mtlgemm/setup.py deps/mtlmesh/setup.py
   git -C deps/trellis2-apple submodule update --init --recursive   # o-voxel needs Eigen
   source .venv/bin/activate && export MACOSX_DEPLOYMENT_TARGET=14.0
   for d in mtlbvh mtldiffrast mtlmesh mtlgemm trellis2-apple/o-voxel; do uv pip install --no-build-isolation deps/$d; done
   ```
   Check with `python -c "import mtldiffrast, flex_gemm, cumesh, mtlbvh, o_voxel.postprocess"`.
   Re-running `setup.sh` from scratch undoes the C++20 patch.
3. RMBG-2.0 (TRELLIS's background remover) is non-commercial. Swap it for `rembg` (Apache-2.0):
   ```bash
   uv pip install rembg onnxruntime
   cp rembg_free.py $TRELLIS/backends/ && git -C $TRELLIS apply /path/to/trellis-mac-generate.patch
   ```
   The patch also keeps all models resident on the GPU (`low_vram = False`).
4. Install Blender (`brew install --cask blender`). The scripts use Blender 5.x.

## Steps

1. **Source image.** Full-body character, T-pose, facing the camera. A transparent PNG skips background
   removal.
2. **Image to 3D** (about 7 min on an M4 Pro):
   ```bash
   cd $TRELLIS && source .venv/bin/activate
   python generate.py tpose.png --output out_name --pipeline-type 1024_cascade --texture-size 2048
   ```
   Use `out_name.glb` (about 200K triangles, clean texture). The `.obj` is huge and untextured.
3. **Inspect.** `render_preview.py` renders front/side/back/top views:
   `Blender -b -P render_preview.py -- out_name.glb /tmp/prev`
4. **Rig.** `Blender -b -P rig_wizard.py -- out_name.glb rig/name_rigged.blend`.
   The bone positions in the `B` table are for the wizard's proportions (1.0 tall, centred on the
   origin, Z-up). For another character, measure it with `landmarks.py` and edit the table.
   `pose_test.py` poses the arms down and renders a check.
5. **Render the frames.**
   `Blender -b rig/name_rigged.blend -P render_clips.py -- clips_768`
   Writes 83 transparent 768px frames in the game's order: idle, walk, cast (front, side, back), hit,
   death, then the diagonal idle/walk/cast clips. Poses are plain code in `pose()`; the camera is
   orthographic, tilted 25 degrees. The hem sway is a shape key, not a cloth simulation.
6. **Pack the sheet.**
   ```bash
   python make_sheet.py clips_768 sheets 64 32 Assets/Resources/Art/Dusk/player_sheet.json
   python make_sheet.py clips_768 sheets 128 48 Assets/Resources/Art/Dusk/player_sheet.json
   ```
   Arguments: frames dir, output dir, frame size, palette colours, reference JSON (copied for clip tags
   and timings). Run with the trellis-mac venv's Python (it has Pillow).
7. **Use it in the game.** Copy the PNG and JSON into `Assets/Resources/Art/Dusk/` as
   `player_sheet_<px>`. `ArtStyle.PixelsPerUnit` scales those names so the wizard keeps its world size,
   and the Wizard art setting switches between sheets. Sheets that have `walk_diag` get five-way facing.

## Other scripts

- `render_sprites.py` and `downscale_sprites.py` make static front/side/back sprites from any GLB, with
  no rig.
- `contact.py` builds a contact sheet of frames for a quick look.

## Known limits

- The wizard renders dark; raise the world light strength in `render_clips.py` to lift it.
- TRELLIS meshes are thin open surfaces. They render well but are not game-ready 3D assets; an attempt at
  automatic retopology made them worse, so this pipeline renders sprites only.
- Humanoid rig only. Other creatures need their own armature, or static renders.
- Thermal throttling can make TRELLIS many times slower.
