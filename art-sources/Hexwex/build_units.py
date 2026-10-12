"""Builds the units and monsters of the Hexwex battle as rounded figures and exports them for Unity.

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
# A figure is small: its edges are rounded finer than a building's.
FIGURE_SOFT = 0.003


class Figure:
    """A body and the parts that move on it. Everything is built in the figure's own space."""

    def __init__(self, key):
        self.key = key
        self.body = Model("Unit_" + key, soft=FIGURE_SOFT)
        self.parts = {}
        self.pivots = {}

    def part(self, name, pivot=None):
        """The part of that name. The first call sets the joint it turns about."""
        if name not in self.parts:
            self.parts[name] = Model(self.key + "__" + name, soft=FIGURE_SOFT)
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

    model.tube(thickness, length, at, mat, sides=5, tilt=tilt)


def man(fig, torso, legs="M_Wood", skin="M_Skin", arms=None, lift=0.0, with_legs=True, bulk=1.0, reach=0.0):
    """
    A man: two legs in boots, hips, a chest wider at the shoulders, a neck, two
    arms that end in hands, and a head with a nose and hair. `lift` raises him,
    as onto a saddle. `bulk` makes him broader without making him taller, and
    `reach` holds his arms out ahead of him by that many degrees. He is built of
    limbs that narrow and of rounded masses, not of blocks, and stands about 0.4
    tall.
    """
    def wide(width, depth):
        return (width * bulk, depth * bulk)

    if with_legs:
        for name, x in (("LegL", -0.034 * bulk), ("LegR", 0.034 * bulk)):
            leg = fig.part(name, (x, 0, lift + 0.15))
            leg.taper(wide(0.036, 0.04), wide(0.052, 0.058), 0.13, (x, 0, lift + 0.02), legs)
            # The boot: a shaft and a toe that points forward.
            leg.taper(wide(0.044, 0.05), wide(0.04, 0.044), 0.035, (x, 0, lift), "M_Wood")
            leg.ball((0.044 * bulk, 0.07 * bulk, 0.03), (x, -0.02 * bulk, lift + 0.014), "M_Wood")

    body = fig.body
    body.ball((0.12 * bulk, 0.085 * bulk, 0.07), (0, 0, lift + 0.16), legs if with_legs else torso)
    body.taper(wide(0.108, 0.074), wide(0.14, 0.088), 0.1, (0, 0, lift + 0.16), torso)
    body.ball((0.15 * bulk, 0.09 * bulk, 0.06), (0, 0, lift + 0.262), torso)
    # A belt at the waist.
    body.taper(wide(0.114, 0.08), wide(0.114, 0.08), 0.014, (0, 0, lift + 0.158), "M_Wood")
    body.taper(wide(0.036, 0.036), wide(0.032, 0.032), 0.03, (0, 0, lift + 0.28), skin)

    # An arm hangs from the shoulder, or is held out ahead by `reach`.
    swing = math.radians(180 - reach)
    out = Vector((0, -math.sin(swing), math.cos(swing)))
    for name, x in (("ArmL", -0.088 * bulk), ("ArmR", 0.088 * bulk)):
        shoulder = Vector((x, 0, lift + 0.265))
        arm = fig.part(name, shoulder)
        arm.ball((0.052 * bulk, 0.056 * bulk, 0.05), shoulder, arms or torso)
        arm.taper(wide(0.042, 0.046), wide(0.03, 0.032), 0.115, shoulder, arms or torso, tilt=("X", swing))
        arm.ball((0.03 * bulk, 0.034 * bulk, 0.038), shoulder + out * 0.13, skin)

    # The head: the skull, the jaw under it, a nose, and hair over the back and the crown.
    body.ball((0.084, 0.09, 0.09), (0, 0, lift + 0.348), skin)
    body.ball((0.066, 0.07, 0.05), (0, -0.008, lift + 0.318), skin)
    body.ball((0.014, 0.02, 0.022), (0, -0.046, lift + 0.338), skin)
    if skin == "M_Skin":
        body.ball((0.09, 0.088, 0.07), (0, 0.012, lift + 0.366), "M_Wood")
        for x in (-0.017, 0.017):
            body.ball((0.011, 0.008, 0.011), (x, -0.041, lift + 0.354), "M_Chitin")


