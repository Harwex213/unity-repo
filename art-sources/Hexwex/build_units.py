"""Builds the units and monsters of the Hexwex battle as low-poly figures and exports them for Unity.

Run from the repository root:

    blender -b --python art-sources/Hexwex/build_units.py

It writes:
    art-sources/Hexwex/Units.blend                  the source scene
    art-sources/Hexwex/Units_preview.png            a picture of every figure
    projects/Hexwex/Assets/Art/Models/Units.fbx     what Unity imports

There are 27 figures: the sixteen units the player can field and the eleven
creatures of the wild islands (`core/units.ts`).

A figure has no bones. What moves on it is a separate object with its origin on
the joint: the game turns those objects to make it walk, strike and fly
(`FigurePose` in Unity). The parts it knows are:

    LegL, LegR          a walker's legs, hinged at the hip
    ArmL, ArmR          the arms, hinged at the shoulder; the right holds the weapon
    Leg1 .. Leg4        the legs of a beast or a horse: front left, front right, back left, back right
    WingL, WingR        wings, hinged where they meet the body

An object is named `Unit_<key>` for the body and `<key>__<part>` for a part,
because Blender wants every name in a file to be different. The keys are those
of `Units.cs`.

A figure stands on z = 0 with its middle on the origin and faces -Y. A man is
about 0.42 tall against a hex 2 units across. The player's units wear the blue
of `M_Team`; no monster does.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from lowpoly import HERE, Model, export_fbx, render_preview, report  # noqa: E402

OUT_BLEND = os.path.join(HERE, "Units.blend")
OUT_PREVIEW = os.path.join(HERE, "Units_preview.png")

# The right hand holds the weapon, the left the shield or the bow.
RIGHT = 0.1
LEFT = -0.1
# A rider sits this high on the horse and shows no legs.
SADDLE = 0.14


class Figure:
    """A body and the parts that move on it. Everything is built in the figure's own space."""

    def __init__(self, key):
        self.key = key
        self.body = Model("Unit_" + key)
        self.parts = {}
        self.pivots = {}

    def part(self, name, pivot=None):
        """The part of that name. The first call sets the joint it turns about."""
        if name not in self.parts:
            self.parts[name] = Model(self.key + "__" + name)
            self.pivots[name] = Vector(pivot)

        return self.parts[name]

    def resize(self, factor):
        """Scales the whole figure about the origin: an ogre is a man drawn large."""
        self.body.resize(factor)
        for name, model in self.parts.items():
            model.resize(factor)
            self.pivots[name] = self.pivots[name] * factor

    def finish(self):
        """Returns the body object, with every part parented to it at its joint."""
        root = self.body.finish()
        for name, model in self.parts.items():
            pivot = self.pivots[name]
            bmesh.ops.translate(model.bm, vec=-pivot, verts=model.bm.verts[:])
            made = model.finish()
            made.parent = root
            made.location = pivot

        return root


def rod(model, thickness, length, at, mat, lean=0.0, sideways=0.0):
    """A straight stick from `at`: upright, leaning forward by `lean` or to the side by `sideways`, in degrees."""
    tilt = None
    if lean:
        tilt = ("X", math.radians(lean))
    elif sideways:
        tilt = ("Y", math.radians(sideways))

    model.tube(thickness, length, at, mat, sides=4, tilt=tilt, turn=math.pi / 4)


def man(fig, torso, legs="M_Wood", skin="M_Skin", arms=None, lift=0.0, with_legs=True):
    """A man: two legs, a torso, two arms and a head. `lift` raises him, as onto a saddle."""
    if with_legs:
        for name, x in (("LegL", -0.035), ("LegR", 0.035)):
            fig.part(name, (x, 0, lift + 0.14)).box((0.05, 0.055, 0.14), (x, 0, lift), legs)

    fig.body.box((0.14, 0.09, 0.15), (0, 0, lift + 0.14), torso)
    for name, x in (("ArmL", -0.09), ("ArmR", 0.09)):
        arm = fig.part(name, (x, 0, lift + 0.27))
        arm.box((0.04, 0.045, 0.13), (x, 0, lift + 0.15), arms or torso)
        arm.box((0.04, 0.045, 0.03), (x, 0, lift + 0.12), skin)

    fig.body.box((0.09, 0.09, 0.09), (0, 0, lift + 0.3), skin)


