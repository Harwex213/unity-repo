"""
Low-poly art for the Floating Island level.

Run:  Blender -b --factory-startup --python fi_lowpoly.py
Writes:
  Assets/Art/FloatingIsland/Textures/T_FI_Palette.png           (base colour atlas)
  Assets/Art/FloatingIsland/Textures/T_FI_PaletteEmission.png   (emission atlas)
  Assets/Art/FloatingIsland/Models/FI_*.fbx
  art-sources/Ostrov/FloatingIsland/FloatingIsland.blend         (all models side by side)

Every model is flat shaded and uses one material, M_FI_LowPoly. Each face maps
all its UVs to the centre of one palette cell, so the face gets one flat colour.
"""
import bpy, bmesh, math, os, random, struct, zlib
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ART = os.path.join(REPO, "projects/Ostrov/Assets/Art/FloatingIsland")
MODELS = os.path.join(ART, "Models")
TEXTURES = os.path.join(ART, "Textures")
MAT_NAME = "M_FI_LowPoly"

# ---------------------------------------------------------------------------
# Palette: 8 x 8 cells, 4 px each. Row 0 is the top row of the PNG.
# ---------------------------------------------------------------------------
CELL = 4
COLS = ROWS = 8
PALETTE = {
    # basalt: near-black charcoal with a cold tint
    "basalt": ["16181b", "1b1e22", "202328", "25292f", "2b2f36", "131517", "1e2126", "30353c"],
    # moss: saturated greens, light to dark
    "moss":   ["7a9228", "6c8622", "86a02e", "5e7720", "92ad35", "526a1c", "72902a", "9ab83c"],
    # castle stone: mid greys, a hair warm
    "stone":  ["6b6c6e", "77787a", "606164", "828385", "57585b", "8c8d8f", "717274", "4c4d50"],
    # pine needles (darker than the moss) and trunk wood
    "pine":   ["2f5426", "386030", "2a4a1f", "426b36", "34592a", "263f1d", "1f2a1a", "2a221c"],
    # misc: 0 window glow, 1 dark window, 2 dirt path, 3 deck, 4 moss shadow
    "misc":   ["e8813a", "0d0e10", "3f3b35", "4d4c4a", "3c5f1e", "45403a", "2e3a26", "1a1c1e"],
    # distant backdrop basalt (cold, a little lighter so fog reads it)
    "far":    ["23272e", "282d35", "1e2228", "2d323b", "343a44", "1a1d22", "262a31", "30353e"],
}
ROW_INDEX = {name: i for i, name in enumerate(PALETTE)}
EMISSIVE = {("misc", 0): (255, 96, 24)}


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def write_png(path, width, height, pixel):
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixel(x, y))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def write_palettes():
    size = CELL * COLS
    names = list(PALETTE)

    def base(x, y):
        r, c = y // CELL, x // CELL
        if r < len(names):
            return hex_rgb(PALETTE[names[r]][c])
        return (255, 0, 255)

    def emit(x, y):
        r, c = y // CELL, x // CELL
        if r < len(names):
            return EMISSIVE.get((names[r], c), (0, 0, 0))
        return (0, 0, 0)

    write_png(os.path.join(TEXTURES, "T_FI_Palette.png"), size, size, base)
    write_png(os.path.join(TEXTURES, "T_FI_PaletteEmission.png"), size, size, emit)


def cell_uv(row, col):
    r = ROW_INDEX[row]
    return ((col + 0.5) / COLS, 1.0 - (r + 0.5) / ROWS)


