import { BufferAttribute, BufferGeometry } from "three";

/**
 * The planet is a single sphere that is already cut into shards. At rest the
 * cuts are invisible. The fissures that glow before the blast are the same
 * cuts, so the crust really breaks along the lines the player has just seen.
 */

const SUBDIVISIONS = 7;
const UNIFORM_SHARDS = 230;
const EPICENTRE_SHARDS = 170;
/** Vertices further than this many hops from a cut all count as "far". */
const EDGE_HOPS = 6;

type TRandom = () => number;

const createRandom = (seed: number): TRandom => {
  let state = seed;

  return () => {
    state = (state + 0x6d2b79f5) | 0;
    let x = Math.imul(state ^ (state >>> 15), 1 | state);
    x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x;

    return ((x ^ (x >>> 14)) >>> 0) / 4294967296;
  };
};

type TSphere = {
  readonly positions: Float32Array;
  readonly triangles: Uint32Array;
  readonly vertexCount: number;
};

const buildSphere = (levels: number): TSphere => {
  const vertexCapacity = 10 * 4 ** levels + 2;
  const positions = new Float32Array(vertexCapacity * 3);
  const t = (1 + Math.sqrt(5)) / 2;
  const seed = [
    -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, 0, 0, -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, t, 0, -1, t, 0, 1, -t, 0, -1, -t, 0, 1,
  ];
  const length = Math.hypot(1, t);
  seed.forEach((value, index) => {
    positions[index] = value / length;
  });

  let triangles = Uint32Array.from([
    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2,
    6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
  ]);
  let vertexCount = 12;

  for (let level = 0; level < levels; level += 1) {
    const middles = new Map<number, number>();
    const next = new Uint32Array(triangles.length * 4);

    const middle = (a: number, b: number): number => {
      const key = a < b ? a * 0x200000 + b : b * 0x200000 + a;
      const found = middles.get(key);
      if (found !== undefined) {
        return found;
      }

      const x = positions[a * 3]! + positions[b * 3]!;
      const y = positions[a * 3 + 1]! + positions[b * 3 + 1]!;
      const z = positions[a * 3 + 2]! + positions[b * 3 + 2]!;
      const scale = 1 / Math.hypot(x, y, z);
      const index = vertexCount;
      positions[index * 3] = x * scale;
      positions[index * 3 + 1] = y * scale;
      positions[index * 3 + 2] = z * scale;
      vertexCount += 1;
      middles.set(key, index);

      return index;
    };

    for (let i = 0; i < triangles.length; i += 3) {
      const a = triangles[i]!;
      const b = triangles[i + 1]!;
      const c = triangles[i + 2]!;
      const ab = middle(a, b);
      const bc = middle(b, c);
      const ca = middle(c, a);
      next.set([a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca], i * 4);
    }

    triangles = next;
  }

  return { positions, triangles, vertexCount };
};

/** Bends the sphere with sines, so the straight Voronoi cuts turn into jagged cracks. */
const warp = (x: number, y: number, z: number, out: Float32Array): void => {
  const a = 0.085;
  const b = 0.036;
  const c = 0.014;
  out[0] = x + a * Math.sin(y * 6.1 + 1.3) + b * Math.sin(z * 17.3 + 4.1) + c * Math.sin(y * 43.1 + z * 37.7);
  out[1] = y + a * Math.sin(z * 5.7 + 2.9) + b * Math.sin(x * 16.1 + 0.7) + c * Math.sin(z * 41.3 + x * 39.1);
  out[2] = z + a * Math.sin(x * 6.7 + 5.1) + b * Math.sin(y * 18.7 + 2.3) + c * Math.sin(x * 47.3 + y * 35.3);
};

type TSeeds = {
  readonly directions: Float32Array;
  readonly weights: Float32Array;
  readonly count: number;
};