def helmet(fig, mat="M_Steel", lift=0.0, crest=None):
    fig.body.box((0.1, 0.1, 0.05), (0, 0, lift + 0.36), mat)
    fig.body.box((0.1, 0.02, 0.03), (0, 0.045, lift + 0.33), mat)
    if crest:
        fig.body.box((0.02, 0.09, 0.05), (0, 0.01, lift + 0.41), crest)


def shield(fig, mat, lift=0.0, round_one=False):
    arm = fig.part("ArmL")
    if round_one:
        arm.tube(0.07, 0.015, (LEFT - 0.01, -0.06, lift + 0.2), mat, sides=8, tilt=("X", math.radians(90)))
        arm.tube(0.02, 0.02, (LEFT - 0.01, -0.063, lift + 0.2), "M_Metal", sides=6, tilt=("X", math.radians(90)))
    else:
        arm.box((0.1, 0.015, 0.13), (LEFT - 0.01, -0.06, lift + 0.14), mat)
        arm.box((0.02, 0.018, 0.13), (LEFT - 0.01, -0.062, lift + 0.14), "M_Metal")


def spear(fig, length, lift=0.0):
    arm = fig.part("ArmR")
    rod(arm, 0.008, length, (RIGHT, -0.04, lift + 0.02), "M_Timber")
    arm.tube(0.018, 0.06, (RIGHT, -0.04, lift + 0.02 + length), "M_Steel", sides=4, top=0.0)


def sword(fig, length, lift=0.0):
    arm = fig.part("ArmR")
    rod(arm, 0.012, length, (RIGHT, -0.05, lift + 0.17), "M_Steel", lean=-20)
    arm.box((0.05, 0.015, 0.015), (RIGHT, -0.05, lift + 0.165), "M_Metal")


def bow(fig, tall, lift=0.0):
    """A bow held out in the left hand, its string toward the archer."""
    arm = fig.part("ArmL")
    middle = lift + 0.2
    rod(arm, 0.007, tall * 0.5, (LEFT, -0.09, middle), "M_Timber", lean=14)
    rod(arm, 0.007, tall * 0.5, (LEFT, -0.09, middle), "M_Timber", lean=166)
    rod(arm, 0.003, tall * 0.94, (LEFT, -0.055, middle - tall * 0.47), "M_Pale")


def quiver(fig, lift=0.0):
    fig.body.box((0.04, 0.03, 0.12), (0.03, 0.06, lift + 0.17), "M_Wood")
    for x in (0.02, 0.035, 0.045):
        fig.body.box((0.008, 0.008, 0.04), (x, 0.06, lift + 0.29), "M_Pale")


def sling(fig, lift=0.0):
    arm = fig.part("ArmR")
    rod(arm, 0.004, 0.14, (RIGHT, -0.02, lift + 0.16), "M_Wood", lean=-70)
    arm.blob((0.035, 0.035, 0.035), (RIGHT, -0.15, lift + 0.21), "M_Rock", seed=2)


def musket(fig, lift=0.0):
    arm = fig.part("ArmR")
    rod(arm, 0.012, 0.3, (RIGHT - 0.02, 0.02, lift + 0.16), "M_Wood", lean=-62)
    rod(arm, 0.007, 0.2, (RIGHT - 0.02, -0.11, lift + 0.23), "M_Iron", lean=-62)


def wide_hat(fig, mat, lift=0.0, feather=None):
    fig.body.tube(0.09, 0.012, (0, 0, lift + 0.385), mat, sides=8)
    fig.body.tube(0.05, 0.05, (0, 0, lift + 0.397), mat, sides=8, top=0.04)
    if feather:
        rod(fig.body, 0.006, 0.09, (0.04, 0.02, lift + 0.4), feather, lean=25)


