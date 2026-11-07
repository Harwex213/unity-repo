"""Builds the biome scenery of Hexwex as low-poly pieces and exports them for Unity.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_scenery.py

It writes:
    art-sources/Hexwex/Scenery.blend                  the source scene
    art-sources/Hexwex/Scenery_preview.png            a picture of every piece
    projects/Hexwex/Assets/Art/Models/Scenery.fbx     what Unity imports

A piece is one small thing that stands on an empty hex: a tree, a boulder, a
peak. The game scatters a few of them over a hex of the matching biome
(`Props.CreateScenery`), so a piece is sized against a hex 2 units across, and
most keep under 0.5. The object names are `Scn_<piece name>`.
"""

import math
import random
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from lowpoly import HERE, Model, export_fbx, render_preview, report  # noqa: E402

OUT_BLEND = os.path.join(HERE, "Scenery.blend")
OUT_PREVIEW = os.path.join(HERE, "Scenery_preview.png")


def piece(name):
    return Model("Scn_" + name, soft=0.006)


def pine(name, height, seed):
    """Taiga: a dark fir, its skirts of branches wide at the foot and close at the head."""
    model = piece(name)
    model.taper((0.07, 0.07), (0.03, 0.03), height * 0.5, (0, 0, 0), "M_Wood", sides=8)
    skirts = 6
    for index in range(skirts):
        share = index / (skirts - 1)
        radius = (0.23 - 0.17 * share) * (height / 0.6)
        tall = (0.26 - 0.06 * share) * height
        at = (0.14 + 0.68 * share) * height
        # Each skirt hangs a little off true, and the lowest are the darkest.
        model.tube(radius, tall, (math.sin(seed + index * 2.1) * 0.012, math.cos(seed + index * 1.3) * 0.012, at),
                   "M_Leaf_Pine" if index < 4 else "M_Leaf", sides=7, top=radius * 0.12, turn=seed + index * 0.7)

    return model.finish()


def tree(name, height, seed):
    """Forest: a broad crown of many masses of leaf on a trunk that forks."""
    model = piece(name)
    model.taper((0.085, 0.085), (0.05, 0.05), height * 0.5, (0, 0, 0), "M_Wood", sides=8)
    for index, lean in enumerate((34, -30, 20)):
        model.taper((0.036, 0.036), (0.016, 0.016), height * 0.34, (0, 0, height * 0.42), "M_Wood", sides=6, tilt=("Y" if index < 2 else "X", math.radians(lean)))
    dice = random.Random(seed)
    crown = height * 0.78
    for index in range(7):
        angle = index * math.tau / 6 + dice.uniform(-0.3, 0.3)
        reach = 0.0 if index == 6 else 0.13 + dice.uniform(-0.02, 0.03)
        lift = 0.12 if index == 6 else dice.uniform(-0.04, 0.05)
        size = dice.uniform(0.2, 0.27)
        model.blob((size, size * 0.95, size * 0.82), (math.cos(angle) * reach, math.sin(angle) * reach, crown + lift),
                   "M_Leaf_Light" if index in (1, 6) else "M_Leaf", seed=seed + index, rough=0.09)

    return model.finish()

def jungle(name, height, seed):
    """Rainforest: a tall leaning trunk under wide flat crowns."""
    model = piece(name)
    lean = ("Y", math.radians(6 if seed % 2 else -5))
    model.tube(0.03, height, (0, 0, 0), "M_Wood", sides=6, tilt=lean)
    top = (math.sin(lean[1]) * height, 0, height)
    model.blob((0.5, 0.44, 0.14), top, "M_Leaf_Jungle", seed=seed, rough=0.1)
    model.blob((0.3, 0.34, 0.12), (top[0] - 0.1, 0.06, top[2] - 0.13), "M_Leaf", seed=seed + 3, rough=0.1)
    model.blob((0.24, 0.2, 0.1), (top[0] + 0.12, -0.08, top[2] + 0.08), "M_Leaf_Light", seed=seed + 5, rough=0.1)

    return model.finish()


def fern():
    """Rainforest: a spray of fronds on the ground."""
    model = piece("fern")
    for index in range(6):
        angle = index * math.pi / 3
        model.tube(0.03, 0.2, (0, 0, 0), "M_Leaf_Jungle" if index % 2 else "M_Leaf", sides=4, top=0.0, turn=angle, tilt=("X", math.radians(58)))

    return model.finish()


def acacia():
    """Savanna: an umbrella tree."""
    model = piece("acacia")
    model.tube(0.028, 0.3, (0, 0, 0), "M_Wood", sides=6, tilt=("Y", math.radians(8)))
    model.tube(0.018, 0.16, (0.03, 0, 0.2), "M_Wood", sides=5, tilt=("Y", math.radians(-40)))
    model.blob((0.56, 0.46, 0.1), (0.04, 0, 0.33), "M_Leaf_Dry", seed=4, rough=0.08)
    model.blob((0.3, 0.26, 0.08), (-0.1, 0.03, 0.39), "M_Leaf_Light", seed=6, rough=0.08)

    return model.finish()


def bush(name, mat, seed):
    """A low shrub: dry on the savanna, mossy on the tundra."""
    model = piece(name)
    model.blob((0.2, 0.18, 0.14), (0, 0, 0.05), mat, seed=seed, rough=0.14, floor=0.0)
    model.blob((0.13, 0.12, 0.1), (0.08, 0.04, 0.035), mat, seed=seed + 1, rough=0.14, floor=0.0)

    return model.finish()


def peak(name, radius, height, sides, seed):
    """Mountains: a broken grey horn with snow on its head, a shoulder beside it and scree at its foot."""
    model = piece(name)
    spin = int(seed * 10)
    model.spire(radius, height, (0, 0, 0), "M_Rock", sides=7, seed=spin, rough=0.2, top=radius * 0.06, cap="M_Snow", cap_from=0.58)
    model.spire(radius * 0.62, height * 0.6, (radius * 0.6, -radius * 0.34, 0), "M_Rock_Light", sides=6, seed=spin + 3, rough=0.22, top=radius * 0.05, cap="M_Snow", cap_from=0.82)
    model.spire(radius * 0.45, height * 0.36, (-radius * 0.62, radius * 0.3, 0), "M_Rock", sides=6, seed=spin + 5, rough=0.22, top=radius * 0.06)
    for index in range(5):
        angle = spin + index * 1.3
        model.blob((0.12, 0.1, 0.08), (math.cos(angle) * radius * 0.95, math.sin(angle) * radius * 0.95, 0.02), "M_Rock_Light" if index % 2 else "M_Rock", seed=spin + index, rough=0.16, floor=0.0)

    return model.finish()

def volcano():
    """Volcano: one cone that fills the hex, with fire in its mouth."""
    model = piece("volcano")
    model.tube(0.74, 0.34, (0, 0, 0), "M_Basalt", sides=9, top=0.44)
    model.tube(0.44, 0.3, (0, 0, 0.34), "M_Rock_Dark", sides=9, top=0.22)
    model.tube(0.24, 0.05, (0, 0, 0.62), "M_Basalt", sides=9, top=0.2)
    model.tube(0.17, 0.02, (0, 0, 0.655), "M_Lava", sides=9)
    # Two tongues of lava down the slope.
    model.box((0.06, 0.34, 0.03), (0.05, -0.32, 0.46), "M_Lava", turn=0.15)
    model.box((0.05, 0.26, 0.03), (-0.26, 0.16, 0.44), "M_Lava", turn=1.0)

    return model.finish()


def hill(name, size, seed):
    """Hills: a grassy mound."""
    model = piece(name)
    model.blob((size, size * 0.86, size * 0.62), (0, 0, 0.02), "M_Leaf_Light", seed=seed, rough=0.06, floor=0.0)
    model.blob((size * 0.5, size * 0.44, size * 0.4), (size * 0.26, -size * 0.14, 0.0), "M_Grass", seed=seed + 2, rough=0.06, floor=0.0)

    return model.finish()


def cliff(name, seed):
    """Cliffs: a stand of broken columns, with what has fallen from them lying round."""
    model = piece(name)
    spin = int(seed * 10)
    columns = ((0.0, 0.0, 0.18, 0.58), (0.17, 0.08, 0.14, 0.4), (-0.15, 0.1, 0.13, 0.3), (0.04, -0.17, 0.12, 0.22))
    for index, (x, y, width, tall) in enumerate(columns):
        model.spire(width, tall, (x, y, 0), "M_Rock_Light" if index % 2 == 0 else "M_Rock", sides=6, seed=spin + index, rough=0.14, top=width * 0.66)
    for index in range(4):
        angle = spin + index * 1.7
        model.blob((0.1, 0.09, 0.07), (math.cos(angle) * 0.3, math.sin(angle) * 0.3, 0.02), "M_Rock", seed=spin + 9 + index, rough=0.18, floor=0.0)

    return model.finish()

def crater():
    """Crater: a ring of thrown-up rock around a dark pit, filling the hex."""
    model = piece("crater")
    model.tube(0.46, 0.012, (0, 0, 0), "M_Rock_Dark", sides=10)
    for index in range(9):
        angle = index * math.tau / 9
        reach = 0.56 + (index % 3) * 0.03
        tall = 0.16 + (index % 4) * 0.04
        model.blob((0.3, 0.24, tall * 2), (math.cos(angle) * reach, math.sin(angle) * reach, 0.0), "M_Rock" if index % 2 else "M_Stone", seed=index, rough=0.12, turn=angle, floor=0.0)

    return model.finish()


def mesa(name, scale, seed):
    """Badlands: a stack of red slabs."""
    model = piece(name)
    model.tube(0.22 * scale, 0.1 * scale, (0, 0, 0), "M_Rock_Rust", sides=6, top=0.19 * scale, turn=seed)
    model.tube(0.17 * scale, 0.09 * scale, (0.01, 0.01, 0.1 * scale), "M_Rock_Red", sides=6, top=0.15 * scale, turn=seed + 0.5)
    model.tube(0.16 * scale, 0.06 * scale, (0.0, 0.02, 0.19 * scale), "M_Rock_Rust", sides=6, top=0.13 * scale, turn=seed + 0.9)
    model.blob((0.1, 0.09, 0.07), (0.26 * scale, -0.1, 0.02), "M_Rock_Red", seed=seed, rough=0.14, floor=0.0)

    return model.finish()


def stone(name, seed):
    """Tundra: boulders, the largest with moss on its lee side."""
    model = piece(name)
    model.blob((0.26, 0.22, 0.2), (0, 0, 0.06), "M_Rock", seed=seed, rough=0.16, floor=0.0)
    model.blob((0.14, 0.13, 0.11), (0.17, -0.06, 0.03), "M_Rock_Light", seed=seed + 4, rough=0.18, floor=0.0)
    model.blob((0.09, 0.08, 0.07), (-0.14, 0.1, 0.02), "M_Rock", seed=seed + 6, rough=0.18, floor=0.0)
    model.blob((0.2, 0.16, 0.06), (0.06, 0.07, 0.1), "M_Moss", seed=seed + 1, rough=0.1)

    return model.finish()


