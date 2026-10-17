using System.Collections.Generic;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// What flies when something is destroyed: the chunks of a building that
    /// comes down, the wedges of a hex that breaks up and falls, and the dust of
    /// both. The prototype removes a razed building and a shattered hex at once;
    /// the rules still do, and this only shows it happen.
    ///
    /// A view owns one field, tells it what broke and where, and steps it every
    /// frame. Everything is placed in the view's own space.
    /// </summary>
    public sealed class DebrisField
    {
        private const float Gravity = 9f;
        /// <summary>The field never holds more pieces than this; a new burst is thinned to fit.</summary>
        private const int MaxPieces = 220;

        /// <summary>How fast the debris moves: 1 is as it is, less slows it down to be looked at.</summary>
        public static float Speed = 1f;

        private static readonly Color[] BuildingColors =
        {
            new Color(0.86f, 0.78f, 0.6f),
            new Color(0.42f, 0.2f, 0.52f),
            new Color(0.52f, 0.52f, 0.58f),
            new Color(0.4f, 0.25f, 0.14f),
            new Color(0.3f, 0.3f, 0.34f),
        };

        private static readonly Color DustColor = new Color(0.72f, 0.66f, 0.56f);
        private static readonly Color RockColor = new Color(0.3f, 0.27f, 0.25f);
        private static readonly Color SoilColor = new Color(0.36f, 0.25f, 0.16f);

        private readonly Transform _parent;
        private readonly Material _material;
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly System.Random _dice = new System.Random(7);

        private sealed class Piece
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float SpinSpeed;
            public Vector3 Size;
            /// <summary>The height it comes to rest on, or negative infinity when it falls for good.</summary>
            public float Ground;
            public float Age;
            public float Life;
            /// <summary>It waits this long before it moves: a building comes down from the top.</summary>
            public float Delay;
            public bool IsDust;
        }

        public DebrisField(Transform parent, Material material)
        {
            _parent = parent;
            _material = material;
        }

        public void Clear()
        {
            foreach (Piece piece in _pieces)
            {
                if (piece.Transform != null)
                {
                    Object.Destroy(piece.Transform.gameObject);
                }
            }

            _pieces.Clear();
        }

        /// <summary>
        /// A building comes down on its hex: masonry, roof and beams burst out of
        /// where it stood, fall back onto the ground and lie a moment, under a
        /// cloud of dust.
        /// </summary>
        /// <param name="at">The foot of the building, in the view's space.</param>
        /// <param name="size">1 for a building; more for the stronghold.</param>
        public void Collapse(Vector3 at, float size = 1f)
        {
            int chunks = Room(22);
            for (int index = 0; index < chunks; index += 1)
            {
                float angle = Roll(0f, Mathf.PI * 2f);
                float reach = Roll(0.05f, 0.42f) * size;
                float high = Roll(0.08f, 0.85f) * size;
                Vector3 out2 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 scale = new Vector3(Roll(0.07f, 0.2f), Roll(0.05f, 0.14f), Roll(0.07f, 0.18f)) * size;
                // The top goes first, and what is higher flies farther.
                Add(Props.Cube, _material, BuildingColors[index % BuildingColors.Length] * Roll(0.75f, 1.05f), at + out2 * reach + Vector3.up * high, scale,
                    out2 * Roll(0.5f, 1.9f) * (0.5f + high) + Vector3.up * Roll(0.6f, 2.6f), at.y + scale.y * 0.5f, Roll(1.5f, 2.3f), (1f - high / size) * 0.22f, false);
            }

            Dust(at, 0.75f * size, 12, 0.8f * size);
        }

        /// <summary>
        /// A hex breaks up: its top splits into six wedges that part and fall away
        /// with the rock that hung under it, and dust is left in the air.
        /// </summary>
        /// <param name="at">The foot of the hex's tile, in the view's space.</param>
        /// <param name="tall">How tall the tile stood.</param>
        public void Shatter(Vector3 at, float tall, Color top)
        {
            Material ground = Props.Ground != null ? Props.Ground : _material;
            int wedges = Mathf.Min(6, Room(6));
            for (int index = 0; index < wedges; index += 1)
            {
                float angle = (60f + index * 60f) * Mathf.Deg2Rad;
                Vector3 out2 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Transform wedge = Add(HexMeshes.Wedge, ground, top * Roll(0.85f, 1.05f), at, new Vector3(0.96f, tall, 0.96f),
                    out2 * Roll(0.5f, 1.3f) + Vector3.up * Roll(0.6f, 2.2f), float.NegativeInfinity, Roll(1.2f, 1.6f), index * 0.03f, false);
                wedge.localRotation = Quaternion.Euler(0f, -index * 60f, 0f);
            }

            int rocks = Room(12);
            for (int index = 0; index < rocks; index += 1)
            {
                float angle = Roll(0f, Mathf.PI * 2f);
                Vector3 out2 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 scale = Vector3.one * Roll(0.12f, 0.34f);
                Add(Props.Cube, _material, (index % 3 == 0 ? SoilColor : RockColor) * Roll(0.8f, 1.2f), at + out2 * Roll(0.1f, 0.7f) + Vector3.down * Roll(0.1f, 1.1f), scale,
                    out2 * Roll(0.3f, 1.6f) + Vector3.up * Roll(-0.5f, 2.4f), float.NegativeInfinity, Roll(1.1f, 1.7f), Roll(0f, 0.12f), false);
            }

            Dust(at + Vector3.up * tall, 0.9f, 12, 0.8f);
        }

        /// <summary>Moves everything on by that many seconds and clears away what is done.</summary>
        public void Step(float deltaTime)
        {
            deltaTime *= Speed;
            for (int index = _pieces.Count - 1; index >= 0; index -= 1)
            {
                Piece piece = _pieces[index];
                if (piece.Transform == null)
                {
                    _pieces.RemoveAt(index);

                    continue;
                }

                if (piece.Delay > 0f)
                {
                    piece.Delay -= deltaTime;

                    continue;
                }

                piece.Age += deltaTime;
                float t = piece.Age / piece.Life;
                if (t >= 1f)
                {
                    Object.Destroy(piece.Transform.gameObject);
                    _pieces.RemoveAt(index);

                    continue;
                }

                Vector3 position = piece.Transform.localPosition + piece.Velocity * deltaTime;
                if (piece.IsDust)
                {
                    // Dust rolls out, slows and thins. The material is opaque, so it thins by shrinking.
                    piece.Velocity *= Mathf.Exp(-2.4f * deltaTime);
                    float swell = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                    piece.Transform.localScale = piece.Size * Mathf.Max(0f, swell);
                }
                else
                {
                    piece.Velocity += Vector3.down * Gravity * deltaTime;
                    if (position.y < piece.Ground)
                    {
                        // It lands, bounces once or twice, and lies still.
                        position.y = piece.Ground;
                        piece.Velocity = new Vector3(piece.Velocity.x * 0.45f, -piece.Velocity.y * 0.28f, piece.Velocity.z * 0.45f);
                        piece.SpinSpeed *= 0.35f;
                    }

                    piece.Transform.localRotation = Quaternion.AngleAxis(piece.SpinSpeed * deltaTime, piece.SpinAxis) * piece.Transform.localRotation;
                    // What lies on the ground sinks away at the end; what falls for good just grows small with distance.
                    float fade = Mathf.Clamp01((1f - t) / 0.3f);
                    piece.Transform.localScale = piece.Size * (float.IsNegativeInfinity(piece.Ground) ? Mathf.Lerp(1f, 0.55f, t) : fade);
                }

                piece.Transform.localPosition = position;
            }
        }

        private void Dust(Vector3 at, float reach, int puffs, float size)
        {
            puffs = Room(puffs);
            for (int index = 0; index < puffs; index += 1)
            {
                float angle = index * Mathf.PI * 2f / puffs + Roll(-0.3f, 0.3f);
                Vector3 out2 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Add(Props.Sphere, _material, DustColor * Roll(0.85f, 1.1f), at + out2 * reach * 0.25f + Vector3.up * Roll(0.05f, 0.3f), Vector3.one * Roll(0.35f, 0.6f) * size,
                    out2 * Roll(0.9f, 1.7f) * reach + Vector3.up * Roll(0.15f, 0.7f), float.NegativeInfinity, Roll(0.9f, 1.5f), Roll(0f, 0.1f), true);
            }
        }

        private Transform Add(Mesh mesh, Material material, Color color, Vector3 at, Vector3 size, Vector3 velocity, float ground, float life, float delay, bool isDust)
        {
            Transform made = Props.Part(_parent, mesh, material, at, isDust ? Vector3.zero : size, color);
            made.name = isDust ? "Dust" : "Debris";
            made.localRotation = isDust ? Quaternion.identity : Quaternion.Euler(Roll(0f, 30f), Roll(0f, 360f), Roll(0f, 30f));
            _pieces.Add(new Piece
            {
                Transform = made,
                Velocity = velocity,
                SpinAxis = new Vector3(Roll(-1f, 1f), Roll(-0.3f, 0.3f), Roll(-1f, 1f)).normalized,
                SpinSpeed = Roll(120f, 420f),
                Size = size,
                Ground = ground,
                Life = life,
                Delay = delay,
                IsDust = isDust,
            });

            return made;
        }

        private int Room(int wanted)
        {
            return Mathf.Max(0, Mathf.Min(wanted, MaxPieces - _pieces.Count));
        }

        private float Roll(float low, float high)
        {
            return Mathf.Lerp(low, high, (float)_dice.NextDouble());
        }
    }
}