def hood(fig, mat, lift=0.0):
    fig.body.box((0.1, 0.1, 0.05), (0, 0.005, lift + 0.36), mat)
    fig.body.box((0.1, 0.03, 0.08), (0, 0.045, lift + 0.3), mat)


def beast_legs(fig, mat, x, front, back, size, tall):
    """Four legs hinged at the belly: front left, front right, back left, back right."""
    spots = ((-x, front), (x, front), (-x, back), (x, back))
    for index, (at_x, at_y) in enumerate(spots):
        fig.part("Leg%d" % (index + 1), (at_x, at_y, tall)).box((size[0], size[1], tall), (at_x, at_y, 0), mat)


def horse(fig):
    """A horse under a rider: the rider sits at `SADDLE` and shows no legs."""
    beast_legs(fig, "M_Horse", 0.045, -0.12, 0.12, (0.035, 0.04), 0.15)
    body = fig.body
    body.box((0.13, 0.36, 0.12), (0, 0, 0.14), "M_Horse")
    body.box((0.07, 0.07, 0.16), (0, -0.17, 0.2), "M_Horse")
    body.box((0.07, 0.14, 0.07), (0, -0.24, 0.31), "M_Horse")
    body.box((0.02, 0.1, 0.12), (0, -0.14, 0.26), "M_Rock_Dark")
    body.box((0.03, 0.03, 0.14), (0, 0.19, 0.1), "M_Rock_Dark")
    # The saddle cloth is the player's colour.
    body.box((0.14, 0.14, 0.02), (0, 0.02, 0.26), "M_Team")
    for x in (-0.068, 0.068):
        body.box((0.03, 0.06, 0.1), (x, 0.0, 0.14), "M_Wood")


# ---------- the player's foot ----------

def militia():
    fig = Figure("militia")
    man(fig, "M_Team", arms="M_Wall")
    fig.body.tube(0.07, 0.015, (0, 0, 0.385), "M_Straw", sides=8)
    fig.body.tube(0.04, 0.03, (0, 0, 0.4), "M_Straw", sides=8, top=0.02)
    # A pitchfork: what a farmhand brings to a fight.
    arm = fig.part("ArmR")
    rod(arm, 0.008, 0.36, (RIGHT, -0.04, 0.02), "M_Timber")
    arm.box((0.06, 0.008, 0.008), (RIGHT, -0.04, 0.38), "M_Iron")
    for x in (-0.026, 0.0, 0.026):
        arm.box((0.008, 0.008, 0.06), (RIGHT + x, -0.04, 0.385), "M_Iron")

    return fig.finish()


def spearman():
    fig = Figure("spearman")
    man(fig, "M_Team")
    fig.body.box((0.1, 0.1, 0.035), (0, 0, 0.375), "M_Wood")
    spear(fig, 0.46)
    shield(fig, "M_Timber", round_one=True)

    return fig.finish()


def swordsman():
    fig = Figure("swordsman")
    man(fig, "M_Team", legs="M_Iron")
    helmet(fig)
    sword(fig, 0.2)
    shield(fig, "M_Team")

    return fig.finish()


def halberdier():
    fig = Figure("halberdier")
    man(fig, "M_Steel", legs="M_Team", arms="M_Team")
    helmet(fig)
    arm = fig.part("ArmR")
    rod(arm, 0.009, 0.5, (RIGHT, -0.04, 0.02), "M_Timber")
    arm.box((0.012, 0.075, 0.09), (RIGHT, -0.065, 0.42), "M_Steel")
    arm.tube(0.014, 0.07, (RIGHT, -0.04, 0.52), "M_Steel", sides=4, top=0.0)

    return fig.finish()