def pebbles(name, mat, other, seed):
    """Loose stones on the ground, for wherever the land is bare."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(6):
        size = dice.uniform(0.035, 0.08)
        model.blob((size * 1.3, size, size * 0.7), (dice.uniform(-0.12, 0.12), dice.uniform(-0.12, 0.12), size * 0.2),
                   mat if index % 2 else other, seed=seed + index, rough=0.2, turn=dice.uniform(0, 3), floor=0.0)

    return model.finish()

def ice(name, seed):
    """Polar desert: shards of ice out of a drift of snow."""
    model = piece(name)
    model.blob((0.4, 0.34, 0.1), (0, 0, 0.01), "M_Snow", seed=seed, rough=0.08, floor=0.0)
    shards = ((0.0, 0.0, 0.08, 0.4, 6), (0.1, 0.04, 0.06, 0.26, -14), (-0.09, -0.03, 0.05, 0.2, 16))
    for index, (x, y, width, tall, lean) in enumerate(shards):
        model.tube(width, tall, (x, y, 0), "M_Ice", sides=4, top=0.0, turn=seed + index, tilt=("Y", math.radians(lean)))

    return model.finish()


def dune(name, seed):
    """Desert: a long low drift of sand."""
    model = piece(name)
    model.blob((0.8, 0.44, 0.22), (0, 0, 0.0), "M_Sand", seed=seed, rough=0.05, floor=0.0)
    model.blob((0.44, 0.3, 0.16), (0.22, 0.14, 0.0), "M_Sand", seed=seed + 1, rough=0.05, floor=0.0)

    return model.finish()


def cactus():
    """Desert: the one thing that grows there."""
    model = piece("cactus")
    model.tube(0.04, 0.3, (0, 0, 0), "M_Leaf", sides=6, top=0.032)
    model.tube(0.024, 0.09, (0.04, 0, 0.12), "M_Leaf", sides=5, tilt=("Y", math.radians(90)))
    model.tube(0.024, 0.11, (0.12, 0, 0.12), "M_Leaf", sides=5, top=0.02)
    model.tube(0.022, 0.07, (-0.04, 0, 0.17), "M_Leaf", sides=5, tilt=("Y", math.radians(-90)))
    model.tube(0.022, 0.08, (-0.1, 0, 0.17), "M_Leaf", sides=5, top=0.018)
    model.blob((0.04, 0.04, 0.03), (0, 0, 0.31), "M_Flower", seed=1)

    return model.finish()


def pool():
    """Swamp: a patch of dark standing water."""
    model = piece("pool")
    model.tube(0.26, 0.008, (0, 0, 0), "M_Mud", sides=8)
    model.tube(0.15, 0.008, (0.2, 0.1, 0), "M_Mud", sides=7)
    model.blob((0.12, 0.1, 0.05), (-0.12, 0.14, 0.0), "M_Moss", seed=2, rough=0.1, floor=0.0)

    return model.finish()


def reeds():
    """Swamp: a clump of cattails."""
    model = piece("reeds")
    stalks = ((0.0, 0.0, 0.26, 3), (0.04, 0.03, 0.2, -6), (-0.04, 0.02, 0.22, 7), (0.02, -0.04, 0.17, -4), (-0.03, -0.03, 0.14, 9))
    for x, y, tall, lean in stalks:
        model.tube(0.008, tall, (x, y, 0), "M_Reed", sides=4, tilt=("Y", math.radians(lean)))
        top = (x + math.sin(math.radians(lean)) * tall, y, math.cos(math.radians(lean)) * tall)
        model.tube(0.016, 0.05, top, "M_Dirt", sides=5, tilt=("Y", math.radians(lean)))

    return model.finish()


def snag():
    """Swamp: a dead tree."""
    model = piece("snag")
    model.tube(0.035, 0.34, (0, 0, 0), "M_Rock_Dark", sides=6, top=0.02, tilt=("Y", math.radians(5)))
    model.tube(0.015, 0.16, (0.015, 0, 0.18), "M_Rock_Dark", sides=5, top=0.006, tilt=("Y", math.radians(52)))
    model.tube(0.013, 0.13, (0.02, 0, 0.24), "M_Rock_Dark", sides=5, top=0.005, tilt=("Y", math.radians(-48)))
    model.blob((0.12, 0.1, 0.05), (0.0, 0.0, 0.0), "M_Moss", seed=5, rough=0.1, floor=0.0)

    return model.finish()


def tuft(name, mat, tall, seed, light=None):
    """Grassland and plains: a clump of blades that lean out from its middle, the outer ones shorter."""
    model = piece(name)
    dice = random.Random(int(seed * 100))
    for index in range(11):
        angle = seed + index * math.tau / 11 + dice.uniform(-0.2, 0.2)
        ring = 0.012 if index < 4 else 0.035
        lean = dice.uniform(6, 14) if index < 4 else dice.uniform(22, 38)
        length = tall * (dice.uniform(0.85, 1.0) if index < 4 else dice.uniform(0.5, 0.75))
        model.taper((0.036, 0.01), (0.004, 0.003), length, (math.cos(angle) * ring, math.sin(angle) * ring, 0),
                    light if light and index % 3 == 0 else mat, sides=4, turn=angle + math.pi / 2, tilt=("X", math.radians(-lean)))

    return model.finish()


def flower(name="flower", petals="M_Flower", heart="M_Wood"):
    """Grassland: a flower in the grass, with a ring of petals round its heart and two leaves on its stem."""
    model = piece(name)
    model.tube(0.005, 0.11, (0, 0, 0), "M_Leaf_Light", sides=5)
    for index in range(6):
        angle = index * math.tau / 6
        model.ball((0.03, 0.022, 0.008), (math.cos(angle) * 0.02, math.sin(angle) * 0.02, 0.112), petals, turn=angle)
    model.ball((0.022, 0.022, 0.014), (0, 0, 0.115), heart)
    for side in (-1, 1):
        model.taper((0.016, 0.005), (0.002, 0.002), 0.07, (0, 0, 0.01), "M_Leaf_Light", sides=4, turn=side * 1.2, tilt=("X", math.radians(-48)))

    return model.finish()

# ---------- the detail of a hex, after the tiles of Civilization V: dense, varied, ground cover under everything ----------

def fir(name, height, seed, light_top=True):
    """Taiga: a fir drawn tall and narrow, its skirts ragged, the young growth at the top lighter."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.05, 0.05), (0.02, 0.02), height * 0.45, (0, 0, 0), "M_Wood", sides=7)
    skirts = 7
    for index in range(skirts):
        share = index / (skirts - 1)
        radius = (0.17 - 0.13 * share) * (height / 0.6) * dice.uniform(0.92, 1.08)
        tall = (0.2 - 0.04 * share) * height
        at = (0.12 + 0.72 * share) * height
        mat = "M_Leaf_Fir" if light_top and index >= skirts - 2 else "M_Leaf_Pine"
        model.tube(radius, tall, (dice.uniform(-0.008, 0.008), dice.uniform(-0.008, 0.008), at), mat, sides=8, top=radius * 0.1, turn=dice.uniform(0, 6.3))
    model.tube(0.012, height * 0.12, (0, 0, height * 0.94), "M_Leaf_Fir", sides=5, top=0.0)

    return model.finish()


def dead_fir():
    """Taiga: a fir that died standing, grey and bare, its stubs of branches still on it."""
    model = piece("fir_dead")
    model.taper((0.04, 0.04), (0.008, 0.008), 0.5, (0, 0, 0), "M_Rock", sides=7)
    for index in range(6):
        z = 0.12 + index * 0.06
        angle = index * 2.1
        model.taper((0.01, 0.01), (0.002, 0.002), 0.08 - index * 0.008, (0, 0, z), "M_Rock", sides=4, turn=angle, tilt=("Y", math.radians(70)))

    return model.finish()


def log():
    """Taiga: a fallen trunk with its torn root plate, half sunk in moss."""
    model = piece("log")
    model.taper((0.05, 0.05), (0.035, 0.035), 0.42, (-0.2, 0, 0.04), "M_Wood", sides=9, tilt=("Y", math.radians(90)))
    model.tube(0.052, 0.012, (-0.215, 0, 0.04), "M_Timber", sides=9, tilt=("Y", math.radians(90)))
    model.blob((0.06, 0.16, 0.14), (-0.23, 0, 0.06), "M_Dirt", seed=4, rough=0.25)
    model.blob((0.2, 0.12, 0.04), (0.02, 0.04, 0.01), "M_Moss", seed=5, rough=0.15, floor=0.0)

    return model.finish()


def stump(name, seed):
    """Taiga: the stump of a felled tree, its cut face pale, a toadstool beside it."""
    model = piece(name)
    model.taper((0.06, 0.06), (0.05, 0.05), 0.06, (0, 0, 0), "M_Wood", sides=9)
    model.tube(0.048, 0.006, (0, 0, 0.06), "M_Timber", sides=9)
    for index in range(4):
        model.taper((0.03, 0.02), (0.01, 0.01), 0.05, (0, 0, 0.01), "M_Wood", sides=4, turn=index * 1.57 + seed, tilt=("Y", math.radians(75)))
    mushroom(model, (0.07, 0.03, 0), 1.0)

    return model.finish()


def mushroom(model, at, size):
    model.taper((0.012 * size, 0.012 * size), (0.009 * size, 0.009 * size), 0.035 * size, at, "M_Bone", sides=6)
    model.ball((0.045 * size, 0.045 * size, 0.022 * size), (at[0], at[1], at[2] + 0.037 * size), "M_Cloth")
    for index in range(3):
        angle = index * 2.1
        model.ball((0.009 * size, 0.009 * size, 0.005 * size), (at[0] + math.cos(angle) * 0.012 * size, at[1] + math.sin(angle) * 0.012 * size, at[2] + 0.046 * size), "M_Pale")


def mushrooms():
    model = piece("mushrooms")
    for index, (x, y, size) in enumerate(((0, 0, 1.2), (0.04, 0.02, 0.8), (-0.03, 0.03, 0.7), (0.01, -0.04, 0.6))):
        mushroom(model, (x, y, 0), size)

    return model.finish()


def moss_patch(name, mat, seed):
    """Ground cover: a low cushion of moss or turf that breaks up the flat of the tile."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(4):
        size = dice.uniform(0.1, 0.2)
        model.blob((size, size * 0.8, 0.035), (dice.uniform(-0.08, 0.08), dice.uniform(-0.08, 0.08), 0.0), mat, seed=seed + index, rough=0.18, floor=0.0)

    return model.finish()


def acacia_big():
    """Savanna: a great umbrella tree, its trunk forking into two, its crown flat and wide in two tiers."""
    model = piece("acacia_big")
    model.taper((0.06, 0.06), (0.04, 0.04), 0.26, (0, 0, 0), "M_Wood", sides=8, tilt=("Y", math.radians(6)))
    for lean, length in ((-34, 0.2), (30, 0.22), (8, 0.18)):
        model.taper((0.03, 0.03), (0.016, 0.016), length, (0.02, 0, 0.24), "M_Wood", sides=6, tilt=("Y", math.radians(lean)))
    model.blob((0.7, 0.56, 0.08), (0.03, 0, 0.42), "M_Leaf_Dry", seed=8, rough=0.07)
    model.blob((0.5, 0.42, 0.07), (0.0, 0.03, 0.47), "M_Leaf", seed=9, rough=0.07)
    model.blob((0.3, 0.26, 0.06), (-0.1, -0.06, 0.51), "M_Leaf_Light", seed=10, rough=0.07)

    return model.finish()


def tall_grass(name, mat, seed, heads=True):
    """Savanna and plains: a clump of tall grass with seed heads that nod at the tips."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(20):
        angle = index * math.tau / 20 + dice.uniform(-0.2, 0.2)
        lean = dice.uniform(4, 30)
        length = dice.uniform(0.15, 0.26)
        at = (math.cos(angle) * 0.03, math.sin(angle) * 0.03, 0)
        model.taper((0.04, 0.01), (0.004, 0.003), length, at, mat, sides=4, turn=angle + math.pi / 2, tilt=("X", math.radians(-lean)))
        if heads and index % 3 == 0:
            tip = (at[0] + math.cos(angle) * math.sin(math.radians(lean)) * length * 0.95,
                   at[1] + math.sin(angle) * math.sin(math.radians(lean)) * length * 0.95,
                   math.cos(math.radians(lean)) * length * 0.95)
            model.ball((0.014, 0.014, 0.04), tip, "M_Wheat", turn=angle, tilt=("X", math.radians(-lean)))

    return model.finish()


