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
    ]

    render_preview(models, OUT_PREVIEW, columns=7, spacing=1.25)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    export_fbx(models, "Scenery.fbx")
    report(models)


main()