# ---------------------------------------------------------------------------
# Mesh helpers
# ---------------------------------------------------------------------------
class Builder:
    """Collects closed shells in one bmesh. Each face carries one palette cell."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def face(self, pts, cell):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        self.paint(f, cell)
        return f

    def paint(self, f, cell):
        u, v = cell_uv(*cell)
        for loop in f.loops:
            loop[self.uv].uv = (u, v)

    def shell(self, verts, faces, cell_of):
        """verts: list of xyz; faces: list of index tuples; cell_of(i, face_idx_tuple) -> cell."""
        bv = [self.bm.verts.new(v) for v in verts]
        out = []
        for i, f in enumerate(faces):
            bf = self.bm.faces.new([bv[k] for k in f])
            self.paint(bf, cell_of(i, f))
            out.append(bf)
        return out

    def finish(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(material())
        me.update()
        return bpy.data.objects.new(self.name, me)


_MAT = None


def material():
    global _MAT
    if _MAT is None:
        _MAT = bpy.data.materials.new(MAT_NAME)
        _MAT.diffuse_color = (0.2, 0.2, 0.2, 1)
    return _MAT


def prism(b, cx, cy, radius, ztop, zbot, sides, rot, top_cell, side_cell, bot_cell=None,
          tip=0.0, tilt=(0.0, 0.0), side_cells=None, top_scale=1.0):
    """A vertical n-gon prism. tip > 0 hangs a point below the bottom ring."""
    verts, faces = [], []
    for k in range(sides):
        a = rot + 2 * math.pi * k / sides
        dx, dy = math.cos(a) * radius, math.sin(a) * radius
        verts.append((cx + dx * top_scale, cy + dy * top_scale, ztop + tilt[0] * dx + tilt[1] * dy))
    for k in range(sides):
        a = rot + 2 * math.pi * k / sides
        verts.append((cx + math.cos(a) * radius, cy + math.sin(a) * radius, zbot))
    faces.append(tuple(range(sides)))
    for k in range(sides):
        n = (k + 1) % sides
        faces.append((k, sides + k, sides + n, n))
    cells = [top_cell] + [(side_cells[k] if side_cells else side_cell) for k in range(sides)]
    if tip > 0:
        verts.append((cx, cy, zbot - tip))
        t = len(verts) - 1
        for k in range(sides):
            faces.append((sides + k, t, sides + (k + 1) % sides))
            cells.append(bot_cell or side_cell or top_cell)
    else:
        faces.append(tuple(range(2 * sides - 1, sides - 1, -1)))
        cells.append(bot_cell or side_cell or top_cell)
    return b.shell(verts, faces, lambda i, f: cells[i])


def box(b, center, size, cell, rot_z=0.0, cells=None):
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    c, s = math.cos(rot_z), math.sin(rot_z)
    pts = []
    for z in (-sz, sz):
        for x, y in ((-sx, -sy), (sx, -sy), (sx, sy), (-sx, sy)):
            pts.append((cx + x * c - y * s, cy + x * s + y * c, cz + z))
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return b.shell(pts, faces, lambda i, f: (cells[i] if cells else cell))


def extrude_profile(b, pts2d, to3d, depth, cell_front, cell_side, top_cell=None):
    """Extrudes a simple polygon (u, v) along w in [-depth/2, depth/2]. to3d(u, v, w) -> xyz."""
    n = len(pts2d)
    front = [to3d(u, v, -depth / 2) for u, v in pts2d]
    back = [to3d(u, v, depth / 2) for u, v in pts2d]
    verts = front + back
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    cells = [cell_front, cell_front]
    for k in range(n):
        m = (k + 1) % n
        faces.append((k, m, n + m, n + k))
        if top_cell is not None:
            (u0, v0), (u1, v1) = pts2d[k], pts2d[m]
            du, dv = u1 - u0, v1 - v0
            l = math.hypot(du, dv) or 1
            # Outward normal of an edge of a CCW polygon is (dv, -du).
            up = (-du / l)
            cells.append(top_cell(k) if up > 0.6 else cell_side(k))
        else:
            cells.append(cell_side(k))
    return b.shell(verts, faces, lambda i, f: cells[i])


def pick(rng, row, choices):
    return (row, rng.choice(choices))


# ---------------------------------------------------------------------------
# Island: a moss slab on a cluster of hexagonal basalt columns
# ---------------------------------------------------------------------------
def outline_fn(R, rng):
    ph = [rng.uniform(0, 2 * math.pi) for _ in range(4)]

    def f(t):
        return R * (1 + 0.10 * math.sin(2 * t + ph[0]) + 0.07 * math.sin(3 * t + ph[1])
                    + 0.04 * math.sin(5 * t + ph[2]))
    return f


def island(name, R, spacing, depth, seed, hill=3.0, hill_dir=0.0, crag=5.0, rim_depth=None):
    rng = random.Random(seed)
    b = Builder(name)
    Rm = outline_fn(R, rng)
    off = Vector((rng.uniform(0, 100), rng.uniform(0, 100), 0))

    def hill_w(x, y):
        r = math.hypot(x, y)
        t = math.atan2(y, x)
        rr = r / Rm(t)
        ang = max(0.0, math.cos(t - hill_dir))
        radial = min(1.0, max(0.0, (rr - 0.25) / 0.75))
        radial = radial * radial * (3 - 2 * radial)
        return (ang ** 2) * radial

    def height(x, y):
        n = noise.noise(Vector((x * 0.12, y * 0.12, 0)) + off)
        return 0.6 * n + hill * hill_w(x, y)

    # --- moss slab: Delaunay over a jittered lattice inside the outline ---
    N = max(16, int(R * 1.6))
    outline = []
    for j in range(N):
        t = 2 * math.pi * j / N + rng.uniform(-0.15, 0.15) * (2 * math.pi / N)
        r = Rm(t)
        outline.append((math.cos(t) * r, math.sin(t) * r))
    step = max(1.6, R * 0.17)
    inner = []
    ext = int(R * 1.3 / step) + 1
    for j in range(-ext, ext + 1):
        for i in range(-ext, ext + 1):
            x = step * (i + 0.5 * (j % 2)) + rng.uniform(-0.3, 0.3) * step
            y = step * j * 0.866 + rng.uniform(-0.3, 0.3) * step
            if math.hypot(x, y) < Rm(math.atan2(y, x)) - 0.6 * step:
                inner.append((x, y))
    from mathutils.geometry import delaunay_2d_cdt
    pts2 = [Vector(p) for p in outline + inner]
    cdt_v, _, cdt_f, orig_v, _, _ = delaunay_2d_cdt(pts2, [], [list(range(N))], 2, 1e-5)
    verts = [(v.x, v.y, height(v.x, v.y)) for v in cdt_v]
    ring_of = {}
    for out_i, origs in enumerate(orig_v):
        for o in origs:
            if o < N:
                ring_of[o] = out_i
    last = [ring_of[j] for j in range(N)]
    top_faces = [tuple(f) for f in cdt_f]
    # skirt: drops 1.5 m and tucks in
    skirt = []
    for j in range(N):
        x, y, z = verts[last[j]]
        s = 0.96
        skirt.append(len(verts))
        verts.append((x * s, y * s, z - 1.5))
    bottom = len(verts)
    verts.append((0.0, 0.0, -2.2))

    faces, cells = [], []

    def moss_cell(pts):
        # steeper faces darker
        (ax, ay, az), (bx, by, bz), (cx, cy, cz) = pts[:3]
        n = Vector((bx - ax, by - ay, bz - az)).cross(Vector((cx - ax, cy - ay, cz - az))).normalized()
        steep = 1 - abs(n.z)
        cx_, cy_ = sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)
        n2 = noise.noise(Vector((cx_ * 0.15, cy_ * 0.15, 3.0)) + off)
        if steep > 0.25:
            return pick(rng, "moss", [3, 5])
        if n2 > 0.25:
            return pick(rng, "moss", [2, 4, 7])
        if n2 < -0.25:
            return pick(rng, "moss", [1, 3, 6])
        return pick(rng, "moss", [0, 1, 2, 6])

    faces += top_faces
    for f in faces:
        cells.append(moss_cell([verts[i] for i in f]))
    for j in range(N):
        jn = (j + 1) % N
        faces.append((last[j], skirt[j], skirt[jn], last[jn]))
        cells.append(pick(rng, "misc", [4, 6]))
    for j in range(N):
        faces.append((skirt[j], bottom, skirt[(j + 1) % N]))
        cells.append(("basalt", 5))
    b.shell(verts, faces, lambda i, f: cells[i])

    # --- basalt columns on a hex lattice ---
    d = spacing
    rad = d / math.sqrt(3) * 0.95
    extent = int(R * 1.4 / d) + 2
    rim_depth = rim_depth or depth * 0.35
    count = 0
    for j in range(-extent, extent + 1):
        for i in range(-extent, extent + 1):
            x = d * (i + 0.5 * (j % 2)) + rng.uniform(-0.12, 0.12) * d
            y = d * j * math.sqrt(3) / 2 + rng.uniform(-0.12, 0.12) * d
            r = math.hypot(x, y)
            t = math.atan2(y, x)
            edge = Rm(t)
            if r > edge + 0.35 * d:
                continue
            h = height(min(r, edge * 0.97) * math.cos(t), min(r, edge * 0.97) * math.sin(t))
            rr = min(1.0, r / edge)
            rim = r > edge - 0.6 * d
            w = hill_w(x, y)
            side = pick(rng, "basalt", [0, 1, 2, 3, 6])
            top = side
            if rim:
                roll = rng.random()
                if w > 0.5 and roll < 0.7:
                    ztop = h + rng.uniform(1.0, crag) * w
                    top = pick(rng, "moss", [3, 5]) if rng.random() < 0.5 else pick(rng, "basalt", [3, 7])
                elif roll < 0.1:
                    ztop = h + rng.uniform(0.3, 1.8)
                    top = pick(rng, "moss", [0, 3, 5]) if rng.random() < 0.6 else pick(rng, "basalt", [3, 7])
                else:
                    ztop = h + rng.uniform(-2.8, -0.9)
                    top = pick(rng, "basalt", [3, 4, 7])
            else:
                ztop = h - 1.0
            # inverted cone underneath: deep in the middle, shallow at the rim
            base = rim_depth + (depth - rim_depth) * (1 - rr ** 1.7)
            zbot = -base * rng.uniform(0.75, 1.2) - rng.uniform(0, 3.0)
            tip = rng.uniform(0.6, 2.2) if rng.random() < 0.6 else 0.0
            tilt = (rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12)) if rim else (0.0, 0.0)
            prism(b, x, y, rad * rng.uniform(0.9, 1.0), ztop, zbot, 6, math.pi / 6, top, side,
                  bot_cell=pick(rng, "basalt", [5, 0]), tip=tip, tilt=tilt)
            count += 1
    return b.finish(), count


# ---------------------------------------------------------------------------
# Castle ruin: tower + broken wall + fallen blocks. Origin = tower centre at ground.
# ---------------------------------------------------------------------------
def stone(rng, bright=False):
    return pick(rng, "stone", [1, 3, 5, 6] if bright else [0, 1, 2, 6, 7])


def pointed_window(b, center, normal_axis, sign, w, h, cell, depth=0.25):
    """A pointed-arch window slab sticking out of a wall face."""
    cx, cy, cz = center
    prof = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2 - w * 0.45), (0, h / 2), (-w / 2, h / 2 - w * 0.45)]
    if normal_axis == "x":
        to3d = lambda u, v, t: (cx + sign * t, cy + u, cz + v)
    else:
        to3d = lambda u, v, t: (cx + u, cy + sign * t, cz + v)
    extrude_profile(b, prof, to3d, depth, cell, lambda k: cell)


def castle(seed=7):
    rng = random.Random(seed)
    b = Builder("FI_CastleRuin")
    S = 3.0  # tower half width
    H = 14.0
    # plinth
    prism(b, 0, 0, (S + 0.7) * math.sqrt(2), 0.9, -2.0, 4, math.pi / 4, stone(rng), stone(rng))
    # body with a slight taper, one colour per side
    prism(b, 0, 0, S * math.sqrt(2), H, 0.0, 4, math.pi / 4, stone(rng), None,
          side_cells=[stone(rng) for _ in range(4)], top_scale=0.94)
    # corbel band
    prism(b, 0, 0, (S + 0.35) * math.sqrt(2), H + 0.9, H - 0.4, 4, math.pi / 4, ("stone", 4),
          None, side_cells=[stone(rng, True) for _ in range(4)])
    # merlons, some missing
    missing = {2, 7, 8}
    k = 0
    for side in range(4):
        for m in range(3):
            k += 1
            if k in missing:
                continue
            p = -S + 0.55 + m * (2 * S - 1.1) / 2
            q = S + 0.1
            x, y = [(p, -q), (q, p), (-p, q), (-q, -p)][side]
            hgt = rng.uniform(1.0, 1.4)
            box(b, (x, y, H + 0.9 + hgt / 2), (1.2, 1.2, hgt), stone(rng, True))
    # windows: (axis, sign, z) – glowing ones face -X and -Y (camera side after the yaw)
    win_dark = ("misc", 1)
    glow = ("misc", 0)
    s_out = S * 0.97
    for axis, sign in (("x", 1), ("x", -1), ("y", 1), ("y", -1)):
        for z in (6.5, 10.5):
            lit = (sign < 0 and z == 10.5) or (axis == "x" and sign < 0 and z == 6.5)
            so = s_out * (1 - 0.06 * z / H)
            c = (sign * so, 0.0, z) if axis == "x" else (0.0, sign * so, z)
            pointed_window(b, c, axis, sign, 1.0, 1.9, glow if lit else win_dark)
    # door on -Y
    pointed_window(b, (0.0, -S * 0.99, 2.35), "y", -1, 1.8, 2.9, win_dark)

    # broken wall to +X... runs along -X from the tower, 1.6 m thick
    prof = [(0.0, -1.0), (0.0, 8.5), (1.6, 8.5), (2.4, 7.4), (3.6, 7.7), (4.4, 6.2), (5.6, 6.5),
            (6.6, 4.6), (7.6, 4.9), (8.6, 3.0), (9.8, 2.6), (10.6, 1.2), (11.6, 0.6), (11.6, -1.0),
            (6.8, -1.0), (6.8, 2.6), (6.25, 3.85), (5.25, 4.45), (4.15, 3.85), (3.6, 2.6), (3.6, -1.0)]
    # reverse to get CCW in (u, v)
    prof = prof[::-1]
    to3d = lambda u, v, w: (-(S - 0.3) - u, w, v)
    extrude_profile(b, prof, to3d, 1.6, stone(rng), lambda k: stone(rng),
                    top_cell=lambda k: pick(rng, "moss", [3, 5]) if rng.random() < 0.45 else stone(rng))
    # fallen blocks
    for (x, y, s) in ((-9.5, 2.4, 1.1), (-11.0, 1.8, 0.8), (-13.4, -2.0, 0.9), (-6.2, -2.6, 0.7)):
        box(b, (x, y, s * 0.35), (s * 1.3, s, s), stone(rng), rot_z=rng.uniform(0, 3),
            cells=[stone(rng), pick(rng, "moss", [3, 5]), stone(rng), stone(rng), stone(rng), stone(rng)])
    return b.finish()


def tower_stub(seed=11):
    rng = random.Random(seed)
    b = Builder("FI_TowerStub")
    n = 8
    R = 2.8
    # jagged broken top: per-corner heights, via a closed fan
    heights = [rng.uniform(5.5, 9.5) for _ in range(n)]
    heights[2] = 10.5
    heights[3] = 9.8
    verts = []
    for k in range(n):
        a = 2 * math.pi * k / n
        verts.append((math.cos(a) * R * 0.95, math.sin(a) * R * 0.95, heights[k]))
    for k in range(n):
        a = 2 * math.pi * k / n
        verts.append((math.cos(a) * R, math.sin(a) * R, -2.0))
    verts.append((0, 0, min(heights) - 1.2))
    c = 2 * n
    faces, cells = [], []
    for k in range(n):
        m = (k + 1) % n
        faces.append((k, n + k, n + m, m))
        cells.append(stone(rng))
        faces.append((m, c, k))
        cells.append(("stone", 7))
    faces.append(tuple(range(2 * n - 1, n - 1, -1)))
    cells.append(("stone", 7))
    b.shell(verts, faces, lambda i, f: cells[i])
    prism(b, 0, 0, R + 0.6, 0.7, -2.0, n, 0, stone(rng), stone(rng))
    for k, z in ((5, 4.8), (6, 4.2)):
        a = 2 * math.pi * (k + 0.5) / n
        rr = R * math.cos(math.pi / n) * 0.98
        cx, cy = math.cos(a) * rr, math.sin(a) * rr
        # slab facing outward
        prof = [(-0.45, -0.9), (0.45, -0.9), (0.45, 0.5), (0, 0.95), (-0.45, 0.5)]
        tx, ty = -math.sin(a), math.cos(a)
        to3d = lambda u, v, w, cx=cx, cy=cy, a=a, tx=tx, ty=ty: (cx + tx * u + math.cos(a) * w, cy + ty * u + math.sin(a) * w, z + v)
        extrude_profile(b, prof, to3d, 0.3, ("misc", 0) if k == 5 else ("misc", 1), lambda q: ("misc", 1))
    return b.finish()


# ---------------------------------------------------------------------------
# Arch bridge along X, deck top at z = 0, span 30 m
# ---------------------------------------------------------------------------
def bridge(seed=3):
    rng = random.Random(seed)
    b = Builder("FI_ArchBridge")
    L = 15.0
    W = 4.4
    prof = [(-L, 0.0), (-L, -12.0), (-11.6, -12.0), (-11.6, -7.0)]
    seg = 12
    for k in range(1, seg):
        phi = math.pi - math.pi * k / seg
        prof.append((11.6 * math.cos(phi), -7.0 + 5.0 * math.sin(phi)))
    prof += [(11.6, -7.0), (11.6, -12.0), (L, -12.0), (L, 0.0)]
    to3d = lambda u, v, w: (u, w, v)
    extrude_profile(b, prof, to3d, W, stone(rng), lambda k: stone(rng),
                    top_cell=lambda k: ("misc", 3))
    # cornice band
    prism_box = lambda x0, x1, y, z0, z1, wy, cell: box(b, ((x0 + x1) / 2, y, (z0 + z1) / 2), (x1 - x0, wy, z1 - z0), cell)
    prism_box(-L, L, 0, -0.75, -0.2, W + 0.5, stone(rng, True))
    # parapets with breaks
    for y, spans in ((W / 2 - 0.22, [(-L, -5.0), (-3.4, 8.5), (10.2, L)]),
                     (-(W / 2 - 0.22), [(-L, 2.6), (4.6, L)])):
        for x0, x1 in spans:
            prism_box(x0, x1, y, 0.0, 1.0, 0.45, stone(rng))
            # merlon blocks on the parapet
            x = x0 + 0.6
            while x < x1 - 0.8:
                if rng.random() < 0.8:
                    box(b, (x + 0.35, y, 1.25), (0.7, 0.5, 0.5), stone(rng, True))
                x += 1.6
    # moss clumps on the arch spandrels' tops (cornice)
    for _ in range(5):
        x = rng.uniform(-L + 1, L - 1)
        y = rng.choice((-1, 1)) * (W / 2 + 0.05)
        box(b, (x, y, -0.15), (rng.uniform(0.8, 1.8), 0.4, 0.3), pick(rng, "moss", [3, 5]))
    return b.finish()


# ---------------------------------------------------------------------------
# Small props
# ---------------------------------------------------------------------------
def rock(name, seed, flat=0.7):
    rng = random.Random(seed)
    b = Builder(name)
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=1, radius=1.0)
    verts = []
    for v in tmp.verts:
        p = v.co * rng.uniform(0.82, 1.15)
        verts.append((p.x * 1.25, p.y, max(p.z * flat, -0.25)))
    faces = [tuple(v.index for v in f.verts) for f in tmp.faces]
    tmp.free()

    def cell(i, f):
        a, c, d = (Vector(verts[k]) for k in f)
        n = (c - a).cross(d - a).normalized()
        cz = sum(verts[k][2] for k in f) / 3
        if abs(n.z) > 0.6 and cz > 0.3:
            return pick(rng, "moss", [0, 3, 5])
        return pick(rng, "basalt", [1, 2, 3, 4])
    b.shell(verts, faces, cell)
    return b.finish()


def basalt_stub(name, seed, count, height, spread):
    rng = random.Random(seed)
    b = Builder(name)
    pts = [(0.0, 0.0)]
    for k in range(count - 1):
        a = 2 * math.pi * k / (count - 1) + rng.uniform(-0.2, 0.2)
        pts.append((math.cos(a) * spread, math.sin(a) * spread))
    for i, (x, y) in enumerate(pts):
        h = height * (1.0 if i == 0 else rng.uniform(0.35, 0.8))
        tilt = (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))
        top = pick(rng, "moss", [0, 3, 5]) if rng.random() < 0.55 else pick(rng, "basalt", [3, 7])
        prism(b, x, y, spread * 0.6, h, -0.8, 6, math.pi / 6 + rng.uniform(-0.1, 0.1), top,
              pick(rng, "basalt", [0, 1, 2, 6]), tilt=tilt)
    return b.finish()


def pine(name, seed, height):
    rng = random.Random(seed)
    b = Builder(name)
    s = height / 6.5
    prism(b, 0, 0, 0.28 * s, 1.6 * s, -0.4, 5, 0, ("pine", 7), ("pine", 7))
    tiers = [(2.0, 1.1, 3.7), (1.55, 2.7, 5.2), (1.0, 4.1, 6.5)]
    for k, (r, z0, z1) in enumerate(tiers):
        n = 7
        rot = rng.uniform(0, 1)
        verts = [(math.cos(rot + 2 * math.pi * j / n) * r * s * rng.uniform(0.9, 1.08),
                  math.sin(rot + 2 * math.pi * j / n) * r * s * rng.uniform(0.9, 1.08), z0 * s) for j in range(n)]
        verts.append((rng.uniform(-0.1, 0.1) * s, rng.uniform(-0.1, 0.1) * s, z1 * s))
        faces = [(j, (j + 1) % n, n) for j in range(n)]
        faces.append(tuple(range(n - 1, -1, -1)))
        cells = [pick(rng, "pine", [0, 1, 2, 3, 4]) for _ in range(n)] + [("pine", 5)]
        b.shell(verts, faces, lambda i, f: cells[i])
    return b.finish()


def spire(name, seed, height, radius):
    """A tall bundle of basalt columns for the distant backdrop. Origin at the base."""
    rng = random.Random(seed)
    b = Builder(name)
    d = radius * 1.7
    pts = [(0.0, 0.0, 1.0)]
    for k in range(6):
        a = math.pi / 6 + k * math.pi / 3
        pts.append((math.cos(a) * d, math.sin(a) * d, rng.uniform(0.45, 0.85)))
    for k in range(rng.randint(2, 4)):
        a = rng.uniform(0, 2 * math.pi)
        pts.append((math.cos(a) * d * 2, math.sin(a) * d * 2, rng.uniform(0.2, 0.45)))
    for x, y, f in pts:
        top = pick(rng, "moss", [3, 5]) if rng.random() < 0.4 else pick(rng, "far", [3, 4])
        prism(b, x, y, radius, height * f, 0.0, 6, math.pi / 6, top, pick(rng, "far", [0, 1, 2, 5, 6]),
              tilt=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3)))
    return b.finish()


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------
def export(obj):
    scene = bpy.context.scene
    for o in list(scene.collection.objects):
        scene.collection.objects.unlink(o)
    scene.collection.objects.link(obj)
    path = os.path.join(MODELS, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, check_existing=False, use_selection=False,
        object_types={"EMPTY", "MESH"}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", use_space_transform=True, bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        use_custom_props=False, bake_anim=False, path_mode="AUTO", embed_textures=False,
        axis_forward="-Z", axis_up="Y")
    scene.collection.objects.unlink(obj)
    return path


def audit(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    before = [f.normal.copy() for f in bm.faces]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    flipped = sum(1 for f, n in zip(bm.faces, before) if f.normal.dot(n) < 0)
    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    tris = len(bm.faces)
    bm.free()
    return tris, flipped, boundary


def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    os.makedirs(MODELS, exist_ok=True)
    os.makedirs(TEXTURES, exist_ok=True)
    write_palettes()

    objs = []
    main_island, n1 = island("FI_IslandMain", 19.0, 3.0, 28.0, seed=21, hill=4.5, hill_dir=math.radians(15), crag=8.0)
    small_island, n2 = island("FI_IslandSmall", 10.5, 2.6, 17.0, seed=5, hill=1.6, hill_dir=math.radians(100), crag=4.0)
    objs += [main_island, small_island]
    objs.append(castle())
    objs.append(tower_stub())
    objs.append(bridge())
    objs.append(rock("FI_Rock_A", 1))
    objs.append(rock("FI_Rock_B", 2, flat=0.9))
    objs.append(basalt_stub("FI_BasaltStub_A", 3, 5, 2.6, 0.75))
    objs.append(basalt_stub("FI_BasaltStub_B", 4, 4, 1.6, 0.6))
    objs.append(pine("FI_Pine", 8, 6.5))
    d1, _ = island("FI_Debris_A", 2.6, 1.5, 5.5, seed=31, hill=0.6, crag=1.2, rim_depth=1.5)
    d2, _ = island("FI_Debris_B", 1.8, 1.2, 4.0, seed=32, hill=0.0, crag=1.0, rim_depth=1.2)
    objs += [d1, d2]
    objs.append(spire("FI_Spire_A", 41, 90.0, 4.0))
    objs.append(spire("FI_Spire_B", 42, 60.0, 5.0))

    report = {}
    for o in objs:
        tris, flipped, boundary = audit(o)
        export(o)
        report[o.name] = (tris, flipped, boundary)

    # lay out everything for the source .blend
    x = 0.0
    for o in objs:
        bpy.context.scene.collection.objects.link(o)
        o.location.x = x
        x += max(o.dimensions.x, 4) + 6
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "FloatingIsland.blend"))
    total = 0
    for k, (t, fl, bd) in report.items():
        print(f"[fi] {k}: tris={t} flipped={fl} boundary={bd}")
        total += t
    print(f"[fi] columns main={n1} small={n2} total tris={total}")


main()