def termite_mound():
    """Savanna: a termite mound, a tall lump of red earth with towers on its shoulders."""
    model = piece("termite")
    model.spire(0.09, 0.3, (0, 0, 0), "M_Rock_Rust", sides=8, seed=3, rough=0.12, top=0.02)
    model.spire(0.05, 0.16, (0.06, 0.03, 0), "M_Rock_Red", sides=7, seed=4, rough=0.14, top=0.012)
    model.spire(0.04, 0.12, (-0.05, -0.04, 0), "M_Rock_Rust", sides=7, seed=5, rough=0.14, top=0.01)
    model.blob((0.24, 0.22, 0.05), (0, 0, 0.0), "M_Rock_Red", seed=6, rough=0.15, floor=0.0)

    return model.finish()


def kopje():
    """Savanna: a kopje, a pile of rounded boulders warm in the sun, grass at its foot."""
    model = piece("kopje")
    for index, (x, y, size) in enumerate(((0, 0, 0.18), (0.11, 0.04, 0.12), (-0.08, 0.07, 0.1), (0.03, -0.1, 0.09), (0.0, 0.02, 0.1))):
        z = 0.06 if index == 4 else size * 0.3
        model.blob((size, size * 0.9, size * 0.75), (x, y, z), "M_Sand" if index % 2 else "M_Rock_Light", seed=20 + index, rough=0.08, floor=0.0)

    return model.finish()


def wheat_patch(name, seed):
    """Plains: a patch of ripe grain, rows of stalks with heavy heads over a strip of tilled earth."""
    model = piece(name)
    dice = random.Random(seed)
    model.blob((0.36, 0.26, 0.025), (0, 0, 0.0), "M_Straw", seed=seed, rough=0.08, floor=0.0)
    for row in range(5):
        for column in range(8):
            x = -0.14 + column * 0.04 + dice.uniform(-0.008, 0.008)
            y = -0.08 + row * 0.04 + dice.uniform(-0.008, 0.008)
            if (x / 0.17) ** 2 + (y / 0.12) ** 2 > 1:
                continue
            tall = dice.uniform(0.09, 0.13)
            lean = dice.uniform(-8, 8)
            model.taper((0.008, 0.008), (0.005, 0.005), tall, (x, y, 0.01), "M_Wheat", sides=4, tilt=("X", math.radians(lean)))
            model.ball((0.02, 0.02, 0.045), (x, y - math.sin(math.radians(lean)) * tall, 0.01 + tall), "M_Straw", tilt=("X", math.radians(lean)))

    return model.finish()


def hay():
    """Plains: two round bales of hay left in the field."""
    model = piece("hay")
    for index, (x, y, turn) in enumerate(((0, 0, 0.3), (0.1, 0.06, 1.2))):
        model.tube(0.045, 0.07, (x - math.cos(turn) * 0.035, y - math.sin(turn) * 0.035, 0.045), "M_Straw", sides=12, turn=turn, tilt=("Y", math.radians(90)))
        model.tube(0.047, 0.012, (x, y, 0.045), "M_Wheat", sides=12, turn=turn, tilt=("Y", math.radians(90)))

    return model.finish()


def oak():
    """Plains: a lone broad tree with a short thick trunk, the kind that stands alone in a field."""
    model = piece("oak")
    dice = random.Random(77)
    model.taper((0.08, 0.08), (0.055, 0.055), 0.2, (0, 0, 0), "M_Wood", sides=8)
    for lean in (-38, 34):
        model.taper((0.035, 0.035), (0.016, 0.016), 0.16, (0, 0, 0.18), "M_Wood", sides=6, tilt=("Y", math.radians(lean)))
    for index in range(9):
        angle = index * math.tau / 8
        reach = 0.0 if index == 8 else 0.15
        size = dice.uniform(0.18, 0.24)
        model.blob((size, size, size * 0.8), (math.cos(angle) * reach, math.sin(angle) * reach, 0.34 + (0.08 if index == 8 else dice.uniform(-0.03, 0.03))),
                   "M_Leaf_Light" if index % 3 == 0 else "M_Leaf", seed=30 + index, rough=0.1)

    return model.finish()


def crater_rim():
    """
    Crater: the crater itself. A rim of broken rock thrown up all round, steep
    inside and lower outside, a bowl of dark ash within it, cracks that run out
    from the middle, and in the middle a pool of the old poison, glowing low.
    """
    model = piece("crater_rim")
    dice = random.Random(41)
    # The ash floor of the bowl, and the pool in it.
    # The ash lies in uneven drifts, not one disc.
    for index in range(5):
        angle = index * math.tau / 5 + dice.uniform(-0.4, 0.4)
        model.blob((dice.uniform(0.3, 0.42), dice.uniform(0.24, 0.34), 0.03), (math.cos(angle) * 0.17, math.sin(angle) * 0.17, 0.0),
                   "M_Rock_Dark" if index % 2 else "M_Basalt", seed=1 + index, rough=0.14, turn=angle, floor=0.0)
    model.blob((0.24, 0.2, 0.025), (0.02, -0.01, 0.012), "M_Toxic_Pool", seed=2, rough=0.12, floor=0.0)
    model.blob((0.32, 0.28, 0.02), (0.02, -0.01, 0.004), "M_Rock_Dark", seed=3, rough=0.1, floor=0.0)
    # Cracks running out from the pool, black in the grey ash.
    for index in range(5):
        angle = index * math.tau / 5 + dice.uniform(-0.5, 0.5)
        start = Vector((math.cos(angle) * 0.12, math.sin(angle) * 0.12, 0.022))
        # A crack zigzags: each length turns a little off the last.
        for step in range(3):
            angle += dice.uniform(-0.6, 0.6)
            length = dice.uniform(0.06, 0.1)
            model.taper((0.014 - step * 0.004, 0.006), (0.008 - step * 0.002, 0.004), length, start, "M_Crack", sides=4, turn=angle - math.pi / 2, tilt=("X", math.radians(-88)))
            start = start + Vector((math.cos(angle), math.sin(angle), 0)) * length * 0.95
    # The rim: a ring of rock slabs and spurs, leaning out, with gaps where it broke.
    count = 22
    for index in range(count):
        angle = index * math.tau / count + dice.uniform(-0.08, 0.08)
        if index in (5, 6, 15):
            continue
        reach = dice.uniform(0.58, 0.68)
        tall = dice.uniform(0.1, 0.22)
        at = (math.cos(angle) * reach, math.sin(angle) * reach, 0)
        mat = ("M_Rock", "M_Stone", "M_Rock_Light", "M_Rock_Dark")[index % 4]
        model.blob((0.18, 0.14, tall * 2), at, mat, seed=50 + index, rough=0.16, turn=angle, floor=0.0)
        if index % 3 == 0:
            model.spire(0.06, tall + 0.08, (at[0] * 0.97, at[1] * 0.97, 0), "M_Rock", sides=5, seed=70 + index, rough=0.2, top=0.01)
    # The inner slope: smaller stones tumbled toward the middle.
    for index in range(14):
        angle = dice.uniform(0, math.tau)
        reach = dice.uniform(0.32, 0.52)
        size = dice.uniform(0.04, 0.09)
        model.blob((size, size * 0.9, size * 0.7), (math.cos(angle) * reach, math.sin(angle) * reach, 0.0), "M_Rock" if index % 2 else "M_Stone", seed=90 + index, rough=0.2, floor=0.0)

    return model.finish()


def vent():
    """Crater: a little vent of rock with the poison seeping out round it."""
    model = piece("vent")
    model.spire(0.05, 0.08, (0, 0, 0), "M_Rock_Dark", sides=6, seed=5, rough=0.2, top=0.025)
    model.tube(0.02, 0.012, (0, 0, 0.075), "M_Toxic_Pool", sides=8)
    model.blob((0.12, 0.1, 0.015), (0.02, 0.02, 0.0), "M_Toxic_Pool", seed=6, rough=0.2, floor=0.0)

    return model.finish()


def birch(name, height, seed):
    """Meadow: a slim birch, its white trunk marked with dark bands, a light airy crown in three clouds."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.03, 0.03), (0.016, 0.016), height * 0.75, (0, 0, 0), "M_Snow", sides=7, tilt=("Y", math.radians(dice.uniform(-5, 5))))
    for index in range(4):
        z = height * (0.12 + index * 0.15)
        model.taper((0.033 - index * 0.004, 0.033 - index * 0.004), (0.032 - index * 0.004, 0.032 - index * 0.004), 0.012, (0, 0, z), "M_Night", sides=7)
    for index in range(4):
        angle = index * math.tau / 4 + dice.uniform(-0.4, 0.4)
        reach = 0.0 if index == 3 else 0.07
        size = dice.uniform(0.14, 0.2)
        model.blob((size, size, size * 1.15), (math.cos(angle) * reach, math.sin(angle) * reach, height * (0.72 + 0.12 * (index == 3))),
                   "M_Leaf_Light" if index % 2 == 0 else "M_Grass", seed=seed + index, rough=0.12)

    return model.finish()


def flower_bed(name, colors, seed):
    """Meadow: a drift of wildflowers, many small heads standing over a cushion of leaves."""
    model = piece(name)
    dice = random.Random(seed)
    model.blob((0.2, 0.16, 0.03), (0, 0, 0.0), "M_Leaf", seed=seed, rough=0.2, floor=0.0)
    for index in range(16):
        angle = dice.uniform(0, math.tau)
        reach = math.sqrt(dice.random()) * 0.09
        at = (math.cos(angle) * reach, math.sin(angle) * reach, 0.0)
        tall = dice.uniform(0.04, 0.08)
        model.taper((0.004, 0.004), (0.003, 0.003), tall, at, "M_Grass", sides=4)
        model.ball((0.018, 0.018, 0.01), (at[0], at[1], tall), colors[index % len(colors)])

    return model.finish()


def berry_bush():
    """Meadow: a round green bush hung with red berries."""
    model = piece("bush_berry")
    model.blob((0.2, 0.18, 0.15), (0, 0, 0.06), "M_Leaf", seed=12, rough=0.12, floor=0.0)
    model.blob((0.13, 0.12, 0.11), (0.07, 0.04, 0.09), "M_Leaf_Light", seed=13, rough=0.12)
    dice = random.Random(14)
    for index in range(9):
        angle = dice.uniform(0, math.tau)
        model.ball((0.016, 0.016, 0.016), (math.cos(angle) * 0.09, math.sin(angle) * 0.08, dice.uniform(0.05, 0.13)), "M_Cloth")

    return model.finish()


def pond():
    """Meadow: a small clear pond with reeds at its edge and stones on its bank."""
    model = piece("pond")
    model.blob((0.3, 0.22, 0.025), (0, 0, 0.0), "M_Dirt", seed=31, rough=0.1, floor=0.0)
    model.blob((0.26, 0.18, 0.02), (0, 0, 0.006), "M_Glass", seed=32, rough=0.08, floor=0.0)
    dice = random.Random(33)
    for index in range(6):
        angle = dice.uniform(0, math.tau)
        model.blob((0.04, 0.035, 0.03), (math.cos(angle) * 0.14, math.sin(angle) * 0.1, 0.0), "M_Rock_Light", seed=40 + index, rough=0.2, floor=0.0)
    for index in range(7):
        angle = dice.uniform(-0.6, 0.6)
        model.taper((0.008, 0.008), (0.002, 0.002), dice.uniform(0.08, 0.14), (math.cos(angle) * 0.13, math.sin(angle) * 0.08, 0), "M_Reed", sides=4, tilt=("X", math.radians(dice.uniform(-12, 12))))

    return model.finish()


def massif(name, seed):
    """
    Mountains: a range that fills the hex, as a Civilization mountain tile has
    it. One tall horn and three lower ones round it, joined by ridges of broken
    rock, snow on every head and in the gullies, scree fanning out at the foot.
    """
    model = piece(name)
    dice = random.Random(seed)
    peaks = [(0.0, 0.0, 0.36, 1.05)]
    for index in range(3):
        angle = index * math.tau / 3 + dice.uniform(-0.4, 0.4)
        reach = dice.uniform(0.3, 0.42)
        peaks.append((math.cos(angle) * reach, math.sin(angle) * reach, dice.uniform(0.2, 0.27), dice.uniform(0.5, 0.72)))
    for index, (x, y, radius, tall) in enumerate(peaks):
        model.spire(radius, tall, (x, y, 0), "M_Rock" if index % 2 == 0 else "M_Rock_Light", sides=8, seed=seed * 10 + index, rough=0.22,
                    top=radius * 0.05, cap="M_Snow", cap_from=0.55 if index == 0 else 0.7)
    # Ridges: a line of rock lumps from the great horn down to each lesser one.
    for x, y, radius, tall in peaks[1:]:
        for step in range(1, 4):
            share = step / 4
            size = 0.18 - 0.03 * step
            model.blob((size, size * 0.8, size * 1.2), (x * share, y * share, tall * 0.35 * (1 - share) + 0.12), "M_Rock",
                       seed=int(seed * 100 + step * 7 + x * 50), rough=0.25, floor=0.0)
    # Snow lying in the gullies between the horns.
    for index in range(3):
        angle = index * math.tau / 3 + 1.0
        model.blob((0.16, 0.1, 0.07), (math.cos(angle) * 0.18, math.sin(angle) * 0.18, 0.45), "M_Snow", seed=seed + 40 + index, rough=0.2)
    # Scree fanning out round the foot.
    for index in range(16):
        angle = dice.uniform(0, math.tau)
        reach = dice.uniform(0.45, 0.66)
        size = dice.uniform(0.05, 0.11)
        model.blob((size, size * 0.85, size * 0.7), (math.cos(angle) * reach, math.sin(angle) * reach, 0.0), "M_Rock_Light" if index % 3 else "M_Stone",
                   seed=seed + 60 + index, rough=0.22, floor=0.0)

    return model.finish()


def rolling_hills(name, seed):
    """Hills: two or three rounded green rises that run across the hex, a band of bare earth where one breaks."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        angle = index * math.tau / 3 + dice.uniform(-0.5, 0.5)
        reach = dice.uniform(0.18, 0.32)
        size = dice.uniform(0.5, 0.66)
        at = (math.cos(angle) * reach, math.sin(angle) * reach, 0.0)
        model.blob((size, size * 0.8, size * 0.42), at, "M_Leaf_Light" if index % 2 else "M_Grass", seed=seed + index, rough=0.07, turn=angle, floor=0.0)
        if index == 0:
            model.blob((size * 0.4, size * 0.22, size * 0.2), (at[0] * 1.25, at[1] * 1.25, 0.0), "M_Dirt", seed=seed + 9, rough=0.1, turn=angle, floor=0.0)

    return model.finish()


