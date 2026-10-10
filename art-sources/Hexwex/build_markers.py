"""Builds the markers of the Hexwex world map as low-poly models and exports them for Unity.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_markers.py

It writes:
    art-sources/Hexwex/Markers.blend                  the source scene
    art-sources/Hexwex/Markers_preview.png            a picture of every marker
    projects/Hexwex/Assets/Art/Models/Markers.fbx     what Unity imports

A marker is what stands on a scouted cell of the globe: a player's island, a
wild island, the fire over an island whose fight is on, a settlement, the boss's
lair. A cell is about 2 units across, and a marker keeps inside about 0.9.

Two materials are painted by the game, not by this file: `M_Banner` takes the
colour of the player whose island it is, and `M_Tint` takes the colour of the
biome a wild island is made of. The object names are `Mark_<name>`.
"""

import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from lowpoly import HERE, PALETTE, Model, export_fbx, render_preview, report  # noqa: E402

OUT_BLEND = os.path.join(HERE, "Markers.blend")
OUT_PREVIEW = os.path.join(HERE, "Markers_preview.png")

# Shown white here; the game paints them.
PALETTE["M_Banner"] = ((0.900, 0.900, 0.900), 0.70, 0.0)
PALETTE["M_Tint"] = ((0.600, 0.600, 0.600), 0.90, 0.0)

# A flying island hangs this far over the cell it is in.
HOVER = 0.34


def marker(name):
    return Model("Mark_" + name)


def flying_rock(model, radius, top_mat, under_mat, seed=0.0):
    """A slab of land with its root of rock tapering down to a point."""
    model.tube(0.03, HOVER - 0.02, (0, 0, 0.02), under_mat, sides=7, top=radius * 0.92, turn=seed)
    model.tube(radius, 0.07, (0, 0, HOVER), top_mat, sides=7, turn=seed)


def island_player():
    """A player's island: a keep on a flying rock, under a banner in the player's colour."""
    model = marker("island_player")
    ground = HOVER + 0.07
    flying_rock(model, 0.32, "M_Grass", "M_Stone")
    model.box((0.2, 0.18, 0.14), (-0.04, 0.02, ground), "M_Wall")
    model.gable(0.24, 0.22, 0.09, (-0.04, 0.02, ground + 0.14), "M_Roof", along="X")
    model.tube(0.07, 0.24, (0.13, 0.08, ground), "M_Wall", sides=8)
    model.tube(0.09, 0.1, (0.13, 0.08, ground + 0.24), "M_Roof", sides=8, top=0.0)
    model.box((0.05, 0.015, 0.07), (-0.04, -0.075, ground), "M_Wood")
    # The banner: a tall pole, a gold tip and a long flag.
    model.tube(0.012, 0.52, (-0.16, -0.08, ground), "M_Wood", sides=5)
    model.tube(0.022, 0.04, (-0.16, -0.08, ground + 0.52), "M_Metal", sides=5, top=0.0)
    model.box((0.2, 0.012, 0.13), (-0.055, -0.08, ground + 0.37), "M_Banner")

    return model.finish()


def island_wild(name, seed):
    """A wild island: bare rock of its biome's colour, a dead tree and old bones."""
    model = marker(name)
    ground = HOVER + 0.07
    flying_rock(model, 0.27, "M_Tint", "M_Rock_Dark", seed)
    model.blob((0.16, 0.14, 0.16), (0.08, 0.06, ground + 0.03), "M_Rock", seed=int(seed * 10) + 1, rough=0.14, floor=ground)
    model.blob((0.1, 0.1, 0.1), (-0.1, 0.08, ground + 0.02), "M_Rock", seed=int(seed * 10) + 2, rough=0.14, floor=ground)
    model.tube(0.018, 0.2, (-0.06, -0.06, ground), "M_Rock_Dark", sides=5, top=0.008, tilt=("Y", math.radians(8)))
    model.tube(0.008, 0.09, (-0.045, -0.06, ground + 0.11), "M_Rock_Dark", sides=4, top=0.003, tilt=("Y", math.radians(55)))
    model.tube(0.008, 0.07, (-0.04, -0.06, ground + 0.15), "M_Rock_Dark", sides=4, top=0.003, tilt=("Y", math.radians(-50)))
    model.blob((0.05, 0.05, 0.04), (0.1, -0.1, ground + 0.015), "M_Bone", seed=3)
    model.box((0.07, 0.012, 0.012), (0.05, -0.13, ground), "M_Bone", turn=0.5)

    return model.finish()


