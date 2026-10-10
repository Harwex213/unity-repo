"""Builds the nine structures of Hexwex as low-poly models and exports them for Unity.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_buildings.py

It writes:
    art-sources/Hexwex/Buildings.blend                  the source scene
    art-sources/Hexwex/Buildings_preview.png            a picture of all nine
    projects/Hexwex/Assets/Art/Models/Buildings.fbx     what Unity imports

The style and the palette are those of art-sources/Ostrov/Buildings.blend:
flat-shaded blocks, purple roofs, cream walls, stone plinths, gold metal. Four
models are taken from that file as they are (farm, quarry, cottage, castle);
the other five are built here from boxes, cylinders and roofs.

Every model stands on z = 0 with its middle on the origin and its front facing
-Y. One hex is 2 units corner to corner, and a model keeps inside about 1.2.
The object names are `Bld_<art name>`, and the art names are the ones the game
uses (`Buildings.ArtName`, `Stronghold.ArtName`).
"""

import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from lowpoly import HERE, ROOT, Model, export_fbx, placed, render_preview, report  # noqa: E402

OSTROV_BLEND = os.path.join(ROOT, "art-sources", "Ostrov", "Buildings.blend")
OUT_BLEND = os.path.join(HERE, "Buildings.blend")
OUT_PREVIEW = os.path.join(HERE, "Buildings_preview.png")


def building(art_name):
    """The object names are `Bld_<art name>`: the prefab builder in Unity reads the art name from them."""
    return Model("Bld_" + art_name)


def load_ostrov():
    """The four models this game shares with the Ostrov set."""
    names = ["Bld_Farm", "Bld_Quarry", "Bld_Cottage", "Bld_Castle"]
    with bpy.data.libraries.load(OSTROV_BLEND) as (source, target):
        target.objects = [name for name in names if name in source.objects]

    return {made.name: made for made in target.objects}


def sawmill():
    model = building("sawmill")
    model.box((1.15, 0.86, 0.06), (0, 0, 0), "M_Stone")
    # The cabin, on the left.
    model.box((0.42, 0.5, 0.3), (-0.32, 0.1, 0.06), "M_Wood")
    model.gable(0.5, 0.6, 0.22, (-0.32, 0.1, 0.36), "M_Roof", along="Y")
    model.box((0.1, 0.02, 0.18), (-0.32, -0.16, 0.06), "M_Rock_Dark")
    # The open shed over the saw.
    for x in (0.02, 0.5):
        for y in (-0.12, 0.3):
            model.box((0.05, 0.05, 0.34), (x, y, 0.06), "M_Wood")
    model.gable(0.6, 0.56, 0.16, (0.26, 0.09, 0.4), "M_Roof", along="X")
    # The table, the log on it and the blade through the log.
    model.box((0.4, 0.14, 0.12), (0.26, 0.09, 0.06), "M_Wood")
    model.tube(0.05, 0.36, (0.08, 0.09, 0.23), "M_Timber", sides=8, tilt=("Y", math.radians(90)))
    model.tube(0.13, 0.015, (0.3, 0.0975, 0.2), "M_Iron", sides=12, tilt=("X", math.radians(90)))
    # The pile of logs in front.
    for index, (y, z) in enumerate(((-0.34, 0.06), (-0.25, 0.06), (-0.295, 0.135))):
        model.tube(0.047, 0.5, (0.0, y, z + 0.047), "M_Timber" if index == 2 else "M_Wood", sides=8, tilt=("Y", math.radians(90)))

    return model.finish()