def knight():
    fig = Figure("knight")
    man(fig, "M_Steel", legs="M_Steel", skin="M_Steel")
    helmet(fig, crest="M_Metal")
    fig.body.box((0.06, 0.012, 0.02), (0, -0.046, 0.33), "M_Iron")
    for name, x in (("ArmL", -0.095), ("ArmR", 0.095)):
        fig.part(name).box((0.06, 0.06, 0.04), (x, 0, 0.27), "M_Metal")
    fig.body.box((0.13, 0.015, 0.2), (0, 0.055, 0.08), "M_Team")
    sword(fig, 0.26)
    shield(fig, "M_Team")
    fig.resize(1.14)

    return fig.finish()


# ---------- the player's shot, on foot and mounted ----------

def shooter(key, mounted, dress):
    fig = Figure("cavalry_" + key if mounted else key)
    lift = SADDLE if mounted else 0.0
    if mounted:
        horse(fig)

    dress(fig, lift, not mounted)

    return fig.finish()


def dress_slinger(fig, lift, with_legs):
    man(fig, "M_Wall", arms="M_Team", lift=lift, with_legs=with_legs)
    fig.body.box((0.142, 0.092, 0.03), (0, 0, lift + 0.16), "M_Team")
    sling(fig, lift)
    fig.body.box((0.04, 0.03, 0.04), (-0.06, -0.05, lift + 0.14), "M_Wood")


def dress_archer(fig, lift, with_legs):
    man(fig, "M_Team", arms="M_Leaf", lift=lift, with_legs=with_legs)
    hood(fig, "M_Leaf", lift)
    bow(fig, 0.26, lift)
    quiver(fig, lift)


def dress_longbowman(fig, lift, with_legs):
    man(fig, "M_Team", legs="M_Leaf", lift=lift, with_legs=with_legs)
    wide_hat(fig, "M_Leaf", lift, feather="M_Cloth")
    bow(fig, 0.38, lift)
    quiver(fig, lift)


def dress_musketeer(fig, lift, with_legs):
    man(fig, "M_Team", legs="M_Iron", lift=lift, with_legs=with_legs)
    fig.body.box((0.03, 0.095, 0.15), (0, 0, lift + 0.14), "M_Pale")
    wide_hat(fig, "M_Iron", lift, feather="M_Pale")
    musket(fig, lift)


# ---------- the player's wings ----------

def wings(fig, root_x, at_z, inner, outer, inner_mat, outer_mat):
    """Two wings hinged where they meet the body. `inner` and `outer` are (width, depth) of the two feathers of each."""
    for name, side in (("WingL", -1), ("WingR", 1)):
        wing = fig.part(name, (side * root_x, 0, at_z))
        wing.box((inner[0], inner[1], 0.012), (side * (root_x + inner[0] / 2), 0.0, at_z - 0.006), inner_mat, turn=side * 0.2)
        wing.box((outer[0], outer[1], 0.01), (side * (root_x + inner[0] + outer[0] * 0.4), 0.04, at_z - 0.005), outer_mat, turn=side * 0.55)


def bird(key, size, feathers, head, wing_mat):
    fig = Figure(key)
    fig.body.blob((0.1, 0.2, 0.09), (0, 0, 0.06), feathers, seed=3, rough=0.05)
    fig.body.blob((0.07, 0.08, 0.07), (0, -0.12, 0.09), head, seed=5, rough=0.05)
    fig.body.tube(0.016, 0.05, (0, -0.15, 0.085), "M_Metal", sides=4, top=0.0, tilt=("X", math.radians(100)))
    fig.body.box((0.06, 0.1, 0.01), (0, 0.13, 0.06), wing_mat)
    wings(fig, 0.04, 0.085, (0.2, 0.11), (0.1, 0.07), wing_mat, feathers)
    fig.resize(size)

    return fig.finish()


