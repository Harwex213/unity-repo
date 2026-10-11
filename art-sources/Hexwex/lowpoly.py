"""What the Hexwex model scripts share: the palette, the mesh builder, the preview and the export.

The style is that of art-sources/Ostrov/Buildings.blend: flat-shaded blocks in a
small palette, one material per colour. Every model stands on z = 0 with its
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


def material(name):
    found = bpy.data.materials.get(name)
    if found:
        return found

    color, roughness, emission = PALETTE[name]
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    made.diffuse_color = (*color, 1.0)
    bsdf = made.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission

    return made


class Model:
    """One model, gathered into a single mesh with a slot per material."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.slots = []

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
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z")
        if tilt:
            matrix = matrix @ Matrix.Rotation(tilt[1], 4, tilt[0])

        matrix = matrix @ Matrix.Translation((0, 0, height / 2))
        made = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=sides,
            radius1=radius, radius2=radius if top is None else top, depth=height, matrix=matrix)
        self._paint(made["verts"], mat)

    def dome(self, radius, at, mat, squash=1.0):
        """A low-poly ball with its middle on `at`. The lower half is meant to sit inside a wall."""
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Diagonal((1.0, 1.0, squash, 1.0))
        made = bmesh.ops.create_uvsphere(self.bm, u_segments=10, v_segments=6, radius=radius, matrix=matrix)
        self._paint(made["verts"], mat)

    def blob(self, size, at, mat, seed=0, rough=0.0, turn=0.0, floor=None):
        """
        A faceted lump `size` wide, deep and tall with its middle on `at`: a
        canopy, a boulder, a mound. `rough` moves its corners at random, the same
        way for the same seed. `floor` cuts off what would hang below that height.
        """
        matrix = Matrix.Translation(Vector(at)) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Diagonal((size[0] / 2, size[1] / 2, size[2] / 2, 1.0))
        made = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=1.0, matrix=matrix)
        dice = random.Random(seed)
        for vert in made["verts"]:
            if rough > 0:
                vert.co += Vector((dice.uniform(-1, 1) * size[0], dice.uniform(-1, 1) * size[1], dice.uniform(-1, 1) * size[2])) * rough * 0.5
            if floor is not None and vert.co.z < floor:
                vert.co.z = floor

        self._paint(made["verts"], mat)

    def gable(self, width, depth, height, at, mat, along="X"):
        """A gabled roof standing on `at`, with its ridge along X or along Y."""
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

    def finish(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for name in self.slots:
            mesh.materials.append(material(name))

        made = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(made)

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
    """

    def __init__(self, root_name, name):
        self.root_name = root_name
        self.stages = [Model(name + "__" + stage) for stage in STAGES]
        self.base, self.walls, self.roof, self.extra = self.stages

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

            made = model.finish()
            made.parent = root

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
            "emissionR": round(glow[0], 4), "emissionG": round(glow[1], 4), "emissionB": round(glow[2], 4),
            "emissionStrength": round(bsdf.inputs["Emission Strength"].default_value, 3),
        }

    with open(path, "w", encoding="utf-8") as handle:
        json.dump({"materials": [known[name] for name in sorted(known)]}, handle, indent=2)
        handle.write("\n")


def export_fbx(models, file_name):
    """
    Writes the models, and everything parented under them, for Unity: y up, one
    unit to a metre, flat-shaded. Nothing is written if a mesh fails the audit.
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
        bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    export_palette()


def report(models):
    for made in models:
        meshes = [part for part in family(made) if part.type == "MESH"]
        low = [min((part.matrix_world @ Vector(corner))[axis] for part in meshes for corner in part.bound_box) for axis in range(3)]
        high = [max((part.matrix_world @ Vector(corner))[axis] for part in meshes for corner in part.bound_box) for axis in range(3)]
        faces = sum(len(part.data.polygons) for part in meshes)
        print("MODEL %s %.2f x %.2f x %.2f, %d faces in %d parts" % (made.name, high[0] - low[0], high[1] - low[1], high[2] - low[2], faces, len(meshes)))
