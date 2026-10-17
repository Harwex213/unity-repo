using System.Collections.Generic;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// What moves on a standing building: the saw and the water wheel of the
    /// sawmill, the stamp that breaks ore at the mine, the fan of the farm's wind
    /// pump, flags, the smoke of a hearth. The prototype's buildings are still
    /// pictures; this is only for the eye, and no rule reads it.
    ///
    /// The models name their moving parts (<c>lowpoly.Staged</c> in
    /// <c>art-sources/Hexwex</c>): <c>&lt;art&gt;__&lt;kind&gt;_&lt;tag&gt;</c>, with a number at
    /// the end where the kind takes one. This finds them by name and moves each
    /// in its own way. The export leaves a part turned in its own frame, so nothing
    /// here trusts a part's axes: every turn and lift is worked out from the
    /// model's frame, where x is across, y up and z along.
    /// </summary>
    public sealed class BuildingLife : MonoBehaviour
    {
        private const float PoundSeconds = 1.5f;
        private const float PoundLift = 0.13f;
        private const float SmokeEvery = 0.55f;
        private const int MaxPuffs = 14;

        private static readonly Color SmokeColor = new Color(0.6f, 0.6f, 0.64f);
        private static readonly Color DustColor = new Color(0.7f, 0.64f, 0.54f);

        private readonly List<Mover> _movers = new List<Mover>();
        private readonly List<Transform> _smoke = new List<Transform>();
        private readonly List<Transform> _dust = new List<Transform>();
        private readonly List<Puff> _puffs = new List<Puff>();

        private Material _material;
        private Transform _frame;
        private float _phase;
        private float _nextSmoke;

        private enum Kind
        {
            Spin,
            Flag,
            Pound,
            Bob,
            Sway,
            Swing,
        }

        private sealed class Mover
        {
            public Transform Transform;
            public Kind Kind;
            /// <summary>The axis it turns about, in its own frame.</summary>
            public Vector3 Axis;
            /// <summary>A second axis, for what swings two ways.</summary>
            public Vector3 Side;
            /// <summary>The model's up, in the frame of its parent: the way it is lifted.</summary>
            public Vector3 Up;
            public float Number;
            public float Phase;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public bool WasDown;
        }

        private sealed class Puff
        {
            public Transform Transform;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        /// <summary>Finds the moving parts of the building this stands on. <paramref name="material"/> is what smoke and dust are drawn with.</summary>
        public void Bind(Material material)
        {
            _material = material;
            // Two buildings of a kind do not move in step.
            _phase = Mathf.Abs(transform.position.x * 7.3f + transform.position.z * 3.1f) % 10f;
            // The model itself: the prefab holds it under the structure, turned to face the camera.
            _frame = transform.childCount > 0 ? transform.GetChild(0) : transform;

            Transform[] parts = GetComponentsInChildren<Transform>(true);

            // The strips of a flag come side by side, numbered from the pole. Each is
            // hung on the one before it, so that turning one carries the rest.
            Dictionary<string, Transform> strips = new Dictionary<string, Transform>();
            foreach (Transform part in parts)
            {
                if (part.name.Contains("__Flag_"))
                {
                    strips[part.name] = part;
                }
            }

            foreach (KeyValuePair<string, Transform> strip in strips)
            {
                int cut = strip.Key.LastIndexOf('_');
                if (int.TryParse(strip.Key.Substring(cut + 1), out int place) && place > 0 && strips.TryGetValue(strip.Key.Substring(0, cut + 1) + (place - 1), out Transform before))
                {
                    strip.Value.SetParent(before, true);
                }
            }

            foreach (Transform part in parts)
            {
                int mark = part.name.IndexOf("__");
                if (mark < 0)
                {
                    continue;
                }

                string[] words = part.name.Substring(mark + 2).Split('_');
                float number = 0f;
                bool hasNumber = words.Length > 1 && float.TryParse(words[words.Length - 1], out number);
                string kind = words[0];

                if (kind == "Smoke")
                {
                    _smoke.Add(part);
                }
                else if (kind == "Dust")
                {
                    _dust.Add(part);
                }
                else if (kind.StartsWith("Spin") && kind.Length == 5)
                {
                    // The script names the axis as Blender has it: its x is x here, its y runs along z, its z is up.
                    Vector3 axis = kind[4] == 'X' ? Vector3.right : kind[4] == 'Y' ? Vector3.forward : Vector3.up;
                    Add(part, Kind.Spin, axis, hasNumber ? number : 90f);
                }
                else if (System.Enum.TryParse(kind, out Kind parsed) && parsed != Kind.Spin)
                {
                    Add(part, parsed, Vector3.up, hasNumber ? number : 0f);
                }
            }

            enabled = _movers.Count > 0 || _smoke.Count > 0;
        }

        private void Add(Transform part, Kind kind, Vector3 axis, float number)
        {
            // From the model's frame into the part's own, and into its parent's.
            Quaternion toPart = Quaternion.Inverse(part.rotation) * _frame.rotation;
            Quaternion toParent = Quaternion.Inverse(part.parent.rotation) * _frame.rotation;
            _movers.Add(new Mover
            {
                Transform = part,
                Kind = kind,
                Axis = toPart * axis,
                Side = toPart * Vector3.right,
                Up = toParent * Vector3.up,
                Number = number,
                Phase = _phase + _movers.Count * 1.7f,
                RestPosition = part.localPosition,
                RestRotation = part.localRotation,
            });
        }

        private void Update()
        {
            float time = Time.time;
            float deltaTime = Time.deltaTime;

            foreach (Mover mover in _movers)
            {
                if (mover.Transform == null || !mover.Transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float t = time + mover.Phase;
                switch (mover.Kind)
                {
                    case Kind.Spin:
                        mover.Transform.localRotation = mover.RestRotation * Quaternion.AngleAxis(t * mover.Number % 360f, mover.Axis);
                        break;
                    case Kind.Flag:
                        // A wave runs down the cloth from the pole: each strip follows the one before it, a little later and a little wider.
                        float strip = mover.Number;
                        float wave = Mathf.Sin(t * 6.5f - strip * 1.15f) * (9f + strip * 7f) + Mathf.Sin(t * 2.3f - strip * 0.6f) * 6f;
                        mover.Transform.localRotation = mover.RestRotation * Quaternion.AngleAxis(wave, mover.Axis);
                        break;
                    case Kind.Pound:
                        Pound(mover, t);
                        break;
                    case Kind.Bob:
                        mover.Transform.localPosition = mover.RestPosition + mover.Up * (Mathf.Sin(t * 1.6f) * 0.03f);
                        mover.Transform.localRotation = mover.RestRotation * Quaternion.AngleAxis(t * 25f % 360f, mover.Axis);
                        break;
                    case Kind.Sway:
                        // The wind comes and goes: a slow swing with a quicker one on top.
                        mover.Transform.localRotation = mover.RestRotation * Quaternion.AngleAxis(Mathf.Sin(t * 0.7f) * 32f + Mathf.Sin(t * 2.9f) * 7f, mover.Axis);
                        break;
                    case Kind.Swing:
                        mover.Transform.localRotation = mover.RestRotation * Quaternion.AngleAxis(Mathf.Sin(t * 1.4f) * 5f, mover.Side) * Quaternion.AngleAxis(Mathf.Sin(t * 1.1f + 1f) * 3f, Vector3.Cross(mover.Axis, mover.Side));
                        break;
                }
            }

            if (_smoke.Count > 0 && time >= _nextSmoke)
            {
                _nextSmoke = time + SmokeEvery * Random.Range(0.8f, 1.3f);
                Transform from = _smoke[Random.Range(0, _smoke.Count)];
                if (from != null && from.gameObject.activeInHierarchy)
                {
                    Emit(from.position, new Vector3(Random.Range(0.02f, 0.08f), Random.Range(0.16f, 0.24f), Random.Range(-0.03f, 0.03f)), SmokeColor * Random.Range(0.85f, 1.05f), Random.Range(0.05f, 0.085f), Random.Range(1.3f, 1.9f));
                }
            }

            StepPuffs(deltaTime);
        }

        /// <summary>The stamp is lifted slowly, hangs a moment and falls. Dust jumps where it lands.</summary>
        private void Pound(Mover mover, float t)
        {
            float cycle = t / PoundSeconds % 1f;
            float lift;
            if (cycle < 0.62f)
            {
                lift = Mathf.SmoothStep(0f, 1f, cycle / 0.62f);
            }
            else if (cycle < 0.7f)
            {
                lift = 1f;
            }
            else
            {
                float fall = (cycle - 0.7f) / 0.08f;
                lift = Mathf.Max(0f, 1f - fall * fall);
            }

            mover.Transform.localPosition = mover.RestPosition + mover.Up * (lift * PoundLift);

            bool isDown = cycle >= 0.78f;
            if (isDown && !mover.WasDown)
            {
                foreach (Transform dust in _dust)
                {
                    if (dust == null || !dust.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    for (int index = 0; index < 4; index += 1)
                    {
                        float angle = index * Mathf.PI * 0.5f + Random.Range(-0.4f, 0.4f);
                        Emit(dust.position, new Vector3(Mathf.Cos(angle) * 0.22f, Random.Range(0.05f, 0.16f), Mathf.Sin(angle) * 0.22f), DustColor * Random.Range(0.85f, 1.1f), Random.Range(0.05f, 0.08f), Random.Range(0.4f, 0.6f));
                    }
                }
            }

            mover.WasDown = isDown;
        }

        private void Emit(Vector3 at, Vector3 velocity, Color color, float size, float life)
        {
            if (_puffs.Count >= MaxPuffs || _material == null)
            {
                return;
            }

            // A puff does not turn or grow with the part it came from: it hangs under the building itself.
            Transform puff = Props.Part(transform, Props.Sphere, _material, transform.InverseTransformPoint(at), Vector3.zero, color);
            puff.name = "Puff";
            MeshRenderer puffRenderer = puff.GetComponent<MeshRenderer>();
            puffRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _puffs.Add(new Puff { Transform = puff, Velocity = velocity, Life = life, Size = size });
        }

        private void StepPuffs(float deltaTime)
        {
            for (int index = _puffs.Count - 1; index >= 0; index -= 1)
            {
                Puff puff = _puffs[index];
                puff.Age += deltaTime;
                float t = puff.Age / puff.Life;
                if (t >= 1f || puff.Transform == null)
                {
                    if (puff.Transform != null)
                    {
                        Destroy(puff.Transform.gameObject);
                    }

                    _puffs.RemoveAt(index);

                    continue;
                }

                // The material is opaque, so a puff swells and then thins away by shrinking.
                float swell = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f * 0.85f;
                puff.Transform.localPosition += puff.Velocity * deltaTime;
                puff.Transform.localScale = Vector3.one * puff.Size * (0.6f + t) * swell;
            }
        }
    }
}
