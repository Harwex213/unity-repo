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

    LegL, LegR          a walker's thighs, hinged at the hip
    ShinL, ShinR        the shins and boots, hinged at the knee
    ArmL, ArmR          the upper arms, hinged at the shoulder
    ForeL, ForeR        the forearms and hands, hinged at the elbow; the right holds the weapon
    Leg1 .. Leg4        the thighs of a beast or a horse: front left, front right, back left, back right
    Shank1 .. Shank4    the lower legs of a beast, hinged at the knee
    WingL, WingR        wings, hinged where they meet the body
    Seg1 .. Seg4        the rings of a thing that crawls, front to back

Every part hangs on the body, never on another part: the FBX export misplaces
a part two levels down. The game hangs a shin on its thigh when it loads the
figure.
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

    def hand(self, side):
        """The part that carries what the figure holds: the forearm where there is one, else the arm."""
        return self.parts["Fore" + side] if "Fore" + side in self.parts else self.part("Arm" + side)

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
            # The thigh hangs from the hip; the shin and the boot hang from the knee.
            leg = fig.part(name, (x, 0, lift + 0.15))
            leg.taper(wide(0.044, 0.048), wide(0.052, 0.058), 0.07, (x, 0, lift + 0.082), legs)
            leg.ball((0.046 * bulk, 0.05 * bulk, 0.04), (x, 0, lift + 0.085), legs)
            shin = fig.part("Shin" + name[-1], (x, 0, lift + 0.085))
            shin.taper(wide(0.036, 0.04), wide(0.044, 0.048), 0.07, (x, 0, lift + 0.02), legs)
            # The boot: a shaft and a toe that points forward.
            shin.taper(wide(0.044, 0.05), wide(0.04, 0.044), 0.035, (x, 0, lift), "M_Wood")
            shin.ball((0.044 * bulk, 0.07 * bulk, 0.03), (x, -0.02 * bulk, lift + 0.014), "M_Wood")
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
        # The upper arm hangs from the shoulder; the forearm and the hand hang from the elbow.
        elbow = shoulder + out * 0.064
        arm = fig.part(name, shoulder)
        arm.ball((0.052 * bulk, 0.056 * bulk, 0.05), shoulder, arms or torso)
        arm.taper(wide(0.042, 0.046), wide(0.036, 0.04), 0.066, shoulder, arms or torso, tilt=("X", swing))
        fore = fig.part("Fore" + name[-1], elbow)
        fore.ball((0.037 * bulk, 0.041 * bulk, 0.037), elbow, arms or torso)
        fore.taper(wide(0.036, 0.04), wide(0.03, 0.032), 0.056, elbow, arms or torso, tilt=("X", swing))
        fore.ball((0.03 * bulk, 0.034 * bulk, 0.038), shoulder + out * 0.13, skin)
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
    arm = fig.hand("L")
    if round_one:
        arm.tube(0.07, 0.015, (LEFT - 0.01, -0.06, lift + 0.2), mat, sides=8, tilt=("X", math.radians(90)))
        arm.tube(0.02, 0.02, (LEFT - 0.01, -0.063, lift + 0.2), "M_Metal", sides=6, tilt=("X", math.radians(90)))
    else:
        arm.box((0.1, 0.015, 0.13), (LEFT - 0.01, -0.06, lift + 0.14), mat)
        arm.box((0.02, 0.018, 0.13), (LEFT - 0.01, -0.062, lift + 0.14), "M_Metal")


def spear(fig, length, lift=0.0):
    arm = fig.hand("R")
    rod(arm, 0.008, length, (RIGHT, -0.04, lift + 0.02), "M_Timber")
    arm.tube(0.018, 0.06, (RIGHT, -0.04, lift + 0.02 + length), "M_Steel", sides=4, top=0.0)


def sword(fig, length, lift=0.0):
    arm = fig.hand("R")
    rod(arm, 0.012, length, (RIGHT, -0.05, lift + 0.17), "M_Steel", lean=-20)
    arm.box((0.05, 0.015, 0.015), (RIGHT, -0.05, lift + 0.165), "M_Metal")


