"""Builds the nine structures of Hexwex as low-poly models and exports them for Unity.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_buildings.py

It writes:
    art-sources/Hexwex/Buildings.blend                  the source scene
    art-sources/Hexwex/Buildings_preview.png            a picture of all nine
    projects/Hexwex/Assets/Art/Models/Buildings.fbx     what Unity imports
    projects/Hexwex/Assets/Art/Models/Materials.json    what the materials are made of

The style and the palette are those of art-sources/Ostrov/Buildings.blend:
flat-shaded blocks, purple roofs, cream walls, stone plinths, gold metal. The
stronghold is that file's castle inside a wall of its own; the other eight are
built here from boxes, cylinders and roofs.

A building is made in the four stages it goes up in, and each stage is its own
mesh (`lowpoly.Staged`): the ground work, the walls, the roof, and the things
that finish it. The game raises them one after another when the building is
built.

Every model stands on z = 0 with its middle on the origin and its front facing
-Y. One hex is 2 units corner to corner, and a model keeps inside about 1.25.
The objects are named `Bld_<art name>`, and the art names are the ones the game
uses (`Buildings.ArtName`, `Stronghold.ArtName`).
"""

import math
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from lowpoly import HERE, ROOT, Staged, export_fbx, placed, render_preview, report  # noqa: E402

OSTROV_BLEND = os.path.join(ROOT, "art-sources", "Ostrov", "Buildings.blend")
OUT_BLEND = os.path.join(HERE, "Buildings.blend")
OUT_PREVIEW = os.path.join(HERE, "Buildings_preview.png")

# Which way a flat thing on a wall looks: the offset out of the wall, and whether it runs along Y.
FACING = {"front": (0, -1, False), "back": (0, 1, False), "left": (-1, 0, True), "right": (1, 0, True)}


def building(art_name):
    return Staged("Bld_" + art_name, art_name)


# ---------- details shared by the buildings ----------

def plate(model, width, tall, at, facing, mat, thick=0.014):
    """A flat panel on a wall: `at` is the middle of its foot on the wall's face."""
    out_x, out_y, along_y = FACING[facing]
    size = (thick, width, tall) if along_y else (width, thick, tall)
    model.box(size, (at[0] + out_x * thick / 2, at[1] + out_y * thick / 2, at[2]), mat)


def window(model, at, facing, width=0.07, tall=0.09, lit=False):
    """A window with a sill and a lintel. A lit one glows."""
    plate(model, width, tall, at, facing, "M_Lamp" if lit else "M_Glass")
    plate(model, width + 0.03, 0.016, (at[0], at[1], at[2] - 0.016), facing, "M_Wood", thick=0.022)
    plate(model, width + 0.03, 0.016, (at[0], at[1], at[2] + tall), facing, "M_Wood", thick=0.022)


def door(model, at, facing, width=0.09, tall=0.15, mat="M_Wood"):
    plate(model, width, tall, at, facing, mat)
    plate(model, width + 0.03, 0.02, (at[0], at[1], at[2] + tall), facing, "M_Rock_Dark", thick=0.022)
    out_x, out_y, _ = FACING[facing]
    model.box((0.014, 0.014, 0.014), (at[0] + width * 0.3 * (1 if out_y else 0) + out_x * 0.016, at[1] + width * 0.3 * (1 if out_x else 0) + out_y * 0.016, at[2] + tall * 0.45), "M_Metal")


def chimney(model, at, tall, mat="M_Stone"):
    model.box((0.06, 0.06, tall), at, mat)
    model.box((0.08, 0.08, 0.02), (at[0], at[1], at[2] + tall), "M_Rock_Dark")


def barrel(model, at):
    model.tube(0.035, 0.08, at, "M_Wood", sides=8)
    for z in (0.015, 0.055):
        model.tube(0.038, 0.01, (at[0], at[1], at[2] + z), "M_Iron", sides=8)


def crate(model, at, size=0.07, turn=0.0):
    model.box((size, size, size), at, "M_Timber", turn=turn)
    model.box((size * 1.04, size * 0.2, size * 1.04), (at[0], at[1], at[2] - size * 0.02), "M_Wood", turn=turn)


def lantern(model, at, tall=0.2):
    model.box((0.016, 0.016, tall), at, "M_Iron")
    model.box((0.04, 0.04, 0.045), (at[0], at[1], at[2] + tall), "M_Lamp")
    model.tube(0.036, 0.03, (at[0], at[1], at[2] + tall + 0.045), "M_Iron", sides=4, top=0.0, turn=math.pi / 4)


def fence(model, start, end, posts=4, tall=0.08):
    """A rail fence from `start` to `end` along the ground."""
    dx, dy = end[0] - start[0], end[1] - start[1]
    length = math.hypot(dx, dy)
    turn = math.atan2(dy, dx)
    for index in range(posts):
        share = index / (posts - 1)
        model.box((0.018, 0.018, tall), (start[0] + dx * share, start[1] + dy * share, start[2]), "M_Wood")
    for z in (tall * 0.35, tall * 0.75):
        model.box((length, 0.01, 0.014), (start[0] + dx / 2, start[1] + dy / 2, start[2] + z), "M_Timber", turn=turn)


def bush(model, at, size, seed):
    model.blob((size, size * 0.9, size * 0.8), (at[0], at[1], at[2] + size * 0.25), "M_Leaf", seed=seed, rough=0.12, floor=at[2])
    model.blob((size * 0.6, size * 0.6, size * 0.6), (at[0] + size * 0.3, at[1] - size * 0.1, at[2] + size * 0.2), "M_Leaf_Light", seed=seed + 1, rough=0.12, floor=at[2])


