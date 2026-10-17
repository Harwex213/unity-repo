"""
The HUD icons of the buildings, rendered from the models the game shows.

Run from the repository root, after build_buildings.py:

    blender -b --python art-sources/Hexwex/build_icons.py

Reads art-sources/Hexwex/Buildings.blend and writes one picture per building
to projects/Hexwex/Assets/Resources/Icons/<art name>.png, the names the HUD
loads. An icon is the building itself, so it changes when the model does: run
this again after the models are rebuilt.
"""

import math
import os

import bpy
import numpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCE_BLEND = os.path.join(HERE, "Buildings.blend")
OUT_FOLDER = os.path.join(ROOT, "projects", "Hexwex", "Assets", "Resources", "Icons")
OUT_PREVIEW = os.path.join(HERE, "Icons_preview.png")

PREFIX = "Bld_"
SIZE = 256
# The picture is rendered larger and brought down, which keeps thin posts and rails whole.
OVERSAMPLE = 2
# How much of the tile the walls and the roof fill. The ground work runs on past
# the tile's edge and is cut by it.
FILL = 0.8
# The camera looks at the front of a building from its right and from above.
VIEW = Vector((0.62, -1.0, 0.52)).normalized()
# The stages a picture is framed on: the walls and the roof.
FRAMED = ("__S1", "__S2")
# A building stands on a rounded tile the colour of what it yields, so that a
# kind is told by its colour where the picture is too small to read.
TILES = {
    "farm": (0.35, 0.66, 0.3),            # food
    "mine": (0.36, 0.46, 0.62),           # stone
    "sawmill": (0.86, 0.5, 0.2),          # wood
    "village": (0.95, 0.7, 0.22),         # people
    "masons-guild": (0.2, 0.6, 0.6),      # hammers
    "observatory": (0.17, 0.24, 0.56),    # scouting
    "university": (0.24, 0.55, 0.92),     # science
    "converter": (0.1, 0.3, 0.33),        # mana
    "stronghold": (0.78, 0.24, 0.27),
}
TILE_FALLBACK = (0.4, 0.42, 0.55)
TILE_CORNER = 0.22
# The dark edge a building throws on its tile, and the soft shadow under it.
CONTOUR_WIDTH = 2.5
CONTOUR_STRENGTH = 0.5
SHADOW_REACH = 7
SHADOW_DROP = 5
SHADOW_STRENGTH = 0.4
SHEET_COLOR = (0.17, 0.16, 0.24)


def sun(name, direction, color, strength, angle):
    data = bpy.data.lights.new(name, "SUN")
    data.color = color
    data.energy = strength
    data.angle = math.radians(angle)
    made = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(made)
    made.rotation_euler = Vector(direction).normalized().to_track_quat("Z", "Y").to_euler()

    return made


def stage():
    """A camera, a warm key light, a cool fill, an even sky, and nothing behind: the tile is painted in afterwards."""
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Icon")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("Icon", camera_data)
    scene.collection.objects.link(camera)
    camera.rotation_euler = VIEW.to_track_quat("Z", "Y").to_euler()
    scene.camera = camera

    # A sun's direction here is where it stands, not where it shines.
    sun("Key", (-0.55, -0.6, 1.0), (1.0, 0.95, 0.86), 3.4, 12)
    sun("Fill", (1.0, -0.25, 0.45), (0.72, 0.82, 1.0), 1.1, 40)
    sun("Rim", (0.3, 1.0, 0.7), (0.9, 0.8, 1.0), 0.9, 30)

    world = bpy.data.worlds.new("Icon")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.62, 0.68, 0.86, 1.0)
    background.inputs["Strength"].default_value = 0.75
    scene.world = world

    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.render.film_transparent = True
    scene.render.use_freestyle = False
    scene.render.resolution_x = SIZE * OVERSAMPLE
    scene.render.resolution_y = SIZE * OVERSAMPLE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    # The palette is drawn as it is painted: no filmic roll-off to grey the colours.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.eevee.taa_render_samples = 64

    return camera