def bow(fig, tall, lift=0.0):
    """A bow held out in the left hand, its string toward the archer."""
    arm = fig.hand("L")
    middle = lift + 0.2
    rod(arm, 0.007, tall * 0.5, (LEFT, -0.09, middle), "M_Timber", lean=14)
    rod(arm, 0.007, tall * 0.5, (LEFT, -0.09, middle), "M_Timber", lean=166)
    rod(arm, 0.003, tall * 0.94, (LEFT, -0.055, middle - tall * 0.47), "M_Pale")


def quiver(fig, lift=0.0):
    fig.body.box((0.04, 0.03, 0.12), (0.03, 0.06, lift + 0.17), "M_Wood")
    for x in (0.02, 0.035, 0.045):
        fig.body.box((0.008, 0.008, 0.04), (x, 0.06, lift + 0.29), "M_Pale")


def sling(fig, lift=0.0):
    arm = fig.hand("R")
    rod(arm, 0.004, 0.14, (RIGHT, -0.02, lift + 0.16), "M_Wood", lean=-70)
    arm.blob((0.035, 0.035, 0.035), (RIGHT, -0.15, lift + 0.21), "M_Rock", seed=2)


def musket(fig, lift=0.0):
    arm = fig.hand("R")
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
        shank = fig.part("Shank%d" % (index + 1), (at_x, at_y, tall * 0.5))
        shank.ball((size[0] * 0.86, size[1] * 0.86, size[0] * 0.86), (at_x, at_y, tall * 0.5), mat)
        shank.taper((size[0] * 0.62, size[1] * 0.62), (size[0] * 0.8, size[1] * 0.8), tall * 0.44, (at_x, at_y, tall * 0.07), mat)
        shank.taper((size[0] * 0.85, size[1] * 0.95), (size[0] * 0.68, size[1] * 0.7), tall * 0.08, (at_x, at_y - size[1] * 0.1, 0), "M_Rock_Dark")

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
    arm = fig.hand("R")
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
    arm = fig.hand("R")
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
    """
    Two wings hinged where they meet the body. `inner` and `outer` are (length,
    chord) of the arm of the wing and of its hand. Each is a blade thick at its
    leading edge, with a row of flight feathers trailing from it.
    """
    for name, side in (("WingL", -1), ("WingR", 1)):
        wing = fig.part(name, (side * root_x, 0, at_z))
        lay = ("Y", side * math.radians(90))
        wing.taper((0.016, inner[1]), (0.012, inner[1] * 0.82), inner[0], (side * root_x, 0, at_z), inner_mat, sides=6, tilt=lay)
        wrist = side * (root_x + inner[0])
        # The hand of the wing sweeps back from the wrist.
        wing.taper((0.012, outer[1]), (0.004, outer[1] * 0.3), outer[0], (wrist, 0.012, at_z), outer_mat, sides=6, tilt=lay, turn=-side * 0.3)
        feathers = 5
        for index in range(feathers):
            share = (index + 0.5) / feathers
            at_x = side * (root_x + inner[0] * share)
            wing.taper((0.006, inner[1] * 0.3), (0.003, inner[1] * 0.12), inner[1] * (0.5 + 0.25 * share), (at_x, inner[1] * 0.3, at_z - 0.004),
                       outer_mat if index % 2 else inner_mat, sides=4, tilt=("X", math.radians(-90)), turn=side * 0.12 * index)

