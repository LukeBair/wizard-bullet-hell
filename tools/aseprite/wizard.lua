-- Builds tools/aseprite/wizard.aseprite: the Dusk player wizard and its animations.
-- Run inside Aseprite (batch): aseprite -b --script tools/aseprite/wizard.lua
-- Export for the game (from the repo root):
--   aseprite -b tools/aseprite/wizard.aseprite --sheet Assets/Resources/Art/Dusk/player_sheet.png \
--     --data Assets/Resources/Art/Dusk/player_sheet.json --format json-array --list-tags --sheet-type horizontal
-- Parts are drawn as text grids in the 7-colour Dusk palette, then posed per frame on
-- separate layers (body, staff, hand, fx) so the source stays editable in Aseprite.
-- The wizard faces right; the game flips it for left.

local here = debug.getinfo(1, "S").source:sub(2):match("(.*/)") or "./"
local OUT = here .. "wizard.aseprite"
local W, H = 32, 32

local P = {
  o = Color{ r=0x14, g=0x11, b=0x20 },  -- outline
  n = Color{ r=0x2b, g=0x2f, b=0x4a },  -- navy
  c = Color{ r=0xf0, g=0xd2, b=0xbc },  -- cream
  r = Color{ r=0xe5, g=0x55, b=0x5f },  -- coral
  w = Color{ r=0x8c, g=0x34, b=0x46 },  -- wine
  d = Color{ r=0x3e, g=0x5a, b=0x4e },  -- deep sage
  s = Color{ r=0x6f, g=0x9b, b=0x7a },  -- sage
}

-- Parts ------------------------------------------------------------------------------

local HAT = {           -- drawn at (4, 2)
  ".....ooo...........",
  "....onnno..........",
  ".....onnnoo........",
  "......onnnno.......",
  "......onnnnno......",
  ".....onnncnno......",
  ".....onnccnnno.....",
  "....onnnncnnno.....",
  "....orrrrrrrrro....",
  ".ooonnnnnnnnnnnooo.",
  "onnnnnnnnnnnnnnnnnno",
  ".ooonnnnnnnnnnnooo.",
}

local FACE = {          -- drawn at (7, 14): eyes glinting in the brim's shadow, then the beard
  "onnnnncnncno",
  "occcccccccco",
  ".occcccccco.",
}

local FACE_WINCE = {
  "onnnnnnnnnno",
  "occcccccocco",
  ".occcccccco.",
}

local ROBE = {          -- drawn at (4, 17)
  "...owocccccccoo.......",
  "..owwwoccccccowo......",
  ".owwwwoccccccowwo.....",
  ".owrwwwoccccowwwo.....",
  ".owwrwwwoccowwwwo.....",
  ".oowrrrrrocorrrrro....",
  "..owwwwwwwowwwwwo.....",
  "..owwwwwwrwwwwwwwo....",
  ".owwwwwwwrwwwwwwwo....",
  ".owwwwwwwrwwwwwwwwo...",
  "orrrrrrrrrrrrrrrrrro..",
  "oooooooooooooooooooo..",
}

local FOOT = { "onnno", "onnno", "ooooo" }  -- drawn behind the robe; only the toes peek out

local STAFF_ROD = "odo"  -- one row of the staff, centred on x

local ORB = {
  ".ooo.",
  "oscco",
  "occco",
  "occco",
  "occso",
  ".ooo.",
}

local GLOW = {          -- sage halo behind the orb while casting
  "..sss..",
  ".s...s.",
  "s.....s",
  "s.....s",
  "s.....s",
  "s.....s",
  ".s...s.",
  "..sss..",
}

local GLOW_BIG = {
  "...s.s...",
  ".s.....s.",
  "s.......s",
  ".........",
  "s.......s",
  ".........",
  "s.......s",
  ".s.....s.",
  "...s.s...",
}

