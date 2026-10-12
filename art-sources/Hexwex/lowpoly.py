"""What the Hexwex model scripts share: the palette, the mesh builder, the preview and the export.

The style grew out of art-sources/Ostrov/Buildings.blend: solid shapes in a small
palette, one material per colour. A model is built from plain blocks, tubes and
balls; `Model.finish` then rounds every hard edge a little and shades the mesh
smooth, so a wall catches the light on its corners as a built thing does. Every model stands on z = 0 with its
middle on the origin and its front facing -Y. One hex is 2 units corner to corner.
"""

import json
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
MODELS_DIR = os.path.join(ROOT, "projects", "Hexwex", "Assets", "Art", "Models")

# Name: (colour, roughness, emission strength). The first nine are the palette
# of the Ostrov buildings; a file that brings them keeps its own.
PALETTE = {
    "M_Cloth": ((0.768, 0.130, 0.195), 0.85, 0.0),
    "M_Dirt": ((0.254, 0.144, 0.063), 0.95, 0.0),
    "M_Grass": ((0.191, 0.539, 0.070), 0.90, 0.0),
    "M_Metal": ((0.745, 0.381, 0.027), 0.35, 0.0),
    "M_Rock_Dark": ((0.156, 0.147, 0.195), 0.88, 0.0),
    "M_Roof": ((0.198, 0.068, 0.262), 0.75, 0.0),
    "M_Stone": ((0.397, 0.381, 0.462), 0.85, 0.0),
    "M_Wall": ((0.807, 0.672, 0.423), 0.80, 0.0),
    "M_Wood": ((0.258, 0.112, 0.045), 0.80, 0.0),
    # Fresh-cut timber: the ends of logs and planks.
    "M_Timber": ((0.640, 0.420, 0.200), 0.85, 0.0),
    # Dark iron: saw blades, telescope, pipes.
    "M_Iron": ((0.090, 0.095, 0.120), 0.45, 0.0),
    "M_Glass": ((0.250, 0.480, 0.760), 0.20, 0.0),
    # The clean light of the converter.
    "M_Glow": ((0.150, 0.900, 0.650), 0.30, 1.1),
    # Scenery: leaves of each kind of forest, and the ground things of each biome.
    "M_Leaf_Pine": ((0.045, 0.200, 0.110), 0.90, 0.0),
    "M_Leaf": ((0.090, 0.330, 0.085), 0.90, 0.0),
    "M_Leaf_Light": ((0.300, 0.560, 0.120), 0.90, 0.0),
    "M_Leaf_Jungle": ((0.030, 0.280, 0.150), 0.85, 0.0),
    "M_Leaf_Dry": ((0.520, 0.480, 0.110), 0.90, 0.0),
    "M_Straw": ((0.720, 0.580, 0.180), 0.90, 0.0),
    "M_Moss": ((0.300, 0.380, 0.260), 0.95, 0.0),
    "M_Mud": ((0.085, 0.100, 0.050), 0.60, 0.0),
    "M_Reed": ((0.380, 0.420, 0.150), 0.90, 0.0),
    "M_Rock": ((0.330, 0.330, 0.370), 0.90, 0.0),
    "M_Rock_Light": ((0.520, 0.530, 0.570), 0.90, 0.0),
    "M_Rock_Red": ((0.420, 0.150, 0.075), 0.92, 0.0),
    "M_Rock_Rust": ((0.560, 0.260, 0.120), 0.92, 0.0),
    "M_Basalt": ((0.110, 0.060, 0.060), 0.85, 0.0),
    "M_Snow": ((0.900, 0.920, 0.950), 0.60, 0.0),
    "M_Ice": ((0.560, 0.780, 0.900), 0.25, 0.0),
    "M_Sand": ((0.780, 0.640, 0.330), 0.95, 0.0),
    "M_Lava": ((1.000, 0.220, 0.030), 0.40, 1.2),
    "M_Flower": ((0.900, 0.750, 0.150), 0.80, 0.0),
    # Lit windows and lanterns.
    "M_Lamp": ((1.000, 0.620, 0.180), 0.40, 1.6),
    # Units: the player's colour, flesh and gear, and what the monsters are made of.
    "M_Team": ((0.040, 0.150, 0.800), 0.70, 0.0),
    "M_Skin": ((0.780, 0.520, 0.380), 0.80, 0.0),
    "M_Steel": ((0.520, 0.540, 0.580), 0.35, 0.0),
    "M_Horse": ((0.300, 0.150, 0.060), 0.85, 0.0),
    "M_Bone": ((0.780, 0.760, 0.640), 0.80, 0.0),
    "M_Rot": ((0.300, 0.400, 0.250), 0.90, 0.0),
    "M_Fur": ((0.300, 0.290, 0.300), 0.95, 0.0),
    "M_Chitin": ((0.045, 0.040, 0.060), 0.50, 0.0),
    "M_Leech": ((0.280, 0.040, 0.070), 0.45, 0.0),
    "M_Ogre": ((0.360, 0.330, 0.140), 0.90, 0.0),
    "M_Pale": ((0.800, 0.800, 0.850), 0.70, 0.0),
    "M_Night": ((0.050, 0.040, 0.090), 0.80, 0.0),
    "M_Dust": ((0.560, 0.480, 0.360), 0.95, 0.0),
    "M_Feather": ((0.330, 0.190, 0.080), 0.90, 0.0),
    "M_Eye": ((1.000, 0.150, 0.050), 0.40, 2.0),
    # The poison the boss and the witch carry.
    "M_Toxic": ((0.450, 0.900, 0.100), 0.30, 1.3),
}