const createSeeds = (random: TRandom, epicentre: readonly [number, number, number]): TSeeds => {
  const count = UNIFORM_SHARDS + EPICENTRE_SHARDS;
  const directions = new Float32Array(count * 3);
  const weights = new Float32Array(count);

  for (let i = 0; i < count; i += 1) {
    let x = random() * 2 - 1;
    let y = random() * 2 - 1;
    let z = random() * 2 - 1;
    const isNearEpicentre = i >= UNIFORM_SHARDS;
    const length = Math.hypot(x, y, z) || 1;
    const spread = isNearEpicentre ? 0.55 * random() : 1;
    x = x / length * spread + (isNearEpicentre ? epicentre[0] : 0);
    y = y / length * spread + (isNearEpicentre ? epicentre[1] : 0);
    z = z / length * spread + (isNearEpicentre ? epicentre[2] : 0);
    const scale = 1 / (Math.hypot(x, y, z) || 1);
    directions[i * 3] = x * scale;
    directions[i * 3 + 1] = y * scale;
    directions[i * 3 + 2] = z * scale;
    // A wide spread of weights gives a few continents and many splinters.
    weights[i] = 1 + random() ** 3 * 0.12;
  }

  return { directions, weights, count };
};

const assignTriangles = (sphere: TSphere, seeds: TSeeds): Uint16Array => {
  const triangleCount = sphere.triangles.length / 3;
  const owner = new Uint16Array(triangleCount);
  const warped = new Float32Array(3);
  const { positions, triangles } = sphere;

  for (let i = 0; i < triangleCount; i += 1) {
    const a = triangles[i * 3]! * 3;
    const b = triangles[i * 3 + 1]! * 3;
    const c = triangles[i * 3 + 2]! * 3;
    warp(
      (positions[a]! + positions[b]! + positions[c]!) / 3,
      (positions[a + 1]! + positions[b + 1]! + positions[c + 1]!) / 3,
      (positions[a + 2]! + positions[b + 2]! + positions[c + 2]!) / 3,
      warped,
    );

    let best = -Infinity;
    let bestIndex = 0;
    for (let s = 0; s < seeds.count; s += 1) {
      const score =
        (warped[0]! * seeds.directions[s * 3]!
          + warped[1]! * seeds.directions[s * 3 + 1]!
          + warped[2]! * seeds.directions[s * 3 + 2]!)
        * seeds.weights[s]!;
      if (score > best) {
        best = score;
        bestIndex = s;
      }
    }

    owner[i] = bestIndex;
  }

  return owner;
};

/** The edges that separate two shards, stored as start, end and the owner of the side. */
const findCuts = (sphere: TSphere, owner: Uint16Array): Uint32Array => {
  const { triangles } = sphere;
  const open = new Map<number, number>();
  const cuts: number[] = [];

  for (let i = 0; i < triangles.length; i += 1) {
    const triangle = Math.floor(i / 3);
    const a = triangles[i]!;
    const b = triangles[i % 3 === 2 ? i - 2 : i + 1]!;
    const key = a < b ? a * 0x200000 + b : b * 0x200000 + a;
    const other = open.get(key);
    if (other === undefined) {
      open.set(key, triangle);
    } else {
      open.delete(key);
      if (owner[other] !== owner[triangle]) {
        cuts.push(a, b, owner[triangle]!);
        cuts.push(b, a, owner[other]!);
      }
    }
  }

  return Uint32Array.from(cuts);
};

const measureEdgeDistance = (sphere: TSphere, cuts: Uint32Array): Uint8Array => {
  const distance = new Uint8Array(sphere.vertexCount).fill(EDGE_HOPS);
  for (let i = 0; i < cuts.length; i += 3) {
    distance[cuts[i]!] = 0;
    distance[cuts[i + 1]!] = 0;
  }

  const { triangles } = sphere;
  for (let hop = 1; hop < EDGE_HOPS; hop += 1) {
    for (let i = 0; i < triangles.length; i += 3) {
      const a = triangles[i]!;
      const b = triangles[i + 1]!;
      const c = triangles[i + 2]!;
      const near = Math.min(distance[a]!, distance[b]!, distance[c]!);
      if (near === hop - 1) {
        distance[a] = Math.min(distance[a]!, hop);
        distance[b] = Math.min(distance[b]!, hop);
        distance[c] = Math.min(distance[c]!, hop);
      }
    }
  }

  return distance;
};

