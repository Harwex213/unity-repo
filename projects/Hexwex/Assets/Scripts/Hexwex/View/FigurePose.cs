using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// Moves a unit's figure. The figures have no bones: what moves on one is a
    /// separate object with its origin on the joint, named by
    /// <c>art-sources/Hexwex/build_units.py</c> as <c>&lt;key&gt;__&lt;part&gt;</c>. This
    /// class finds those parts and turns them: legs swing while the unit walks,
    /// the weapon arm swings when it strikes, wings beat while it flies.
    ///
    /// A figure faces its own +z, so a hanging limb swings forward when it turns
    /// by a negative angle about x. <see cref="Part.Turn"/> hides that: the angles
    /// here are positive forward.
    /// </summary>
    public sealed class FigurePose
    {
        /// <summary>How fast the legs go at a full walk, in radians per second.</summary>
        private const float StepRate = 13f;
        private const float LegSwing = 34f;
        private const float BeastLegSwing = 28f;
        private const float ArmSwing = 20f;
        /// <summary>How far the weapon arm comes up and over in a melee blow.</summary>
        private const float MeleeStrike = 120f;
        /// <summary>How far the arms come up to loose a shot or a spell.</summary>
        private const float ShotRaise = 75f;
        /// <summary>How far a creature with no arms throws itself at its target.</summary>
        private const float BodyLunge = 16f;
        private const float WingBeat = 36f;
        private const float WingRate = 15f;
        private const float StepBob = 0.014f;

        private readonly Transform _model;
        private readonly Part _legL;
        private readonly Part _legR;
        private readonly Part _armL;
        private readonly Part _armR;
        private readonly Part[] _beastLegs = new Part[4];
        private readonly Part _wingL;
        private readonly Part _wingR;
        private readonly Quaternion _modelRest;
        private readonly float _offset;

        private float _phase;
        private float _gait;

        /// <summary>A part and the way it hangs at rest.</summary>
        private readonly struct Part
        {
            public readonly Transform Transform;
            public readonly Quaternion Rest;

            public Part(Transform transform)
            {
                Transform = transform;
                Rest = transform != null ? transform.localRotation : Quaternion.identity;
            }

            /// <summary>Swings the part forward by that many degrees, and rolls it about the figure's length.</summary>
            public void Turn(float forward, float aboutZ = 0f)
            {
                if (Transform != null)
                {
                    Transform.localRotation = Rest * Quaternion.Euler(-forward, 0f, aboutZ);
                }
            }
        }

        /// <param name="model">The body of the figure; its parts are its children.</param>
        /// <param name="seed">Sets each figure a little out of step with the next.</param>
        public FigurePose(Transform model, int seed)
        {
            _model = model;
            _modelRest = model.localRotation;
            _offset = seed * 1.7f;
            _legL = Find("LegL");
            _legR = Find("LegR");
            _armL = Find("ArmL");
            _armR = Find("ArmR");
            _wingL = Find("WingL");
            _wingR = Find("WingR");
            for (int index = 0; index < _beastLegs.Length; index += 1)
            {
                _beastLegs[index] = Find("Leg" + (index + 1));
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
            float step = Mathf.Sin(_phase + _offset) * _gait;
            float blow = strike >= 0f && strike < 1f ? Mathf.Sin(strike * Mathf.PI) : 0f;

            _legL.Turn(step * LegSwing);
            _legR.Turn(-step * LegSwing);
            // A beast trots: the diagonal pairs step together.
            _beastLegs[0].Turn(step * BeastLegSwing);
            _beastLegs[3].Turn(step * BeastLegSwing);
            _beastLegs[1].Turn(-step * BeastLegSwing);
            _beastLegs[2].Turn(-step * BeastLegSwing);

            bool hasArms = _armL.Transform != null || _armR.Transform != null;
            if (isRanged)
            {
                _armL.Turn(-step * ArmSwing * 0.5f + blow * ShotRaise);
                _armR.Turn(step * ArmSwing * 0.3f + blow * ShotRaise * 0.8f);
            }
            else
            {
                _armL.Turn(-step * ArmSwing + blow * MeleeStrike * 0.25f);
                _armR.Turn(step * ArmSwing * 0.4f + blow * MeleeStrike);
            }

            float beat = Mathf.Sin(Time.unscaledTime * WingRate + _offset) * WingBeat;
            _wingL.Turn(0f, beat);
            _wingR.Turn(0f, -beat);

            // A creature with no arms strikes with its whole body.
            float lunge = blow * (hasArms ? BodyLunge * 0.4f : BodyLunge);
            _model.localRotation = Quaternion.Euler(0f, yaw, 0f) * _modelRest * Quaternion.Euler(lunge, 0f, 0f);
            _model.localPosition = Vector3.up * (Mathf.Abs(step) * StepBob);
        }

        private Part Find(string partName)
        {
            foreach (Transform child in _model)
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