def bird(key, size, feathers, head, wing_mat):
    fig = Figure(key)
    body = fig.body
    # A breast that is deep in front and draws back to the tail.
    body.ball((0.1, 0.2, 0.095), (0, 0, 0.08), feathers)
    body.ball((0.085, 0.12, 0.085), (0, -0.06, 0.085), feathers)
    body.ball((0.066, 0.075, 0.066), (0, -0.125, 0.105), head)
    body.taper((0.03, 0.03), (0.004, 0.004), 0.055, (0, -0.15, 0.1), "M_Metal", sides=6, tilt=("X", math.radians(100)))
    for x in (-0.024, 0.024):
        body.ball((0.012, 0.012, 0.012), (x, -0.145, 0.115), "M_Chitin")
        # The feet are tucked up under it as it flies.
        body.taper((0.012, 0.012), (0.006, 0.006), 0.04, (x, 0.02, 0.04), "M_Metal", sides=5, tilt=("X", math.radians(-110)))
    # The tail: a fan of three feathers.
    for index, turn in enumerate((-0.35, 0.0, 0.35)):
        body.taper((0.034, 0.008), (0.02, 0.004), 0.12, (0, 0.08, 0.075), wing_mat if index != 1 else feathers, sides=4, tilt=("X", math.radians(-92)), turn=turn)
    wings(fig, 0.04, 0.095, (0.2, 0.11), (0.13, 0.08), wing_mat, feathers)
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
    # A swollen abdomen with its red mark, a low head-piece in front, eyes in a cluster and two fangs.
    body.ball((0.17, 0.21, 0.14), (0, 0.08, 0.13), "M_Chitin")
    body.ball((0.06, 0.09, 0.03), (0, 0.09, 0.193), "M_Cloth")
    body.ball((0.11, 0.12, 0.08), (0, -0.07, 0.1), "M_Chitin")
    for x, y, z, size in ((-0.02, -0.122, 0.11, 0.016), (0.02, -0.122, 0.11, 0.016), (-0.04, -0.108, 0.115, 0.011), (0.04, -0.108, 0.115, 0.011), (-0.012, -0.118, 0.128, 0.009), (0.012, -0.118, 0.128, 0.009)):
        body.ball((size, size, size), (x, y, z), "M_Eye")
    for x in (-0.018, 0.018):
        body.taper((0.014, 0.014), (0.003, 0.003), 0.045, (x, -0.125, 0.085), "M_Leech", sides=5, tilt=("X", math.radians(160)))
    # Four pairs of legs; each pair of pairs steps as one, like a beast's. A leg rises to its knee and comes down to its foot.
    for side in (-1, 1):
        for index in range(4):
            fan = side * (0.9 - index * 0.55)
            number = (1 if index < 2 else 3) + (0 if side < 0 else 1)
            hip = Vector((side * 0.04, -0.09 + index * 0.04, 0.1))
            leg = fig.part("Leg%d" % number, (0, -0.07 if index < 2 else -0.01, 0.1))
            out = Vector((side * math.cos(fan), -math.sin(abs(fan)) * (1 if index < 2 else -1) * (1 if abs(fan) > 0.01 else 0), 0))
            out = Vector((side * math.cos(abs(fan)), -math.sin(fan * side), 0)).normalized()
            knee = hip + out * 0.1 + Vector((0, 0, 0.07))
            foot = hip + out * 0.2 + Vector((0, 0, -0.1))
            for start, end, thick in ((hip, knee, 0.018), (knee, foot, 0.012)):
                span = end - start
                lean = math.acos(max(-1, min(1, span.z / span.length)))
                leg.taper((thick, thick), (thick * 0.7, thick * 0.7), span.length, start, "M_Chitin", sides=5, turn=math.atan2(span.y, span.x), tilt=("Y", lean))
            leg.ball((0.022, 0.022, 0.022), knee, "M_Chitin")

    return fig.finish()

def leech():
    fig = Figure("leech")
    # A fat worm in five rings. The middle one is its body; the rest move on it as it crawls.
    rings = ((-0.17, 0.09), (-0.09, 0.125), (0.0, 0.14), (0.09, 0.12), (0.17, 0.085))
    for index, (y, width) in enumerate(rings):
        ring = fig.body if index == 2 else fig.part("Seg%d" % (index + 1 if index < 2 else index), (0, y, width * 0.4))
        ring.ball((width, 0.12, width * 0.8), (0, y, width * 0.4), "M_Leech")
        # A paler band where one ring meets the next.
        ring.taper((width * 0.92, width * 0.74), (width * 0.92, width * 0.74), 0.012, (0, y + 0.045, width * 0.4), "M_Rot", sides=10, tilt=("X", math.radians(90)))
    head = fig.part("Seg1")
    # The mouth: a pale ring of teeth round a dark hole, and two feelers.
    head.tube(0.036, 0.02, (0, -0.2, 0.04), "M_Pale", sides=8, tilt=("X", math.radians(80)))
    head.tube(0.022, 0.022, (0, -0.203, 0.04), "M_Chitin", sides=8, tilt=("X", math.radians(80)))
    for index in range(6):
        angle = index * math.tau / 6
        head.taper((0.008, 0.008), (0.001, 0.001), 0.018, (math.cos(angle) * 0.026, -0.215, 0.04 + math.sin(angle) * 0.026), "M_Bone", sides=4, tilt=("X", math.radians(100)))
    for x in (-0.03, 0.03):
        head.taper((0.008, 0.008), (0.002, 0.002), 0.05, (x, -0.19, 0.07), "M_Leech", sides=5, tilt=("X", math.radians(50)))

    return fig.finish()