def helmet(fig, mat="M_Steel", lift=0.0, crest=None):
    """A round helm with a brim, a nose guard, and a guard over the neck."""
    fig.body.ball((0.1, 0.104, 0.085), (0, 0.002, lift + 0.37), mat)
    fig.body.taper((0.104, 0.108), (0.098, 0.102), 0.012, (0, 0.002, lift + 0.362), mat)
    fig.body.box((0.014, 0.012, 0.04), (0, -0.05, lift + 0.33), mat)
    fig.body.box((0.086, 0.016, 0.04), (0, 0.044, lift + 0.325), mat)
    if crest:
        fig.body.box((0.016, 0.09, 0.04), (0, 0.006, lift + 0.405), crest)


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
    """Four legs hinged at the belly: front left, front right, back left, back right. Each has a thigh, a shank and a foot."""
    spots = ((-x, front), (x, front), (-x, back), (x, back))
    for index, (at_x, at_y) in enumerate(spots):
        leg = fig.part("Leg%d" % (index + 1), (at_x, at_y, tall))
        leg.taper((size[0] * 0.8, size[1] * 0.8), (size[0] * 1.35, size[1] * 1.5), tall * 0.5, (at_x, at_y, tall * 0.5), mat)
        leg.taper((size[0] * 0.62, size[1] * 0.62), (size[0] * 0.8, size[1] * 0.8), tall * 0.44, (at_x, at_y, tall * 0.07), mat)
        leg.taper((size[0] * 0.85, size[1] * 0.95), (size[0] * 0.68, size[1] * 0.7), tall * 0.08, (at_x, at_y - size[1] * 0.1, 0), "M_Rock_Dark")


def horse(fig):
    """A horse under a rider: the rider sits at `SADDLE` and shows no legs."""
    beast_legs(fig, "M_Horse", 0.045, -0.12, 0.12, (0.035, 0.04), 0.15)
    body = fig.body
    # The barrel, the chest in front of it and the haunches behind.
    body.ball((0.13, 0.34, 0.13), (0, 0, 0.2), "M_Horse")
    body.ball((0.125, 0.15, 0.14), (0, -0.11, 0.205), "M_Horse")
    body.ball((0.135, 0.16, 0.145), (0, 0.11, 0.21), "M_Horse")
    # The neck rises forward to the head, which narrows to the muzzle.
    body.taper((0.07, 0.1), (0.05, 0.065), 0.17, (0, -0.15, 0.22), "M_Horse", tilt=("X", math.radians(28)))
    body.ball((0.062, 0.1, 0.075), (0, -0.25, 0.375), "M_Horse", tilt=("X", math.radians(-35)))
    body.ball((0.046, 0.07, 0.05), (0, -0.295, 0.34), "M_Horse", tilt=("X", math.radians(-35)))
    for x in (-0.022, 0.022):
        body.tube(0.011, 0.035, (x, -0.215, 0.4), "M_Horse", sides=6, top=0.002)
        body.ball((0.012, 0.01, 0.012), (x * 1.2, -0.262, 0.385), "M_Chitin")
    # The mane down the neck and the tail.
    body.box((0.018, 0.13, 0.09), (0, -0.16, 0.27), "M_Rock_Dark", turn=0.0)
    body.taper((0.02, 0.02), (0.035, 0.04), 0.16, (0, 0.2, 0.07), "M_Rock_Dark", tilt=("X", math.radians(-14)))
    # The saddle and its cloth, in the player's colour.
    body.ball((0.15, 0.17, 0.05), (0, 0.02, 0.258), "M_Team")
    body.ball((0.09, 0.12, 0.045), (0, 0.02, 0.275), "M_Wood")
    for x in (-0.068, 0.068):
        body.taper((0.03, 0.05), (0.034, 0.06), 0.1, (x, 0.0, 0.14), "M_Wood")


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
    # A deep chest, a lean belly behind it, and a thick neck carried low.
    body.ball((0.11, 0.2, 0.115), (0, -0.04, 0.145), "M_Fur")
    body.ball((0.095, 0.2, 0.095), (0, 0.07, 0.14), "M_Fur")
    body.ball((0.09, 0.12, 0.1), (0, -0.14, 0.165), "M_Fur")
    body.ball((0.085, 0.1, 0.085), (0, -0.2, 0.18), "M_Fur")
    body.taper((0.05, 0.045), (0.034, 0.03), 0.075, (0, -0.225, 0.165), "M_Rock_Dark", tilt=("X", math.radians(95)))
    body.ball((0.02, 0.018, 0.016), (0, -0.3, 0.166), "M_Chitin")
    for x in (-0.03, 0.03):
        body.tube(0.016, 0.045, (x, -0.185, 0.21), "M_Rock_Dark", sides=6, top=0.001)
        body.ball((0.013, 0.01, 0.013), (x * 0.75, -0.238, 0.196), "M_Eye")
    body.taper((0.03, 0.03), (0.012, 0.012), 0.15, (0, 0.15, 0.16), "M_Fur", tilt=("X", math.radians(-125)))

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
    body.ball((0.082, 0.09, 0.085), (0, 0, 0.33), "M_Bone")
    body.ball((0.058, 0.06, 0.04), (0, -0.012, 0.292), "M_Bone")
    for x in (-0.02, 0.02):
        body.ball((0.022, 0.014, 0.024), (x, -0.038, 0.335), "M_Chitin")
    rod(fig.part("ArmR"), 0.011, 0.2, (0.085, -0.04, 0.15), "M_Rock_Rust", lean=-25)

    return fig.finish()


