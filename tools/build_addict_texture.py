"""Generate the drug addict skin: assets/vs-dope/textures/entity/drugaddict.png.

The addict renders with the vanilla seraph shape (game:entity/humanoid/seraph), so this
texture uses the seraph UV layout: 32x76 shape units at 2 px per unit = 64x152 px, the same
size as the vanilla seraph body skins. Every UV rectangle is read from the vanilla shape at
run time, so nothing here hard-codes the layout.

Look: sallow, grimy skin (vanilla skin18 recoloured), dark eye rings, stubble, dull bloodshot
eyes, needle track marks and sores on the forearms, a stained and torn linen shirt with
ragged short sleeves, patched drab trousers cut off ragged at the shin, and rag-wrapped feet.

Decline stages (issue #51): drugaddict-decline1..3.png are the same addict after long use, the
entity's texture alternates (textureIndex = stage). Each stage paints on top of the finished base
with its own RNG, so the base texture never changes: greyer skin, deeper eye rings, hollow cheeks,
ribs showing through the tears, more sores and track marks, and dirtier, more torn clothes.

Usage:  python3 tools/build_addict_texture.py [--preview OUT.png]
Needs Pillow and a Vintage Story install ($VINTAGE_STORY, default /opt/vintagestory).
Deterministic (fixed seed).
"""
import argparse, json, os, random, re
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
VS = Path(os.environ.get('VINTAGE_STORY', '/opt/vintagestory'))
GAME = VS / 'assets/game'
SHAPE = GAME / 'shapes/entity/humanoid/seraph.json'
BASE_SKIN = GAME / 'textures/entity/humanoid/seraphskinparts/body/skin18.png'
HAIR = GAME / 'textures/entity/humanoid/seraphskinparts/hair/rust3.png'  # preview only; keep in sync with drugaddict.json
OUT = ROOT / 'assets/vs-dope/textures/entity/drugaddict.png'
DECLINE_STAGES = 3  # keep in sync with the alternates in drugaddict.json and AddictLedger.DeclineThresholds
PX = 2  # texture pixels per shape UV unit (64x152 texture / 32x76 shape)

rng = random.Random(3606)  # issue #36


def load_shape():
    t = SHAPE.read_text()
    t = re.sub(r'//[^\n]*', '', t)
    t = re.sub(r',(\s*[}\]])', r'\1', t)
    return json.loads(t)


def elements(shape):
    out = {}
    def walk(els, parent):
        for e in els:
            e['_parent'] = e.get('stepParentName', parent)
            out[e['name']] = e
            walk(e.get('children', []), e['name'])
    walk(shape['elements'], None)
    return out


def rect(face):
    u1, v1, u2, v2 = face['uv']
    return (int(round(min(u1, u2) * PX)), int(round(min(v1, v2) * PX)),
            int(round(max(u1, u2) * PX)), int(round(max(v1, v2) * PX)))


def clamp(c):
    return tuple(max(0, min(255, int(round(x)))) for x in c)


def jitter(c, amt):
    d = rng.uniform(-amt, amt)
    return clamp((c[0] + d, c[1] + d, c[2] + d))


def mix(a, b, t):
    return clamp(tuple(a[i] * (1 - t) + b[i] * t for i in range(3)))


# ---- palette -------------------------------------------------------------------------------
SALLOW = (196, 184, 142)       # yellow-grey tint pulled into the vanilla skin
GRIME = (92, 78, 58)
BRUISE = (96, 70, 96)
SORE = (138, 58, 48)
TRACK = (88, 40, 58)
SHIRT = (154, 142, 112)        # unwashed linen
SHIRT_STAIN = (120, 104, 62)
SHIRT_DARK = (104, 94, 72)
TROUSER = (86, 76, 56)         # drab brown-olive
TROUSER_DARK = (62, 54, 40)
PATCH = (112, 88, 60)
STITCH = (170, 160, 130)
ROPE = (140, 116, 70)
RAG = (110, 104, 92)
SOLE = (58, 50, 40)


