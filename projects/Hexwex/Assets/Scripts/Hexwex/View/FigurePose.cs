using System.Collections.Generic;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// Moves a unit's figure. The figures have no bones: what moves on one is a
    /// separate object with its origin on the joint, named by
    /// <c>art-sources/Hexwex/build_units.py</c> as <c>&lt;key&gt;__&lt;part&gt;</c>. This
    /// class finds those parts and turns them.
    ///
    /// A walker swings its thighs and bends its knees, swings its arms against
    /// its legs with the elbows bent, and rises and leans as it goes. A beast
    /// trots on jointed legs. A spider scuttles. A thing in rings crawls: a wave
    /// runs back along it. A flyer beats its wings, hard down and easy up, and
    /// rides up and down on them. Standing still, everything breathes.
    ///
    /// The parts come side by side under the body. A shin is hung on its thigh
    /// here, and a forearm on its upper arm, so that turning one carries the other.
    ///
    /// A figure faces its own +z, so a hanging limb swings forward when it turns
    /// by a negative angle about x. <see cref="Part.Turn"/> hides that: the angles
    /// here are positive forward.
    /// </summary>
    public sealed class FigurePose
    {
        /// <summary>How fast the legs go at a full walk, in radians per second.</summary>
        private const float StepRate = 12f;
        private const float ThighSwing = 32f;
        private const float KneeBend = 52f;
        private const float ArmSwing = 26f;
        private const float ElbowBend = 22f;
        private const float BeastSwing = 30f;
        private const float BeastKnee = 40f;
        /// <summary>How far the weapon arm comes up and over in a melee blow.</summary>
        private const float MeleeStrike = 115f;
        /// <summary>How far the arms come up to loose a shot or a spell.</summary>
        private const float ShotRaise = 72f;
        /// <summary>How far a creature with no arms throws itself at its target.</summary>
        private const float BodyLunge = 16f;
        private const float WingBeat = 40f;
        private const float WingRate = 13f;
        private const float StepBob = 0.016f;
        private const float CrawlRate = 7f;

        private readonly Transform _model;
        private readonly Part _legL;
        private readonly Part _legR;
        private readonly Part _shinL;
        private readonly Part _shinR;
        private readonly Part _armL;
        private readonly Part _armR;
        private readonly Part _foreL;
        private readonly Part _foreR;
        private readonly Part[] _beastLegs = new Part[4];
        private readonly Part[] _shanks = new Part[4];
        private readonly Part _wingL;
        private readonly Part _wingR;
        private readonly List<Part> _rings = new List<Part>();
        private readonly Quaternion _modelRest;
        private readonly float _offset;
        private readonly bool _isSpider;
        private readonly bool _hasJointedBeastLegs;

        private float _phase;
        private float _gait;

        /// <summary>A part and the way it hangs at rest.</summary>
        private readonly struct Part
        {
            public readonly Transform Transform;
            public readonly Quaternion Rest;
            public readonly Vector3 Place;

            public Part(Transform transform)
            {
                Transform = transform;
                Rest = transform != null ? transform.localRotation : Quaternion.identity;
                Place = transform != null ? transform.localPosition : Vector3.zero;
            }

            public bool Exists
            {
                get { return Transform != null; }
            }

            /// <summary>Swings the part forward by that many degrees, rolls it about the figure's length, and turns it about the upright.</summary>
            public void Turn(float forward, float aboutZ = 0f, float aboutY = 0f)
            {
                if (Transform != null)
                {
                    Transform.localRotation = Rest * Quaternion.Euler(-forward, aboutY, aboutZ);
                }
            }
        }

        /// <param name="model">The body of the figure; its parts are its children.</param>
        /// <param name="seed">Sets each figure a little out of step with the next.</param>
        /// <param name="key">What the unit is, as in <c>Units.cs</c>: some creatures move in a way of their own.</param>
        public FigurePose(Transform model, int seed, string key = "")
        {
            _model = model;
            _modelRest = model.localRotation;
            _offset = seed * 1.7f;
            _isSpider = key == "spider";

            // The lower half of a limb is hung on the upper half before either is measured.
            Hang("ShinL", "LegL");
            Hang("ShinR", "LegR");
            Hang("ForeL", "ArmL");
            Hang("ForeR", "ArmR");
            for (int index = 1; index <= 4; index += 1)
            {
                _hasJointedBeastLegs |= Hang("Shank" + index, "Leg" + index);
            }

            _legL = Find("LegL");
            _legR = Find("LegR");
            _shinL = Find("ShinL");
            _shinR = Find("ShinR");
            _armL = Find("ArmL");
            _armR = Find("ArmR");
            _foreL = Find("ForeL");
            _foreR = Find("ForeR");
            _wingL = Find("WingL");
            _wingR = Find("WingR");
            for (int index = 0; index < _beastLegs.Length; index += 1)
            {
                _beastLegs[index] = Find("Leg" + (index + 1));
                _shanks[index] = Find("Shank" + (index + 1));
            }

            for (int index = 1; index <= 8; index += 1)
            {
                Part ring = Find("Seg" + index);
                if (ring.Exists)
                {
                    _rings.Add(ring);
                }
            }
        }

        /// <summary>
        /// Poses the figure for this frame.
        /// </summary>
        /// <param name="stride">0 standing still, 1 at a full walk.</param>
        /// <param name="strike">How far through a blow or a shot the unit is, 0..1, or negative when it is not striking.</param>
        /// <param name="isRanged">The unit shoots: it raises its arms instead of swinging one.</param>
        /// <param name="yaw">Where the figure faces, in degrees.</param>
        public void Apply(float stride, float strike, bool isRanged, float yaw, float deltaTime)
        {
            // The gait fades in and out, so a unit that stops does not freeze mid-step.
            _gait = Mathf.MoveTowards(_gait, stride > 0.05f ? 1f : 0f, deltaTime * 6f);
            _phase += deltaTime * StepRate * Mathf.Max(0.35f, stride) * _gait;
            float at = _phase + _offset;
            float step = Mathf.Sin(at) * _gait;
            float blow = strike >= 0f && strike < 1f ? Mathf.Sin(strike * Mathf.PI) : 0f;
            float breath = Mathf.Sin(Time.unscaledTime * 1.7f + _offset);
            float idle = 1f - _gait;

            float lift = 0f;
            float lean = 0f;
            float twist = 0f;

            if (_legL.Exists || _legR.Exists)
            {
                Walk(at, step, blow, isRanged, breath, idle);
                // He rises over each planted foot, leans into the walk and turns his shoulders with his arms.
                lift = (0.5f - 0.5f * Mathf.Cos(at * 2f)) * StepBob * _gait + breath * 0.002f * idle;
                lean = 5f * _gait;
                twist = step * 5f;
            }
            else if (_rings.Count > 0)
            {
                Crawl(at, idle);
            }
            else if (_beastLegs[0].Exists)
            {
                if (_isSpider)
                {
                    Scuttle(at);
                    lift = Mathf.Abs(Mathf.Sin(at * 2f)) * 0.006f * _gait;
                }
                else
                {
                    Trot(at);
                    lift = Mathf.Abs(step) * StepBob;
                    lean = Mathf.Sin(at * 2f) * 2.5f * _gait;
                }
            }

            if (_wingL.Exists || _wingR.Exists)
            {
                // The down stroke is quick and the up stroke slow, and the body rides against the wings.
                float flap = Time.unscaledTime * WingRate + _offset;
                float beat = Mathf.Sin(flap) + 0.3f * Mathf.Sin(flap * 2f);
                _wingL.Turn(0f, beat * WingBeat - 8f);
                _wingR.Turn(0f, -beat * WingBeat + 8f);
                lift += beat * 0.035f;
                lean += 6f * _gait;
            }

            // A creature with no arms strikes with its whole body.
            bool hasArms = _armL.Exists || _armR.Exists;
            float lunge = blow * (hasArms ? BodyLunge * 0.4f : BodyLunge);
            _model.localRotation = Quaternion.Euler(0f, yaw + twist, 0f) * _modelRest * Quaternion.Euler(lunge + lean, 0f, 0f);
            _model.localPosition = Vector3.up * lift;
        }

        private void Walk(float at, float step, float blow, bool isRanged, float breath, float idle)
        {
            _legL.Turn(step * ThighSwing);
            _legR.Turn(-step * ThighSwing);
            // A knee bends as its leg comes through from behind, and straightens to take the ground.
            _shinL.Turn(-Mathf.Max(0f, Mathf.Cos(at + 0.5f)) * KneeBend * _gait);
            _shinR.Turn(-Mathf.Max(0f, -Mathf.Cos(at + 0.5f)) * KneeBend * _gait);

            float sway = breath * 2f * idle;
            float elbow = ElbowBend * _gait + 6f;
            if (isRanged)
            {
                _armL.Turn(-step * ArmSwing * 0.5f + blow * ShotRaise + sway);
                _armR.Turn(step * ArmSwing * 0.3f + blow * ShotRaise * 0.8f - sway);
                _foreL.Turn(elbow * 0.6f + blow * 12f);
                _foreR.Turn(elbow * 0.6f + blow * 34f);
            }
            else
            {
                // The weapon arm draws back before it comes over: the first third of a blow is the wind-up.
                float windUp = blow > 0f ? Mathf.Clamp01(1f - blow * 3f) * -20f : 0f;
                _armL.Turn(-step * ArmSwing + blow * MeleeStrike * 0.25f + sway);
                _armR.Turn(step * ArmSwing * 0.5f + blow * MeleeStrike + windUp - sway);
                _foreL.Turn(elbow + Mathf.Max(0f, -step) * 14f);
                _foreR.Turn(elbow * 0.7f + Mathf.Max(0f, step) * 10f + blow * 25f);
            }
        }

        /// <summary>A beast trots: the diagonal pairs step together, each leg folding as it comes forward.</summary>
        private void Trot(float at)
        {
            for (int index = 0; index < _beastLegs.Length; index += 1)
            {
                // Front left and back right go together; the other two half a step behind.
                float own = at + (index == 0 || index == 3 ? 0f : Mathf.PI);
                _beastLegs[index].Turn(Mathf.Sin(own) * BeastSwing * _gait);
                if (_hasJointedBeastLegs)
                {
                    _shanks[index].Turn(-Mathf.Max(0f, Mathf.Cos(own + 0.5f)) * BeastKnee * _gait);
                }
            }
        }

        /// <summary>A spider's legs go in two sets, each lifted, carried round and set down while the other pushes.</summary>
        private void Scuttle(float at)
        {
            for (int index = 0; index < _beastLegs.Length; index += 1)
            {
                float own = at * 1.6f + (index == 0 || index == 3 ? 0f : Mathf.PI);
                float side = index % 2 == 0 ? -1f : 1f;
                float carry = Mathf.Sin(own) * 13f * _gait;
                float raise = Mathf.Max(0f, Mathf.Cos(own)) * 9f * _gait;
                _beastLegs[index].Turn(0f, side * raise, carry);
            }
        }

        /// <summary>
        /// A thing in rings crawls: a wave runs back along it. Where the wave is, a
        /// ring draws up short and tall and is carried forward; behind it the ring
        /// lies long and flat. Standing still it only swells and sinks.
        /// </summary>
        private void Crawl(float at, float idle)
        {
            float wave = (_phase * CrawlRate / StepRate + _offset) * (0.4f + 0.6f * _gait) + Time.unscaledTime * 0.9f * idle;
            float strength = 0.35f + 0.65f * _gait;

            foreach (Part ring in _rings)
            {
                // Where the ring lies along the body sets how late the wave reaches it.
                float along = ring.Place.z * 14f;
                float hump = Mathf.Sin(wave * 2f + along);
                float raised = Mathf.Max(0f, hump) * strength;
                ring.Transform.localPosition = ring.Place + new Vector3(0f, raised * 0.028f, hump * 0.018f * strength);
                ring.Transform.localScale = new Vector3(1f + raised * 0.08f, 1f + raised * 0.22f, 1f - raised * 0.16f);
                ring.Transform.localRotation = ring.Rest * Quaternion.Euler(-hump * 9f * strength, Mathf.Sin(wave + along) * 5f * strength, 0f);
            }
        }

        /// <summary>Hangs one part on another, keeping it where it is. Returns <c>true</c> when both are there.</summary>
        private bool Hang(string partName, string onName)
        {
            Transform part = Find(partName).Transform;
            Transform on = Find(onName).Transform;
            if (part == null || on == null)
            {
                return false;
            }

            part.SetParent(on, true);

            return true;
        }

        private Part Find(string partName)
        {
            foreach (Transform child in _model.GetComponentsInChildren<Transform>(true))
            {
                int mark = child.name.LastIndexOf("__");
                if (mark >= 0 && child.name.Substring(mark + 2) == partName)
                {
                    return new Part(child);
                }
            }

            return new Part(null);
        }
    }
}