local HAND = {          -- sleeve and hand gripping the staff, drawn at (17, 18)
  ".oooo....",
  "owwwwooo.",
  "owwwwocco",
  "oowwwocco",
  ".oooo.oo.",
}

local STAFF_FALLEN = {  -- lying on the ground
  ".ooo..............",
  "occcooooooooooooo.",
  "occcddddddddddddo.",
  "ocscoooooooooooo..",
  ".ooo..............",
}

local PILE = {          -- empty robe slumped on the floor
  "....oooooooooooo....",
  "..oowwwwwwwwwwwwoo..",
  ".owwwwwrwwwwwwwwwwo.",
  "orrrrrrrrrrrrrrrrrro",
  "oooooooooooooooooooo",
}

local HAT_FALLEN = {    -- the hat on its own, tipped over
  "......ooo.....",
  "....oonnno....",
  "..oonnnnno....",
  ".onrrrnnnnoo..",
  "onnnrrrnnnnno.",
  ".oooooooooooo.",
}

-- Drawing ----------------------------------------------------------------------------

local spr = Sprite(W, H, ColorMode.RGB)
spr.filename = OUT
local layers = {}
spr.layers[1].name = "body"
layers.body = spr.layers[1]
for _, name in ipairs({ "staff", "hand", "fx" }) do
  local l = spr:newLayer()
  l.name = name
  layers[name] = l
end

local frameCount = 0
local images

local function newFrame(duration)
  frameCount = frameCount + 1
  local frame = frameCount == 1 and spr.frames[1] or spr:newEmptyFrame()
  frame.duration = duration / 1000
  images = {}
  for name, layer in pairs(layers) do
    images[name] = Image(W, H, ColorMode.RGB)
  end
  return frame
end

local function commit(frame)
  for name, layer in pairs(layers) do
    if not images[name]:isEmpty() then spr:newCel(layer, frame, images[name], Point(0, 0)) end
  end
end

-- Stamp a part. remap optionally swaps palette keys (e.g. a hit flash).
local function stamp(layer, part, x, y, remap)
  local img = images[layer]
  for row, line in ipairs(part) do
    local col = 0
    for ch in line:gmatch(utf8 and utf8.charpattern or ".") do
      local key = remap and remap[ch] or ch
      local color = P[key]
      local px, py = x + col, y + row - 1
      if color and px >= 0 and py >= 0 and px < W and py < H then img:drawPixel(px, py, color) end
      col = col + 1
    end
  end
end

local function staff(x, top, bottom, orbY)
  for yy = top, bottom do stamp("staff", { STAFF_ROD }, x - 1, yy) end
  stamp("staff", { "o" }, x, bottom + 1)
  stamp("staff", ORB, x - 2, orbY)
end