# How much of a metal each of these is. Everything else is not a metal at all.
METALLIC = {"M_Metal": 0.85, "M_Steel": 0.9, "M_Iron": 0.8}

# An edge sharper than this is rounded off, and stays a visible corner in the shading.
HARD_EDGE = math.radians(28)


def material(name):
    found = bpy.data.materials.get(name)
    if found:
        bsdf = found.node_tree.nodes.get("Principled BSDF") if found.use_nodes else None
        if bsdf is not None and name in METALLIC:
            bsdf.inputs["Metallic"].default_value = METALLIC[name]

        return found

    color, roughness, emission = PALETTE[name]
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    made.diffuse_color = (*color, 1.0)
    bsdf = made.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = METALLIC.get(name, 0.0)
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission

    return made


class Model:
    """One model, gathered into a single mesh with a slot per material."""

    def __init__(self, name, soft=0.008):
        self.name = name
        self.bm = bmesh.new()
        self.slots = []
        # How wide the rounding of a hard edge is. A figure a fifth the size of a house takes a finer one.
        self.soft = soft

    def slot(self, material_name):
        if material_name not in self.slots:
            self.slots.append(material_name)

        return self.slots.index(material_name)

    def _paint(self, verts, material_name):
        index = self.slot(material_name)
        seen = set()
        for vert in verts:
            for face in vert.link_faces:
                if face not in seen:
                    seen.add(face)
                    face.material_index = index

    def box(self, size, at, mat, turn=0.0):
        """A box `size` wide, deep and tall, standing on `at`."""
        matrix = Matrix.Translation(Vector(at) + Vector((0, 0, size[2] / 2))) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Diagonal((*size, 1.0))
        made = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)
        self._paint(made["verts"], mat)

    def tube(self, radius, height, at, mat, sides=10, top=None, tilt=None, turn=0.0):
        """A cylinder or a cone standing on `at`. `tilt` is (axis, angle) about its foot."""
        # Four sides is a square post and stays one. Anything rounder is made properly round.
        if sides > 4:
            sides = min(24, sides * 2)

        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z")
        if tilt:
            matrix = matrix @ Matrix.Rotation(tilt[1], 4, tilt[0])

        matrix = matrix @ Matrix.Translation((0, 0, height / 2))
        made = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=sides,
            radius1=radius, radius2=radius if top is None else top, depth=height, matrix=matrix)
        self._paint(made["verts"], mat)

    def dome(self, radius, at, mat, squash=1.0):
        """A ball with its middle on `at`. The lower half is meant to sit inside a wall."""
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Diagonal((1.0, 1.0, squash, 1.0))
        made = bmesh.ops.create_uvsphere(self.bm, u_segments=20, v_segments=12, radius=radius, matrix=matrix)
        self._paint(made["verts"], mat)

    def ball(self, size, at, mat, turn=0.0, tilt=None):
        """An egg `size` wide, deep and tall with its middle on `at`: a head, a belly, a shoulder."""
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z")
        if tilt:
            matrix = matrix @ Matrix.Rotation(tilt[1], 4, tilt[0])

        matrix = matrix @ Matrix.Diagonal((size[0] / 2, size[1] / 2, size[2] / 2, 1.0))
        made = bmesh.ops.create_uvsphere(self.bm, u_segments=14, v_segments=9, radius=1.0, matrix=matrix)
        self._paint(made["verts"], mat)

    def taper(self, foot, head, height, at, mat, sides=12, tilt=None, turn=0.0):
        """
        A limb standing on `at`: oval in section, `foot` (width, depth) at the
        bottom and `head` at the top. `tilt` is (axis, angle) about its foot.
        """
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z")
        if tilt:
            matrix = matrix @ Matrix.Rotation(tilt[1], 4, tilt[0])

        made = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=sides,
            radius1=0.5, radius2=0.5, depth=1.0, matrix=Matrix.Translation((0, 0, 0.5)))
        for vert in made["verts"]:
            width, depth = foot if vert.co.z < 0.5 else head
            vert.co = matrix @ Vector((vert.co.x * width, vert.co.y * depth, vert.co.z * height))

        self._paint(made["verts"], mat)

    def spire(self, radius, height, at, mat, sides=6, seed=0, rough=0.18, top=0.0, cap=None, cap_from=0.6):
        """
        A horn of rock standing on `at`: rings of `sides` corners that narrow to
        `top` and are knocked out of true, the same way for the same seed. It
        keeps its facets, as broken stone does. `cap` paints everything above
        `cap_from` of the height in another material: snow on a peak.
        """
        dice = random.Random(seed)
        levels = (0.0, 0.3, 0.58, 0.82, 1.0)
        base = Vector(at)
        rings = []
        for level in levels:
            around = radius + (top - radius) * (level ** 0.8)
            ring = []
            for side in range(sides):
                angle = side * math.tau / sides + dice.uniform(-0.25, 0.25) * rough * 4
                reach = around * (1 + dice.uniform(-1, 1) * rough) if level > 0 else around
                ring.append(self.bm.verts.new(base + Vector((
                    math.cos(angle) * reach, math.sin(angle) * reach,
                    height * level + (dice.uniform(-1, 1) * rough * height * 0.12 if 0 < level < 1 else 0)))))
            rings.append(ring)

        rock = self.slot(mat)
        white = self.slot(cap) if cap else rock
        for level in range(len(rings) - 1):
            for side in range(sides):
                made = self.bm.faces.new((rings[level][side], rings[level][(side + 1) % sides], rings[level + 1][(side + 1) % sides], rings[level + 1][side]))
                made.material_index = white if levels[level] >= cap_from else rock

        self.bm.faces.new(list(reversed(rings[0]))).material_index = rock
        self.bm.faces.new(rings[-1]).material_index = white

    def blob(self, size, at, mat, seed=0, rough=0.0, turn=0.0, floor=None):
        """
        A faceted lump `size` wide, deep and tall with its middle on `at`: a
        canopy, a boulder, a mound. `rough` moves its corners at random, the same
        way for the same seed. `floor` cuts off what would hang below that height.
        """
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Diagonal((size[0] / 2, size[1] / 2, size[2] / 2, 1.0))
        made = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=1.0, matrix=matrix)
        dice = random.Random(seed)
        for vert in made["verts"]:
            if rough > 0:
                vert.co += Vector((dice.uniform(-1, 1) * size[0], dice.uniform(-1, 1) * size[1], dice.uniform(-1, 1) * size[2])) * rough * 0.5
            if floor is not None and vert.co.z < floor:
                vert.co.z = floor

        self._paint(made["verts"], mat)

    def gable(self, width, depth, height, at, mat, along="X", tiles=True):
        """
        A gabled roof standing on `at`, with its ridge along X or along Y. A roof
        big enough to show them is laid with courses of tiles, each lapping the
        one below, under a ridge beam.
        """
        w, d = width / 2, depth / 2
        if along == "X":
            points = [(-w, -d, 0), (w, -d, 0), (w, d, 0), (-w, d, 0), (-w, 0, height), (w, 0, height)]
        else:
            points = [(-w, -d, 0), (-w, d, 0), (w, d, 0), (w, -d, 0), (0, -d, height), (0, d, height)]

        verts = [self.bm.verts.new(Vector(point) + Vector(at)) for point in points]
        faces = [(0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4), (3, 2, 1, 0)]
        index = self.slot(mat)
        for face in faces:
            made = self.bm.faces.new([verts[i] for i in face])
            made.material_index = index

        # The slope runs across the ridge: half the span out, the height up.
        long_way, span = (width, depth) if along == "X" else (depth, width)
        slope = math.hypot(span / 2, height)
        if not tiles or slope < 0.18 or long_way < 0.18:
            return

        pitch = math.atan2(height, span / 2)
        courses = max(3, int(round(slope / 0.075)))
        course = slope / courses
        turn = Matrix.Rotation(0.0 if along == "X" else math.pi / 2, 4, "Z")
        for side in (-1, 1):
            for row in range(courses):
                # A course lies on the slope and sticks out a little over the one below it.
                along_slope = (row + 0.5) * course - 0.012
                out = side * (span / 2 - along_slope * math.cos(pitch))
                up = along_slope * math.sin(pitch) + 0.012
                tile = (
                    Matrix.Translation(Vector(at)) @ turn @ Matrix.Translation((0, out, up))
                    @ Matrix.Rotation(-side * pitch, 4, "X") @ Matrix.Diagonal((long_way + 0.03, course + 0.02, 0.016, 1.0)))
                made = bmesh.ops.create_cube(self.bm, size=1.0, matrix=tile)
                self._paint(made["verts"], mat)

        ridge = Matrix.Translation(Vector(at)) @ turn @ Matrix.Translation((0, 0, height + 0.012)) @ Matrix.Diagonal((long_way + 0.05, 0.035, 0.03, 1.0))
        made = bmesh.ops.create_cube(self.bm, size=1.0, matrix=ridge)
        self._paint(made["verts"], mat)

    def take(self, source, matrix=None, keep=None):
        """
        Adds a copy of another object's mesh, with its materials. `keep` picks
        among its loose pieces: it is given the material names of one piece, most
        used first, and says whether the piece comes along. A piece is taken
        whole, so what comes along stays a closed shell.
        """
        before_verts = set(self.bm.verts)
        before_faces = set(self.bm.faces)
        self.bm.from_mesh(source.data)
        names = [mat.name for mat in source.data.materials]
        fresh = [face for face in self.bm.faces if face not in before_faces]

        if keep is not None:
            unseen = set(fresh)
            dropped = []
            while unseen:
                piece = [unseen.pop()]
                for face in piece:
                    for edge in face.edges:
                        for other in edge.link_faces:
                            if other in unseen:
                                unseen.discard(other)
                                piece.append(other)

                counts = {}
                for face in piece:
                    counts[names[face.material_index]] = counts.get(names[face.material_index], 0) + 1

                if not keep(sorted(counts, key=counts.get, reverse=True)):
                    dropped.extend(piece)

            gone = set(dropped)
            fresh = [face for face in fresh if face not in gone]
            if dropped:
                bmesh.ops.delete(self.bm, geom=dropped, context="FACES")

        remap = [self.slot(name) for name in names]
        for face in fresh:
            face.material_index = remap[face.material_index]

        if matrix is not None:
            bmesh.ops.transform(self.bm, matrix=matrix, verts=[vert for vert in self.bm.verts if vert not in before_verts])

    def resize(self, factor):
        """Scales everything built so far about the origin: an ogre is a man drawn large."""
        bmesh.ops.scale(self.bm, vec=Vector((factor, factor, factor)), verts=self.bm.verts[:])

    def soften(self):
        """
        Rounds every hard edge by `soft` and marks which edges stay corners. A
        block built by a script has edges like a razor, which nothing built has:
        the rounding is what catches the light. Shells stay closed.
        """
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        if self.soft > 0:
            hard = [edge for edge in self.bm.edges if len(edge.link_faces) == 2 and edge.calc_face_angle(0.0) > HARD_EDGE]
            if hard:
                bmesh.ops.bevel(
                    self.bm, geom=hard, offset=self.soft, offset_type="OFFSET", segments=2, profile=0.5,
                    affect="EDGES", clamp_overlap=True)
                bmesh.ops.dissolve_degenerate(self.bm, edges=self.bm.edges[:], dist=1e-6)
                bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])

        for face in self.bm.faces:
            face.smooth = True

        for edge in self.bm.edges:
            edge.smooth = len(edge.link_faces) != 2 or edge.calc_face_angle(0.0) < math.radians(50)

    def finish(self):
        self.soften()
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for name in self.slots:
            mesh.materials.append(material(name))

        made = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(made)
        # A wide flat face keeps its own direction; only the rounding between two faces bends.
        weighted = made.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
        weighted.keep_sharp = True
        weighted.weight = 100

        return made