def flag(made, tag, at, tall, cloth="M_Cloth", length=0.1, drop=0.06, pole=True):
    """
    A pole with a pennant that flies. The cloth is three strips, numbered from the
    pole; the game hangs each on the one before it, so a wave runs down the cloth.
    """
    if pole:
        made.extra.tube(0.008, tall, at, "M_Wood", sides=5)
        made.extra.tube(0.014, 0.03, (at[0], at[1], at[2] + tall), "M_Metal", sides=5, top=0.0)
    strip = length / 3
    for index in range(3):
        start = at[0] + 0.006 + index * strip
        high = drop * (1 - 0.14 * index)
        part = made.mover("Flag", tag, (start, at[1], at[2] + tall - 0.04), number=index)
        part.box((strip + 0.003, 0.007, high), (start + strip / 2, at[1], at[2] + tall - 0.04 - high / 2), cloth)


def keep_banner(castle):
    """Removes the banner on the keep's pole from the castle mesh: the cloth that hangs above everything else."""
    mesh = castle.data
    cloth = [index for index, slot in enumerate(mesh.materials) if slot and slot.name == "M_Cloth"]
    bm = bmesh.new()
    bm.from_mesh(mesh)
    doomed = [face for face in bm.faces if face.material_index in cloth and min(vert.co.z for vert in face.verts) > 1.2]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    print("keep banner faces removed:", len(doomed))


def cottage(house, at, size, turn, lit=False):
    """A village house: a stone footing, timbered cream walls, a stepped roof with a chimney."""
    x, y, ground = at
    wide, deep, tall = 0.3 * size, 0.24 * size, 0.17 * size
    cos, sin = math.cos(turn), math.sin(turn)

    def spot(local_x, local_y, z):
        return (x + local_x * cos - local_y * sin, y + local_x * sin + local_y * cos, ground + z)

    house.base.box((wide + 0.04, deep + 0.04, 0.03), spot(0, 0, 0), "M_Stone", turn=turn)
    house.walls.box((wide, deep, tall), spot(0, 0, 0.03), "M_Wall", turn=turn)
    # The timber frame shows on the corners and under the eaves.
    for corner_x in (-wide / 2, wide / 2):
        for corner_y in (-deep / 2, deep / 2):
            house.walls.box((0.022, 0.022, tall), spot(corner_x, corner_y, 0.03), "M_Wood", turn=turn)
    house.walls.box((wide + 0.012, deep + 0.012, 0.018), spot(0, 0, 0.03 + tall - 0.018), "M_Wood", turn=turn)

    # The roof narrows in steps to its ridge, so it turns with the house.
    roof_tall = 0.15 * size
    steps = 5
    for index in range(steps):
        share = index / steps
        house.roof.box(
            (wide + 0.07, (deep + 0.07) * (1 - share), roof_tall / steps),
            spot(0, 0, 0.03 + tall + roof_tall * share), "M_Roof", turn=turn)
    house.roof.box((0.05 * size, 0.05 * size, roof_tall * 0.9), spot(wide * 0.25, deep * 0.12, 0.03 + tall + roof_tall * 0.25), "M_Stone", turn=turn)
    house.roof.box((0.065 * size, 0.065 * size, 0.014), spot(wide * 0.25, deep * 0.12, 0.03 + tall + roof_tall * 1.15), "M_Rock_Dark", turn=turn)
    if lit:
        house.marker("Smoke", "hearth%d" % round((x + 1) * 50), spot(wide * 0.25, deep * 0.12, 0.03 + tall + roof_tall * 1.2))

    house.extra.box((0.07 * size, 0.016, 0.11 * size), spot(-wide * 0.2, -deep / 2 - 0.008, 0.03), "M_Wood", turn=turn)
    house.extra.box((0.07 * size, 0.014, 0.07 * size), spot(wide * 0.22, -deep / 2 - 0.007, 0.03 + tall * 0.4), "M_Lamp" if lit else "M_Glass", turn=turn)
    house.extra.box((0.1 * size, 0.02, 0.014), spot(wide * 0.22, -deep / 2 - 0.01, 0.03 + tall * 0.4 - 0.014), "M_Wood", turn=turn)


# ---------- the eight buildings ----------