def masons_guild():
    model = building("masons-guild")
    model.box((1.15, 0.9, 0.06), (0, 0, 0), "M_Stone")
    # The hall of cut stone, with a parapet.
    model.box((0.66, 0.5, 0.4), (-0.18, 0.12, 0.06), "M_Stone")
    model.box((0.72, 0.56, 0.05), (-0.18, 0.12, 0.46), "M_Rock_Dark")
    for x in (-0.5, -0.34, -0.18, -0.02, 0.14):
        for y in (-0.13, 0.37):
            model.box((0.08, 0.06, 0.07), (x, y, 0.51), "M_Stone")
    model.box((0.16, 0.02, 0.24), (-0.18, -0.14, 0.06), "M_Wood")
    model.box((0.2, 0.03, 0.04), (-0.18, -0.145, 0.3), "M_Metal")
    for x in (-0.4, 0.04):
        model.box((0.1, 0.02, 0.1), (x, -0.135, 0.24), "M_Metal")
    model.box((0.1, 0.1, 0.3), (-0.4, 0.28, 0.46), "M_Rock_Dark")
    # The yard: dressed blocks and the crane that lifts them.
    model.box((0.2, 0.16, 0.12), (0.36, -0.22, 0.06), "M_Wall")
    model.box((0.18, 0.14, 0.11), (0.37, -0.215, 0.18), "M_Wall", turn=0.3)
    model.box((0.16, 0.16, 0.1), (0.36, 0.0, 0.06), "M_Wall", turn=-0.2)
    model.box((0.06, 0.06, 0.62), (0.44, 0.3, 0.06), "M_Wood")
    model.box((0.36, 0.05, 0.05), (0.32, 0.3, 0.62), "M_Wood")
    model.box((0.015, 0.015, 0.26), (0.18, 0.3, 0.36), "M_Iron")
    model.box((0.12, 0.1, 0.09), (0.18, 0.3, 0.27), "M_Wall")

    return model.finish()


def observatory():
    model = building("observatory")
    model.tube(0.56, 0.06, (0, 0, 0), "M_Stone", sides=12)
    model.tube(0.46, 0.05, (0, 0, 0.06), "M_Stone", sides=12)
    # The round tower.
    model.tube(0.34, 0.62, (0, 0, 0.11), "M_Wall", sides=12)
    model.tube(0.37, 0.06, (0, 0, 0.3), "M_Roof", sides=12)
    model.tube(0.38, 0.07, (0, 0, 0.7), "M_Metal", sides=12)
    model.box((0.14, 0.04, 0.24), (0, -0.33, 0.11), "M_Wood")
    for angle in (50, 130, 230, 310):
        x, y = math.cos(math.radians(angle)) * 0.335, math.sin(math.radians(angle)) * 0.335
        model.box((0.07, 0.03, 0.12), (x, y, 0.42), "M_Glass", turn=math.radians(angle + 90))
    # The dome, its slit and the telescope looking out of it.
    model.dome(0.34, (0, 0, 0.77), "M_Roof", squash=0.9)
    model.box((0.1, 0.36, 0.3), (0, -0.16, 0.77), "M_Rock_Dark")
    model.tube(0.055, 0.5, (0, -0.02, 0.86), "M_Iron", sides=8, tilt=("X", math.radians(52)))
    model.tube(0.07, 0.05, (0, -0.02, 0.86), "M_Metal", sides=8, tilt=("X", math.radians(52)))
    # The stair to the door.
    model.box((0.24, 0.14, 0.05), (0, -0.46, 0.06), "M_Stone")

    return model.finish()


def university():
    model = building("university")
    model.box((1.2, 0.84, 0.06), (0, 0, 0), "M_Stone")
    model.box((1.1, 0.74, 0.05), (0, 0.02, 0.06), "M_Stone")
    # The hall.
    model.box((0.96, 0.46, 0.42), (0, 0.12, 0.11), "M_Wall")
    model.box((1.0, 0.5, 0.04), (0, 0.12, 0.53), "M_Stone")
    model.gable(1.04, 0.56, 0.2, (0, 0.12, 0.57), "M_Roof", along="X")
    for x in (-0.36, -0.2, 0.2, 0.36):
        model.box((0.08, 0.02, 0.16), (x, -0.115, 0.26), "M_Glass")
    # The portico: four columns under a pediment.
    for x in (-0.24, -0.08, 0.08, 0.24):
        model.tube(0.035, 0.38, (x, -0.24, 0.11), "M_Wall", sides=8)
        model.box((0.09, 0.09, 0.03), (x, -0.24, 0.11), "M_Stone")
    model.box((0.62, 0.2, 0.05), (0, -0.22, 0.49), "M_Stone")
    model.gable(0.62, 0.2, 0.14, (0, -0.22, 0.54), "M_Roof", along="Y")
    model.box((0.16, 0.02, 0.26), (0, -0.115, 0.11), "M_Wood")
    model.box((0.5, 0.12, 0.05), (0, -0.36, 0.06), "M_Stone")
    # The cupola over the middle, with its gold tip.
    model.tube(0.12, 0.16, (0, 0.12, 0.7), "M_Wall", sides=8)
    model.tube(0.15, 0.2, (0, 0.12, 0.86), "M_Roof", sides=8, top=0.0)
    model.tube(0.02, 0.1, (0, 0.12, 1.04), "M_Metal", sides=6, top=0.0)

    return model.finish()