# The order a building goes up in. A part of a model is one of these stages, so
# the game can raise it piece by piece (`BuildAnimation` in Unity).
STAGES = ("S0", "S1", "S2", "S3")
# Where a loose piece of a model taken from another file belongs, by what it is mostly made of.
STAGE_BY_MATERIAL = {
    "M_Stone": 0, "M_Dirt": 0, "M_Grass": 0, "M_Rock_Dark": 0,
    "M_Wall": 1, "M_Wood": 1,
    "M_Roof": 2,
}


class Staged:
    """
    A building in the four stages it is built in: `base` the ground work,
    `walls`, `roof`, and `extra` the things that finish it. Each stage is its own
    mesh under one empty, named `<name>__S0` .. `<name>__S3`.

    What moves on a building is a part of its own, with its origin on the joint
    it turns about. It hangs on the root beside the stages, never deeper: the FBX
    export turns and misplaces a part two levels down. The game raises it with
    the last stage and takes it down with it. The game moves it by its name (`BuildingLife` in
    Unity): `<name>__<kind>_<tag>`, with a number at the end where the kind
    takes one.

        SpinX, SpinY, SpinZ   turns about that axis; the number is degrees a second
        Flag                  a strip of a flag; the number is its place from the pole
        Pound                 rises and drops, as a stamp on ore
        Bob                   floats up and down
        Sway                  turns to and fro about the upright, as a vane in the wind
        Swing                 hangs and swings, as a load on a rope

    A marker is a point with no mesh, named the same way: `Smoke` where smoke
    rises, `Dust` where a stamp comes down.
    """

    def __init__(self, root_name, name):
        self.root_name = root_name
        self.name = name
        self.stages = [Model(name + "__" + stage) for stage in STAGES]
        self.base, self.walls, self.roof, self.extra = self.stages
        self.movers = []
        self.markers = []

    def mover(self, kind, tag, pivot, number=None):
        """A part that moves about `pivot`."""
        name = "%s__%s_%s" % (self.name, kind, tag) + ("" if number is None else "_%d" % number)
        model = Model(name)
        self.movers.append((model, Vector(pivot)))

        return model

    def marker(self, kind, tag, at):
        self.markers.append(("%s__%s_%s" % (self.name, kind, tag), Vector(at)))

    def take(self, source, matrix=None):
        """Adds another object's mesh, each loose piece of it to the stage its main material belongs to."""
        for index, model in enumerate(self.stages):
            model.take(source, matrix, keep=lambda names, index=index: STAGE_BY_MATERIAL.get(names[0], 3) == index)

    def finish(self):
        root = bpy.data.objects.new(self.root_name, None)
        bpy.context.scene.collection.objects.link(root)
        for model in self.stages:
            if len(model.bm.faces) == 0:
                model.bm.free()
                continue

            model.finish().parent = root

        for model, pivot in self.movers:
            bmesh.ops.translate(model.bm, vec=-pivot, verts=model.bm.verts[:])
            made = model.finish()
            made.parent = root
            made.location = pivot

        for name, at in self.markers:
            made = bpy.data.objects.new(name, None)
            bpy.context.scene.collection.objects.link(made)
            made.parent = root
            made.location = at

        return root