-- A full standing pose. o = options: dy body bob, hatDy extra hat bob, feet = {lx, ly, rx, ry},
-- staffDx/staffDy staff offset, glow 0/1/2, face part, dx whole-body shift, remap palette swap.
local function pose(o)
  local dx, dy = o.dx or 0, (o.dy or 0) - 1
  local hatDy = o.hatDy or 0
  local feet = o.feet or { 6, 27, 13, 27 }
  stamp("body", FOOT, feet[1] + dx, feet[2], o.remap)
  stamp("body", FOOT, feet[3] + dx, feet[4], o.remap)
  local top, hem = {}, {}
  for i, row in ipairs(ROBE) do
    if i <= #ROBE - 2 then top[#top + 1] = row else hem[#hem + 1] = row end
  end
  stamp("body", top, 4 + dx, 17 + dy, o.remap)
  stamp("body", hem, 4 + dx + (o.hemDx or 0), 17 + dy + #top, o.remap)
  stamp("body", o.face or FACE, 7 + dx, 14 + dy + hatDy, o.remap)
  stamp("body", HAT, 4 + dx, 2 + dy + hatDy, o.remap)
  local sx, sy = 23 + dx + (o.staffDx or 0), dy + (o.staffDy or 0)
  if o.glow == 1 then stamp("fx", GLOW, sx - 3, 4 + sy) end
  if o.glow == 2 then stamp("fx", GLOW_BIG, sx - 4, 3 + sy) end
  staff(sx, 11 + sy, 27 + dy, 5 + sy)
  stamp("hand", HAND, 17 + dx + (o.staffDx or 0), 18 + dy + math.min(0, o.staffDy or 0), o.remap)
end

local tags = {}
local function anim(name, frames)
  local from = frameCount + 1
  for _, f in ipairs(frames) do
    local frame = newFrame(f.ms or 120)
    if f.draw then f.draw() else pose(f) end
    commit(frame)
  end
  tags[#tags + 1] = { name = name, from = from, to = frameCount }
end

-- Animations -------------------------------------------------------------------------

anim("idle", {
  { ms = 220 },
  { ms = 180, hatDy = 0, dy = 0, staffDy = -1 },
  { ms = 220, dy = 1, hatDy = 0, staffDy = 0 },
  { ms = 180, dy = 1, staffDy = 1 },
})

-- Feet: stride, passing, stride (other foot), passing.
anim("walk", {
  { ms = 100, hemDx = 1, feet = { 4, 27, 15, 27 } },
  { ms = 100, dy = -1, feet = { 7, 27, 12, 26 } },
  { ms = 100, dy = -1, hemDx = -1, feet = { 10, 27, 9, 26 } },
  { ms = 100, hemDx = -1, feet = { 15, 27, 4, 27 } },
  { ms = 100, dy = -1, feet = { 12, 26, 7, 27 } },
  { ms = 100, dy = -1, hemDx = 1, feet = { 9, 26, 10, 27 } },
})

anim("cast", {
  { ms = 80, staffDy = -2 },
  { ms = 70, staffDx = 1, staffDy = -3, glow = 1 },
  { ms = 90, staffDx = 1, staffDy = -3, glow = 2 },
  { ms = 100, staffDy = -1 },
})

local FLASH = { n = "c", w = "c", r = "c", d = "c", s = "c" }
anim("hit", {
  { ms = 70, dx = -1, face = FACE_WINCE, remap = FLASH },
  { ms = 110, dx = -1, face = FACE_WINCE },
})

anim("death", {
  { ms = 110, dx = -1, face = FACE_WINCE, remap = FLASH },
  { ms = 130, dx = -1, dy = 2, hatDy = -1, face = FACE_WINCE },
  { ms = 140, draw = function()
      stamp("body", PILE, 4, 24)
      stamp("body", FACE_WINCE, 8, 21)
      stamp("body", HAT, 4, 10)
      stamp("staff", STAFF_FALLEN, 12, 25)
    end },
  { ms = 150, draw = function()
      stamp("body", PILE, 4, 25)
      stamp("body", HAT_FALLEN, 7, 20)
      stamp("staff", STAFF_FALLEN, 12, 25)
      stamp("fx", { ".s...s.", "..s.s..", "s.....s" }, 9, 14)
    end },
  { ms = 160, draw = function()
      stamp("body", PILE, 4, 25)
      stamp("body", HAT_FALLEN, 7, 20)
      stamp("staff", STAFF_FALLEN, 12, 25)
      stamp("fx", { "..s...", "s...s.", "......", ".s..s." }, 10, 9)
    end },
  { ms = 600, draw = function()
      stamp("body", PILE, 4, 25)
      stamp("body", HAT_FALLEN, 7, 20)
      stamp("staff", STAFF_FALLEN, 12, 25)
    end },
})

for _, t in ipairs(tags) do
  local tag = spr:newTag(t.from, t.to)
  tag.name = t.name
end

spr:saveAs(OUT)
print("frames " .. frameCount)
for _, t in ipairs(tags) do print(t.name .. " " .. t.from .. "-" .. t.to) end