def outcrop(name, seed):
    """Hills: a ledge of layered grey rock breaking through the turf."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(4):
        size = (0.2 - index * 0.035) * dice.uniform(0.9, 1.1)
        model.spire(size * 0.5, 0.05, (dice.uniform(-0.02, 0.02), dice.uniform(-0.02, 0.02), index * 0.045), "M_Rock_Light" if index % 2 else "M_Stone",
                    sides=6, seed=seed + index, rough=0.18, top=size * 0.42)
    for index in range(4):
        angle = dice.uniform(0, math.tau)
        model.blob((0.05, 0.045, 0.04), (math.cos(angle) * 0.13, math.sin(angle) * 0.12, 0.0), "M_Rock", seed=seed + 20 + index, rough=0.2, floor=0.0)

    return model.finish()


# ---------- desert ----------

def dune_field(name, seed):
    """Desert: long dunes that cross the whole hex, each with a sharp crest and a ripple of wind marks on its back."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        offset = -0.4 + index * 0.4 + dice.uniform(-0.06, 0.06)
        length = dice.uniform(1.0, 1.3) - abs(offset) * 0.6
        tall = dice.uniform(0.05, 0.08)
        turn = dice.uniform(-0.3, 0.3)
        at = Vector((math.sin(turn) * offset, math.cos(turn) * offset, 0))
        model.blob((length, 0.52, tall * 2), at, "M_Sand", seed=seed + index, rough=0.05, turn=turn, floor=0.0)
        # The lee face, steeper and in shade, falls away behind the crest.
        model.blob((length * 0.85, 0.2, tall * 2.05), at + Vector((math.sin(turn) * 0.1, math.cos(turn) * 0.1, 0)), "M_Sand", seed=seed + index + 10, rough=0.04, turn=turn, floor=0.0)

    return model.finish()


def cactus_small(name, seed):
    """Desert: a clump of barrel cacti, one with a flower on its head."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        angle = index * 2.2
        size = dice.uniform(0.6, 1.0)
        at = (math.cos(angle) * 0.04 * index, math.sin(angle) * 0.04 * index, 0)
        model.taper((0.06 * size, 0.06 * size), (0.04 * size, 0.04 * size), 0.07 * size, at, "M_Leaf", sides=10)
        model.ball((0.045 * size, 0.045 * size, 0.03 * size), (at[0], at[1], 0.07 * size), "M_Leaf")
        if index == 0:
            model.ball((0.025, 0.025, 0.015), (at[0], at[1], 0.085 * size), "M_Cloth")

    return model.finish()


def bones():
    """Desert: the bleached skull and ribs of something that never crossed it."""
    model = piece("bones")
    model.ball((0.07, 0.1, 0.05), (0, 0, 0.025), "M_Bone")
    for side in (-1, 1):
        model.taper((0.012, 0.012), (0.003, 0.003), 0.09, (side * 0.03, 0.03, 0.03), "M_Bone", sides=5, tilt=("Y", math.radians(side * 60)))
        for index in range(4):
            model.taper((0.008, 0.008), (0.005, 0.005), 0.08, (side * 0.02, -0.08 - index * 0.03, 0.005), "M_Bone", sides=4, tilt=("Y", math.radians(side * 55)))

    return model.finish()


def sandstone(name, seed):
    """Desert: a wind-carved stack of sandstone, banded red and pale."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(5):
        radius = (0.12 - abs(index - 2) * 0.012) * dice.uniform(0.85, 1.1)
        model.spire(radius, 0.06, (dice.uniform(-0.01, 0.01), 0, index * 0.055), "M_Rock_Rust" if index % 2 else "M_Sand",
                    sides=7, seed=seed + index, rough=0.14, top=radius * 0.95)

    return model.finish()


def palm():
    """Desert: a date palm with a curved trunk and a burst of drooping fronds."""
    model = piece("palm")
    at = Vector((0, 0, 0))
    for index in range(6):
        lean = 6 + index * 3
        model.taper((0.035, 0.035), (0.03, 0.03), 0.075, at, "M_Wood", sides=7, tilt=("Y", math.radians(lean)))
        at = at + Vector((math.sin(math.radians(lean)), 0, math.cos(math.radians(lean)))) * 0.072
    for index in range(8):
        angle = index * math.tau / 8
        model.taper((0.05, 0.006), (0.006, 0.003), 0.2, at, "M_Frond", sides=4, turn=angle, tilt=("Y", math.radians(105)))
    model.ball((0.05, 0.05, 0.04), at, "M_Wood")

    return model.finish()


def oasis():
    """Desert: a little oasis, a pool rimmed in green."""
    model = piece("oasis")
    model.blob((0.36, 0.28, 0.025), (0, 0, 0.0), "M_Grass", seed=50, rough=0.12, floor=0.0)
    model.blob((0.26, 0.2, 0.02), (0, 0, 0.008), "M_Glass", seed=51, rough=0.08, floor=0.0)

    return model.finish()


# ---------- tundra ----------

def lichen_stones(name, seed):
    """Tundra: frost-split stones crusted with orange and grey lichen."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(5):
        size = dice.uniform(0.05, 0.12)
        at = (dice.uniform(-0.1, 0.1), dice.uniform(-0.1, 0.1), 0.0)
        model.blob((size * 1.2, size, size * 0.7), at, "M_Rock_Light" if index % 2 else "M_Rock", seed=seed + index, rough=0.25, floor=0.0)
        model.blob((size * 0.7, size * 0.6, size * 0.25), (at[0], at[1], size * 0.45), "M_Lichen" if index % 2 == 0 else "M_Moss", seed=seed + 10 + index, rough=0.3)

    return model.finish()


def snow_patch(name, seed):
    """Tundra: a drift of old snow lying in a hollow."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        model.blob((dice.uniform(0.14, 0.24), dice.uniform(0.1, 0.18), 0.03), (dice.uniform(-0.06, 0.06), dice.uniform(-0.05, 0.05), 0.0), "M_Snow", seed=seed + index, rough=0.18, floor=0.0)

    return model.finish()


def cotton_grass():
    """Tundra: a tuft of cotton grass, thin blades with white fluff on their tips."""
    model = piece("cotton_grass")
    dice = random.Random(61)
    for index in range(9):
        angle = index * math.tau / 9
        lean = dice.uniform(5, 20)
        tall = dice.uniform(0.08, 0.13)
        at = (math.cos(angle) * 0.015, math.sin(angle) * 0.015, 0)
        model.taper((0.006, 0.006), (0.002, 0.002), tall, at, "M_Reed", sides=4, turn=angle + math.pi / 2, tilt=("X", math.radians(-lean)))
        if index % 2 == 0:
            tip = (at[0] + math.cos(angle) * math.sin(math.radians(lean)) * tall, at[1] + math.sin(angle) * math.sin(math.radians(lean)) * tall, math.cos(math.radians(lean)) * tall)
            model.ball((0.022, 0.022, 0.026), tip, "M_Snow")

    return model.finish()


def dwarf_shrub(name, seed):
    """Tundra: a low dwarf birch, a mat of twigs and small red-gold leaves hugging the ground."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(6):
        angle = index * math.tau / 6 + dice.uniform(-0.3, 0.3)
        model.taper((0.01, 0.01), (0.004, 0.004), 0.1, (0, 0, 0.005), "M_Wood", sides=4, turn=angle, tilt=("Y", math.radians(70)))
    for index in range(7):
        angle = dice.uniform(0, math.tau)
        model.blob((0.07, 0.06, 0.04), (math.cos(angle) * 0.05, math.sin(angle) * 0.05, 0.02), "M_Lichen" if index % 2 else "M_Leaf_Dry", seed=seed + index, rough=0.2, floor=0.0)

    return model.finish()


# ---------- swamp ----------

def bog(name, seed):
    """Swamp: dark still water in pools that run together, edged with mud and scum, lily pads on it."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(4):
        angle = index * math.tau / 4 + dice.uniform(-0.4, 0.4)
        reach = dice.uniform(0.08, 0.3)
        at = (math.cos(angle) * reach, math.sin(angle) * reach, 0.0)
        size = dice.uniform(0.3, 0.44)
        model.blob((size + 0.06, size * 0.8 + 0.06, 0.02), at, "M_Mud", seed=seed + index, rough=0.14, turn=angle, floor=0.0)
        model.blob((size, size * 0.8, 0.022), (at[0], at[1], 0.004), "M_Bog", seed=seed + 10 + index, rough=0.1, turn=angle, floor=0.0)
        for pad in range(3):
            pa = dice.uniform(0, math.tau)
            model.tube(0.022, 0.004, (at[0] + math.cos(pa) * size * 0.25, at[1] + math.sin(pa) * size * 0.2, 0.026), "M_Leaf_Light" if pad else "M_Grass", sides=8)
        model.blob((size * 0.4, size * 0.2, 0.01), (at[0] + size * 0.2, at[1], 0.022), "M_Moss", seed=seed + 20 + index, rough=0.3)

    return model.finish()