def placed(x, y, scale, turn=0.0):
    return Matrix.Translation((x, y, 0)) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Diagonal((scale, scale, scale, 1.0))


def family(made):
    """An object and everything parented under it."""
    found = [made]
    for child in made.children:
        found.extend(family(child))

    return found


def render_preview(models, path, columns=3, spacing=1.9):
    """All the models in rows, lit plainly, to check them by eye."""
    rows = (len(models) + columns - 1) // columns
    for index, made in enumerate(models):
        made.location = ((index % columns) * spacing, -(index // columns) * spacing, 0)

    middle = Vector(((columns - 1) * spacing / 2, -(rows - 1) * spacing / 2, 0.3))
    camera_data = bpy.data.cameras.new("Preview")
    camera = bpy.data.objects.new("Preview", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = middle + Vector((5.2, -8.6, 7.1))
    camera.rotation_euler = (middle - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(columns, rows) * spacing * 1.3
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    # Back faces are not drawn in Unity. Hiding them here shows a face wound the wrong way as a hole.
    scene.display.shading.show_backface_culling = True
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.45, 0.62, 0.8)
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1100
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(camera)
    for made in models:
        made.location = (0, 0, 0)


def audit():
    """
    Counts what Unity would show wrong: faces wound inward and edges with one
    face. A mesh must be closed for "outward" to mean anything, so both must be
    0. Returns the names of the objects that fail.
    """
    failed = []
    for made in bpy.data.objects:
        if made.type != "MESH":
            continue

        bm = bmesh.new()
        bm.from_mesh(made.data)
        bm.normal_update()
        before = [face.normal.copy() for face in bm.faces]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.normal_update()
        flipped = sum(1 for face, normal in zip(bm.faces, before) if face.normal.dot(normal) < 0.0)
        boundary = sum(1 for edge in bm.edges if len(edge.link_faces) == 1)
        bm.free()
        mirrored = made.scale.x * made.scale.y * made.scale.z < 0
        if flipped or boundary or mirrored:
            failed.append("%s flipped=%d boundary=%d mirrored=%s" % (made.name, flipped, boundary, mirrored))

    return failed


def export_palette():
    """
    Writes what the materials are made of beside the models. An FBX carries a
    material's name and little else, so Unity builds its materials from this
    file. The colours are linear, as Blender keeps them.
    """
    path = os.path.join(MODELS_DIR, "Materials.json")
    known = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as handle:
            known = {entry["name"]: entry for entry in json.load(handle)["materials"]}

    for made in bpy.data.materials:
        if made.users == 0 or not made.use_nodes:
            continue

        bsdf = made.node_tree.nodes.get("Principled BSDF")
        if bsdf is None:
            continue

        color = bsdf.inputs["Base Color"].default_value
        glow = bsdf.inputs["Emission Color"].default_value
        known[made.name] = {
            "name": made.name,
            "r": round(color[0], 4), "g": round(color[1], 4), "b": round(color[2], 4),
            "roughness": round(bsdf.inputs["Roughness"].default_value, 3),
            "metallic": round(bsdf.inputs["Metallic"].default_value, 3),
            "emissionR": round(glow[0], 4), "emissionG": round(glow[1], 4), "emissionB": round(glow[2], 4),
            "emissionStrength": round(bsdf.inputs["Emission Strength"].default_value, 3),
        }

    with open(path, "w", encoding="utf-8") as handle:
        json.dump({"materials": [known[name] for name in sorted(known)]}, handle, indent=2)
        handle.write("\n")


def export_fbx(models, file_name):
    """
    Writes the models, and everything parented under them, for Unity: y up, one
    unit to a metre, with the normals the models are shaded by. Nothing is written if a mesh fails the audit.
    """
    failed = audit()
    if failed:
        raise RuntimeError("These meshes would show wrong in Unity:\n  " + "\n  ".join(failed))

    os.makedirs(MODELS_DIR, exist_ok=True)
    chosen = set()
    for made in models:
        chosen.update(family(made))

    for made in bpy.data.objects:
        made.select_set(made in chosen)

    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODELS_DIR, file_name), use_selection=True, object_types={"EMPTY", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True, mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    export_palette()


def report(models):
    for made in models:
        meshes = [part for part in family(made) if part.type == "MESH"]
        low = [min((part.matrix_world @ Vector(corner))[axis] for part in meshes for corner in part.bound_box) for axis in range(3)]
        high = [max((part.matrix_world @ Vector(corner))[axis] for part in meshes for corner in part.bound_box) for axis in range(3)]
        faces = sum(len(part.data.polygons) for part in meshes)
        print("MODEL %s %.2f x %.2f x %.2f, %d faces in %d parts" % (made.name, high[0] - low[0], high[1] - low[1], high[2] - low[2], faces, len(meshes)))