def farm():
    made = building("farm")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.box((1.22, 0.96, 0.03), (0, 0, 0), "M_Dirt")
    base.box((0.5, 0.56, 0.03), (-0.3, 0.14, 0.03), "M_Stone")
    # The field: furrows of dark earth.
    for index in range(5):
        base.box((0.6, 0.07, 0.025), (0.27, -0.33 + index * 0.115, 0.03), "M_Rock_Dark")

    # The barn, with a hayloft door over the big one.
    walls.box((0.44, 0.5, 0.3), (-0.3, 0.14, 0.06), "M_Wood")
    plate(walls, 0.2, 0.2, (-0.3, -0.11, 0.06), "front", "M_Timber")
    plate(walls, 0.012, 0.2, (-0.3, -0.125, 0.06), "front", "M_Wood")
    plate(walls, 0.09, 0.07, (-0.3, -0.11, 0.3), "front", "M_Timber")
    # The silo beside it.
    walls.tube(0.1, 0.44, (-0.02, 0.3, 0.03), "M_Wall", sides=10)
    for z in (0.14, 0.3):
        walls.tube(0.104, 0.014, (-0.02, 0.3, z), "M_Iron", sides=10)

    # A gambrel roof: a steep skirt under a shallow cap.
    roof.gable(0.52, 0.56, 0.12, (-0.3, 0.14, 0.36), "M_Roof", along="Y")
    roof.gable(0.34, 0.58, 0.1, (-0.3, 0.14, 0.44), "M_Roof", along="Y")
    roof.tube(0.115, 0.1, (-0.02, 0.3, 0.47), "M_Roof", sides=10, top=0.0)
    roof.tube(0.012, 0.05, (-0.02, 0.3, 0.57), "M_Metal", sides=5, top=0.0)

    # The crop in its rows, the fence round it and what stands about the yard.
    for index in range(5):
        for step in range(6):
            extra.box((0.05, 0.03, 0.05 + 0.012 * ((index + step) % 3)), (0.02 + step * 0.1, -0.33 + index * 0.115, 0.055), "M_Grass" if (index + step) % 2 else "M_Leaf_Light")
    fence(extra, (-0.06, -0.44, 0.03), (0.58, -0.44, 0.03), posts=5)
    fence(extra, (0.58, -0.44, 0.03), (0.58, 0.2, 0.03), posts=5)
    # The scarecrow.
    extra.box((0.016, 0.016, 0.2), (0.3, 0.02, 0.055), "M_Wood")
    for y in (0.36, 0.44):
        extra.tube(0.045, 0.09, (0.3 - 0.045, y, 0.075), "M_Straw", sides=8, tilt=("Y", math.radians(90)))
    barrel(extra, (-0.5, -0.2, 0.03))
    barrel(extra, (-0.42, -0.24, 0.03))
    # The weather vane on the barn turns with the wind.
    extra.box((0.012, 0.012, 0.06), (-0.3, 0.14, 0.54), "M_Iron")
    vane = made.mover("Sway", "vane", (-0.3, 0.14, 0.6))
    vane.box((0.1, 0.008, 0.03), (-0.3, 0.14, 0.59), "M_Metal")
    vane.tube(0.014, 0.03, (-0.36, 0.14, 0.605), "M_Metal", sides=4, top=0.0, tilt=("Y", math.radians(-90)))
    # A wind pump in the corner of the yard: a braced mast and a fan that turns.
    for x, y in ((0.46, 0.3), (0.54, 0.3), (0.46, 0.38), (0.54, 0.38)):
        extra.taper((0.016, 0.016), (0.012, 0.012), 0.42, (x, y, 0.03), "M_Wood", sides=4, tilt=("X", math.radians(4 if y < 0.34 else -4)))
    extra.box((0.1, 0.1, 0.014), (0.5, 0.34, 0.24), "M_Timber")
    extra.box((0.06, 0.06, 0.03), (0.5, 0.34, 0.44), "M_Iron")
    extra.box((0.012, 0.14, 0.05), (0.5, 0.43, 0.43), "M_Metal")
    fan = made.mover("SpinY", "fan", (0.5, 0.3, 0.455), number=170)
    fan.tube(0.022, 0.03, (0.5, 0.315, 0.455), "M_Iron", sides=8, tilt=("X", math.radians(90)))
    for index in range(8):
        fan.taper((0.012, 0.005), (0.05, 0.005), 0.13, (0.5, 0.3, 0.455), "M_Timber" if index % 2 else "M_Wall", sides=4, tilt=("Y", index * math.tau / 8))
    # The scarecrow shifts on its pole.
    crow = made.mover("Sway", "scarecrow", (0.3, 0.02, 0.055))
    crow.box((0.14, 0.014, 0.014), (0.3, 0.02, 0.2), "M_Wood")
    crow.box((0.07, 0.03, 0.07), (0.3, 0.02, 0.15), "M_Cloth")
    crow.blob((0.05, 0.05, 0.05), (0.3, 0.02, 0.27), "M_Straw", seed=3)

    return made.finish()