/**
 * Builds the shards. Each one is the piece of the sphere it owns, plus a rough
 * belly of bare rock under it and the walls between the two.
 *
 * The `aCenter.w` attribute holds the size of a shard: the share of the sphere it owns.
 * The `aSurface.x` attribute is 0 on the crust, 1 on the belly and 2 on the walls.
 * The `aSurface.y` attribute is the distance to a cut on the crust and the depth in the rock.
 */
const createPlanetGeometry = (epicentre: readonly [number, number, number]): BufferGeometry => {
  const random = createRandom(20261107);
  const sphere = buildSphere(SUBDIVISIONS);
  const seeds = createSeeds(random, epicentre);
  const owner = assignTriangles(sphere, seeds);
  const cuts = findCuts(sphere, owner);
  const edgeDistance = measureEdgeDistance(sphere, cuts);

  const triangleCount = owner.length;
  const counts = new Uint32Array(seeds.count + 1);
  for (let i = 0; i < triangleCount; i += 1) {
    counts[owner[i]! + 1]! += 1;
  }
  for (let i = 1; i <= seeds.count; i += 1) {
    counts[i]! += counts[i - 1]!;
  }

  const order = new Uint32Array(triangleCount);
  const cursor = counts.slice();
  for (let i = 0; i < triangleCount; i += 1) {
    order[cursor[owner[i]!]!] = i;
    cursor[owner[i]!]! += 1;
  }

  const cutsByShard: number[][] = Array.from({ length: seeds.count }, () => []);
  for (let i = 0; i < cuts.length; i += 3) {
    cutsByShard[cuts[i + 2]!]!.push(cuts[i]!, cuts[i + 1]!);
  }

  const cutCount = cuts.length / 3;
  const vertexCapacity = (sphere.vertexCount + cutCount) * 2 + cutCount * 4;
  const indexCapacity = triangleCount * 6 + cutCount * 6;
  const position = new Float32Array(vertexCapacity * 3);
  const center = new Float32Array(vertexCapacity * 4);
  const shardRandom = new Float32Array(vertexCapacity * 4);
  const surface = new Float32Array(vertexCapacity * 2);
  const index = new Uint32Array(indexCapacity);
  const stamp = new Int32Array(sphere.vertexCount).fill(-1);
  const remap = new Uint32Array(sphere.vertexCount);

  let vertexCursor = 0;
  let indexCursor = 0;

  const pushVertex = (
    x: number, y: number, z: number,
    cx: number, cy: number, cz: number, size: number,
    r0: number, r1: number, r2: number, r3: number,
    kind: number, value: number,
  ): number => {
    const v = vertexCursor;
    position.set([x, y, z], v * 3);
    center.set([cx, cy, cz, size], v * 4);
    shardRandom.set([r0, r1, r2, r3], v * 4);
    surface.set([kind, value], v * 2);
    vertexCursor += 1;

    return v;
  };

  const { positions, triangles } = sphere;

  for (let shard = 0; shard < seeds.count; shard += 1) {
    const from = counts[shard]!;
    const to = counts[shard + 1]!;
    if (from === to) {
      continue;
    }

    let cx = 0;
    let cy = 0;
    let cz = 0;
    for (let i = from; i < to; i += 1) {
      const a = triangles[order[i]! * 3]! * 3;
      cx += positions[a]!;
      cy += positions[a + 1]!;
      cz += positions[a + 2]!;
    }
    const scale = 1 / (Math.hypot(cx, cy, cz) || 1);
    cx *= scale;
    cy *= scale;
    cz *= scale;

    const size = (to - from) / triangleCount;
    const r0 = random();
    const r1 = random();
    const r2 = random();
    const r3 = random();

    for (let i = from; i < to; i += 1) {
      for (let corner = 0; corner < 3; corner += 1) {
        const v = triangles[order[i]! * 3 + corner]!;
        if (stamp[v] !== shard) {
          stamp[v] = shard;
          remap[v] = pushVertex(
            positions[v * 3]!, positions[v * 3 + 1]!, positions[v * 3 + 2]!,
            cx, cy, cz, size, r0, r1, r2, r3, 0, edgeDistance[v]! / EDGE_HOPS,
          );
        }
        index[indexCursor] = remap[v]!;
        indexCursor += 1;
      }
    }

    // The slab: a thick plate of crust with a torn belly. It is deepest under the middle.
    const reach = Math.sqrt(size) * 3.4 + 0.02;
    const thickness = 0.07 + Math.min(0.24, reach * 0.42) + r0 * 0.05;
    const belly = (v: number): void => {
      const x = positions[v * 3]!;
      const y = positions[v * 3 + 1]!;
      const z = positions[v * 3 + 2]!;
      const away = Math.acos(Math.min(1, x * cx + y * cy + z * cz)) / reach;
      const middle = Math.max(0, 1 - away * away);
      const crag = Math.sin(x * 31 + y * 17 + r1 * 9) * Math.sin(y * 37 + z * 23 + r2 * 9) * 0.5
        + Math.sin(z * 71 + x * 53) * Math.sin(y * 83 + z * 61) * 0.28
        + Math.sin(x * 127 + z * 149 + y * 113) * 0.12;
      const depth = thickness * (0.3 + middle * 0.95 + crag * (0.32 + middle * 0.35));
      const pull = 0.14;
      const px = x + (cx - x) * pull;
      const py = y + (cy - y) * pull;
      const pz = z + (cz - z) * pull;
      const norm = (1 - depth) / (Math.hypot(px, py, pz) || 1);
      remap[v] = pushVertex(px * norm, py * norm, pz * norm, cx, cy, cz, size, r0, r1, r2, r3, 1, 0.35 + middle * 0.65);
    };

    for (let i = from; i < to; i += 1) {
      const triangle = order[i]! * 3;
      for (let corner = 0; corner < 3; corner += 1) {
        const v = triangles[triangle + corner]!;
        if (stamp[v] !== -shard - 2) {
          stamp[v] = -shard - 2;
          belly(v);
        }
      }
      // The belly looks away from the crust, so its faces turn the other way.
      index[indexCursor] = remap[triangles[triangle]!]!;
      index[indexCursor + 1] = remap[triangles[triangle + 2]!]!;
      index[indexCursor + 2] = remap[triangles[triangle + 1]!]!;
      indexCursor += 3;
    }

    const shardCuts = cutsByShard[shard]!;
    for (let i = 0; i < shardCuts.length; i += 2) {
      const a = shardCuts[i]!;
      const b = shardCuts[i + 1]!;
      const copy = (from: number, value: number, source: Float32Array, at: number): number =>
        pushVertex(source[at]!, source[at + 1]!, source[at + 2]!, cx, cy, cz, size, r0, r1, r2, r3, from, value);
      const topA = copy(2, 0, positions, a * 3);
      const topB = copy(2, 0, positions, b * 3);
      const lowA = copy(2, 1, position, remap[a]! * 3);
      const lowB = copy(2, 1, position, remap[b]! * 3);
      // The crust walks this cut from a to b, so the wall walks it from b to a.
      index.set([topB, topA, lowA, topB, lowA, lowB], indexCursor);
      indexCursor += 6;
    }
  }

  const geometry = new BufferGeometry();
  geometry.setAttribute("position", new BufferAttribute(position.subarray(0, vertexCursor * 3), 3));
  geometry.setAttribute("aCenter", new BufferAttribute(center.subarray(0, vertexCursor * 4), 4));
  geometry.setAttribute("aRand", new BufferAttribute(shardRandom.subarray(0, vertexCursor * 4), 4));
  geometry.setAttribute("aSurface", new BufferAttribute(surface.subarray(0, vertexCursor * 2), 2));
  geometry.setIndex(new BufferAttribute(index.subarray(0, indexCursor), 1));

  return geometry;
};

export { createPlanetGeometry, createRandom };