def zombie():
    fig = Figure("zombie")
    # A dead man in what he was buried in. The arms reach out ahead of it, and claw at what they reach.
    man(fig, "M_Moss", legs="M_Dirt", skin="M_Rot", reach=82)
    body = fig.body
    # A wound in the side, a slack jaw and sunken eyes.
    body.ball((0.06, 0.03, 0.05), (0.035, -0.034, 0.21), "M_Leech")
    body.ball((0.04, 0.03, 0.022), (0.004, -0.03, 0.305), "M_Chitin")
    for x in (-0.017, 0.019):
        body.ball((0.016, 0.01, 0.016), (x, -0.04, 0.354), "M_Chitin")
    body.ball((0.07, 0.06, 0.03), (-0.01, 0.016, 0.385), "M_Dirt")

    return fig.finish()


def ogre():
    fig = Figure("ogre")
    man(fig, "M_Ogre", legs="M_Ogre", skin="M_Ogre", bulk=1.55)
    body = fig.body
    # A belly over a hide kilt, a heavy brow, and tusks from the lower jaw.
    body.ball((0.2, 0.17, 0.15), (0, -0.02, 0.2), "M_Ogre")
    body.taper((0.2, 0.15), (0.19, 0.14), 0.07, (0, 0, 0.1), "M_Wood")
    body.ball((0.09, 0.03, 0.022), (0, -0.036, 0.366), "M_Ogre")
    for x in (-0.02, 0.02):
        body.ball((0.012, 0.008, 0.01), (x, -0.043, 0.352), "M_Eye")
        body.tube(0.008, 0.03, (x * 1.2, -0.036, 0.312), "M_Bone", sides=6, top=0.001)
    # A club that is half a tree.
    arm = fig.part("ArmR")
    rod(arm, 0.014, 0.2, (0.14, -0.05, 0.12), "M_Timber", lean=-30)
    arm.tube(0.03, 0.16, (0.14, -0.15, 0.293), "M_Wood", sides=8, top=0.045, tilt=("X", math.radians(-30)))
    for turn in range(5):
        angle = turn * math.pi * 0.4
        arm.tube(0.008, 0.02, (0.14 + math.cos(angle) * 0.04, -0.21 + math.sin(angle) * 0.02, 0.4), "M_Iron", sides=4, top=0.0)
    fig.resize(1.75)

    return fig.finish()