def frame(camera, meshes):
    """Sets the camera so that the walls and the roof of a building fill the tile, centred."""
    framed = [mesh for mesh in meshes if mesh.name.endswith(FRAMED)]
    meshes = framed or meshes
    turn = camera.rotation_euler.to_matrix()
    right = turn @ Vector((1, 0, 0))
    up = turn @ Vector((0, 1, 0))
    across = []
    upward = []
    for mesh in meshes:
        for vertex in mesh.data.vertices:
            point = mesh.matrix_world @ vertex.co
            across.append(point.dot(right))
            upward.append(point.dot(up))

    centre = right * ((min(across) + max(across)) / 2) + up * ((min(upward) + max(upward)) / 2)
    camera.location = centre + VIEW * 20
    camera.data.ortho_scale = max(max(across) - min(across), max(upward) - min(upward)) / FILL
    camera.data.clip_end = 100


def read(path):
    """A picture as rows of straight-alpha RGBA, bottom row first, as Blender keeps it."""
    image = bpy.data.images.load(path)
    width, height = image.size
    pixels = numpy.empty(width * height * 4, dtype=numpy.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)

    return pixels.reshape(height, width, 4)


def write(pixels, path):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new("Out", width, height, alpha=True)
    image.pixels.foreach_set(numpy.ascontiguousarray(pixels, dtype=numpy.float32).ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def shrink(pixels, factor):
    """Brings a picture down by a whole factor, averaging in premultiplied colour."""
    if factor == 1:
        return pixels

    height, width = pixels.shape[0] // factor, pixels.shape[1] // factor
    alpha = pixels[..., 3:4]
    heavy = numpy.concatenate([pixels[..., :3] * alpha, alpha], axis=2)
    heavy = heavy.reshape(height, factor, width, factor, 4).mean(axis=(1, 3))
    alpha = heavy[..., 3:4]
    color = numpy.divide(heavy[..., :3], alpha, out=numpy.zeros_like(heavy[..., :3]), where=alpha > 1e-6)

    return numpy.concatenate([color, alpha], axis=2)


def spread(alpha, radius):
    """Grows a shape by that many pixels all round, with a soft last pixel."""
    grown = alpha.copy()
    reach = int(math.ceil(radius))
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            distance = math.hypot(dx, dy)
            weight = min(1.0, max(0.0, radius + 0.5 - distance))
            if weight <= 0 or (dx == 0 and dy == 0):
                continue

            grown = numpy.maximum(grown, numpy.roll(alpha, (dy, dx), axis=(0, 1)) * weight)

    return grown


def blur(alpha, radius):
    """A box blur run three times, which is near enough a gaussian."""
    blurred = alpha
    for _ in range(3):
        for axis in (0, 1):
            total = numpy.zeros_like(blurred)
            for shift in range(-radius, radius + 1):
                total += numpy.roll(blurred, shift, axis=axis)

            blurred = total / (radius * 2 + 1)

    return blurred


def over(top_color, top_alpha, under_color, under_alpha):
    alpha = top_alpha + under_alpha * (1 - top_alpha)
    color = top_color * top_alpha + under_color * under_alpha * (1 - top_alpha)
    color = numpy.divide(color, alpha, out=numpy.zeros_like(color), where=alpha > 1e-6)

    return color, alpha


def finish(pixels, tint):
    """Stands the render on its tile: a rounded square, lighter at the top, cut clean at the edge."""
    size = pixels.shape[0]
    color = pixels[..., :3]
    alpha = pixels[..., 3:4]
    tint = numpy.array(tint, dtype=numpy.float32)

    # Rows run bottom first.
    rows, columns = numpy.mgrid[0:size, 0:size].astype(numpy.float32)
    height = rows[..., None] / (size - 1)
    tile = (tint * 0.62) * (1 - height) + (tint * 1.12 + 0.1) * height
    # A light behind the building, a little above the middle.
    glow = numpy.hypot(columns - size * 0.5, rows - size * 0.56)[..., None] / (size * 0.62)
    tile = tile + (1 - numpy.clip(glow, 0, 1)) ** 2 * 0.2

    shape = alpha[..., 0]
    shadow = numpy.roll(blur(spread(shape, 2), SHADOW_REACH), -SHADOW_DROP, axis=0)[..., None]
    tile = tile * (1 - shadow * SHADOW_STRENGTH)
    contour = spread(shape, CONTOUR_WIDTH)[..., None]
    tile = tile * (1 - contour * CONTOUR_STRENGTH)

    color = color * alpha + tile * (1 - alpha)

    # How far a pixel is inside the rounded square: negative inside, 0 on the edge.
    half = size / 2 - 1
    corner = size * TILE_CORNER
    away_x = numpy.maximum(numpy.abs(columns - (size - 1) / 2) - (half - corner), 0)
    away_y = numpy.maximum(numpy.abs(rows - (size - 1) / 2) - (half - corner), 0)
    inside = (numpy.hypot(away_x, away_y) - corner)[..., None]
    # A thin bright line just inside the edge, strongest at the top, and a dark rim on it.
    shine = numpy.clip(1 - numpy.abs(inside + 5) / 2, 0, 1) * (0.1 + 0.25 * height)
    rim = numpy.clip(1 - numpy.abs(inside + 1.5) / 2, 0, 1) * 0.45
    color = (color + shine) * (1 - rim)
    mask = numpy.clip(0.5 - inside, 0, 1)

    return numpy.concatenate([numpy.clip(color, 0, 1), mask], axis=2).astype(numpy.float32)


def contact_sheet(icons, out_path, columns=5):
    """All the icons on one sheet the colour of a HUD card, full size and at the sizes the HUD draws them."""
    gap = 16
    cell = SIZE + gap
    rows = (len(icons) + columns - 1) // columns
    small = SIZE // 4 + SIZE // 8 + gap * 2
    sheet = numpy.empty((rows * (cell + small), columns * cell, 4), dtype=numpy.float32)
    sheet[..., :3] = SHEET_COLOR
    sheet[..., 3] = 1.0

    def put(picture, left, top):
        height, width = picture.shape[:2]
        bottom = sheet.shape[0] - top - height
        target = sheet[bottom:bottom + height, left:left + width, :3]
        target[...] = picture[..., :3] * picture[..., 3:4] + target * (1 - picture[..., 3:4])

    for index, icon in enumerate(icons):
        left = (index % columns) * cell + gap // 2
        top = (index // columns) * (cell + small) + gap // 2
        put(icon, left, top)
        put(shrink(icon, 4), left + 40, top + SIZE + gap)
        put(shrink(icon, 8), left + 40 + SIZE // 4 + gap, top + SIZE + gap + SIZE // 16)

    write(sheet, out_path)


def main():
    bpy.ops.wm.open_mainfile(filepath=SOURCE_BLEND)
    camera = stage()
    roots = sorted((made for made in bpy.data.objects if made.name.startswith(PREFIX) and made.parent is None), key=lambda made: made.name)
    every_mesh = [made for made in bpy.data.objects if made.type == "MESH"]
    names = []
    icons = []

    for root in roots:
        own = [made for made in every_mesh if made.parent == root or made == root]
        for made in every_mesh:
            made.hide_render = made not in own

        bpy.context.view_layer.update()
        frame(camera, own)
        path = os.path.join(OUT_FOLDER, root.name[len(PREFIX):] + ".png")
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

        icon = finish(shrink(read(path), OVERSAMPLE), TILES.get(root.name[len(PREFIX):], TILE_FALLBACK))
        write(icon, path)
        names.append(os.path.basename(path))
        icons.append(icon)

    contact_sheet(icons, OUT_PREVIEW)
    print("Hexwex icons: %d written to %s: %s" % (len(names), OUT_FOLDER, ", ".join(names)))


main()