def swamp_tree(name, seed):
    """Swamp: a swamp cypress on its knees of root, its crown hung with grey moss."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.09, 0.09), (0.035, 0.035), 0.42, (0, 0, 0), "M_Wood", sides=8)
    for index in range(6):
        angle = index * math.tau / 6 + dice.uniform(-0.3, 0.3)
        model.taper((0.03, 0.03), (0.01, 0.01), 0.1, (math.cos(angle) * 0.03, math.sin(angle) * 0.03, 0.06), "M_Wood", sides=5, turn=angle, tilt=("Y", math.radians(-120)))
        model.taper((0.014, 0.014), (0.004, 0.004), 0.045, (math.cos(angle) * 0.13, math.sin(angle) * 0.13, 0), "M_Wood", sides=5)
    for index in range(5):
        angle = index * math.tau / 5
        model.blob((0.2, 0.18, 0.1), (math.cos(angle) * 0.1, math.sin(angle) * 0.1, 0.42 + dice.uniform(-0.03, 0.03)), "M_Leaf_Pine" if index % 2 else "M_Moss", seed=seed + index, rough=0.15)
        model.taper((0.012, 0.012), (0.004, 0.004), 0.1, (math.cos(angle) * 0.14, math.sin(angle) * 0.14, 0.3), "M_Rock_Light", sides=4)

    return model.finish()


def cattails(name, seed):
    """Swamp: a stand of cattails, tall blades and brown heads."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(12):
        angle = dice.uniform(0, math.tau)
        reach = math.sqrt(dice.random()) * 0.06
        at = (math.cos(angle) * reach, math.sin(angle) * reach, 0)
        tall = dice.uniform(0.14, 0.26)
        lean = dice.uniform(-10, 10)
        model.taper((0.012, 0.006), (0.002, 0.002), tall, at, "M_Reed", sides=4, turn=angle, tilt=("X", math.radians(lean)))
        if index % 2 == 0:
            model.ball((0.014, 0.014, 0.05), (at[0], at[1] - math.sin(math.radians(lean)) * tall * 0.8, tall * 0.8), "M_Dirt", tilt=("X", math.radians(lean)))

    return model.finish()


# ---------- leafy forest ----------

def broadleaf(name, height, seed, leaves=("M_Leaf", "M_Leaf_Light")):
    """Forest: a tall broadleaf, a straight trunk forking high and a crown of many rounded masses."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.06, 0.06), (0.035, 0.035), height * 0.55, (0, 0, 0), "M_Wood", sides=8)
    for lean in (-30, 28, 12):
        model.taper((0.028, 0.028), (0.012, 0.012), height * 0.25, (0, 0, height * 0.48), "M_Wood", sides=6, tilt=("Y" if lean != 12 else "X", math.radians(lean)))
    for index in range(9):
        angle = index * math.tau / 8 + dice.uniform(-0.3, 0.3)
        reach = 0.0 if index == 8 else dice.uniform(0.08, 0.13)
        size = dice.uniform(0.15, 0.21)
        lift = height * (0.78 if index < 8 else 0.92) + dice.uniform(-0.04, 0.04)
        model.blob((size, size, size * 0.85), (math.cos(angle) * reach, math.sin(angle) * reach, lift), leaves[index % len(leaves)], seed=seed + index, rough=0.1)

    return model.finish()


def sapling(name, seed):
    """Forest: a young tree, a thin stem and a small crown."""
    model = piece(name)
    model.taper((0.014, 0.014), (0.008, 0.008), 0.14, (0, 0, 0), "M_Wood", sides=5)
    model.blob((0.1, 0.1, 0.1), (0, 0, 0.17), "M_Leaf_Light", seed=seed, rough=0.12)
    model.blob((0.07, 0.07, 0.07), (0.03, 0.02, 0.21), "M_Grass", seed=seed + 1, rough=0.12)

    return model.finish()


def fern_big(name, seed, mat="M_Leaf_Jungle"):
    """Forest and jungle: a fern of many fronds arching out from its heart."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(10):
        angle = index * math.tau / 10 + dice.uniform(-0.15, 0.15)
        model.taper((0.035, 0.006), (0.004, 0.003), dice.uniform(0.13, 0.19), (0, 0, 0.005), mat if index % 2 else "M_Leaf",
                    sides=4, turn=angle + math.pi / 2, tilt=("X", math.radians(-dice.uniform(50, 66))))

    return model.finish()


# ---------- rainforest ----------

def kapok(name, height, seed):
    """Jungle: a giant of the canopy, a tall pale trunk on buttress roots, a wide flat crown in two tiers, lianas hanging."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.06, 0.06), (0.04, 0.04), height * 0.8, (0, 0, 0), "M_Rock_Light", sides=8)
    for index in range(5):
        angle = index * math.tau / 5
        model.taper((0.05, 0.012), (0.012, 0.006), 0.13, (math.cos(angle) * 0.07, math.sin(angle) * 0.07, 0), "M_Rock_Light", sides=4, turn=angle + math.pi / 2, tilt=("X", math.radians(-50)))
    for tier, (lift, spread) in enumerate(((0.82, 0.18), (0.98, 0.1))):
        for index in range(6):
            angle = index * math.tau / 6 + tier * 0.5
            size = dice.uniform(0.17, 0.23)
            model.blob((size, size, size * 0.5), (math.cos(angle) * spread, math.sin(angle) * spread, height * lift), "M_Leaf_Jungle" if (index + tier) % 2 else "M_Leaf", seed=seed + tier * 10 + index, rough=0.1)
    for index in range(4):
        angle = dice.uniform(0, math.tau)
        model.taper((0.006, 0.006), (0.004, 0.004), height * 0.45, (math.cos(angle) * 0.2, math.sin(angle) * 0.2, height * 0.35), "M_Leaf", sides=4)

    return model.finish()


def banana(name, seed):
    """Jungle: a banana plant, a soft stem and a few huge broad leaves arching out."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.03, 0.03), (0.02, 0.02), 0.16, (0, 0, 0), "M_Leaf", sides=7)
    for index in range(6):
        angle = index * math.tau / 6 + dice.uniform(-0.2, 0.2)
        model.taper((0.07, 0.008), (0.03, 0.005), 0.2, (0, 0, 0.15), "M_Leaf_Light" if index % 2 else "M_Leaf_Jungle", sides=4,
                    turn=angle + math.pi / 2, tilt=("X", math.radians(-dice.uniform(45, 70))))

    return model.finish()


def jungle_bloom(name, seed):
    """Jungle: a low bush of glossy leaves starred with bright flowers."""
    model = piece(name)
    dice = random.Random(seed)
    model.blob((0.16, 0.14, 0.1), (0, 0, 0.04), "M_Leaf_Jungle", seed=seed, rough=0.14, floor=0.0)
    for index in range(7):
        angle = dice.uniform(0, math.tau)
        model.ball((0.026, 0.026, 0.016), (math.cos(angle) * 0.06, math.sin(angle) * 0.05, dice.uniform(0.06, 0.1)), "M_Bloom" if index % 3 else "M_Flower")

    return model.finish()


# ---------- cliffs ----------

def cliff_fir(model, at, size):
    """A small hardy fir on the top of a cliff, sized to the shelf."""
    model.taper((0.012 * size, 0.012 * size), (0.008 * size, 0.008 * size), 0.04 * size, at, "M_Wood", sides=5)
    for tier in range(3):
        model.tube((0.05 - tier * 0.013) * size, 0.05 * size, (at[0], at[1], at[2] + (0.03 + tier * 0.035) * size),
                   "M_Leaf_Pine" if tier < 2 else "M_Leaf_Fir", sides=7, top=0.004)


def escarpment(name, seed):
    """
    Cliffs: an escarpment, as real cliffs stand. A shelf of rock fills the back
    half of the hex and breaks off along a ragged curved front: a sheer face of
    bedded rock in bands of grey and buff, split into buttresses, a ledge part
    way down, a cap of turf on top with a few firs and bushes at its lip, and
    the rock that fell from the face lying in a talus slope at its foot.
    """
    model = piece(name)
    dice = random.Random(seed)
    beds = ("M_Rock", "M_Rock_Light", "M_Stone", "M_Rock_Light", "M_Rock")
    count = 11
    for index in range(count):
        # The front runs in an arc across the hex; each buttress stands back a little from it.
        share = index / (count - 1)
        angle = math.pi * (0.15 + 0.7 * share)
        front = Vector((math.cos(angle) * 0.55, -math.sin(angle) * 0.2 + 0.05, 0))
        tall = 0.38 + 0.07 * math.sin(share * math.pi) + dice.uniform(-0.015, 0.015)
        width = dice.uniform(0.15, 0.19)
        depth = dice.uniform(0.32, 0.42)
        at = front + Vector((0, depth * 0.5, 0))
        # The rock lies in beds, each a little wider or narrower than the one below.
        layers = 5
        z = 0.0
        for layer in range(layers):
            high = tall / layers * dice.uniform(0.85, 1.15)
            if layer == layers - 1:
                high = tall - z
            grow = 1.0 + dice.uniform(-0.08, 0.08)
            # A bed is a rough slab, not a brick: its corners knocked off and out of true.
            model.spire(width * 0.62 * grow, high, (at.x + dice.uniform(-0.01, 0.01), at.y + dice.uniform(-0.015, 0.015), z), beds[(layer + index) % len(beds)],
                        sides=7, seed=seed * 100 + index * 10 + layer, rough=0.12, top=width * 0.6 * grow)
            z += high
        # The turf cap, and a ledge part way down the face.
        # The turf rolls over the top in soft lumps of grass and moss, hanging a little over the lip.
        for lump in range(3):
            model.blob((width * 1.3, width * 1.2, 0.05), (at.x + dice.uniform(-0.02, 0.02), at.y - 0.06 + lump * 0.09, tall - 0.005),
                       "M_Leaf" if (index + lump) % 3 else "M_Moss", seed=seed * 7 + index * 3 + lump, rough=0.14)
        if index % 3 == 1:
            model.box((width * 0.9, 0.06, 0.02), (front.x, front.y - 0.01, tall * 0.45), "M_Moss", turn=(angle - math.pi / 2) * 0.6)
        # The top is a little meadow: small firs and bushes, tufts of grass, stones that lie in it.
        for spot in range(2):
            top = (at.x + dice.uniform(-0.04, 0.04), at.y + dice.uniform(-0.1, 0.12), tall + 0.02)
            roll = dice.random()
            if roll < 0.55:
                cliff_fir(model, top, dice.uniform(0.7, 1.15))
            elif roll < 0.8:
                model.blob((0.07, 0.06, 0.05), (top[0], top[1], top[2] + 0.015), "M_Leaf" if spot else "M_Leaf_Light", seed=seed + index * 5 + spot, rough=0.15)
        for tuft in range(4):
            spot = (at.x + dice.uniform(-0.06, 0.06), at.y + dice.uniform(-0.12, 0.14), tall + 0.015)
            for blade in range(3):
                model.taper((0.012, 0.006), (0.002, 0.002), dice.uniform(0.035, 0.06), spot, "M_Leaf_Light" if blade % 2 else "M_Grass",
                            sides=4, turn=blade * 2.1 + tuft, tilt=("X", math.radians(dice.uniform(10, 30))))
        for stone in range(2):
            size = dice.uniform(0.022, 0.045)
            model.blob((size * 1.3, size, size * 0.7), (at.x + dice.uniform(-0.06, 0.06), at.y + dice.uniform(-0.12, 0.14), tall + 0.015),
                       "M_Rock_Light" if stone else "M_Stone", seed=seed * 3 + index * 7 + stone, rough=0.25)
        if index % 4 == 2:
            flower_spot = (at.x + dice.uniform(-0.05, 0.05), at.y + dice.uniform(-0.1, 0.1), tall + 0.03)
            for petal in range(4):
                model.ball((0.014, 0.014, 0.01), (flower_spot[0] + math.cos(petal * 1.6) * 0.02, flower_spot[1] + math.sin(petal * 1.6) * 0.02, flower_spot[2]), "M_Flower" if petal % 2 else "M_Snow")
    # The talus: blocks fallen from the face, big ones near it, small ones spread out.
    for index in range(26):
        angle = math.pi * dice.uniform(0.12, 0.88)
        reach = dice.uniform(0.0, 0.32)
        base = Vector((math.cos(angle) * 0.55, -math.sin(angle) * 0.2 + 0.05, 0))
        out = Vector((0, -1, 0)) * reach + Vector((dice.uniform(-0.04, 0.04), 0, 0))
        size = 0.1 * (1 - reach / 0.4) * dice.uniform(0.5, 1.1) + 0.02
        model.blob((size * 1.2, size, size * 0.8), base + out, "M_Rock_Light" if index % 3 else "M_Stone", seed=seed + 40 + index, rough=0.25, floor=0.0)

    return model.finish()