def griffin():
    fig = Figure("griffin")
    body = fig.body
    # A lion behind, an eagle in front. It flies, so its legs hang still.
    for x in (-0.045, 0.045):
        body.box((0.04, 0.045, 0.1), (x, 0.11, 0), "M_Sand")
        body.box((0.035, 0.04, 0.1), (x, -0.1, 0), "M_Metal")
    body.box((0.14, 0.32, 0.11), (0, 0.01, 0.09), "M_Sand")
    body.box((0.1, 0.1, 0.12), (0, -0.16, 0.14), "M_Feather")
    body.blob((0.09, 0.1, 0.09), (0, -0.22, 0.29), "M_Snow", seed=5, rough=0.05)
    body.tube(0.02, 0.07, (0, -0.26, 0.28), "M_Metal", sides=4, top=0.0, tilt=("X", math.radians(110)))
    rod(body, 0.008, 0.16, (0, 0.17, 0.14), "M_Sand", lean=-55)
    body.blob((0.04, 0.04, 0.04), (0, 0.3, 0.24), "M_Feather", seed=1)
    wings(fig, 0.07, 0.21, (0.26, 0.14), (0.14, 0.09), "M_Feather", "M_Snow")

    return fig.finish()


# ---------- the creatures ----------

def wolf():
    fig = Figure("wolf")
    body = fig.body
    beast_legs(fig, "M_Fur", 0.04, -0.09, 0.1, (0.03, 0.035), 0.1)
    body.box((0.11, 0.28, 0.1), (0, 0, 0.09), "M_Fur")
    body.box((0.1, 0.1, 0.1), (0, -0.17, 0.13), "M_Fur")
    body.box((0.05, 0.08, 0.045), (0, -0.25, 0.13), "M_Rock_Dark")
    for x in (-0.032, 0.032):
        body.tube(0.018, 0.045, (x, -0.15, 0.23), "M_Rock_Dark", sides=4, top=0.0)
        body.box((0.012, 0.01, 0.012), (x * 0.8, -0.221, 0.19), "M_Eye")
    rod(body, 0.014, 0.14, (0, 0.14, 0.15), "M_Fur", lean=-125)

    return fig.finish()


def spider():
    fig = Figure("spider")
    body = fig.body
    body.blob((0.16, 0.2, 0.13), (0, 0.07, 0.11), "M_Chitin", seed=2, rough=0.06)
    body.blob((0.1, 0.1, 0.08), (0, -0.07, 0.09), "M_Chitin", seed=4, rough=0.06)
    body.blob((0.05, 0.05, 0.03), (0, 0.09, 0.175), "M_Cloth", seed=1)
    # Four pairs of legs; each pair of pairs steps as one, like a beast's.
    for side in (-1, 1):
        body.box((0.012, 0.01, 0.012), (side * 0.02, -0.118, 0.1), "M_Eye")
        for index in range(4):
            y = -0.09 + index * 0.045
            turn = side * (0.5 - index * 0.33)
            number = (1 if index < 2 else 3) + (0 if side < 0 else 1)
            leg = fig.part("Leg%d" % number, (0, 0.0 if index < 2 else 0.04, 0.11))
            leg.box((0.13, 0.012, 0.012), (side * 0.09, y, 0.11), "M_Chitin", turn=turn)
            rod(leg, 0.006, 0.13, (side * 0.15 * math.cos(turn), y + 0.15 * math.sin(abs(turn)) * (1 if index < 2 else -1) * 0.6, 0.0), "M_Chitin")

    return fig.finish()


def leech():
    fig = Figure("leech")
    body = fig.body
    segments = ((0.16, 0.1), (0.08, 0.13), (-0.01, 0.14), (-0.09, 0.12), (-0.16, 0.1))
    for index, (y, width) in enumerate(segments):
        rise = 0.03 * (index - 2) if index >= 3 else 0.0
        body.blob((width, 0.11, width * 0.8), (0, y, width * 0.4 + rise), "M_Leech", seed=index, rough=0.04)
    # The mouth: a pale ring with a dark hole.
    body.tube(0.04, 0.02, (0, -0.2, 0.11), "M_Pale", sides=8, tilt=("X", math.radians(80)))
    body.tube(0.024, 0.022, (0, -0.203, 0.11), "M_Chitin", sides=8, tilt=("X", math.radians(80)))

    return fig.finish()