def witch():
    fig = Figure("witch")
    body = fig.body
    # A long robe that spreads to the ground, drawn in at the waist.
    body.taper((0.2, 0.19), (0.1, 0.09), 0.17, (0, 0, 0), "M_Roof", sides=14)
    body.taper((0.1, 0.09), (0.12, 0.085), 0.09, (0, 0, 0.17), "M_Roof", sides=14)
    body.ball((0.13, 0.09, 0.05), (0, 0, 0.255), "M_Roof")
    body.taper((0.03, 0.03), (0.028, 0.028), 0.03, (0, 0, 0.265), "M_Rot")
    # A long face: a hooked nose, a chin, and the poison in her eyes.
    body.ball((0.074, 0.08, 0.085), (0, 0, 0.325), "M_Rot")
    body.ball((0.04, 0.045, 0.04), (0, -0.02, 0.292), "M_Rot")
    body.tube(0.009, 0.035, (0, -0.036, 0.322), "M_Rot", sides=6, top=0.001, tilt=("X", math.radians(110)))
    for x in (-0.016, 0.016):
        body.ball((0.012, 0.008, 0.01), (x, -0.036, 0.336), "M_Toxic")
    body.ball((0.09, 0.07, 0.09), (0, 0.02, 0.315), "M_Pale")
    # The hat: a wide brim and a crooked point.
    body.tube(0.1, 0.01, (0, 0, 0.356), "M_Night", sides=10)
    body.tube(0.046, 0.09, (0, 0, 0.364), "M_Night", sides=10, top=0.026)
    body.tube(0.026, 0.09, (0, 0.004, 0.45), "M_Night", sides=10, top=0.002, tilt=("X", math.radians(-28)))
    body.tube(0.048, 0.012, (0, 0, 0.366), "M_Roof", sides=10)
    for name, x in (("ArmL", -0.07), ("ArmR", 0.07)):
        shoulder = Vector((x, -0.005, 0.25))
        arm = fig.part(name, shoulder)
        arm.taper((0.04, 0.04), (0.05, 0.05), 0.11, shoulder, "M_Roof", tilt=("X", math.radians(150)))
        arm.ball((0.026, 0.03, 0.03), shoulder + Vector((0, -0.062, -0.107)), "M_Rot")
    # A staff with the poison alight on its head: she casts with it.
    arm = fig.part("ArmR")
    rod(arm, 0.007, 0.42, (0.09, -0.07, 0), "M_Wood")
    arm.blob((0.06, 0.06, 0.06), (0.09, -0.07, 0.45), "M_Toxic", seed=2)
    for turn in range(3):
        angle = turn * math.pi * 2 / 3
        arm.tube(0.006, 0.05, (0.09 + math.cos(angle) * 0.02, -0.07 + math.sin(angle) * 0.02, 0.4), "M_Wood", sides=5, top=0.002)

    return fig.finish()


def vampire():
    fig = Figure("vampire")
    man(fig, "M_Night", legs="M_Night", skin="M_Pale")
    body = fig.body
    # A red waistcoat, slicked black hair and eyes that burn.
    body.ball((0.07, 0.03, 0.1), (0, -0.034, 0.215), "M_Cloth")
    body.ball((0.09, 0.088, 0.066), (0, 0.014, 0.368), "M_Night")
    body.tube(0.012, 0.03, (0, -0.02, 0.385), "M_Night", sides=6, top=0.001, tilt=("X", math.radians(120)))
    for x in (-0.017, 0.017):
        body.ball((0.012, 0.008, 0.01), (x, -0.041, 0.354), "M_Eye")
    # The cloak, black outside and red within, with its collar up.
    body.taper((0.24, 0.02), (0.15, 0.02), 0.27, (0, 0.058, 0.02), "M_Night", sides=10)
    body.taper((0.22, 0.012), (0.135, 0.012), 0.255, (0, 0.047, 0.03), "M_Cloth", sides=10)
    for x in (-0.06, 0.06):
        body.taper((0.03, 0.06), (0.022, 0.05), 0.08, (x, 0.03, 0.285), "M_Night", sides=8)
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
    body.tube(0.2, 0.34, (0, 0, 0), "M_Night", sides=12, top=0.12)
    body.ball((0.28, 0.17, 0.1), (0, 0, 0.44), "M_Basalt")
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
    body.ball((0.1, 0.11, 0.105), (0, -0.01, 0.51), "M_Bone")
    body.ball((0.07, 0.07, 0.05), (0, -0.025, 0.462), "M_Bone")
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