def cliff_island(name, seed):
    """
    Cliffs: a block of bedded rock standing in the middle of the hex, sheer on
    every side, its walls split into buttresses of grey and buff. Its top is
    a little upland meadow: a lid of rock under rolling turf, small firs and
    bushes, grass, stones and a few flowers. The ground round it is left to
    the game, which scatters stones and small trees there.
    """
    model = piece(name)
    dice = random.Random(seed)
    beds = ("M_Rock", "M_Rock_Light", "M_Stone", "M_Rock_Light", "M_Rock")
    count = 16
    tall = 0.34 + dice.uniform(-0.02, 0.03)

    def edge(angle):
        # The outline wanders: wider one way than the other, with bays and bulges.
        return 0.44 + 0.05 * math.sin(angle * 2 + seed) + 0.03 * math.sin(angle * 3 + seed * 1.7)

    for index in range(count):
        angle = index / count * math.tau + dice.uniform(-0.06, 0.06)
        reach = edge(angle) - 0.07
        at = Vector((math.cos(angle) * reach, math.sin(angle) * reach, 0))
        width = dice.uniform(0.17, 0.21)
        high_here = tall + dice.uniform(-0.03, 0.02)
        layers = 4
        z = 0.0
        for layer in range(layers):
            high = high_here / layers * dice.uniform(0.85, 1.15)
            if layer == layers - 1:
                high = high_here - z
            grow = 1.0 + dice.uniform(-0.08, 0.08)
            model.spire(width * 0.62 * grow, high, (at.x + dice.uniform(-0.01, 0.01), at.y + dice.uniform(-0.01, 0.01), z), beds[(layer + index) % len(beds)],
                        sides=7, seed=seed * 100 + index * 10 + layer, rough=0.12, top=width * 0.6 * grow)
            z += high
        if index % 4 == 1:
            model.box((width * 0.8, 0.05, 0.02), (math.cos(angle) * (reach + 0.08), math.sin(angle) * (reach + 0.08), tall * 0.5), "M_Moss", turn=angle + math.pi / 2)

    # The body and lid of rock that fill between the walls.
    model.spire(0.4, tall - 0.02, (0, 0, 0), "M_Rock", sides=12, seed=seed + 7, rough=0.05, top=0.4)

    # The turf rolls over the whole top and hangs a little over the edge: one green cover, lumps on it.
    model.spire(0.42, 0.035, (0, 0, tall - 0.02), "M_Leaf", sides=12, seed=seed + 9, rough=0.05, top=0.4)
    for ring, (reach, many) in enumerate(((0.0, 1), (0.17, 6), (0.33, 11))):
        for index in range(many):
            angle = index / many * math.tau + ring * 0.4 + dice.uniform(-0.15, 0.15)
            x, y = math.cos(angle) * reach, math.sin(angle) * reach
            size = 0.22 if ring < 2 else 0.18
            model.blob((size, size * 0.9, 0.06), (x, y, tall + 0.005), ("M_Leaf", "M_Grass", "M_Leaf_Light")[(index + ring) % 3],
                       seed=seed * 7 + ring * 20 + index, rough=0.14)

    # What grows and lies on the top: small firs, bushes, grass, stones, flowers.
    def spot(limit=0.36):
        reach = limit * math.sqrt(dice.random())
        angle = dice.uniform(0, math.tau)
        return (math.cos(angle) * reach, math.sin(angle) * reach, tall + 0.025)

    for index in range(9):
        cliff_fir(model, spot(0.34), dice.uniform(0.75, 1.25))
    for index in range(4):
        x, y, z = spot()
        model.blob((0.075, 0.065, 0.05), (x, y, z + 0.01), "M_Leaf" if index % 2 else "M_Leaf_Light", seed=seed + 60 + index, rough=0.15)
    for tuft in range(12):
        at = spot()
        for blade in range(3):
            model.taper((0.012, 0.006), (0.002, 0.002), dice.uniform(0.035, 0.06), at, "M_Leaf_Light" if blade % 2 else "M_Grass",
                        sides=4, turn=blade * 2.1 + tuft, tilt=("X", math.radians(dice.uniform(10, 30))))
    for stone in range(7):
        size = dice.uniform(0.025, 0.055)
        x, y, z = spot()
        model.blob((size * 1.3, size, size * 0.7), (x, y, z - 0.005), "M_Rock_Light" if stone % 2 else "M_Stone", seed=seed * 3 + stone, rough=0.25)
    for flower in range(3):
        x, y, z = spot()
        for petal in range(4):
            model.ball((0.014, 0.014, 0.01), (x + math.cos(petal * 1.6) * 0.02, y + math.sin(petal * 1.6) * 0.02, z + 0.005), "M_Flower" if petal % 2 else "M_Snow")

    # Blocks fallen from the walls lie close round the foot.
    for index in range(20):
        angle = dice.uniform(0, math.tau)
        reach = edge(angle) + dice.uniform(0.0, 0.08)
        size = dice.uniform(0.04, 0.09)
        model.blob((size * 1.2, size, size * 0.8), (math.cos(angle) * reach, math.sin(angle) * reach, size * 0.22), "M_Rock_Light" if index % 3 else "M_Stone",
                   seed=seed + 40 + index, rough=0.25, floor=0.0)

    return model.finish()


def crag(name, seed):
    """Cliffs: a jagged ridge across the hex, a wall of columns that steps down at its ends, scree under its faces."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(9):
        along = -0.5 + index * 0.125
        tall = (0.55 - abs(along) * 0.6) * dice.uniform(0.75, 1.1)
        width = dice.uniform(0.09, 0.13)
        model.spire(width, tall, (along, dice.uniform(-0.05, 0.05), 0), "M_Rock_Light" if index % 2 else "M_Rock", sides=6,
                    seed=seed + index, rough=0.14, top=width * 0.6)
    for index in range(14):
        along = dice.uniform(-0.5, 0.5)
        side = 1 if index % 2 else -1
        size = dice.uniform(0.04, 0.09)
        model.blob((size, size * 0.9, size * 0.7), (along, side * dice.uniform(0.12, 0.26), 0.0), "M_Rock_Light" if index % 3 else "M_Stone", seed=seed + 30 + index, rough=0.22, floor=0.0)

    return model.finish()


# ---------- badlands ----------

def hoodoo(name, seed):
    """Badlands: a hoodoo, a thin stack of soft red rock wearing a hard cap stone."""
    model = piece(name)
    dice = random.Random(seed)
    tall = dice.uniform(0.22, 0.34)
    for index in range(4):
        radius = 0.05 - index * 0.006 + dice.uniform(-0.004, 0.004)
        model.spire(radius, tall / 4, (0, 0, index * tall / 4), "M_Rock_Rust" if index % 2 else "M_Rock_Red", sides=7, seed=seed + index, rough=0.12, top=radius * 0.85)
    model.blob((0.1, 0.09, 0.04), (0, 0, tall + 0.01), "M_Sand", seed=seed + 9, rough=0.15)

    return model.finish()


def strata(name, seed):
    """Badlands: eroded ridges across the hex, banded in rust, red and pale, with gullies cut between them."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        offset = -0.36 + index * 0.36 + dice.uniform(-0.05, 0.05)
        length = dice.uniform(0.7, 1.0) - abs(offset) * 0.5
        turn = dice.uniform(-0.25, 0.25)
        at = Vector((math.sin(turn) * offset, math.cos(turn) * offset, 0))
        for band in range(3):
            model.blob((length * (1 - band * 0.2), 0.22 - band * 0.05, 0.08), at + Vector((0, 0, band * 0.045)),
                       ("M_Rock_Rust", "M_Rock_Red", "M_Sand")[band], seed=seed + index * 5 + band, rough=0.08, turn=turn, floor=0.0)

    return model.finish()


def dead_bush():
    """Badlands: a dead bush, a tangle of grey twigs."""
    model = piece("bush_dead")
    dice = random.Random(71)
    for index in range(9):
        angle = index * math.tau / 9
        model.taper((0.008, 0.008), (0.002, 0.002), dice.uniform(0.06, 0.11), (0, 0, 0), "M_Rock", sides=4, turn=angle, tilt=("Y", math.radians(dice.uniform(25, 60))))

    return model.finish()


# ---------- polar desert ----------

def snowfield(name, seed):
    """Polar desert: wind-packed snow in long drifts across the hex, a pressure ridge of broken ice along one side."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(4):
        offset = -0.45 + index * 0.3 + dice.uniform(-0.05, 0.05)
        length = dice.uniform(0.9, 1.2) - abs(offset) * 0.5
        turn = dice.uniform(-0.25, 0.25)
        at = Vector((math.sin(turn) * offset, math.cos(turn) * offset, 0))
        model.blob((length, 0.4, dice.uniform(0.08, 0.14)), at, "M_Snow", seed=seed + index, rough=0.06, turn=turn, floor=0.0)
    # The pressure ridge: slabs of ice shoved up on edge.
    for index in range(9):
        along = -0.45 + index * 0.11
        size = dice.uniform(0.07, 0.12)
        model.box((size, 0.03, size * dice.uniform(0.7, 1.3)), (along, 0.5 + dice.uniform(-0.03, 0.03), 0), "M_Ice", turn=dice.uniform(-0.6, 0.6))

    return model.finish()


def serac(name, seed):
    """Polar desert: a serac, a broken tower of blue ice with snow on its shoulders."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(3):
        tall = dice.uniform(0.14, 0.3)
        width = dice.uniform(0.06, 0.1)
        at = (dice.uniform(-0.06, 0.06), dice.uniform(-0.06, 0.06), 0)
        model.spire(width, tall, at, "M_Ice", sides=5, seed=seed + index, rough=0.2, top=width * 0.5, cap="M_Snow", cap_from=0.8)

    return model.finish()


def frozen_pool(name, seed):
    """Polar desert: a frozen pool, smooth blue ice ringed with snow, cracked across."""
    model = piece(name)
    dice = random.Random(seed)
    model.blob((0.36, 0.28, 0.03), (0, 0, 0.0), "M_Snow", seed=seed, rough=0.1, floor=0.0)
    model.blob((0.28, 0.21, 0.02), (0, 0, 0.012), "M_Ice", seed=seed + 1, rough=0.06, floor=0.0)
    for index in range(4):
        angle = dice.uniform(0, math.tau)
        model.taper((0.006, 0.003), (0.003, 0.002), 0.1, (0, 0, 0.034), "M_Snow", sides=4, turn=angle, tilt=("X", math.radians(-89)))

    return model.finish()