def mine():
    made = building("mine")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.box((1.2, 0.96, 0.04), (0, 0, 0), "M_Stone")
    # The hill the adit is driven into.
    base.blob((0.72, 0.5, 0.62), (-0.18, 0.2, 0.1), "M_Rock", seed=5, rough=0.1, floor=0.04)
    base.blob((0.4, 0.36, 0.4), (0.26, 0.26, 0.08), "M_Rock_Dark", seed=9, rough=0.12, floor=0.04)
    base.blob((0.3, 0.26, 0.26), (-0.46, -0.06, 0.06), "M_Rock_Dark", seed=13, rough=0.12, floor=0.04)

    # The timbered mouth of the adit.
    walls.box((0.2, 0.06, 0.2), (-0.18, -0.07, 0.04), "M_Night")
    for x in (-0.3, -0.06):
        walls.box((0.04, 0.09, 0.24), (x, -0.08, 0.04), "M_Wood")
    walls.box((0.32, 0.1, 0.045), (-0.18, -0.08, 0.28), "M_Wood")
    # The headframe over the shaft.
    for x in (0.2, 0.44):
        for y in (-0.12, 0.08):
            walls.box((0.03, 0.03, 0.5), (x, y, 0.04), "M_Wood")
    walls.box((0.3, 0.26, 0.025), (0.32, -0.02, 0.3), "M_Timber")
    walls.box((0.28, 0.02, 0.02), (0.32, -0.12, 0.17), "M_Wood")
    walls.box((0.28, 0.02, 0.02), (0.32, 0.08, 0.17), "M_Wood")

    roof.gable(0.36, 0.32, 0.1, (0.32, -0.02, 0.54), "M_Roof", along="X")

    # The winding wheel, the rails, a loaded cart and the ore that has come up.
    # The winding wheel turns over the shaft.
    wheel = made.mover("SpinY", "winder", (0.32, -0.04, 0.4), number=110)
    wheel.tube(0.07, 0.02, (0.32, -0.03, 0.4), "M_Iron", sides=10, tilt=("X", math.radians(90)))
    for index in range(4):
        wheel.taper((0.014, 0.024), (0.014, 0.024), 0.065, (0.32, -0.04, 0.4), "M_Metal", sides=4, tilt=("Y", index * math.tau / 4 + 0.4))
    extra.tube(0.02, 0.024, (0.32, -0.032, 0.45), "M_Metal", sides=6, tilt=("X", math.radians(90)))
    extra.box((0.012, 0.012, 0.16), (0.32, -0.02, 0.2), "M_Iron")
    # The stamp that breaks the ore: an iron head on a shaft, lifted in its frame and let fall on the block.
    for x in (0.03, 0.19):
        extra.box((0.03, 0.03, 0.34), (x, -0.36, 0.04), "M_Wood")
    extra.box((0.2, 0.04, 0.03), (0.11, -0.36, 0.36), "M_Wood")
    extra.box((0.2, 0.03, 0.02), (0.11, -0.36, 0.24), "M_Timber")
    extra.box((0.12, 0.12, 0.05), (0.11, -0.36, 0.04), "M_Rock_Dark")
    for index, (x, y) in enumerate(((0.07, -0.4), (0.15, -0.41), (0.12, -0.31))):
        extra.blob((0.045, 0.045, 0.035), (x, y, 0.1), "M_Metal" if index == 1 else "M_Rock", seed=index + 21, rough=0.16)
    stamp = made.mover("Pound", "stamp", (0.11, -0.36, 0.09))
    stamp.box((0.075, 0.075, 0.075), (0.11, -0.36, 0.09), "M_Iron")
    stamp.box((0.022, 0.022, 0.25), (0.11, -0.36, 0.165), "M_Timber")
    stamp.box((0.05, 0.05, 0.02), (0.11, -0.36, 0.3), "M_Iron")
    made.marker("Dust", "stamp", (0.11, -0.36, 0.1))
    for x in (-0.22, -0.14):
        extra.box((0.012, 0.36, 0.012), (x, -0.28, 0.04), "M_Iron")
    for y in (-0.42, -0.32, -0.22, -0.12):
        extra.box((0.14, 0.02, 0.008), (-0.18, y, 0.04), "M_Wood")
    extra.box((0.13, 0.16, 0.07), (-0.18, -0.34, 0.075), "M_Wood")
    for y in (-0.39, -0.29):
        for x in (-0.24, -0.12):
            extra.tube(0.022, 0.012, (x, y, 0.072), "M_Iron", sides=8, tilt=("Y", math.radians(90)))
    for index, (x, y) in enumerate(((-0.2, -0.36), (-0.15, -0.31), (-0.19, -0.3))):
        extra.blob((0.06, 0.06, 0.05), (x, y, 0.155), "M_Metal" if index != 1 else "M_Rock", seed=index + 2, rough=0.14)
    for index, (x, y, size) in enumerate(((0.36, -0.36, 0.14), (0.46, -0.3, 0.1), (0.28, -0.3, 0.09), (0.4, -0.42, 0.08))):
        extra.blob((size, size, size * 0.8), (x, y, 0.04 + size * 0.2), "M_Metal" if index % 2 else "M_Rock", seed=index + 7, rough=0.14, floor=0.04)
    lantern(extra, (0.02, -0.14, 0.04), tall=0.2)
    crate(extra, (-0.48, -0.36, 0.04), 0.08, 0.3)

    return made.finish()


def sawmill():
    made = building("sawmill")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.box((1.2, 0.9, 0.05), (0, 0, 0), "M_Stone")
    base.box((0.5, 0.56, 0.02), (-0.32, 0.12, 0.05), "M_Dirt")
    # The mill race under the wheel.
    base.box((0.16, 0.5, 0.012), (-0.6 + 0.08, 0.12, 0.05), "M_Glass")

    # The cabin.
    walls.box((0.42, 0.5, 0.3), (-0.3, 0.12, 0.07), "M_Wood")
    for z in (0.14, 0.22, 0.3):
        walls.box((0.43, 0.51, 0.012), (-0.3, 0.12, z), "M_Timber")
    # The open shed over the saw, and the table it stands on.
    for x in (0.06, 0.52):
        for y in (-0.1, 0.32):
            walls.box((0.045, 0.045, 0.34), (x, y, 0.05), "M_Wood")
    walls.box((0.42, 0.16, 0.12), (0.29, 0.11, 0.05), "M_Wood")

    roof.gable(0.5, 0.6, 0.2, (-0.3, 0.12, 0.37), "M_Roof", along="Y")
    roof.gable(0.6, 0.56, 0.15, (0.29, 0.11, 0.39), "M_Roof", along="X")
    chimney(roof, (-0.42, 0.26, 0.42), 0.18)

    door(extra, (-0.3, -0.13, 0.07), "front")
    window(extra, (-0.09, 0.12, 0.17), "right")
    # The log on the table and the blade that turns through it.
    extra.tube(0.05, 0.38, (0.1, 0.11, 0.22), "M_Timber", sides=8, tilt=("Y", math.radians(90)))
    blade = made.mover("SpinY", "blade", (0.33, 0.111, 0.19), number=560)
    blade.tube(0.14, 0.014, (0.33, 0.118, 0.19), "M_Iron", sides=14, tilt=("X", math.radians(90)))
    for index in range(14):
        blade.taper((0.03, 0.012), (0.004, 0.012), 0.022, (0.33 + math.cos(index * math.tau / 14) * 0.136, 0.111, 0.19 + math.sin(index * math.tau / 14) * 0.136), "M_Iron", sides=4, tilt=("Y", math.pi / 2 - index * math.tau / 14 + 0.5))
    blade.tube(0.03, 0.02, (0.33, 0.121, 0.19), "M_Metal", sides=6, tilt=("X", math.radians(90)))
    # The water wheel on the cabin's side turns in the race.
    wheel = made.mover("SpinX", "wheel", (-0.54, 0.12, 0.27), number=48)
    wheel.tube(0.2, 0.05, (-0.515, 0.12, 0.27), "M_Wood", sides=12, tilt=("Y", math.radians(-90)))
    wheel.tube(0.15, 0.054, (-0.513, 0.12, 0.27), "M_Timber", sides=12, tilt=("Y", math.radians(-90)))
    for index in range(8):
        angle = index * math.tau / 8
        wheel.box((0.07, 0.03, 0.012), (-0.54, 0.12 + math.cos(angle) * 0.2, 0.264 + math.sin(angle) * 0.2), "M_Timber")
    made.marker("Smoke", "chimney", (-0.42, 0.26, 0.64))
    # Logs waiting, planks done, and the stump where the axe lives.
    for index, (y, z) in enumerate(((-0.36, 0.05), (-0.265, 0.05), (-0.17, 0.05), (-0.31, 0.13), (-0.22, 0.13), (-0.265, 0.21))):
        extra.tube(0.047, 0.5, (0.02, y, z + 0.047), "M_Timber" if index in (3, 5) else "M_Wood", sides=8, tilt=("Y", math.radians(90)))
    for index in range(4):
        extra.box((0.3, 0.09, 0.02), (-0.36, -0.33, 0.05 + index * 0.022), "M_Timber", turn=0.06 * (index % 2))
    extra.tube(0.05, 0.06, (-0.1, -0.36, 0.05), "M_Wood", sides=8)
    extra.box((0.012, 0.012, 0.12), (-0.1, -0.36, 0.11), "M_Timber", turn=0.4)
    extra.box((0.05, 0.012, 0.035), (-0.085, -0.355, 0.2), "M_Iron", turn=0.4)
    extra.blob((0.16, 0.12, 0.06), (0.46, -0.06, 0.05), "M_Sand", seed=4, rough=0.08, floor=0.05)

    return made.finish()