def beacon():
    """The fire over an island whose fight is on."""
    model = marker("beacon")
    model.tube(0.07, 0.2, (0, 0, 0), "M_Eye", sides=6, top=0.0)
    model.tube(0.045, 0.13, (0.015, 0.01, 0.0), "M_Lava", sides=5, top=0.0, turn=0.6)
    model.tube(0.03, 0.09, (-0.03, 0.02, 0.0), "M_Eye", sides=4, top=0.0, tilt=("Y", math.radians(-14)))

    return model.finish()


def house(model, at, size, turn):
    width, depth, tall = 0.16 * size, 0.13 * size, 0.1 * size
    model.box((width, depth, tall), at, "M_Wall", turn=turn)
    # The roof is a low pyramid, so a house reads the same from any side of the globe.
    model.tube(width * 0.78, tall * 0.8, (at[0], at[1], at[2] + tall), "M_Roof", sides=4, top=0.0, turn=turn + math.pi / 4)


def settlement():
    """A neutral town: a ring of houses round a market cross, behind a low wall."""
    model = marker("settlement")
    model.tube(0.46, 0.03, (0, 0, 0), "M_Stone", sides=10)
    model.tube(0.42, 0.031, (0, 0, 0), "M_Dirt", sides=10)
    for index in range(5):
        angle = index * math.tau / 5 + 0.3
        house(model, (math.cos(angle) * 0.27, math.sin(angle) * 0.27, 0.03), 1.0 + (index % 2) * 0.25, angle)
    model.tube(0.05, 0.03, (0, 0, 0.03), "M_Stone", sides=8)
    model.tube(0.012, 0.3, (0, 0, 0.03), "M_Wood", sides=5)
    model.box((0.12, 0.01, 0.07), (0.06, 0, 0.25), "M_Cloth")

    return model.finish()


def lair():
    """The boss's lair: a black citadel in a crown of spikes, with the poison burning on its head."""
    model = marker("lair")
    model.tube(0.5, 0.05, (0, 0, 0), "M_Basalt", sides=9)
    model.tube(0.44, 0.052, (0, 0, 0), "M_Eye", sides=9)
    model.tube(0.4, 0.056, (0, 0, 0), "M_Basalt", sides=9)
    for index in range(9):
        angle = index * math.tau / 9
        tall = 0.3 + (index % 3) * 0.1
        model.tube(0.07, tall, (math.cos(angle) * 0.43, math.sin(angle) * 0.43, 0.02), "M_Rock_Dark", sides=4, top=0.0, turn=angle, tilt=("Y", math.radians(10)))
    model.tube(0.2, 0.5, (0, 0, 0.05), "M_Night", sides=6, top=0.13)
    model.tube(0.17, 0.08, (0, 0, 0.55), "M_Iron", sides=6)
    model.tube(0.13, 0.36, (0, 0, 0.63), "M_Night", sides=6, top=0.07)
    for index in range(6):
        angle = index * math.tau / 6
        model.tube(0.03, 0.14, (math.cos(angle) * 0.14, math.sin(angle) * 0.14, 0.63), "M_Basalt", sides=4, top=0.0)
    model.blob((0.18, 0.18, 0.2), (0, 0, 1.09), "M_Toxic", seed=4, rough=0.06)
    for angle in (0.4, 2.5, 4.4):
        model.box((0.04, 0.015, 0.1), (math.cos(angle) * 0.175, math.sin(angle) * 0.175, 0.25), "M_Toxic", turn=angle + math.pi / 2)

    return model.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    models = [island_player(), island_wild("island_wild_a", 0.2), island_wild("island_wild_b", 1.4), beacon(), settlement(), lair()]

    render_preview(models, OUT_PREVIEW, columns=3, spacing=1.5)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    export_fbx(models, "Markers.fbx")
    report(models)


main()