def skeleton():
    fig = Figure("skeleton")
    body = fig.body
    for name, x in (("LegL", -0.03), ("LegR", 0.03)):
        fig.part(name, (x, 0, 0.14)).box((0.022, 0.022, 0.14), (x, 0, 0), "M_Bone")
    body.box((0.1, 0.03, 0.03), (0, 0, 0.14), "M_Bone")
    body.box((0.022, 0.03, 0.13), (0, 0, 0.14), "M_Bone")
    for z in (0.19, 0.22, 0.25):
        body.box((0.11, 0.06, 0.012), (0, 0, z), "M_Bone")
    for name, x in (("ArmL", -0.075), ("ArmR", 0.075)):
        fig.part(name, (x, 0, 0.26)).box((0.02, 0.02, 0.14), (x, 0, 0.13), "M_Bone")
    body.box((0.085, 0.085, 0.08), (0, 0, 0.29), "M_Bone")
    body.box((0.06, 0.05, 0.025), (0, -0.01, 0.275), "M_Bone")
    for x in (-0.02, 0.02):
        body.box((0.02, 0.012, 0.022), (x, -0.04, 0.32), "M_Chitin")
    rod(fig.part("ArmR"), 0.011, 0.2, (0.085, -0.04, 0.15), "M_Rock_Rust", lean=-25)

    return fig.finish()


def zombie():
    fig = Figure("zombie")
    body = fig.body
    for name, x in (("LegL", -0.035), ("LegR", 0.035)):
        fig.part(name, (x, 0, 0.14)).box((0.05, 0.055, 0.14), (x, 0, 0), "M_Dirt")
    body.box((0.14, 0.09, 0.15), (0, -0.01, 0.14), "M_Moss")
    body.box((0.06, 0.092, 0.05), (0.03, -0.01, 0.16), "M_Rot")
    # The arms reach out ahead of it, and claw at what they reach.
    for name, x in (("ArmL", -0.09), ("ArmR", 0.09)):
        rod(fig.part(name, (x, -0.02, 0.25)), 0.022, 0.14, (x, -0.02, 0.25), "M_Rot", lean=-95)
    body.box((0.09, 0.09, 0.09), (0.01, -0.03, 0.28), "M_Rot")
    for x in (-0.012, 0.03):
        body.box((0.016, 0.012, 0.016), (x, -0.076, 0.33), "M_Chitin")

    return fig.finish()


def ogre():
    fig = Figure("ogre")
    body = fig.body
    for name, x in (("LegL", -0.05), ("LegR", 0.05)):
        fig.part(name, (x, 0, 0.12)).box((0.07, 0.08, 0.12), (x, 0, 0), "M_Ogre")
    body.box((0.2, 0.14, 0.05), (0, 0, 0.11), "M_Wood")
    body.box((0.22, 0.15, 0.17), (0, 0, 0.15), "M_Ogre")
    body.blob((0.2, 0.16, 0.12), (0, -0.03, 0.2), "M_Ogre", seed=6, rough=0.04)
    for name, x in (("ArmL", -0.14), ("ArmR", 0.14)):
        fig.part(name, (x, 0, 0.3)).box((0.065, 0.07, 0.17), (x, 0, 0.14), "M_Ogre")
    body.box((0.09, 0.09, 0.08), (0, -0.01, 0.32), "M_Ogre")
    for x in (-0.025, 0.025):
        body.tube(0.012, 0.03, (x, -0.05, 0.325), "M_Bone", sides=4, top=0.0)
    # A club that is half a tree.
    arm = fig.part("ArmR")
    rod(arm, 0.018, 0.2, (0.15, -0.05, 0.14), "M_Timber", lean=-30)
    arm.tube(0.045, 0.16, (0.15, -0.15, 0.31), "M_Wood", sides=6, top=0.055, tilt=("X", math.radians(-30)))
    fig.resize(1.75)

    return fig.finish()