def recolor_skin(img):
    """Pull the vanilla seraph skin toward a pallid, jaundiced tone, keeping its grain."""
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            base = mix((lum, lum, lum), SALLOW, 0.55)
            base = tuple(base[i] * (0.62 + lum / 255 * 0.45) for i in range(3))
            px[x, y] = clamp(base) + (255,)


def splotch(img, box, color, count, size=(1, 3), alpha=(0.25, 0.55)):
    x1, y1, x2, y2 = box
    if x2 <= x1 or y2 <= y1:
        return
    px = img.load()
    for _ in range(count):
        cx, cy = rng.randrange(x1, x2), rng.randrange(y1, y2)
        s = rng.randint(*size)
        a = rng.uniform(*alpha)
        for y in range(cy - s // 2, cy + s - s // 2):
            for x in range(cx - s // 2, cx + s - s // 2):
                if x1 <= x < x2 and y1 <= y < y2 and rng.random() < 0.8:
                    px[x, y] = mix(px[x, y][:3], color, a) + (255,)


def cloth(img, box, color, amt=10, weave=True):
    x1, y1, x2, y2 = box
    px = img.load()
    for y in range(y1, y2):
        for x in range(x1, x2):
            c = jitter(color, amt)
            if weave and (x + y) % 2 == 0:
                c = mix(c, (0, 0, 0), 0.06)
            px[x, y] = c + (255,)


def side_faces(e):
    return [(n, e['faces'][n]) for n in ('north', 'east', 'south', 'west') if n in e['faces']]


def cover_side(img, e, color, top_frac, bottom_frac, ragged_bottom=0, ragged_top=0, weave=True, amt=10):
    """Paint cloth on the side faces of an element between two height fractions (0 = top).

    ragged_* is the max depth in px of a torn edge (random per column)."""
    px = img.load()
    for _, f in side_faces(e):
        x1, y1, x2, y2 = rect(f)
        h = y2 - y1
        for x in range(x1, x2):
            top = y1 + int(round(h * top_frac)) + (rng.randint(0, ragged_top) if ragged_top else 0)
            bot = y1 + int(round(h * bottom_frac)) - (rng.randint(0, ragged_bottom) if ragged_bottom else 0)
            for y in range(max(y1, top), min(y2, bot)):
                c = jitter(color, amt)
                if weave and (x + y) % 2 == 0:
                    c = mix(c, (0, 0, 0), 0.06)
                px[x, y] = c + (255,)
            # frayed, darker edge thread
            if ragged_bottom and y1 <= bot - 1 < y2 and bot < y1 + h:
                px[x, bot - 1] = mix(px[x, bot - 1][:3], (40, 34, 26), 0.35) + (255,)


def face_box(e, name):
    return rect(e['faces'][name]) if name in e['faces'] else None


def hole(img, box, n, skin_img):
    """Tear small holes through cloth, showing the (already painted) skin underneath."""
    x1, y1, x2, y2 = box
    px, sk = img.load(), skin_img.load()
    for _ in range(n):
        cx, cy = rng.randrange(x1 + 1, max(x1 + 2, x2 - 1)), rng.randrange(y1 + 1, max(y1 + 2, y2 - 1))
        for x, y in ((cx, cy), (cx + 1, cy), (cx, cy + 1)):
            if x1 <= x < x2 and y1 <= y < y2 and rng.random() < 0.85:
                px[x, y] = sk[x, y]
        for x, y in ((cx - 1, cy), (cx + 2, cy), (cx, cy - 1)):
            if x1 <= x < x2 and y1 <= y < y2:
                px[x, y] = mix(px[x, y][:3], (40, 34, 26), 0.3) + (255,)


def build():
    rng.seed(3606)
    shape = load_shape()
    els = elements(shape)
    img = Image.open(BASE_SKIN).convert('RGBA')
    assert img.size == (shape['textureWidth'] * PX, shape['textureHeight'] * PX), img.size

    # Face-feature strips at the top-right of the seraph layout (eyes, lids, lips, brows)
    # are sampled by tiny elements. Remember them before the skin recolour.
    recolor_skin(img)
    px = img.load()

    def fill(box, color, amt=6):
        x1, y1, x2, y2 = box
        for y in range(y1, y2):
            for x in range(x1, x2):
                px[x, y] = jitter(color, amt) + (255,)

    # Face features (rows derived from the eye/lid/mouth/brow element UVs, see seraph.json)
    fx1 = int(28 * PX)
    fill((fx1, 0, 64, 8), (112, 104, 88), 6)                  # dull, washed-out eyes
    for _ in range(4):                                        # bloodshot flecks
        px[rng.randrange(fx1, 64), rng.randrange(0, 4)] = clamp((140, 70, 62)) + (255,)
    fill((fx1, 8, 64, 12), (205, 190, 170), 6)                 # eye shine, muted
    fill((fx1, 12, 64, 16), (92, 72, 80), 6)                   # heavy, bruised eyelids
    fill((fx1, 16, 64, 20), (118, 88, 86), 6)                  # dry, cracked lips
    fill((fx1, 20, 64, 28), (46, 38, 30), 6)                   # greasy brows

    # Body grime everywhere on skin
    for name in ('LowerTorso', 'UpperTorso', 'UpperArmR', 'UpperArmL', 'LowerArmR', 'LowerArmL',
                 'Neck', 'Head', 'UpperFootL', 'UpperFootR', 'LowerFootL', 'LowerFootR'):
        for _, f in els[name]['faces'].items():
            b = rect(f)
            area = (b[2] - b[0]) * (b[3] - b[1])
            splotch(img, b, GRIME, max(1, area // 18), (1, 3), (0.15, 0.4))

    # Head: the face is the west side (the eyes sit at the head's min-x).
    head = els['Head']
    hx1, hy1, hx2, hy2 = face_box(head, 'west')
    h = hy2 - hy1
    # sunken eye rings just below eye level (eyes span local y 1.75..2.75 of a 5-unit head)
    ring_y = hy1 + int(round((5 - 1.75) / 5 * h))
    for x in range(hx1 + 1, hx2 - 1):
        for y, a in ((ring_y, 0.55), (ring_y + 1, 0.3)):
            if hx1 + h // 2 - 1 <= x <= hx1 + h // 2:   # lighter across the nose bridge
                a *= 0.4
            px[x, y] = mix(px[x, y][:3], BRUISE, a) + (255,)
    # hollow cheeks and stubble on the jaw of every head side
    for name in ('west', 'north', 'south', 'east'):
        x1, y1, x2, y2 = face_box(head, name)
        for y in range(y2 - 3, y2):
            for x in range(x1, x2):
                if rng.random() < (0.45 if y >= y2 - 2 else 0.2):
                    px[x, y] = mix(px[x, y][:3], (40, 36, 30), 0.55) + (255,)
    for x in (hx1, hx1 + 1, hx2 - 2, hx2 - 1):
        px[x, ring_y + 2] = mix(px[x, ring_y + 2][:3], (60, 54, 50), 0.35) + (255,)
    splotch(img, (hx1, hy1, hx2, hy2), SORE, 2, (1, 1), (0.5, 0.7))  # picked scabs

    # Forearms: needle track marks on the inner faces, sores everywhere
    for arm, inner in (('LowerArmR', 'east'), ('LowerArmL', 'west')):
        e = els[arm]
        x1, y1, x2, y2 = face_box(e, inner)
        tx = x1 + (x2 - x1) // 2
        for y in range(y1 + 2, y1 + (y2 - y1) * 2 // 3, 2):
            px[tx + rng.choice((-1, 0)), y] = clamp(TRACK) + (255,)
        splotch(img, (x1, y1, x2, y2), BRUISE, 3, (2, 3), (0.25, 0.4))
        for _, f in side_faces(e):
            splotch(img, rect(f), SORE, 2, (1, 1), (0.55, 0.8))

    skin_only = img.copy()

    # ---- clothes ------------------------------------------------------------------------
    # Shirt: whole upper torso + untucked over the top 55% of the lower torso.
    ut, lt = els['UpperTorso'], els['LowerTorso']
    for _, f in ut['faces'].items():
        cloth(img, rect(f), SHIRT)
    cover_side(img, lt, SHIRT, 0.0, 0.55, ragged_bottom=2)
    cloth(img, face_box(lt, 'up'), SHIRT)
    # open collar: skin shows at the top centre of the chest (west = front)
    x1, y1, x2, y2 = face_box(ut, 'west')
    cx = (x1 + x2) // 2
    for y in range(y1, y1 + 4):
        for x in range(cx - 2 + (y - y1) // 2, cx + 2 - (y - y1) // 2):
            px[x, y] = skin_only.load()[x, y]
    # sweat / food stains and tears
    for name in ('west', 'east', 'north', 'south'):
        splotch(img, face_box(ut, name), SHIRT_STAIN, 4, (2, 4), (0.3, 0.55))
        splotch(img, face_box(ut, name), SHIRT_DARK, 3, (1, 2), (0.3, 0.5))
    hole(img, face_box(ut, 'west'), 2, skin_only)
    hole(img, face_box(ut, 'east'), 2, skin_only)
    # Ragged short sleeves on the upper arms
    for arm in ('UpperArmR', 'UpperArmL'):
        e = els[arm]
        cover_side(img, e, SHIRT, 0.0, 0.62, ragged_bottom=3)
        cloth(img, face_box(e, 'up'), SHIRT)
        for _, f in side_faces(e):
            splotch(img, rect(f), SHIRT_STAIN, 1, (1, 3), (0.3, 0.5))

    # Trousers: lower 45% of the lower torso, whole thighs, top of the shins.
    cover_side(img, lt, TROUSER, 0.55, 1.0, ragged_top=1)
    cloth(img, face_box(lt, 'down'), TROUSER)
    # rope belt, partly visible under the untucked shirt
    for _, f in side_faces(lt):
        x1, y1, x2, y2 = rect(f)
        by = y1 + int(round((y2 - y1) * 0.55))
        for x in range(x1, x2):
            if rng.random() < 0.7:
                px[x, by] = jitter(ROPE, 12) + (255,)
    for leg in ('UpperFootR', 'UpperFootL'):
        e = els[leg]
        for _, f in e['faces'].items():
            cloth(img, rect(f), TROUSER)
            splotch(img, rect(f), TROUSER_DARK, 3, (2, 3), (0.3, 0.6))
        hole(img, face_box(e, 'west'), 1, skin_only)
    # Knee patch with stitches on the right leg front
    x1, y1, x2, y2 = face_box(els['UpperFootR'], 'west')
    p = (x1 + 1, y2 - 7, x2 - 1, y2 - 2)
    cloth(img, p, PATCH, 8, weave=False)
    for x in range(p[0], p[2]):
        if x % 2 == 0:
            px[x, p[1]] = STITCH + (255,)
            px[x, p[3] - 1] = STITCH + (255,)
    # Shins: cloth to ~40% with a torn hem, then bare dirty skin, rag-wrapped feet.
    for leg in ('LowerFootR', 'LowerFootL'):
        e = els[leg]
        cover_side(img, e, TROUSER, 0.0, 0.42, ragged_bottom=3)
        cover_side(img, e, RAG, 0.78, 1.0, ragged_top=1, amt=14)
        cloth(img, face_box(e, 'down'), SOLE, 8, weave=False)
        for _, f in side_faces(e):
            x1, y1, x2, y2 = rect(f)
            h = y2 - y1
            splotch(img, (x1, y1 + int(h * 0.42), x2, y1 + int(h * 0.8)), GRIME, 9, (1, 3), (0.35, 0.65))
            for y in range(y1 + int(h * 0.8), y2, 2):  # wrap bands
                for x in range(x1, x2):
                    if (x + y) % 3 == 0:
                        px[x, y] = mix(px[x, y][:3], (60, 55, 48), 0.4) + (255,)
    return img, skin_only, shape, els


WASTED = (150, 152, 124)   # grey-green pallor of a body giving out
RIB = (70, 58, 56)


def decline(base, skin_only, els, stage):
    """Paint decline stage 1..3 over the finished base texture."""
    global rng
    saved, rng = rng, random.Random(5100 + stage)
    try:
        skin = skin_only.copy()
        sp = skin.load()
        fx1, fy2 = int(28 * PX), 28          # face-feature strip (eyes, lids, lips, brows)

        def feature(x, y):
            return x >= fx1 and y < fy2

        # Pallor, deepening with each stage (keeps the grain of the base skin).
        for y in range(skin.height):
            for x in range(skin.width):
                if feature(x, y):
                    continue
                c = sp[x, y][:3]
                sp[x, y] = clamp(tuple(v * (1 - 0.07 * stage) for v in mix(c, WASTED, 0.16 * stage))) + (255,)
        # Heavier, darker eyelids.
        for y in range(12, 16):
            for x in range(fx1, 64):
                sp[x, y] = mix(sp[x, y][:3], (60, 40, 56), 0.2 * stage) + (255,)

        head = els['Head']
        hx1, hy1, hx2, hy2 = face_box(head, 'west')
        h = hy2 - hy1
        ring_y = hy1 + int(round((5 - 1.75) / 5 * h))
        for x in range(hx1 + 1, hx2 - 1):
            for y, a in ((ring_y, 0.12), (ring_y + 1, 0.07)):
                sp[x, y] = mix(sp[x, y][:3], BRUISE, a * stage) + (255,)
        # Hollow cheeks: shadow the sides of the face below the eyes.
        for x in (hx1, hx1 + 1, hx2 - 2, hx2 - 1):
            for y in range(ring_y + 2, hy2 - 1):
                sp[x, y] = mix(sp[x, y][:3], (54, 48, 46), 0.12 * stage) + (255,)
        splotch(skin, (hx1, hy1, hx2, hy2), SORE, stage, (1, 1), (0.5, 0.75))

        # Ribs on the chest and back, visible through the collar and the tears.
        if stage >= 2:
            ut = els['UpperTorso']
            for side in ('west', 'east'):
                x1, y1, x2, y2 = face_box(ut, side)
                for y in range(y1 + 3, y2 - 1, 2):
                    for x in range(x1 + 1, x2 - 1):
                        if abs(x - (x1 + x2) // 2) > 0:
                            sp[x, y] = mix(sp[x, y][:3], RIB, 0.18 * stage) + (255,)

        # More track marks and sores; by stage 3 the upper arms are used too.
        arms = [('LowerArmR', 'east'), ('LowerArmL', 'west')]
        if stage >= 3:
            arms += [('UpperArmR', 'east'), ('UpperArmL', 'west')]
        for arm, inner in arms:
            x1, y1, x2, y2 = face_box(els[arm], inner)
            for _ in range(2 * stage):
                sp[rng.randrange(x1, x2), rng.randrange(y1, y2)] = clamp(TRACK) + (255,)
            splotch(skin, (x1, y1, x2, y2), BRUISE, stage, (1, 3), (0.3, 0.5))
        for name in ('LowerArmR', 'LowerArmL', 'LowerFootR', 'LowerFootL', 'Neck'):
            for _, f in side_faces(els[name]):
                splotch(skin, rect(f), SORE, stage, (1, 2), (0.45, 0.75))

        # Composite: declined skin wherever the base shows bare skin, clothes elsewhere.
        img = base.copy()
        px, bp, so = img.load(), base.load(), skin_only.load()
        for y in range(img.height):
            for x in range(img.width):
                if bp[x, y] == so[x, y]:
                    px[x, y] = sp[x, y]

        # Clothes: filthier and more torn, tears showing the declined skin.
        ut, lt = els['UpperTorso'], els['LowerTorso']
        for name in ('west', 'east', 'north', 'south'):
            splotch(img, face_box(ut, name), SHIRT_STAIN, 2 * stage, (2, 4), (0.3, 0.6))
            splotch(img, face_box(ut, name), GRIME, stage, (1, 3), (0.3, 0.5))
        for name in ('west', 'east'):
            hole(img, face_box(ut, name), stage, skin)
        for leg in ('UpperFootR', 'UpperFootL'):
            splotch(img, face_box(els[leg], 'west'), TROUSER_DARK, 2 * stage, (2, 3), (0.3, 0.6))
            hole(img, face_box(els[leg], 'west'), stage - 1, skin)
        hole(img, face_box(lt, 'west'), stage - 1, skin)
        return img
    finally:
        rng = saved


# ---- preview: flat orthographic front/back render (rest pose, ignores rotations) --------------
def world_boxes(els):
    boxes = {}
    def origin(name):
        e = els[name]
        if name in boxes:
            return boxes[name][0]
        p = e['_parent']
        o = (0, 0, 0) if p is None else origin(p)
        f = e['from']
        boxes[name] = ((o[0] + f[0], o[1] + f[1], o[2] + f[2]), None)
        return boxes[name][0]
    out = {}
    for n, e in els.items():
        o = origin(n)
        out[n] = (o, tuple(o[i] + e['to'][i] - e['from'][i] for i in range(3)))
    return out


def render(tex, hair, els, side, scale=12):
    wb = world_boxes(els)
    face = 'west' if side == 'front' else 'east'
    items = []
    for n, (a, b) in wb.items():
        f = els[n].get('faces', {}).get(face)
        if not f or f.get('enabled') is False:
            continue
        depth = a[0] if side == 'front' else -b[0]
        items.append((depth, n, a, b, f))
    items.sort(key=lambda t: -t[0])
    zs = [v for _, _, a, b, _ in items for v in (a[2], b[2])]
    ys = [v for _, _, a, b, _ in items for v in (a[1], b[1])]
    zmin, zmax, ymin, ymax = min(zs), max(zs), min(ys), max(ys)
    W, H = int((zmax - zmin + 2) * scale), int((ymax - ymin + 2) * scale)
    canvas = Image.new('RGBA', (W, H), (46, 44, 52, 255))
    for _, n, a, b, f in items:
        src = hair if f['texture'] == '#hair' else tex
        k = src.width / (48 if f['texture'] == '#hair' else 32)
        u1, v1, u2, v2 = f['uv']
        crop = src.crop((int(min(u1, u2) * k), int(min(v1, v2) * k), max(int(min(u1, u2) * k) + 1, int(max(u1, u2) * k)), max(int(min(v1, v2) * k) + 1, int(max(v1, v2) * k))))
        w = max(1, int(round((b[2] - a[2]) * scale)))
        h = max(1, int(round((b[1] - a[1]) * scale)))
        crop = crop.resize((w, h), Image.NEAREST)
        if side == 'back':
            crop = crop.transpose(Image.FLIP_LEFT_RIGHT)
            x = int((zmax - b[2] + 1) * scale)
        else:
            x = int((a[2] - zmin + 1) * scale)
        y = int((ymax - b[1] + 1) * scale)
        canvas.alpha_composite(crop, (x, y))
    return canvas


def preview(img, els, path):
    hair = Image.open(HAIR).convert('RGBA')
    front, back = render(img, hair, els, 'front'), render(img, hair, els, 'back')
    flat = img.resize((img.width * 4, img.height * 4), Image.NEAREST)
    H = max(front.height, flat.height)
    sheet = Image.new('RGBA', (front.width + back.width + flat.width + 40, H + 20), (30, 30, 36, 255))
    sheet.alpha_composite(front, (10, 10))
    sheet.alpha_composite(back, (front.width + 20, 10))
    sheet.alpha_composite(flat, (front.width + back.width + 30, 10))
    sheet.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--preview', help='also write a preview sheet (front, back, flat texture)')
    args = ap.parse_args()
    img, skin_only, shape, els = build()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    img.save(OUT)
    print(f'wrote {OUT.relative_to(ROOT)} {img.size}')
    stages = [img]
    for stage in range(1, DECLINE_STAGES + 1):
        out = OUT.with_name(f'{OUT.stem}-decline{stage}.png')
        stages.append(decline(img, skin_only, els, stage))
        stages[-1].save(out)
        print(f'wrote {out.relative_to(ROOT)}')
    if args.preview:
        sheets = []
        for i, tex in enumerate(stages):
            path = Path(args.preview)
            path = path if i == 0 else path.with_name(f'{path.stem}-decline{i}{path.suffix}')
            preview(tex, els, path)
            print(f'wrote {path}')


if __name__ == '__main__':
    main()