def snow_rock(name, seed):
    """Polar desert: a dark rock showing through the snow, its top white."""
    model = piece(name)
    model.blob((0.16, 0.13, 0.11), (0, 0, 0.03), "M_Rock_Dark", seed=seed, rough=0.2, floor=0.0)
    model.blob((0.13, 0.1, 0.04), (0, 0, 0.09), "M_Snow", seed=seed + 1, rough=0.18)
    model.blob((0.2, 0.16, 0.03), (0.02, 0.02, 0.0), "M_Snow", seed=seed + 2, rough=0.15, floor=0.0)

    return model.finish()


# ---------- volcano ----------

def ribbon(model, path, thick, mat):
    """
    A closed flat strip along a path of (x, y, z, heading, half width): lava down
    a slope, a stream in a bed. The sides of each step are joined to the next,
    so the strip bends smoothly and stays one closed shell.
    """
    import bmesh as _bmesh
    index = model.slot(mat)
    rings = []
    for x, y, z, heading, wide in path:
        across = Vector((-math.sin(heading), math.cos(heading), 0)) * wide
        middle = Vector((x, y, z))
        rings.append([model.bm.verts.new(middle - across), model.bm.verts.new(middle + across),
                      model.bm.verts.new(middle + across - Vector((0, 0, thick))), model.bm.verts.new(middle - across - Vector((0, 0, thick)))])
    for a, b in zip(rings, rings[1:]):
        for side in range(4):
            face = model.bm.faces.new((a[side], a[(side + 1) % 4], b[(side + 1) % 4], b[side]))
            face.material_index = index
    for ring in (rings[0], list(reversed(rings[-1]))):
        model.bm.faces.new(ring).material_index = index


def volcano_height(radius, angle):
    """
    How high the volcano's ground stands at a point, so that the cone and what
    lies on it are built from one surface. A bowl in the middle, a rim, and a
    slope that falls away ever gentler to the edge of the hex, scored by ribs.
    """
    if radius < 0.16:
        return 0.70 + (radius / 0.16) ** 2 * 0.1
    if radius < 0.21:
        return 0.80 + 0.012 * math.sin(angle * 6.0)
    share = min(1.0, (radius - 0.21) / 0.59)
    slope = 0.80 * (1.0 - share) ** 1.5
    ribs = 0.035 * max(0.0, math.cos(angle * 7.0 + radius * 4.0)) * math.sin(math.pi * share)
    wobble = (0.012 * math.sin(angle * 5.0 + radius * 9.0) + 0.008 * math.sin(angle * 11.0 - radius * 5.0)) * (1.0 - share)
    return slope + ribs + wobble


def volcano_rib(radius, angle):
    return max(0.0, math.cos(angle * 7.0 + radius * 4.0))


def volcano_big():
    """
    Volcano: one great cone over the whole hex, built as a single closed surface:
    a crater bowl of fire, a rim, and a long slope scored by ribs. The lava runs
    down it in channels that lie on the surface itself, so nothing floats and
    nothing sinks; each channel narrows as it cools and ends in a crusted toe.
    The game finds the empties `__Ash_crater` and `__Embers_crater` and makes
    them smoke and spit (`BuildingLife`), and makes the lava glow and pulse.
    """
    model = piece("volcano_big")
    dice = random.Random(17)
    rings = 30
    sectors = 72
    rock = model.slot("M_Basalt")
    ridge = model.slot("M_Rock_Dark")

    radii = [0.0] + [0.8 * ((index + 1) / rings) ** 1.15 for index in range(rings)]
    grid = []
    for ring, radius in enumerate(radii):
        if ring == 0:
            grid.append([model.bm.verts.new((0, 0, volcano_height(0.0, 0.0)))])
            continue
        row = []
        for sector in range(sectors):
            angle = sector * math.tau / sectors
            z = 0.0 if ring == rings else volcano_height(radius, angle)
            row.append(model.bm.verts.new((math.cos(angle) * radius, math.sin(angle) * radius, z)))
        grid.append(row)

    for sector in range(sectors):
        following = (sector + 1) % sectors
        model.bm.faces.new((grid[0][0], grid[1][sector], grid[1][following])).material_index = rock
        for ring in range(1, rings):
            radius = (radii[ring] + radii[ring + 1]) / 2
            angle = (sector + 0.5) * math.tau / sectors
            face = model.bm.faces.new((grid[ring][sector], grid[ring + 1][sector], grid[ring + 1][following], grid[ring][following]))
            face.material_index = ridge if volcano_rib(radius, angle) > 0.55 and radius > 0.25 else rock
    model.bm.faces.new(list(reversed(grid[rings]))).material_index = rock

    # The fire in the crater: a pool whose edge hides under the walls of the bowl, a crust floating on it.
    model.tube(0.15, 0.012, (0, 0, 0.733), "M_Lava", sides=20)
    model.blob((0.06, 0.05, 0.02), (0.03, -0.02, 0.746), "M_Basalt", seed=5, rough=0.25)

    # The channels: each a ribbon whose every corner is set on the surface, a hair above it.
    lift = 0.01
    thick = 0.022
    for channel in range(4):
        angle = channel * math.tau / 4 + dice.uniform(-0.35, 0.35)
        path = []
        radius = 0.19
        while radius < 0.72:
            angle += dice.uniform(-0.035, 0.035) / max(radius, 0.3)
            path.append((radius, angle))
            radius += 0.018
        index = model.slot("M_Lava")
        rows = []
        for step, (radius, angle) in enumerate(path):
            share = step / (len(path) - 1)
            half = (0.04 * (1.0 - share * 0.55)) / radius
            row = []
            for side in (-1, 1):
                a = angle + side * half
                z = volcano_height(radius, a) + lift
                row.append((math.cos(a) * radius, math.sin(a) * radius, z))
            left, right = row
            rows.append([model.bm.verts.new(left), model.bm.verts.new(right),
                         model.bm.verts.new((right[0], right[1], right[2] - thick)), model.bm.verts.new((left[0], left[1], left[2] - thick))])
        for a, b in zip(rows, rows[1:]):
            for side in range(4):
                model.bm.faces.new((a[side], b[side], b[(side + 1) % 4], a[(side + 1) % 4])).material_index = index
        model.bm.faces.new(rows[0]).material_index = index
        model.bm.faces.new(list(reversed(rows[-1]))).material_index = index
        # The toe of the flow, cooled black.
        radius, angle = path[-1]
        model.blob((0.07, 0.06, 0.035), (math.cos(angle) * (radius + 0.02), math.sin(angle) * (radius + 0.02), volcano_height(radius + 0.02, angle)), "M_Basalt", seed=60 + channel, rough=0.25)

    made = model.finish()
    for kind in ("Ash", "Embers"):
        marker = bpy.data.objects.new("volcano_big__%s_crater" % kind, None)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = made
        marker.location = (0, 0, 0.78)

    return made


def lava_pool(name, seed):
    """Volcano: a pool of lava at the foot, crusted black round the edge."""
    model = piece(name)
    model.blob((0.26, 0.2, 0.04), (0, 0, 0.0), "M_Basalt", seed=seed, rough=0.15, floor=0.0)
    model.blob((0.18, 0.13, 0.03), (0, 0, 0.015), "M_Lava", seed=seed + 1, rough=0.1, floor=0.0)
    model.blob((0.06, 0.05, 0.025), (0.03, 0.02, 0.03), "M_Basalt", seed=seed + 2, rough=0.2)

    return model.finish()


def basalt_rocks(name, seed):
    """Volcano: black rocks thrown out of the cone, glowing in their cracks."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(5):
        size = dice.uniform(0.05, 0.12)
        at = (dice.uniform(-0.1, 0.1), dice.uniform(-0.1, 0.1), 0.0)
        model.blob((size * 1.1, size, size * 0.8), at, "M_Basalt" if index % 2 else "M_Rock_Dark", seed=seed + index, rough=0.25, floor=0.0)
        if index % 2 == 0:
            model.taper((0.01, 0.004), (0.004, 0.002), size * 0.8, (at[0], at[1], size * 0.6), "M_Lava", sides=4, turn=index, tilt=("X", math.radians(-80)))

    return model.finish()


def charred_tree(name, seed):
    """Volcano: a tree burnt to a black spike, its branches snapped short."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.03, 0.03), (0.006, 0.006), 0.32, (0, 0, 0), "M_Night", sides=6, tilt=("Y", math.radians(dice.uniform(-8, 8))))
    for index in range(3):
        model.taper((0.01, 0.01), (0.002, 0.002), 0.07, (0, 0, 0.12 + index * 0.06), "M_Night", sides=4, turn=index * 2.2, tilt=("Y", math.radians(65)))

    return model.finish()


def ash_patch(name, seed):
    """Volcano: grey ash lying in a drift."""
    model = piece(name)
    model.blob((0.24, 0.18, 0.025), (0, 0, 0.0), "M_Rock", seed=seed, rough=0.15, floor=0.0)

    return model.finish()


# ---------- the kinds of the taiga: a forest of one dark fir looked like a plantation ----------

def spruce(name, height, seed, snow=False):
    """Taiga: a spruce, dense and near blue, its many tiers drooping; under snow each tier carries a white cap."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.045, 0.045), (0.018, 0.018), height * 0.4, (0, 0, 0), "M_Wood", sides=7)
    tiers = 10
    for index in range(tiers):
        share = index / (tiers - 1)
        radius = (0.12 - 0.1 * share) * (height / 0.6) * dice.uniform(0.9, 1.1)
        tall = (0.13 - 0.04 * share) * height
        at = (0.08 + 0.8 * share) * height
        lean = (dice.uniform(-0.01, 0.01), dice.uniform(-0.01, 0.01), at)
        model.tube(radius, tall, lean, "M_Leaf_Spruce" if index % 3 else "M_Leaf_Pine", sides=7, top=radius * 0.25, turn=dice.uniform(0, 6.3))
        if snow and index % 2 == 1:
            model.tube(radius * 0.85, tall * 0.55, (lean[0], lean[1], at + tall * 0.5), "M_Snow", sides=7, top=radius * 0.22, turn=dice.uniform(0, 6.3))
    model.tube(0.01, height * 0.1, (0, 0, height * 0.94), "M_Snow" if snow else "M_Leaf_Spruce", sides=5, top=0.0)

    return model.finish()


def scots_pine(name, height, seed):
    """Taiga: a Scots pine, a tall bare trunk glowing orange and bent a little, its crown a few flat clouds of needles at the head."""
    model = piece(name)
    dice = random.Random(seed)
    bend = dice.uniform(-6, 6)
    model.taper((0.04, 0.04), (0.022, 0.022), height * 0.78, (0, 0, 0), "M_Bark_Pine", sides=7, tilt=("X", math.radians(bend)))
    model.taper((0.05, 0.05), (0.04, 0.04), height * 0.06, (0, 0, 0), "M_Wood", sides=7)
    top = height * 0.76
    drift = math.sin(math.radians(bend)) * -top
    for index in range(3):
        model.taper((0.008, 0.008), (0.003, 0.003), 0.04, (0, drift * (0.3 + index * 0.2), top * (0.35 + index * 0.12)), "M_Wood", sides=4, turn=index * 2.3 + seed, tilt=("Y", math.radians(80)))
    clouds = 4
    for index in range(clouds):
        angle = index * math.tau / clouds + dice.uniform(-0.4, 0.4)
        reach = dice.uniform(0.04, 0.09) * height / 0.6
        lift = top + dice.uniform(-0.04, 0.08) * height
        x, y = math.cos(angle) * reach, drift + math.sin(angle) * reach
        model.taper((0.012, 0.012), (0.006, 0.006), reach * 1.2, (0, drift, lift - 0.04), "M_Bark_Pine", sides=4, turn=angle, tilt=("Y", math.radians(65)))
        size = dice.uniform(0.15, 0.21) * height / 0.6
        model.blob((size, size * 0.85, size * 0.62), (x * 0.7, y, lift), "M_Leaf_Pine" if index % 2 else "M_Leaf_Fir", seed=seed * 7 + index, rough=0.2)
    model.blob((0.14 * height / 0.6, 0.12 * height / 0.6, 0.07 * height / 0.6), (0, drift, top + 0.11 * height), "M_Leaf_Fir", seed=seed + 31, rough=0.2)

    return model.finish()


def larch(name, height, seed, gold=False):
    """Taiga: a larch, slender and see-through, its few soft tiers spread wide with sky between them; in autumn it turns gold."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.035, 0.035), (0.01, 0.01), height * 0.95, (0, 0, 0), "M_Wood", sides=7)
    tiers = 6
    for index in range(tiers):
        share = index / (tiers - 1)
        radius = (0.14 - 0.1 * share) * (height / 0.6) * dice.uniform(0.85, 1.1)
        at = (0.22 + 0.68 * share) * height
        for limb in range(3):
            angle = limb * math.tau / 3 + index * 1.1 + dice.uniform(-0.3, 0.3)
            x, y = math.cos(angle) * radius * 0.55, math.sin(angle) * radius * 0.55
            mat = ("M_Larch_Gold" if limb or index % 2 else "M_Leaf_Dry") if gold else ("M_Leaf_Larch" if limb else "M_Leaf_Light")
            model.blob((radius * 1.1, radius * 0.7, 0.045 * height / 0.6), (x, y, at), mat, seed=seed * 11 + index * 3 + limb, rough=0.25, turn=angle)
    model.tube(0.008, height * 0.08, (0, 0, height * 0.93), "M_Larch_Gold" if gold else "M_Leaf_Larch", sides=5, top=0.0)

    return model.finish()