def bone(model, start, end, thick, mat="M_Bone"):
    """A long bone from `start` to `end`, with a knob at each end."""
    start, end = Vector(start), Vector(end)
    span = end - start
    lean = math.acos(max(-1, min(1, span.z / span.length)))
    model.taper((thick, thick), (thick * 0.85, thick * 0.85), span.length, start, mat, sides=6, turn=math.atan2(span.y, span.x), tilt=("Y", lean))
    for at in (start, end):
        model.ball((thick * 1.5, thick * 1.5, thick * 1.5), at, mat)


def skeleton():
    fig = Figure("skeleton")
    body = fig.body
    for name, x in (("LegL", -0.03), ("LegR", 0.03)):
        bone(fig.part(name, (x, 0, 0.15)), (x, 0, 0.15), (x, 0, 0.085), 0.016)
        shin = fig.part("Shin" + name[-1], (x, 0, 0.085))
        bone(shin, (x, 0, 0.085), (x, 0, 0.015), 0.013)
        shin.ball((0.026, 0.06, 0.018), (x, -0.018, 0.01), "M_Bone")
    # The pelvis, the spine up from it, and the cage of the ribs.
    body.ball((0.1, 0.05, 0.045), (0, 0, 0.155), "M_Bone")
    bone(body, (0, 0.01, 0.16), (0, 0.01, 0.285), 0.016)
    for index in range(4):
        z = 0.2 + index * 0.022
        wide = 0.1 + 0.012 * math.sin((index + 0.5) / 4 * math.pi)
        body.taper((wide, 0.07), (wide, 0.07), 0.009, (0, -0.004, z), "M_Bone", sides=12)
    body.ball((0.08, 0.05, 0.085), (0, 0.0, 0.235), "M_Night")
    body.taper((0.014, 0.012), (0.014, 0.012), 0.08, (0, -0.036, 0.195), "M_Bone", sides=4)
    body.taper((0.13, 0.02), (0.13, 0.02), 0.012, (0, 0, 0.283), "M_Bone", sides=6)
    for name, x in (("ArmL", -0.07), ("ArmR", 0.07)):
        bone(fig.part(name, (x, 0, 0.287)), (x, 0, 0.287), (x, 0, 0.215), 0.012)
        fore = fig.part("Fore" + name[-1], (x, 0, 0.215))
        bone(fore, (x, 0, 0.215), (x, 0, 0.15), 0.01)
        fore.ball((0.022, 0.026, 0.03), (x, 0, 0.138), "M_Bone")
    # The skull: a round vault, a jaw that hangs, sockets and the hole of the nose.
    body.ball((0.082, 0.09, 0.085), (0, 0, 0.345), "M_Bone")
    body.ball((0.058, 0.06, 0.04), (0, -0.012, 0.3), "M_Bone")
    for x in (-0.02, 0.02):
        body.ball((0.024, 0.016, 0.026), (x, -0.038, 0.35), "M_Chitin")
    body.ball((0.012, 0.01, 0.016), (0, -0.044, 0.328), "M_Chitin")
    for x in (-0.018, -0.006, 0.006, 0.018):
        body.box((0.008, 0.006, 0.012), (x, -0.036, 0.3), "M_Pale")
    rod(fig.hand("R"), 0.011, 0.2, (0.085, -0.04, 0.15), "M_Rock_Rust", lean=-25)

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
    arm = fig.hand("R")
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
    arm = fig.hand("R")
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
    body.ball((0.07, 0.11, 0.08), (0, 0, 0.065), "M_Night")
    body.ball((0.055, 0.06, 0.055), (0, -0.06, 0.078), "M_Fur")
    body.ball((0.02, 0.02, 0.016), (0, -0.088, 0.07), "M_Leech")
    for side in (-1, 1):
        body.taper((0.03, 0.012), (0.004, 0.004), 0.05, (side * 0.02, -0.055, 0.095), "M_Night", sides=5, tilt=("Y", math.radians(side * 14)))
        body.ball((0.011, 0.009, 0.011), (side * 0.016, -0.085, 0.085), "M_Eye")
        body.taper((0.004, 0.004), (0.001, 0.001), 0.014, (side * 0.008, -0.088, 0.066), "M_Pale", sides=4, tilt=("X", math.radians(170)))
    # A wing is skin stretched over an arm and three long fingers.
    for name, side in (("WingL", -1), ("WingR", 1)):
        root = (side * 0.03, 0, 0.078)
        wing = fig.part(name, root)
        lay = ("Y", side * math.radians(90))
        wing.taper((0.012, 0.012), (0.008, 0.008), 0.12, root, "M_Fur", sides=5, tilt=lay)
        wrist = (side * 0.15, 0, 0.078)
        for index, (turn, length) in enumerate(((-0.25, 0.14), (-0.9, 0.12), (-1.5, 0.1))):
            wing.taper((0.007, 0.007), (0.002, 0.002), length, wrist, "M_Fur", sides=4, tilt=lay, turn=side * turn)
        # The skin: flat blades from the body and from the wrist, back to the trailing edge.
        wing.taper((0.005, 0.1), (0.005, 0.11), 0.12, (side * 0.03, 0.04, 0.076), "M_Rock_Dark", sides=4, tilt=lay)
        wing.taper((0.005, 0.11), (0.004, 0.03), 0.13, (side * 0.15, 0.04, 0.076), "M_Night", sides=4, tilt=lay, turn=-side * 0.35)

    return fig.finish()