def village():
    made = building("village")
    base, extra = made.base, made.extra

    base.tube(0.1, 0.012, (0.0, 0.0, 0.0), "M_Dirt", sides=8)
    for index, (x, y) in enumerate(((0.0, -0.16), (0.02, -0.3), (-0.02, -0.44), (0.13, 0.04), (-0.14, 0.05))):
        base.tube(0.05, 0.012, (x, y, 0.0), "M_Stone", sides=6, turn=index)

    cottage(made, (-0.36, 0.22, 0), 1.0, 0.25, lit=True)
    cottage(made, (0.36, 0.2, 0), 0.94, -0.45)
    cottage(made, (0.3, -0.34, 0), 0.82, 0.12, lit=True)
    cottage(made, (-0.36, -0.3, 0), 0.74, -0.2)

    # The well in the middle, under its little roof.
    made.walls.tube(0.07, 0.07, (0, 0.0, 0.0), "M_Stone", sides=8)
    made.walls.tube(0.05, 0.072, (0, 0.0, 0.0), "M_Night", sides=8)
    for x in (-0.06, 0.06):
        made.walls.box((0.016, 0.016, 0.17), (x, 0.0, 0.0), "M_Wood")
    made.roof.gable(0.18, 0.12, 0.06, (0, 0.0, 0.17), "M_Roof", along="X")
    extra.box((0.12, 0.012, 0.012), (0, 0.0, 0.14), "M_Timber")
    extra.box((0.006, 0.006, 0.06), (0, 0.0, 0.08), "M_Iron")
    lantern(extra, (0.14, -0.14, 0.0), tall=0.18)
    lantern(extra, (-0.16, -0.06, 0.0), tall=0.18)
    fence(extra, (-0.12, 0.44, 0.0), (0.14, 0.44, 0.0), posts=4, tall=0.07)
    bush(extra, (0.04, 0.32, 0.0), 0.12, 3)
    bush(extra, (-0.08, -0.42, 0.0), 0.1, 8)
    # A tree behind the houses.
    extra.tube(0.022, 0.16, (0.0, 0.52, 0.0), "M_Wood", sides=6)
    extra.blob((0.24, 0.22, 0.22), (0.0, 0.52, 0.25), "M_Leaf", seed=6, rough=0.12)
    extra.blob((0.14, 0.14, 0.14), (0.05, 0.48, 0.34), "M_Leaf_Light", seed=7, rough=0.12)

    return made.finish()


