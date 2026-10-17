"""
The Hexwex logo: the six letters of the name, each cast in gold on a hexagon
of dark stone in a gold rim.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_logo.py

Writes projects/Hexwex/Assets/Resources/Icons/logo.png, which the main menu
shows, and a small emblem of one hexagon, logo-mark.png.
"""

import math
import os

import bpy
import numpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_FOLDER = os.path.join(ROOT, "projects", "Hexwex", "Assets", "Resources", "Icons")
OUT_BLEND = os.path.join(HERE, "Logo.blend")

NAME = "HEXWEX"
FONT = "C:/Windows/Fonts/palab.ttf"
# A hexagon's radius is 1. The tiles stand point up, shoulder to shoulder, each a little higher or lower than the last.
STEP = math.sqrt(3) * 1.02
WAVE = 0.2
WIDTH = 1792
HEIGHT = 512
SHADOW_REACH = 14
SHADOW_STRENGTH = 0.7


def material(name, color, metallic, roughness, emission=0.0):
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    bsdf = made.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission

    return made


def hexagon(name, radius, depth, top, bevel, paint, at):
    """A hexagonal plate lying flat, point up, its top face at the given height."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=depth, location=(at[0], at[1], top - depth / 2), rotation=(0, 0, math.radians(30)))
    made = bpy.context.object
    made.name = name
    made.data.materials.append(paint)
    if bevel > 0:
        modifier = made.modifiers.new("Bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.limit_method = "ANGLE"

    return made


def letter(text, font, paint, at, top):
    curve = bpy.data.curves.new("Letter_" + text, "FONT")
    curve.body = text
    curve.font = font
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.size = 1.45
    curve.extrude = 0.07
    curve.bevel_depth = 0.022
    curve.bevel_resolution = 3
    made = bpy.data.objects.new("Letter_" + text, curve)
    bpy.context.scene.collection.objects.link(made)
    made.location = (at[0], at[1] - 0.03, top + 0.07)
    made.data.materials.append(paint)

    return made


def tile(text, font, paints, at):
    gold, stone, glow = paints
    hexagon("Rim_" + text, 1.0, 0.34, 0.0, 0.05, gold, at)
    hexagon("Face_" + text, 0.86, 0.3, 0.03, 0.03, stone, at)
    # A thin lit line inside the rim, as the seam of an inlay.
    hexagon("Seam_" + text, 0.8, 0.3, 0.036, 0.0, glow, at)
    hexagon("Field_" + text, 0.775, 0.3, 0.04, 0.02, stone, at)
    letter(text, font, gold, at, 0.04)


def sun(direction, color, strength, angle):
    data = bpy.data.lights.new("Sun", "SUN")
    data.color = color
    data.energy = strength
    data.angle = math.radians(angle)
    made = bpy.data.objects.new("Sun", data)
    bpy.context.scene.collection.objects.link(made)
    made.rotation_euler = Vector(direction).normalized().to_track_quat("Z", "Y").to_euler()


def stage(width, height, across):
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("Logo")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = across
    camera = bpy.data.objects.new("Logo", camera_data)
    scene.collection.objects.link(camera)
    # Seen from the front and a little from below, so the plates show their thickness.
    view = Vector((0.0, -0.3, 1.0)).normalized()
    camera.location = Vector((0, 0, -0.05)) + view * 20
    camera.rotation_euler = view.to_track_quat("Z", "Y").to_euler()
    scene.camera = camera

    world = bpy.data.worlds.new("Logo")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.9, 0.78, 0.6, 1.0)
    background.inputs["Strength"].default_value = 0.45
    scene.world = world

    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue

    scene.render.film_transparent = True
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.eevee.taa_render_samples = 64

    return camera


def blur(alpha, radius):
    blurred = alpha
    for _ in range(3):
        for axis in (0, 1):
            total = numpy.zeros_like(blurred)
            for shift in range(-radius, radius + 1):
                total += numpy.roll(blurred, shift, axis=axis)

            blurred = total / (radius * 2 + 1)

    return blurred


def shadowed(path):
    """Lays a soft dark shadow under the render, so the logo reads on any picture."""
    image = bpy.data.images.load(path)
    width, height = image.size
    pixels = numpy.empty(width * height * 4, dtype=numpy.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    pixels = pixels.reshape(height, width, 4)

    alpha = pixels[..., 3:4]
    # Rows run bottom first: the shadow falls a little down the picture.
    shadow = numpy.roll(blur(alpha[..., 0], SHADOW_REACH), -6, axis=0)[..., None] * SHADOW_STRENGTH
    out_alpha = alpha + shadow * (1 - alpha)
    color = numpy.divide(pixels[..., :3] * alpha, out_alpha, out=numpy.zeros_like(pixels[..., :3]), where=out_alpha > 1e-6)

    made = bpy.data.images.new("Out", width, height, alpha=True)
    made.pixels.foreach_set(numpy.concatenate([color, out_alpha], axis=2).astype(numpy.float32).ravel())
    made.filepath_raw = path
    made.file_format = "PNG"
    made.save()
    bpy.data.images.remove(made)


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    shadowed(path)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    font = bpy.data.fonts.load(FONT)
    paints = (
        material("M_LogoGold", (0.95, 0.5, 0.1), 1.0, 0.34),
        material("M_LogoStone", (0.012, 0.022, 0.06), 0.0, 0.6),
        material("M_LogoGlow", (0.12, 0.42, 0.95), 0.0, 0.5, 1.6),
    )

    first = -(len(NAME) - 1) / 2 * STEP
    for index, text in enumerate(NAME):
        tile(text, font, paints, (first + index * STEP, WAVE if index % 2 == 0 else -WAVE))

    sun((-0.5, -0.5, 1.0), (1.0, 0.95, 0.85), 3.2, 8)
    sun((0.8, 0.3, 0.6), (0.6, 0.75, 1.0), 1.6, 30)
    sun((0.0, -1.0, 0.25), (1.0, 0.8, 0.55), 1.4, 20)

    across = len(NAME) * STEP + 0.7
    camera = stage(WIDTH, HEIGHT, across)
    render(os.path.join(OUT_FOLDER, "logo.png"))

    # The emblem: the first tile alone.
    for made in bpy.data.objects:
        if made.type in ("MESH", "FONT"):
            made.hide_render = not made.name.endswith("_H")

    scene = bpy.context.scene
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    camera.data.ortho_scale = 2.5
    camera.location.x = first
    camera.location.y += WAVE
    render(os.path.join(OUT_FOLDER, "logo-mark.png"))

    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("Hexwex logo written to " + OUT_FOLDER)


main()