def plague_lord():
    """The boss: a robed giant with a horned skull, the poison burning in his hands and at his feet, and a scythe."""
    fig = Figure("plague_lord")
    body = fig.body
    # The robe falls wide to the ground from a sash at the waist, over a hem that burns.
    body.taper((0.44, 0.42), (0.27, 0.2), 0.34, (0, 0, 0), "M_Night", sides=18)
    body.taper((0.46, 0.44), (0.44, 0.42), 0.022, (0, 0, 0), "M_Toxic", sides=18)
    for index in range(9):
        angle = index * math.tau / 9
        body.taper((0.05, 0.03), (0.02, 0.012), 0.2, (math.cos(angle) * 0.2, math.sin(angle) * 0.19, 0.01), "M_Basalt", sides=5, turn=angle, tilt=("Y", math.radians(-14)))
    body.taper((0.285, 0.215), (0.285, 0.215), 0.03, (0, 0, 0.325), "M_Cloth", sides=14)
    body.ball((0.06, 0.03, 0.05), (0, -0.108, 0.34), "M_Metal")
    # A chest that widens to great shoulders, plated in front, with the poison set in it.
    body.taper((0.27, 0.2), (0.36, 0.21), 0.14, (0, 0, 0.345), "M_Basalt", sides=14)
    body.ball((0.42, 0.21, 0.11), (0, 0, 0.485), "M_Basalt")
    body.ball((0.2, 0.07, 0.15), (0, -0.085, 0.425), "M_Iron")
    for z in (0.385, 0.425, 0.465):
        body.taper((0.2, 0.012), (0.2, 0.012), 0.008, (0, -0.118, z), "M_Bone", sides=8)
    body.ball((0.06, 0.05, 0.06), (0, -0.125, 0.43), "M_Toxic")
    # The cloak down his back and the cowl behind his head.
    body.taper((0.4, 0.04), (0.34, 0.05), 0.42, (0, 0.115, 0.06), "M_Night", sides=10)
    body.taper((0.2, 0.2), (0.15, 0.16), 0.06, (0, 0.0, 0.515), "M_Night", sides=12)
    body.ball((0.18, 0.17, 0.19), (0, 0.035, 0.6), "M_Night")
    # The skull in the cowl: jaw, teeth, burning sockets.
    body.ball((0.1, 0.11, 0.105), (0, -0.025, 0.595), "M_Bone")
    body.ball((0.072, 0.07, 0.05), (0, -0.04, 0.546), "M_Bone")
    for x in (-0.024, -0.008, 0.008, 0.024):
        body.box((0.01, 0.008, 0.016), (x, -0.07, 0.548), "M_Pale")
    body.ball((0.014, 0.012, 0.02), (0, -0.08, 0.582), "M_Chitin")
    # A crown of iron with gold points.
    body.tube(0.056, 0.02, (0, -0.02, 0.636), "M_Iron", sides=10)
    for index in range(7):
        angle = index * math.tau / 7
        body.taper((0.014, 0.014), (0.002, 0.002), 0.035, (math.cos(angle) * 0.052, -0.02 + math.sin(angle) * 0.052, 0.652), "M_Metal", sides=4)

    for name, side in (("ArmL", -1), ("ArmR", 1)):
        body.ball((0.03, 0.022, 0.03), (side * 0.026, -0.072, 0.602), "M_Toxic")
        # A horn curls out and up from the temple in three lengths.
        at = Vector((side * 0.04, -0.01, 0.625))
        for lean, length, thick in ((62, 0.07, 0.026), (30, 0.07, 0.019), (2, 0.07, 0.011)):
            body.taper((thick, thick), (thick * 0.7, thick * 0.7), length, at, "M_Bone", sides=6, tilt=("Y", math.radians(side * lean)))
            at = at + Vector((math.sin(math.radians(side * lean)), 0, math.cos(math.radians(side * lean)))) * length * 0.94
        # A plated shoulder with spikes.
        body.ball((0.16, 0.17, 0.11), (side * 0.2, 0, 0.49), "M_Iron")
        body.taper((0.17, 0.18), (0.17, 0.18), 0.012, (side * 0.2, 0, 0.47), "M_Metal", sides=12)
        for lean in (20, 55):
            body.taper((0.03, 0.03), (0.003, 0.003), 0.1, (side * 0.2, 0, 0.5), "M_Bone", sides=5, tilt=("Y", math.radians(side * lean)))
        # The arm in a sleeve that opens wide at the wrist, a hand of bone, and the poison held over it.
        shoulder = Vector((side * 0.2, -0.02, 0.46))
        swing = math.radians(132)
        out = Vector((0, -math.sin(swing), math.cos(swing)))
        elbow = shoulder + out * 0.11
        arm = fig.part(name, shoulder)
        arm.taper((0.085, 0.085), (0.075, 0.075), 0.115, shoulder, "M_Night", sides=10, tilt=("X", swing))
        fore = fig.part("Fore" + name[-1], elbow)
        fore.ball((0.078, 0.078, 0.078), elbow, "M_Night")
        fore.taper((0.07, 0.07), (0.12, 0.12), 0.11, elbow, "M_Night", sides=10, tilt=("X", swing))
        hand = shoulder + out * 0.235
        fore.ball((0.05, 0.05, 0.045), hand, "M_Bone")
        for finger in (-1, 0, 1):
            fore.taper((0.012, 0.012), (0.004, 0.004), 0.05, hand + Vector((finger * 0.018, 0, 0)), "M_Bone", sides=4, tilt=("X", math.radians(100 + abs(finger) * 14)), turn=finger * -0.3)
        fore.blob((0.08, 0.08, 0.08), hand + Vector((0, -0.03, 0.075)), "M_Toxic", seed=2 + side, rough=0.08)

    # The scythe stands beside him: a long snath and a blade that curves down to its point.
    rod(body, 0.012, 0.7, (0.3, -0.12, 0), "M_Wood")
    body.tube(0.018, 0.03, (0.3, -0.12, 0.66), "M_Iron", sides=6)
    at = Vector((0.3, -0.12, 0.675))
    for lean, length, wide in ((-72, 0.1, 0.06), (-92, 0.1, 0.05), (-114, 0.09, 0.036), (-138, 0.08, 0.02)):
        body.taper((wide, 0.012), (wide * 0.7, 0.008), length, at, "M_Steel", sides=4, tilt=("Y", math.radians(lean)))
        at = at + Vector((math.sin(math.radians(lean)), 0, math.cos(math.radians(lean)))) * length * 0.95
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