def masons_guild():
    made = building("masons-guild")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.box((1.2, 0.94, 0.05), (0, 0, 0), "M_Stone")
    base.box((1.08, 0.82, 0.02), (0, 0, 0.05), "M_Rock_Dark")
    base.box((0.72, 0.56, 0.03), (-0.2, 0.14, 0.07), "M_Stone")

    # The hall, of big dressed blocks with quoins at the corners.
    walls.box((0.66, 0.5, 0.4), (-0.2, 0.14, 0.1), "M_Stone")
    for x in (-0.53, 0.13):
        for y in (-0.11, 0.39):
            for index in range(4):
                walls.box((0.07, 0.07, 0.07), (x, y, 0.1 + index * 0.1), "M_Rock" if index % 2 else "M_Wall")
    walls.box((0.68, 0.52, 0.03), (-0.2, 0.14, 0.3), "M_Rock_Dark")

    # A flat roof behind a parapet, a forge chimney at the back.
    roof.box((0.72, 0.56, 0.05), (-0.2, 0.14, 0.5), "M_Rock_Dark")
    for index in range(6):
        x = -0.5 + index * 0.12
        for y in (-0.12, 0.4):
            roof.box((0.07, 0.05, 0.07), (x, y, 0.55), "M_Stone")
    for index in range(4):
        y = -0.02 + index * 0.11
        for x in (-0.54, 0.14):
            roof.box((0.05, 0.07, 0.07), (x, y, 0.55), "M_Stone")
    chimney(roof, (-0.4, 0.3, 0.55), 0.22, "M_Rock")

    # The great door under the guild's sign, lit windows either side.
    door(extra, (-0.2, -0.11, 0.1), "front", width=0.14, tall=0.24)
    plate(extra, 0.2, 0.05, (-0.2, -0.11, 0.38), "front", "M_Metal", thick=0.02)
    for x in (-0.42, 0.02):
        window(extra, (x, -0.11, 0.2), "front", lit=True)
    flag(made, "guild", (0.16, -0.12, 0.1), 0.5)
    made.marker("Smoke", "forge", (-0.4, 0.3, 0.8))
    # The yard: blocks cut and stacked, and one hanging from the crane.
    for index, (x, y, z, size, turn) in enumerate((
            (0.34, -0.3, 0.07, (0.2, 0.15, 0.1), 0.0), (0.35, -0.3, 0.17, (0.17, 0.13, 0.09), 0.25),
            (0.36, -0.08, 0.07, (0.15, 0.15, 0.1), -0.2), (0.12, -0.34, 0.07, (0.12, 0.1, 0.08), 0.5),
            (0.5, -0.1, 0.07, (0.1, 0.1, 0.07), 0.1))):
        extra.box(size, (x, y, z), "M_Wall" if index % 2 == 0 else "M_Rock_Light", turn=turn)
    extra.box((0.06, 0.06, 0.66), (0.46, 0.3, 0.07), "M_Wood")
    extra.box((0.4, 0.05, 0.05), (0.32, 0.3, 0.66), "M_Wood")
    extra.tube(0.012, 0.3, (0.44, 0.3, 0.4), "M_Timber", sides=4, tilt=("Y", math.radians(-52)))
    load = made.mover("Swing", "load", (0.17, 0.3, 0.66))
    load.box((0.012, 0.012, 0.26), (0.17, 0.3, 0.4), "M_Iron")
    load.box((0.13, 0.11, 0.1), (0.17, 0.3, 0.3), "M_Wall")
    # A statue half out of its block.
    extra.box((0.12, 0.12, 0.05), (-0.46, -0.32, 0.07), "M_Stone")
    extra.box((0.08, 0.07, 0.13), (-0.46, -0.32, 0.12), "M_Rock_Light")
    extra.blob((0.07, 0.07, 0.07), (-0.46, -0.32, 0.29), "M_Rock_Light", seed=2, rough=0.05)
    barrel(extra, (-0.3, -0.3, 0.07))

    return made.finish()


def observatory():
    made = building("observatory")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.tube(0.6, 0.05, (0, 0, 0), "M_Stone", sides=12)
    base.tube(0.5, 0.05, (0, 0, 0.05), "M_Rock_Dark", sides=12)
    base.tube(0.4, 0.04, (0, 0.02, 0.1), "M_Stone", sides=12)
    for index in range(3):
        base.box((0.24 - index * 0.03, 0.08, 0.035), (0, -0.44 + index * 0.06, 0.05 + index * 0.03), "M_Stone")

    # The round tower, banded, with buttresses, and a keeper's lodge against it.
    walls.tube(0.34, 0.66, (0, 0.02, 0.14), "M_Wall", sides=12)
    walls.tube(0.36, 0.06, (0, 0.02, 0.14), "M_Stone", sides=12)
    walls.tube(0.355, 0.05, (0, 0.02, 0.42), "M_Roof", sides=12)
    for angle in (40, 140, 220, 320):
        x, y = math.cos(math.radians(angle)) * 0.35, 0.02 + math.sin(math.radians(angle)) * 0.35
        walls.box((0.09, 0.09, 0.44), (x, y, 0.1), "M_Stone", turn=math.radians(angle))
        walls.tube(0.064, 0.08, (x, y, 0.54), "M_Rock_Dark", sides=4, top=0.0, turn=math.radians(angle) + math.pi / 4)
    walls.box((0.26, 0.22, 0.2), (0.38, 0.22, 0.1), "M_Wall", turn=0.5)

    # The dome on a gold ring, cut open for the telescope.
    roof.tube(0.38, 0.07, (0, 0.02, 0.8), "M_Metal", sides=12)
    roof.dome(0.35, (0, 0.02, 0.87), "M_Roof", squash=0.92)
    roof.box((0.1, 0.38, 0.32), (0, -0.15, 0.87), "M_Night")
    roof.tube(0.2, 0.12, (0.38, 0.22, 0.3), "M_Roof", sides=4, top=0.0, turn=0.5 + math.pi / 4)

    extra.tube(0.055, 0.52, (0, -0.0, 0.96), "M_Iron", sides=8, tilt=("X", math.radians(52)))
    extra.tube(0.07, 0.06, (0, -0.0, 0.96), "M_Metal", sides=8, tilt=("X", math.radians(52)))
    extra.tube(0.064, 0.03, (0, -0.39, 1.265), "M_Glass", sides=8, tilt=("X", math.radians(52)))
    door(extra, (0, -0.32, 0.14), "front", width=0.12, tall=0.2)
    for angle, lit in ((60, True), (120, False), (240, True), (300, False)):
        x, y = math.cos(math.radians(angle)) * 0.335, 0.02 + math.sin(math.radians(angle)) * 0.335
        extra.box((0.07, 0.03, 0.12), (x, y, 0.56), "M_Lamp" if lit else "M_Glass", turn=math.radians(angle + 90))
    # An armillary sphere on a pedestal: three gold rings round a small globe.
    extra.tube(0.05, 0.12, (-0.42, -0.24, 0.1), "M_Stone", sides=6)
    extra.blob((0.06, 0.06, 0.06), (-0.42, -0.24, 0.32), "M_Glass", seed=2)
    rings = made.mover("SpinZ", "armillary", (-0.42, -0.24, 0.32), number=36)
    rings.tube(0.09, 0.012, (-0.42, -0.24, 0.314), "M_Metal", sides=12)
    rings.tube(0.09, 0.012, (-0.42 - 0.006, -0.24, 0.32), "M_Metal", sides=12, tilt=("Y", math.radians(90)))
    rings.tube(0.09, 0.012, (-0.42, -0.24 - 0.006, 0.32), "M_Metal", sides=12, tilt=("X", math.radians(-90)))
    lantern(extra, (0.2, -0.42, 0.05), tall=0.2)
    bush(extra, (-0.3, 0.38, 0.1), 0.12, 5)

    return made.finish()


