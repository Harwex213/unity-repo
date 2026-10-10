using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>Marks the collider of a hex, so a raycast can name the hex it hit.</summary>
    public sealed class HexHandle : MonoBehaviour
    {
        public string HexId;
    }

    public enum HexMark
    {
        None,
        Hovered,
        Selected,
        /// <summary>The armed building can go here.</summary>
        Allowed,
        /// <summary>The hex the soil cleansing will destroy.</summary>
        Sacrifice,
    }

    /// <summary>
    /// Draws one player's island in 3D. It stands where the prototype's
    /// <c>island-canvas.tsx</c> stood: it only reads the island and never changes
    /// it. <see cref="Render"/> is called after every change of the session, and
    /// rebuilds only the hexes whose look has changed.
    /// </summary>
    public sealed class IslandView : MonoBehaviour
    {
        /// <summary>The gap between two hexes, as a share of the hex radius.</summary>
        private const float HexInset = 0.96f;
        private static readonly Color RootColor = new Color(0.27f, 0.23f, 0.21f);
        private static readonly Color DeadColor = new Color(0.2f, 0.17f, 0.22f);
        private static readonly Color ToxicColor = new Color(0.52f, 0.2f, 0.62f);

        [SerializeField] private Material surfaceMaterial;

        private readonly Dictionary<string, HexVisual> _visuals = new Dictionary<string, HexVisual>();

        private sealed class HexVisual
        {
            public GameObject Root;
            public Transform Top;
            public MeshRenderer TopRenderer;
            public GameObject Content;
            public string ContentKey;
            /// <summary>The scale the content rests at: ruins lie flat.</summary>
            public Vector3 ContentScale = Vector3.one;
            public float Height;
        }

        public Material SurfaceMaterial
        {
            get { return surfaceMaterial; }
            set { surfaceMaterial = value; }
        }

        /// <summary>The point above a hex where a plate with its roll is shown.</summary>
        public bool TryGetPlateAnchor(string hexId, out Vector3 position)
        {
            if (_visuals.TryGetValue(hexId, out HexVisual visual))
            {
                position = visual.Root.transform.position + Vector3.up * (visual.Height + 1.0f);

                return true;
            }

            position = default;

            return false;
        }

        public void Render(Player player, System.Func<HexTile, HexMark> markOf)
        {
            HashSet<string> present = new HashSet<string>();

            foreach (HexTile hex in player.Hexes)
            {
                present.Add(hex.Id);
                if (!_visuals.TryGetValue(hex.Id, out HexVisual visual))
                {
                    visual = CreateVisual(hex);
                    _visuals[hex.Id] = visual;
                }

                UpdateContent(player, hex, visual);
                PaintTop(hex, visual, markOf != null ? markOf(hex) : HexMark.None);
            }

            // A hex sacrificed to the soil cleansing has left the island.
            List<string> gone = new List<string>();
            foreach (string hexId in _visuals.Keys)
            {
                if (!present.Contains(hexId))
                {
                    gone.Add(hexId);
                }
            }

            foreach (string hexId in gone)
            {
                Destroy(_visuals[hexId].Root);
                _visuals.Remove(hexId);
            }
        }

        /// <summary>
        /// The squash-and-stretch pulse of what stands on a hex, as when its die is
        /// revealed. <paramref name="t"/> runs from 0 to 1; anything outside puts the
        /// content back at rest.
        /// </summary>
        public void Pulse(string hexId, float t)
        {
            if (!_visuals.TryGetValue(hexId, out HexVisual visual) || visual.Content == null)
            {
                return;
            }

            Vector3 pulse = Vector3.one;
            if (t > 0f && t < 1f)
            {
                // Down first, then up past its height, and settling.
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t * 0.5f);
                pulse = new Vector3(1f + wave * 0.16f, 1f - wave * 0.3f, 1f + wave * 0.16f);
            }

            visual.Content.transform.localScale = Vector3.Scale(visual.ContentScale, pulse);
        }

        public void Clear()
        {
            foreach (HexVisual visual in _visuals.Values)
            {
                Destroy(visual.Root);
            }

            _visuals.Clear();
        }

        private HexVisual CreateVisual(HexTile hex)
        {
            float height = Props.BiomeHeight(hex.Biome);
            int distance = HexMath.Distance(hex.Q, hex.R, 0, 0);
            Rng rng = Rng.FromText("root:" + hex.Id);

            GameObject root = new GameObject("Hex " + hex.Id);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = HexMeshes.WorldPosition(hex.Q, hex.R);

            GameObject top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(HexInset, height, HexInset);
            top.AddComponent<MeshFilter>().sharedMesh = HexMeshes.Prism;
            MeshRenderer topRenderer = top.AddComponent<MeshRenderer>();
            topRenderer.sharedMaterials = new[] { surfaceMaterial, surfaceMaterial };
            top.AddComponent<MeshCollider>().sharedMesh = HexMeshes.Prism;
            top.AddComponent<HexHandle>().HexId = hex.Id;

            // The rock is deepest under the island's centre, so the whole island
            // tapers to a point the way a floating island should.
            float depth = Mathf.Max(0.6f, 3.6f - distance * 0.85f + (float)rng.Next() * 0.9f);
            GameObject rock = new GameObject("Root");
            rock.transform.SetParent(root.transform, false);
            rock.transform.localScale = new Vector3(HexInset, depth, HexInset);
            rock.AddComponent<MeshFilter>().sharedMesh = HexMeshes.Root;
            MeshRenderer rockRenderer = rock.AddComponent<MeshRenderer>();
            rockRenderer.sharedMaterial = surfaceMaterial;
            Props.Paint(rockRenderer, RootColor * (0.85f + (float)rng.Next() * 0.3f));

            return new HexVisual { Root = root, Top = top.transform, TopRenderer = topRenderer, Height = height };
        }

        /// <summary>The scenery of an empty hex, or the model of what stands on it.</summary>
        private void UpdateContent(Player player, HexTile hex, HexVisual visual)
        {
            StructureKind? kind = StructureHp.KindOn(player, hex);
            bool isRuined = StructureHp.IsRuinedStronghold(player, hex);
            string key = kind.HasValue ? kind.Value + (isRuined ? ":ruined" : "") : "scenery:" + hex.Biome;
            if (visual.ContentKey == key)
            {
                return;
            }

            if (visual.Content != null)
            {
                Destroy(visual.Content);
            }

            visual.ContentKey = key;
            visual.Content = kind.HasValue
                ? Props.CreateStructure(kind.Value, surfaceMaterial, visual.Root.transform)
                : Props.CreateScenery(hex, surfaceMaterial, visual.Root.transform);
            visual.Content.transform.localPosition = Vector3.up * visual.Height;
            visual.ContentScale = Vector3.one;

            if (isRuined)
            {
                // Ruins: the same stronghold, sunk and knocked over.
                visual.ContentScale = new Vector3(1f, 0.35f, 1f);
                visual.Content.transform.localScale = visual.ContentScale;
                visual.Content.transform.localRotation = Quaternion.Euler(6f, 0f, -8f);
            }
        }

        private static void PaintTop(HexTile hex, HexVisual visual, HexMark mark)
        {
            Biome biome = Biomes.Get(hex.Biome);
            Color top = Props.Hex(biome.Color);
            Color side = Props.Hex(biome.EdgeColor);

            // Toxicity creeps over the hex, and a dead hex loses its colour.
            float toxicity = Mathf.Clamp01(hex.Toxicity / 100f);
            top = Tax.IsDead(hex) ? DeadColor : Color.Lerp(top, ToxicColor, toxicity * 0.75f);
            side = Tax.IsDead(hex) ? DeadColor * 0.7f : Color.Lerp(side, ToxicColor * 0.6f, toxicity * 0.75f);

            switch (mark)
            {
                case HexMark.Hovered:
                    top = Color.Lerp(top, Color.white, 0.28f);
                    break;
                case HexMark.Selected:
                    top = Color.Lerp(top, new Color(1f, 0.92f, 0.45f), 0.5f);
                    break;
                case HexMark.Allowed:
                    top = Color.Lerp(top, new Color(0.55f, 1f, 0.55f), 0.4f);
                    break;
                case HexMark.Sacrifice:
                    top = Color.Lerp(top, new Color(1f, 0.25f, 0.2f), 0.65f);
                    break;
            }

            Props.Paint(visual.TopRenderer, top, 0);
            Props.Paint(visual.TopRenderer, side, 1);
        }
    }
}