def witch():
    fig = Figure("witch")
    body = fig.body
    body.tube(0.11, 0.26, (0, 0, 0), "M_Roof", sides=8, top=0.055)
    rod(fig.part("ArmL", (-0.075, -0.01, 0.24)), 0.018, 0.12, (-0.075, -0.01, 0.24), "M_Roof", lean=-150)
    body.box((0.08, 0.08, 0.08), (0, 0, 0.26), "M_Rot")
    body.tube(0.012, 0.03, (0, -0.045, 0.285), "M_Rot", sides=4, top=0.0, tilt=("X", math.radians(95)))
    body.tube(0.11, 0.012, (0, 0, 0.335), "M_Night", sides=8)
    body.tube(0.055, 0.17, (0, 0, 0.345), "M_Night", sides=8, top=0.0, tilt=("X", math.radians(-8)))
    # A staff with the poison alight on its head: she casts with it.
    arm = fig.part("ArmR", (0.075, -0.01, 0.24))
    rod(arm, 0.018, 0.12, (0.075, -0.01, 0.24), "M_Roof", lean=-150)
    rod(arm, 0.008, 0.42, (0.11, -0.05, 0), "M_Wood")
    arm.blob((0.06, 0.06, 0.06), (0.11, -0.05, 0.45), "M_Toxic", seed=2)

    return fig.finish()


def vampire():
    fig = Figure("vampire")
    body = fig.body
    for name, x in (("LegL", -0.03), ("LegR", 0.03)):
        fig.part(name, (x, 0, 0.16)).box((0.04, 0.05, 0.16), (x, 0, 0), "M_Night")
    body.box((0.12, 0.08, 0.17), (0, 0, 0.16), "M_Night")
    body.box((0.04, 0.082, 0.12), (0, 0, 0.19), "M_Cloth")
    for name, x in (("ArmL", -0.08), ("ArmR", 0.08)):
        arm = fig.part(name, (x, 0, 0.31))
        arm.box((0.035, 0.04, 0.15), (x, 0, 0.17), "M_Night")
        arm.box((0.03, 0.035, 0.03), (x, 0, 0.14), "M_Pale")
    body.box((0.08, 0.085, 0.09), (0, 0, 0.34), "M_Pale")
    body.box((0.085, 0.05, 0.03), (0, 0.02, 0.42), "M_Night")
    for x in (-0.02, 0.02):
        body.box((0.014, 0.01, 0.01), (x, -0.045, 0.385), "M_Eye")
    # The cloak, black outside and red within, with its collar up.
    body.box((0.2, 0.015, 0.3), (0, 0.055, 0.03), "M_Night")
    body.box((0.18, 0.012, 0.28), (0, 0.044, 0.04), "M_Cloth")
    for x in (-0.065, 0.065):
        body.box((0.03, 0.06, 0.09), (x, 0.03, 0.33), "M_Night")
    fig.resize(1.12)

    return fig.finish()


def moth():
    fig = Figure("moth")
    body = fig.body
    body.blob((0.07, 0.18, 0.07), (0, 0, 0.06), "M_Dust", seed=2, rough=0.05)
    body.blob((0.06, 0.06, 0.06), (0, -0.1, 0.07), "M_Fur", seed=3)
    for name, side in (("WingL", -1), ("WingR", 1)):
        wing = fig.part(name, (side * 0.03, 0, 0.08))
        wing.box((0.2, 0.16, 0.008), (side * 0.13, -0.03, 0.075), "M_Dust", turn=side * -0.35)
        wing.box((0.14, 0.12, 0.008), (side * 0.1, 0.09, 0.07), "M_Wall", turn=side * 0.4)
        wing.blob((0.05, 0.05, 0.012), (side * 0.15, -0.03, 0.082), "M_Night", seed=4)
        rod(body, 0.004, 0.08, (side * 0.018, -0.12, 0.09), "M_Night", lean=-50)

    return fig.finish()