def university():
    made = building("university")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.box((1.24, 0.92, 0.05), (0, 0, 0), "M_Stone")
    base.box((1.12, 0.8, 0.04), (0, 0.02, 0.05), "M_Rock_Dark")
    for index in range(3):
        base.box((0.5 - index * 0.04, 0.07, 0.03), (0, -0.42 + index * 0.05, 0.05 + index * 0.02), "M_Stone")

    # The great hall between two lower wings.
    walls.box((0.52, 0.46, 0.46), (0, 0.14, 0.09), "M_Wall")
    for x in (-0.4, 0.4):
        walls.box((0.3, 0.38, 0.3), (x, 0.14, 0.09), "M_Wall")
        walls.box((0.32, 0.4, 0.03), (x, 0.14, 0.39), "M_Stone")
    walls.box((0.56, 0.5, 0.04), (0, 0.14, 0.55), "M_Stone")
    walls.box((1.12, 0.48, 0.03), (0, 0.14, 0.09), "M_Stone")
    # The portico: four columns on bases.
    for x in (-0.21, -0.07, 0.07, 0.21):
        walls.tube(0.034, 0.4, (x, -0.2, 0.11), "M_Wall", sides=8)
        walls.box((0.085, 0.085, 0.03), (x, -0.2, 0.11), "M_Stone")
        walls.box((0.085, 0.085, 0.025), (x, -0.2, 0.5), "M_Stone")

    roof.gable(0.6, 0.56, 0.22, (0, 0.14, 0.59), "M_Roof", along="X")
    for x in (-0.4, 0.4):
        roof.gable(0.36, 0.44, 0.14, (x, 0.14, 0.42), "M_Roof", along="Y")
    roof.box((0.56, 0.22, 0.05), (0, -0.19, 0.525), "M_Stone")
    roof.gable(0.56, 0.22, 0.15, (0, -0.19, 0.575), "M_Roof", along="Y")
    # The cupola and its gold spire.
    roof.tube(0.11, 0.16, (0, 0.14, 0.74), "M_Wall", sides=8)
    roof.tube(0.14, 0.03, (0, 0.14, 0.9), "M_Stone", sides=8)
    roof.tube(0.13, 0.2, (0, 0.14, 0.93), "M_Roof", sides=8, top=0.0)

    extra.tube(0.02, 0.12, (0, 0.14, 1.11), "M_Metal", sides=6, top=0.0)
    # The clock in the pediment.
    extra.tube(0.05, 0.014, (0, -0.302, 0.64), "M_Metal", sides=10, tilt=("X", math.radians(90)))
    extra.tube(0.036, 0.016, (0, -0.305, 0.64), "M_Pale", sides=10, tilt=("X", math.radians(90)))
    door(extra, (0, -0.09, 0.11), "front", width=0.14, tall=0.26)
    for x in (-0.19, 0.19):
        window(extra, (x, -0.09, 0.26), "front", tall=0.14, lit=True)
    for x in (-0.46, -0.34, 0.34, 0.46):
        window(extra, (x, -0.05, 0.2), "front", width=0.06, tall=0.1, lit=x in (-0.46, 0.34))
    for x in (-0.55, 0.55):
        window(extra, (x, 0.14, 0.2), "left" if x < 0 else "right", width=0.08, tall=0.1)
    # Banners between the columns, a founder each side of the steps, lamps and hedges.
    for x in (-0.14, 0.14):
        extra.box((0.07, 0.008, 0.2), (x, -0.215, 0.26), "M_Cloth")
        extra.box((0.07, 0.01, 0.02), (x, -0.215, 0.44), "M_Metal")
    for x in (-0.34, 0.34):
        extra.box((0.08, 0.08, 0.08), (x, -0.36, 0.05), "M_Stone")
        extra.box((0.045, 0.04, 0.1), (x, -0.36, 0.13), "M_Rock_Light")
        extra.blob((0.045, 0.045, 0.045), (x, -0.36, 0.255), "M_Rock_Light", seed=3, rough=0.05)
    lantern(extra, (-0.54, -0.36, 0.05), tall=0.22)
    lantern(extra, (0.54, -0.36, 0.05), tall=0.22)
    for x in (-0.5, 0.5):
        extra.box((0.18, 0.06, 0.07), (x, -0.2, 0.09), "M_Leaf")

    return made.finish()