def converter():
    model = building("converter")
    model.tube(0.62, 0.07, (0, 0, 0), "M_Stone", sides=12)
    model.tube(0.5, 0.07, (0, 0, 0.07), "M_Stone", sides=12)
    model.tube(0.36, 0.1, (0, 0, 0.14), "M_Rock_Dark", sides=12)
    # The vessel where the poison turns into clean air.
    model.tube(0.2, 0.74, (0, 0, 0.24), "M_Glow", sides=10)
    for z in (0.24, 0.56, 0.9):
        model.tube(0.235, 0.07, (0, 0, z), "M_Metal", sides=10)
    model.tube(0.235, 0.2, (0, 0, 0.97), "M_Iron", sides=10, top=0.09)
    model.tube(0.09, 0.2, (0, 0, 1.17), "M_Iron", sides=8)
    model.tube(0.12, 0.05, (0, 0, 1.33), "M_Metal", sides=8)
    model.dome(0.1, (0, 0, 1.47), "M_Glow")
    # Four pylons around it, each feeding the vessel through a pipe.
    for angle in (45, 135, 225, 315):
        x, y = math.cos(math.radians(angle)) * 0.44, math.sin(math.radians(angle)) * 0.44
        turn = math.radians(angle)
        model.box((0.14, 0.14, 0.52), (x, y, 0.07), "M_Stone", turn=turn)
        model.box((0.17, 0.17, 0.04), (x, y, 0.59), "M_Metal", turn=turn)
        model.tube(0.1, 0.14, (x, y, 0.63), "M_Metal", sides=4, top=0.0)
        model.box((0.26, 0.05, 0.05), (x * 0.62, y * 0.62, 0.44), "M_Iron", turn=turn)

    return model.finish()


def village(cottage):
    model = building("village")
    model.take(cottage, placed(-0.34, 0.24, 0.48, 0.25))
    model.take(cottage, placed(0.34, 0.2, 0.46, -0.5))
    model.take(cottage, placed(0.02, -0.34, 0.44, 0.0))
    # The well between the houses.
    model.tube(0.07, 0.07, (-0.02, 0.02, 0), "M_Stone", sides=8)
    model.box((0.02, 0.02, 0.16), (-0.08, 0.02, 0.0), "M_Wood")
    model.box((0.02, 0.02, 0.16), (0.04, 0.02, 0.0), "M_Wood")
    model.gable(0.18, 0.12, 0.05, (-0.02, 0.02, 0.16), "M_Roof", along="X")

    return model.finish()


def copied(art_name, source):
    model = building(art_name)
    model.take(source)

    return model.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # The Ostrov file brings its own materials; the palette only adds the ones it lacks.
    ostrov = load_ostrov()
    models = [
        copied("farm", ostrov["Bld_Farm"]),
        copied("mine", ostrov["Bld_Quarry"]),
        sawmill(),
        village(ostrov["Bld_Cottage"]),
        masons_guild(),
        observatory(),
        university(),
        converter(),
        copied("stronghold", ostrov["Bld_Castle"]),
    ]

    # The Ostrov objects were only sources: the file keeps the nine models alone.
    for source in ostrov.values():
        bpy.data.objects.remove(source)

    render_preview(models, OUT_PREVIEW)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

    export_fbx(models, "Buildings.fbx")
    report(models)


main()