def cedar(name, height, seed):
    """Taiga: a Siberian stone pine, an old giant, thick in the trunk and broad in the crown, with more than one head."""
    model = piece(name)
    dice = random.Random(seed)
    model.taper((0.075, 0.075), (0.04, 0.04), height * 0.55, (0, 0, 0), "M_Wood", sides=8)
    for root in range(4):
        model.taper((0.04, 0.03), (0.01, 0.01), 0.07, (0, 0, 0.01), "M_Wood", sides=4, turn=root * 1.57 + seed, tilt=("Y", math.radians(72)))
    masses = ((0.0, 0.0, 0.3, 0.34), (0.06, 0.03, 0.48, 0.28), (-0.05, -0.04, 0.55, 0.24), (0.02, -0.02, 0.72, 0.2), (-0.03, 0.05, 0.85, 0.12))
    for index, (x, y, lift, size) in enumerate(masses):
        size *= height / 0.6 * dice.uniform(0.92, 1.08)
        model.blob((size, size * 0.9, size * 0.62), (x * height / 0.6, y * height / 0.6, lift * height), "M_Leaf_Pine" if index % 2 == 0 else "M_Leaf_Fir", seed=seed * 5 + index, rough=0.1)
    for index in range(3):
        angle = index * 2.1 + seed
        model.tube(0.03 * height / 0.6, 0.12 * height, (math.cos(angle) * 0.05 * height / 0.6, math.sin(angle) * 0.05 * height / 0.6, 0.86 * height), "M_Leaf_Fir", sides=6, top=0.0)

    return model.finish()


def juniper(name, seed):
    """Taiga undergrowth: a juniper, a ragged column of grey needles with dusky berries."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(5):
        lift = 0.04 + index * 0.035
        size = 0.11 - index * 0.016
        model.blob((size, size * 0.85, 0.08), (dice.uniform(-0.015, 0.015), dice.uniform(-0.015, 0.015), lift), "M_Leaf_Juniper", seed=seed + index, rough=0.3)
    for index in range(7):
        angle = dice.uniform(0, math.tau)
        lift = dice.uniform(0.04, 0.17)
        reach = 0.055 - lift * 0.18
        model.ball((0.013, 0.013, 0.013), (math.cos(angle) * reach, math.sin(angle) * reach, lift), "M_Berry_Blue")

    return model.finish()


def berries(name, berry, seed):
    """Taiga ground cover: a low spread of bilberry or cowberry, the leaves dark, the berries bright among them."""
    model = piece(name)
    dice = random.Random(seed)
    for index in range(6):
        x, y = dice.uniform(-0.09, 0.09), dice.uniform(-0.09, 0.09)
        size = dice.uniform(0.06, 0.09)
        model.blob((size, size * 0.9, 0.045), (x, y, 0.02), "M_Leaf" if index % 2 else "M_Leaf_Pine", seed=seed + index, rough=0.3, floor=0.0)
    for index in range(12):
        model.ball((0.012, 0.012, 0.012), (dice.uniform(-0.1, 0.1), dice.uniform(-0.1, 0.1), dice.uniform(0.03, 0.045)), berry)

    return model.finish()


def anthill():
    """Taiga: a forest anthill, a heap of needles as high as a knee against a stump."""
    model = piece("anthill")
    model.blob((0.2, 0.18, 0.15), (0, 0, 0.0), "M_Dirt", seed=3, rough=0.12, floor=0.0)
    model.blob((0.16, 0.14, 0.1), (0.01, 0.0, 0.03), "M_Wood", seed=4, rough=0.2, floor=0.0)
    model.taper((0.045, 0.045), (0.04, 0.04), 0.07, (-0.08, 0.02, 0), "M_Wood", sides=8)
    model.tube(0.036, 0.005, (-0.08, 0.02, 0.07), "M_Timber", sides=8)

    return model.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    models = [
        pine("pine_a", 0.62, 0.0), pine("pine_b", 0.46, 0.5),
        tree("tree_a", 0.44, 11), tree("tree_b", 0.36, 23),
        jungle("jungle_a", 0.56, 4), jungle("jungle_b", 0.42, 7), fern(),
        acacia(), bush("bush_dry", "M_Leaf_Dry", 3),
        peak("peak_a", 0.42, 0.95, 5, 0.3), peak("peak_b", 0.3, 0.62, 5, 1.2),
        volcano(),
        hill("hill_a", 0.72, 8), hill("hill_b", 0.52, 15),
        cliff("cliff_a", 0.2), cliff("cliff_b", 1.5),
        crater(),
        mesa("mesa_a", 1.0, 0.2), mesa("mesa_b", 0.72, 1.1),
        stone("stone_a", 21), stone("stone_b", 34), bush("bush_moss", "M_Moss", 9),
        ice("ice_a", 2), ice("ice_b", 9),
        dune("dune_a", 6), dune("dune_b", 12), cactus(),
        pool(), reeds(), snag(),
        tuft("grass", "M_Leaf_Light", 0.15, 0.4, light="M_Grass"), tuft("grass_b", "M_Leaf_Light", 0.11, 2.1, light="M_Grass"),
        flower(), flower("flower_white", "M_Snow", "M_Flower"), flower("flower_red", "M_Cloth", "M_Flower"),
        tuft("straw", "M_Straw", 0.17, 1.3, light="M_Leaf_Dry"),
        tuft("grass_dark", "M_Leaf", 0.12, 3.3, light="M_Leaf_Pine"),
        pebbles("pebbles", "M_Rock", "M_Rock_Light", 5), pebbles("pebbles_red", "M_Rock_Red", "M_Rock_Rust", 8),
        fir("fir_a", 0.72, 1), fir("fir_b", 0.56, 2), fir("fir_c", 0.42, 3, light_top=False), fir("fir_young", 0.24, 4), dead_fir(),
        spruce("spruce_a", 0.7, 5), spruce("spruce_b", 0.5, 6), spruce("spruce_snow", 0.62, 7, snow=True), spruce("spruce_snow_b", 0.42, 8, snow=True),
        scots_pine("scots_a", 0.78, 3), scots_pine("scots_b", 0.62, 9), larch("larch", 0.66, 4), larch("larch_gold", 0.6, 5, gold=True), larch("larch_gold_b", 0.44, 12, gold=True),
        cedar("cedar", 0.74, 6), juniper("juniper", 3), juniper("juniper_b", 8), berries("bilberry", "M_Berry_Blue", 4), berries("cowberry", "M_Cloth", 9), anthill(),
        log(), stump("stump", 1), mushrooms(), moss_patch("moss", "M_Moss", 3), moss_patch("turf_dry", "M_Grass_Dry", 5), moss_patch("turf", "M_Leaf_Light", 7),
        acacia_big(), tall_grass("grass_tall_dry", "M_Grass_Dry", 3), tall_grass("grass_tall", "M_Straw", 9), termite_mound(), kopje(),
        wheat_patch("wheat_a", 11), wheat_patch("wheat_b", 23), hay(), oak(),
        dune_field("dunes_a", 3), dune_field("dunes_b", 8), cactus_small("cactus_barrel", 4), bones(), sandstone("sandstone_a", 2), sandstone("sandstone_b", 9), palm(), oasis(),
        lichen_stones("lichen_a", 3), lichen_stones("lichen_b", 12), snow_patch("snow_a", 4), snow_patch("snow_b", 13), cotton_grass(), dwarf_shrub("shrub_a", 5), dwarf_shrub("shrub_b", 15),
        bog("bog_a", 3), bog("bog_b", 11), swamp_tree("cypress_a", 4), swamp_tree("cypress_b", 14), cattails("cattails", 7),
        broadleaf("oak_tall", 0.62, 3), broadleaf("beech", 0.5, 7), broadleaf("tree_autumn", 0.5, 11, ("M_Leaf_Autumn", "M_Leaf_Dry", "M_Leaf")), sapling("sapling", 4), fern_big("fern_big", 5, "M_Leaf_Light"),
        kapok("kapok_a", 0.82, 3), kapok("kapok_b", 0.66, 9), banana("banana", 4), jungle_bloom("bloom", 6), fern_big("fern_jungle", 8),
        crag("crag_a", 3), crag("crag_b", 12), escarpment("escarpment_a", 4), escarpment("escarpment_b", 13), cliff_island("cliff_island_a", 3), cliff_island("cliff_island_b", 8),
        hoodoo("hoodoo_a", 3), hoodoo("hoodoo_b", 8), strata("strata_a", 4), strata("strata_b", 10), dead_bush(),
        snowfield("snowfield_a", 3), snowfield("snowfield_b", 9), serac("serac_a", 2), serac("serac_b", 7), frozen_pool("frozen_pool", 5), snow_rock("snow_rock_a", 4), snow_rock("snow_rock_b", 11),
        volcano_big(), lava_pool("lava_pool", 6), basalt_rocks("basalt_a", 3), basalt_rocks("basalt_b", 8), charred_tree("charred_a", 2), charred_tree("charred_b", 5), ash_patch("ash", 7),
        massif("massif_a", 3), massif("massif_b", 7), rolling_hills("hills_a", 5), rolling_hills("hills_b", 11), outcrop("outcrop_a", 2), outcrop("outcrop_b", 6),
 birch("birch_b", 0.38, 8), berry_bush(), pond(),
        flower_bed("flowers_warm", ("M_Flower", "M_Cloth", "M_Snow"), 4), flower_bed("flowers_cool", ("M_Snow", "M_Glass", "M_Flower"), 9),
        tall_grass("grass_lush", "M_Grass", 17, heads=False), tall_grass("grass_lush_b", "M_Leaf_Light", 21, heads=False),
    ]

    render_preview(models, OUT_PREVIEW, columns=7, spacing=1.25)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    export_fbx(models, "Scenery.fbx")
    report(models)


main()