def converter():
    made = building("converter")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra

    base.tube(0.62, 0.07, (0, 0, 0), "M_Stone", sides=12)
    base.tube(0.52, 0.06, (0, 0, 0.07), "M_Rock_Dark", sides=12)
    base.tube(0.38, 0.1, (0, 0, 0.13), "M_Stone", sides=12)
    # The channel the poison runs in, glowing in the floor.
    base.tube(0.47, 0.066, (0, 0, 0.07), "M_Toxic", sides=12)
    base.tube(0.43, 0.07, (0, 0, 0.07), "M_Rock_Dark", sides=12)

    # Four pylons, and the iron cage that holds the vessel.
    for angle in (45, 135, 225, 315):
        x, y = math.cos(math.radians(angle)) * 0.46, math.sin(math.radians(angle)) * 0.46
        turn = math.radians(angle)
        walls.box((0.15, 0.15, 0.56), (x, y, 0.07), "M_Stone", turn=turn)
        walls.box((0.17, 0.17, 0.05), (x, y, 0.07), "M_Rock_Dark", turn=turn)
        walls.box((0.02, 0.02, 0.86), (x * 0.5, y * 0.5, 0.23), "M_Iron", turn=turn)
    for z in (0.23, 0.6, 0.98):
        walls.tube(0.25, 0.07, (0, 0, z), "M_Metal", sides=10)

    roof.tube(0.25, 0.2, (0, 0, 1.05), "M_Iron", sides=10, top=0.1)
    roof.tube(0.1, 0.2, (0, 0, 1.25), "M_Iron", sides=8)
    roof.tube(0.13, 0.05, (0, 0, 1.41), "M_Metal", sides=8)
    for angle in (45, 135, 225, 315):
        x, y = math.cos(math.radians(angle)) * 0.46, math.sin(math.radians(angle)) * 0.46
        roof.box((0.18, 0.18, 0.04), (x, y, 0.63), "M_Metal", turn=math.radians(angle))
        roof.tube(0.11, 0.15, (x, y, 0.67), "M_Metal", sides=4, top=0.0, turn=math.radians(angle) + math.pi / 4)

    # The vessel where the poison turns into clean air, the pipes that feed it, and what it gives off.
    extra.tube(0.2, 0.76, (0, 0, 0.26), "M_Glow", sides=10)
    made.mover("Bob", "orb", (0, 0, 1.55)).dome(0.11, (0, 0, 1.55), "M_Glow")
    for angle in (45, 135, 225, 315):
        x, y = math.cos(math.radians(angle)) * 0.46, math.sin(math.radians(angle)) * 0.46
        extra.box((0.28, 0.05, 0.05), (x * 0.6, y * 0.6, 0.46), "M_Iron", turn=math.radians(angle))
        made.mover("Bob", "spark%d" % angle, (x, y, 0.88)).blob((0.07, 0.07, 0.09), (x, y, 0.88), "M_Glow", seed=int(angle), rough=0.05)
    for angle in (0, 90, 180, 270):
        x, y = math.cos(math.radians(angle)) * 0.5, math.sin(math.radians(angle)) * 0.5
        extra.tube(0.04, 0.16, (x, y, 0.07), "M_Iron", sides=6)
        extra.tube(0.05, 0.02, (x, y, 0.23), "M_Metal", sides=6)
        extra.blob((0.06, 0.06, 0.05), (x, y, 0.27), "M_Toxic", seed=int(angle) + 1, rough=0.05)

    return made.finish()


def stronghold(castle):
    """The Ostrov castle, inside a wall of its own with a tower at each corner."""
    made = building("stronghold")
    base, walls, roof, extra = made.base, made.walls, made.roof, made.extra
    # The keep's banner comes out of the Ostrov castle as one stiff slab; it is cut away here
    # and flown again as strips that wave, like the pennants on the towers.
    keep_banner(castle)
    made.take(castle, placed(0, 0.03, 0.86))
    flag(made, "keep", (0.0, 0.03 - 0.279 * 0.86, 1.375 * 0.86 + 0.04), 0.0, length=0.15, drop=0.12, pole=False)

    base.box((1.12, 1.12, 0.04), (0, 0, 0), "M_Stone")
    base.box((0.98, 0.98, 0.02), (0, 0, 0.04), "M_Dirt")

    reach = 0.5
    # The curtain wall, open at the front for the gate.
    for x in (-reach, reach):
        walls.box((0.07, reach * 2, 0.2), (x, 0, 0.04), "M_Stone")
    walls.box((reach * 2, 0.07, 0.2), (0, reach, 0.04), "M_Stone")
    for x in (-0.325, 0.325):
        walls.box((0.35, 0.07, 0.2), (x, -reach, 0.04), "M_Stone")
    for x in (-0.15, 0.15):
        walls.box((0.09, 0.11, 0.3), (x, -reach, 0.04), "M_Rock")
    walls.box((0.4, 0.1, 0.06), (0, -reach, 0.3), "M_Rock")
    for x in (-reach, reach):
        for y in (-reach, reach):
            walls.tube(0.1, 0.34, (x, y, 0.04), "M_Wall", sides=8)
            walls.tube(0.11, 0.04, (x, y, 0.36), "M_Stone", sides=8)

    for x in (-reach, reach):
        for y in (-reach, reach):
            roof.tube(0.12, 0.18, (x, y, 0.4), "M_Roof", sides=8, top=0.0)
    for index in range(7):
        offset = -0.36 + index * 0.12
        for x in (-reach, reach):
            roof.box((0.07, 0.06, 0.05), (x, offset, 0.24), "M_Stone")
        roof.box((0.06, 0.07, 0.05), (offset, reach, 0.24), "M_Stone")

    # The gate, torches beside it, and a pennant on every tower.
    plate(extra, 0.2, 0.24, (0, -reach - 0.04, 0.04), "front", "M_Wood", thick=0.02)
    for x in (-0.06, 0.0, 0.06):
        plate(extra, 0.012, 0.24, (x, -reach - 0.06, 0.04), "front", "M_Iron", thick=0.01)
    for x in (-0.24, 0.24):
        lantern(extra, (x, -reach - 0.06, 0.04), tall=0.2)
    for x in (-reach, reach):
        for y in (-reach, reach):
            flag(made, "tower%d%d" % (x > 0, y > 0), (x, y, 0.58), 0.16, length=0.09, drop=0.05)

    return made.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # The Ostrov file brings its own materials; the palette only adds the ones it lacks.
    with bpy.data.libraries.load(OSTROV_BLEND) as (source, target):
        target.objects = ["Bld_Castle"]
    castle = target.objects[0]

    models = [farm(), mine(), sawmill(), village(), masons_guild(), observatory(), university(), converter(), stronghold(castle)]
    bpy.data.objects.remove(castle)

    render_preview(models, OUT_PREVIEW)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    export_fbx(models, "Buildings.fbx")
    report(models)


main()