def bat():
    fig = Figure("bat")
    body = fig.body
    body.blob((0.07, 0.1, 0.08), (0, 0, 0.06), "M_Night", seed=2, rough=0.05)
    for name, side in (("WingL", -1), ("WingR", 1)):
        body.tube(0.016, 0.045, (side * 0.02, -0.02, 0.09), "M_Night", sides=4, top=0.0)
        body.box((0.01, 0.008, 0.01), (side * 0.016, -0.05, 0.065), "M_Eye")
        wing = fig.part(name, (side * 0.03, 0, 0.075))
        wing.box((0.15, 0.1, 0.008), (side * 0.1, 0.0, 0.07), "M_Rock_Dark", turn=side * 0.3)
        wing.box((0.1, 0.07, 0.008), (side * 0.2, 0.04, 0.07), "M_Night", turn=side * 0.75)

    return fig.finish()


def plague_lord():
    """The boss: a robed giant, horned, with the poison burning in his hands and at his feet."""
    fig = Figure("plague_lord")
    body = fig.body
    body.tube(0.2, 0.34, (0, 0, 0), "M_Night", sides=8, top=0.12)
    body.tube(0.21, 0.03, (0, 0, 0), "M_Toxic", sides=8)
    body.box((0.26, 0.14, 0.14), (0, 0, 0.32), "M_Basalt")
    body.box((0.06, 0.142, 0.12), (0, 0, 0.33), "M_Rot")
    for name, side in (("ArmL", -1), ("ArmR", 1)):
        body.blob((0.13, 0.13, 0.09), (side * 0.15, 0, 0.47), "M_Iron", seed=3, rough=0.05)
        body.tube(0.03, 0.1, (side * 0.17, 0, 0.49), "M_Bone", sides=4, top=0.0, tilt=("Y", math.radians(side * 30)))
        arm = fig.part(name, (side * 0.16, -0.02, 0.42))
        rod(arm, 0.03, 0.2, (side * 0.16, -0.02, 0.4), "M_Night", lean=-140)
        arm.blob((0.07, 0.07, 0.07), (side * 0.16, -0.16, 0.25), "M_Toxic", seed=2)
    # The skull and its crown of horns.
    body.box((0.1, 0.1, 0.1), (0, -0.01, 0.46), "M_Bone")
    body.box((0.07, 0.06, 0.03), (0, -0.02, 0.44), "M_Bone")
    for side in (-1, 1):
        body.box((0.022, 0.012, 0.025), (side * 0.024, -0.06, 0.5), "M_Toxic")
        body.tube(0.022, 0.14, (side * 0.045, 0, 0.55), "M_Bone", sides=4, top=0.0, tilt=("Y", math.radians(side * 28)))
    body.box((0.11, 0.1, 0.02), (0, 0, 0.56), "M_Metal")
    body.box((0.12, 0.03, 0.2), (0, 0.06, 0.38), "M_Night")
    # The scythe stands beside him.
    rod(body, 0.012, 0.66, (0.24, -0.1, 0), "M_Wood")
    body.box((0.22, 0.012, 0.045), (0.14, -0.1, 0.62), "M_Steel")
    fig.resize(2.1)

    return fig.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    shots = (("slinger", dress_slinger), ("archer", dress_archer), ("longbowman", dress_longbowman), ("musketeer", dress_musketeer))
    models = [militia(), spearman(), swordsman(), halberdier(), knight()]
    models += [shooter(key, False, dress) for key, dress in shots]
    models += [shooter(key, True, dress) for key, dress in shots]
    models += [
        bird("crow", 0.8, "M_Night", "M_Night", "M_Rock_Dark"),
        bird("great_eagle", 1.25, "M_Feather", "M_Snow", "M_Feather"),
        griffin(),
        wolf(), spider(), leech(), skeleton(), zombie(), ogre(), witch(), vampire(), moth(), bat(),
        plague_lord(),
    ]

    render_preview(models, OUT_PREVIEW, columns=7, spacing=1.0)
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    # The parts go out with their bodies, each still hanging on its own.
    export_fbx([made for made in bpy.data.objects if made.type == "MESH"], "Units.fbx")
    report(models)
    print("PARTS", sorted({made.name.split("__")[1] for made in bpy.data.objects if "__" in made.name}))


main()
