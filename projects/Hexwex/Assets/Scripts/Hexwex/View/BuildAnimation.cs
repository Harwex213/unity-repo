using System.Collections.Generic;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// A building going up. The models are made in the four stages they are built
    /// in, each its own mesh named <c>&lt;art name&gt;__S0</c> .. <c>__S3</c> by
    /// <c>art-sources/Hexwex/build_buildings.py</c>: the ground work, the walls,
    /// the roof, and the things that finish it. This class raises them one after
    /// another inside a scaffold, over a puff of dust, and takes the scaffold down
    /// when the last piece is on.
    ///
    /// The prototype has no such thing: a building appears on its hex at once. The
    /// rules still build at once; only the picture takes its time.
    /// </summary>
    public sealed class BuildAnimation
    {
        /// <summary>The whole show, from the first dust to the settled building.</summary>
        public const float Seconds = 1.9f;

        private const float ScaffoldUpAt = 0.2f;
        private const float ScaffoldDownAt = 1.3f;
        private const float SettleAt = 1.6f;

        private static readonly Color ScaffoldColor = new Color(0.55f, 0.38f, 0.2f);
        private static readonly Color DustColor = new Color(0.78f, 0.72f, 0.6f);

        /// <summary>When each stage starts and how long it takes, in seconds.</summary>
        private static readonly float[] StageStart = { 0f, 0.32f, 0.82f, 1.18f };
        private static readonly float[] StageLength = { 0.38f, 0.56f, 0.42f, 0.4f };

        private readonly Transform _content;
        private readonly Transform _model;
        private readonly Vector3 _modelScale;
        private readonly Stage[] _stages = new Stage[4];
        private readonly Transform _scaffold;
        private readonly List<Transform> _dust = new List<Transform>();
        private float _elapsed;
        private readonly float _height;
        private readonly bool _hasStages;

        private struct Stage
        {
            public Transform Transform;
            public Vector3 Position;
            public Vector3 Scale;
        }

        public BuildAnimation(Transform content, Material material)
        {
            _content = content;

            // What the finished building takes up, measured before anything is moved.
            Bounds bounds = new Bounds(content.position, Vector3.zero);
            foreach (Renderer renderer in content.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            _height = Mathf.Max(0.3f, bounds.max.y - content.position.y);
            _model = content.childCount > 0 ? content.GetChild(0) : content;
            _modelScale = _model.localScale;

            foreach (Transform part in content.GetComponentsInChildren<Transform>())
            {
                for (int index = 0; index < _stages.Length; index += 1)
                {
                    if (part.name.EndsWith("__S" + index))
                    {
                        _stages[index] = new Stage { Transform = part, Position = part.localPosition, Scale = part.localScale };
                        _hasStages = true;
                    }
                }
            }

            _scaffold = BuildScaffold(content, material, bounds);
            BuildDust(content, material);
            Apply(0f);
        }

        /// <summary>Moves the show on by that many seconds. Returns <c>true</c> when it is over and the building stands as modelled.</summary>
        public bool Step(float deltaTime)
        {
            if (_content == null)
            {
                return true;
            }

            _elapsed += deltaTime;
            float elapsed = _elapsed;
            if (elapsed >= Seconds)
            {
                Finish();

                return true;
            }

            Apply(elapsed);

            return false;
        }

        /// <summary>Puts everything where the model has it and clears the props away.</summary>
        public void Finish()
        {
            if (_content == null)
            {
                return;
            }

            foreach (Stage stage in _stages)
            {
                if (stage.Transform != null)
                {
                    stage.Transform.localPosition = stage.Position;
                    stage.Transform.localScale = stage.Scale;
                }
            }

            _model.localScale = _modelScale;
            if (_scaffold != null)
            {
                Object.Destroy(_scaffold.gameObject);
            }

            foreach (Transform puff in _dust)
            {
                if (puff != null)
                {
                    Object.Destroy(puff.gameObject);
                }
            }

            _dust.Clear();
        }

        private void Apply(float elapsed)
        {
            if (_hasStages)
            {
                ApplyStages(elapsed);
            }
            else
            {
                // A model with no stages rises whole.
                float rise = EaseOutBack(Mathf.Clamp01(elapsed / 0.9f));
                _model.localScale = Vector3.Scale(_modelScale, new Vector3(Mathf.Lerp(0.7f, 1f, rise), rise, Mathf.Lerp(0.7f, 1f, rise)));
            }

            // The scaffold climbs ahead of the walls and comes down once the roof is on.
            if (_scaffold != null)
            {
                float up = Mathf.Clamp01((elapsed - ScaffoldUpAt) / 0.3f);
                float down = Mathf.Clamp01((elapsed - ScaffoldDownAt) / 0.3f);
                float standing = Mathf.SmoothStep(0f, 1f, up) * (1f - Mathf.SmoothStep(0f, 1f, down));
                _scaffold.localScale = new Vector3(1f, standing, 1f);
                // Flattened to nothing it would still draw as lines on the ground.
                _scaffold.gameObject.SetActive(standing > 0.02f);
            }

            // The dust bursts out from the foot of the work and thins away.
            for (int index = 0; index < _dust.Count; index += 1)
            {
                float t = Mathf.Clamp01((elapsed - index * 0.03f) / 0.6f);
                float swell = t < 0.35f ? t / 0.35f : 1f - (t - 0.35f) / 0.65f;
                float angle = index * Mathf.PI * 2f / _dust.Count;
                float reach = Mathf.Lerp(0.25f, 0.7f, t);
                _dust[index].localPosition = new Vector3(Mathf.Cos(angle) * reach, 0.04f + t * 0.12f, Mathf.Sin(angle) * reach);
                _dust[index].localScale = Vector3.one * 0.16f * Mathf.Max(0f, swell);
            }

            // The finished building settles onto its ground.
            if (_hasStages && elapsed >= SettleAt)
            {
                float t = (elapsed - SettleAt) / (Seconds - SettleAt);
                float squash = Mathf.Sin(t * Mathf.PI) * 0.05f;
                _model.localScale = Vector3.Scale(_modelScale, new Vector3(1f + squash * 0.6f, 1f - squash, 1f + squash * 0.6f));
            }
        }

        private void ApplyStages(float elapsed)
        {
            for (int index = 0; index < _stages.Length; index += 1)
            {
                Stage stage = _stages[index];
                if (stage.Transform == null)
                {
                    continue;
                }

                float t = Mathf.Clamp01((elapsed - StageStart[index]) / StageLength[index]);
                Vector3 scale = Vector3.one;
                Vector3 offset = Vector3.zero;

                switch (index)
                {
                    case 0:
                        // The ground work spreads out from the middle.
                        float spread = EaseOutBack(t);
                        scale = new Vector3(spread, Mathf.Clamp01(t * 2f), spread);
                        break;
                    case 1:
                        // The walls climb.
                        scale = new Vector3(1f, EaseOutBack(t), 1f);
                        break;
                    case 2:
                        // The roof is lowered on and bounces once.
                        scale = t > 0f ? Vector3.one : Vector3.zero;
                        offset = Vector3.up * (1f - EaseOutBounce(t)) * (_height * 0.5f + 0.2f);
                        break;
                    default:
                        // The last things pop into place.
                        scale = Vector3.one * EaseOutBack(t);
                        break;
                }

                stage.Transform.localScale = Vector3.Scale(stage.Scale, scale);
                // The offset is in the world's up, whatever way the model is turned.
                stage.Transform.localPosition = stage.Position + stage.Transform.parent.InverseTransformVector(offset);
            }
        }

        /// <summary>Four poles round the work with rails between them, growing from the ground.</summary>
        private Transform BuildScaffold(Transform content, Material material, Bounds bounds)
        {
            GameObject root = new GameObject("Scaffold");
            root.transform.SetParent(content, false);
            Transform t = root.transform;

            Vector3 middle = content.InverseTransformPoint(bounds.center);
            float halfX = Mathf.Min(0.68f, bounds.extents.x + 0.03f);
            float halfZ = Mathf.Min(0.68f, bounds.extents.z + 0.03f);
            float tall = _height * 0.92f;

            foreach (float x in new[] { -halfX, halfX })
            {
                foreach (float z in new[] { -halfZ, halfZ })
                {
                    Props.Part(t, Props.Cube, material, new Vector3(middle.x + x, tall * 0.5f, middle.z + z), new Vector3(0.035f, tall, 0.035f), ScaffoldColor);
                }
            }

            int levels = Mathf.Clamp(Mathf.RoundToInt(tall / 0.3f), 1, 4);
            for (int level = 1; level <= levels; level += 1)
            {
                float y = tall * level / levels - 0.02f;
                foreach (float z in new[] { -halfZ, halfZ })
                {
                    Props.Part(t, Props.Cube, material, new Vector3(middle.x, y, middle.z + z), new Vector3(halfX * 2f + 0.06f, 0.025f, 0.03f), ScaffoldColor * 1.15f);
                }

                foreach (float x in new[] { -halfX, halfX })
                {
                    Props.Part(t, Props.Cube, material, new Vector3(middle.x + x, y - 0.03f, middle.z), new Vector3(0.03f, 0.025f, halfZ * 2f + 0.06f), ScaffoldColor * 0.9f);
                }
            }

            return t;
        }

        private void BuildDust(Transform content, Material material)
        {
            for (int index = 0; index < 8; index += 1)
            {
                Transform puff = Props.Part(content, Props.Sphere, material, Vector3.zero, Vector3.zero, DustColor);
                puff.name = "Dust";
                _dust.Add(puff);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float shifted = Mathf.Clamp01(t) - 1f;

            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        private static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 1f / 2.75f)
            {
                return 7.5625f * t * t;
            }

            if (t < 2f / 2.75f)
            {
                t -= 1.5f / 2.75f;

                return 7.5625f * t * t + 0.75f;
            }

            if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;

                return 7.5625f * t * t + 0.9375f;
            }

            t -= 2.625f / 2.75f;

            return 7.5625f * t * t + 0.984375f;
        }
    }
}
