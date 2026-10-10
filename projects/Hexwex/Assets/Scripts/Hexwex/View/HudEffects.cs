using System;
using System.Collections.Generic;
using Hexwex.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hexwex.View
{
    /// <summary>
    /// What moves on the HUD: the plates popping in at the production reveal, the
    /// motes flying from the buildings to the resources and the meter, the reels
    /// of the toxicity slot, and the numbers floating up from a hit in battle.
    ///
    /// None of it changes a result. The session computes everything at once; the
    /// HUD only takes its time to show it, as the prototype's timers do.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>One mote's flight from a building to the HUD.</summary>
        private const float FlightSeconds = 0.7f;
        /// <summary>The gap between the starts of two buildings' motes.</summary>
        private const float FlightStagger = 0.11f;
        /// <summary>A toxicity mote leaves a little after the resource it came with.</summary>
        private const float ToxicityFlightOffset = 0.07f;
        /// <summary>The pause after the last landing, before the phase moves on.</summary>
        private const float AfterFlightsPause = 0.35f;
        /// <summary>How high the arc lifts, as a share of the distance it covers.</summary>
        private const float ArcLiftRatio = 0.45f;
        private const float ArcLiftMax = 240f;
        /// <summary>One plate's pop-in.</summary>
        private const float PlatePopSeconds = 0.26f;
        /// <summary>When each reel of the slot stops, and the pause before the result shows.</summary>
        private static readonly float[] ReelStopSeconds = { 0.9f, 1.3f, 1.7f };
        private const float SlotRevealPause = 0.25f;
        private const float ReelFrameSeconds = 0.07f;
        private const float FloaterSeconds = 0.8f;
        private const int MaxFloaters = 48;

        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Floater> _floaters = new List<Floater>();
        private readonly Dictionary<ResourceId, int> _landed = new Dictionary<ResourceId, int>();
        private readonly Dictionary<ResourceId, VisualElement> _chipByResource = new Dictionary<ResourceId, VisualElement>();

        private VisualElement _effects;
        private VisualElement _meterBar;
        private Action _onFlightsDone;
        private float _flightsDoneAt = -1f;
        private SlotSpin _spinShown;
        private float _spinStart;

        private sealed class Flight
        {
            public VisualElement Element;
            public Vector2 From;
            public Vector2 Control;
            public Vector2 To;
            public float StartAt;
            /// <summary>The resource the mote carries, or <c>null</c> for toxicity on its way to the meter.</summary>
            public ResourceId? Resource;
            public int Amount;
        }

        private sealed class Floater
        {
            public Label Element;
            public Vector3 World;
            public float StartAt;
        }

        /// <summary>
        /// Plays the collection: every payout flies from its building to its place
        /// in the HUD, and the counters grow as the motes land. The session has paid
        /// nothing yet; <paramref name="done"/> is called after the last landing,
        /// and that is where the phase really ends.
        /// </summary>
        public void StartCollection(List<TaxPayout> payouts, Action done)
        {
            EnsureEffectsLayer();
            ClearFlights();
            _onFlightsDone = done;

            Camera worldCamera = _game.WorldCamera;
            Dictionary<string, int> slotByHex = new Dictionary<string, int>();
            float now = Time.unscaledTime;
            float last = now;

            foreach (TaxPayout payout in payouts)
            {
                if (!_game.IslandView.TryGetPlateAnchor(payout.HexId, out Vector3 world))
                {
                    continue;
                }

                // The motes of one building leave together; the buildings go one after another.
                if (!slotByHex.TryGetValue(payout.HexId, out int slot))
                {
                    slot = slotByHex.Count;
                    slotByHex[payout.HexId] = slot;
                }

                Vector2 from = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, worldCamera);
                float startAt = now + slot * FlightStagger;

                if (payout.Amount > 0 && _chipByResource.TryGetValue(payout.Resource, out VisualElement chip))
                {
                    AddFlight(from, chip.worldBound.center, startAt, ResourceTable.Get(payout.Resource).Icon, "+" + payout.Amount, payout.Resource, payout.Amount, false);
                    last = Mathf.Max(last, startAt + FlightSeconds);
                }

                if (payout.Toxicity > 0 && _meterBar != null)
                {
                    // A toxicity mote flies to the meter, so it shows the meter points it adds.
                    AddFlight(from, _meterBar.worldBound.center, startAt + ToxicityFlightOffset, "toxicity", "+" + ToxicSlot.MeterGain(payout.Toxicity), null, 0, true);
                    last = Mathf.Max(last, startAt + ToxicityFlightOffset + FlightSeconds);
                }
            }

            _flightsDoneAt = last + (_flights.Count > 0 ? AfterFlightsPause : 0f);
        }

        /// <summary>A number floating up from a point of the world, as from a hit in battle.</summary>
        public void SpawnFloater(Vector3 world, string text, bool isBad)
        {
            if (_root == null || _floaters.Count >= MaxFloaters)
            {
                return;
            }

            EnsureEffectsLayer();
            Label label = new Label(text);
            label.AddToClassList("floater");
            label.AddToClassList(isBad ? "bad" : "good");
            label.pickingMode = PickingMode.Ignore;
            label.style.display = DisplayStyle.None;
            _effects.Add(label);
            _floaters.Add(new Floater { Element = label, World = world, StartAt = Time.unscaledTime });
        }

        /// <summary>What the motes have already brought, on top of what the session holds.</summary>
        private int Landed(ResourceId resource)
        {
            return _landed.TryGetValue(resource, out int amount) ? amount : 0;
        }

        private void LateUpdate()
        {
            if (_root == null || _root.panel == null)
            {
                return;
            }

            PlacePlates();
            UpdateFlights();
            UpdateFloaters();
        }

        /// <summary>A plate waits for its building's turn in the reveal, then pops in.</summary>
        private void ApplyPlatePop(string hexId, VisualElement plate)
        {
            float age = _game.PlateAge(hexId);
            if (age < 0f)
            {
                plate.style.display = DisplayStyle.None;

                return;
            }

            float size = age >= PlatePopSeconds ? 1f : Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(age / PlatePopSeconds));
            plate.style.scale = new Scale(new Vector3(size, size, 1f));
        }

        private void AddFlight(Vector2 from, Vector2 to, float startAt, string icon, string text, ResourceId? resource, int amount, bool isToxic)
        {
            VisualElement mote = El(isToxic ? "flight toxic" : "flight", _effects);
            mote.pickingMode = PickingMode.Ignore;
            mote.style.display = DisplayStyle.None;
            Icon(icon, "plate-icon", mote).pickingMode = PickingMode.Ignore;
            Text(text, "plate-amount", mote).pickingMode = PickingMode.Ignore;

            // The spec asks for a bezier, so the mote follows a real quadratic curve.
            float lift = Mathf.Min(ArcLiftMax, Vector2.Distance(from, to) * ArcLiftRatio);
            Vector2 control = new Vector2((from.x + to.x) * 0.5f, Mathf.Min(from.y, to.y) - lift);
            _flights.Add(new Flight { Element = mote, From = from, Control = control, To = to, StartAt = startAt, Resource = resource, Amount = amount });
        }

        private void UpdateFlights()
        {
            if (_flightsDoneAt < 0f)
            {
                return;
            }

            float now = Time.unscaledTime;
            bool hasLanded = false;

            for (int index = _flights.Count - 1; index >= 0; index -= 1)
            {
                Flight flight = _flights[index];
                float t = (now - flight.StartAt) / FlightSeconds;
                if (t < 0f)
                {
                    continue;
                }

                if (t >= 1f)
                {
                    if (flight.Resource.HasValue)
                    {
                        _landed[flight.Resource.Value] = Landed(flight.Resource.Value) + flight.Amount;
                        hasLanded = true;
                    }

                    flight.Element.RemoveFromHierarchy();
                    _flights.RemoveAt(index);

                    continue;
                }

                // Slow off the building, fast into the HUD.
                float eased = t * t * (3f - 2f * t);
                float inverse = 1f - eased;
                Vector2 point = inverse * inverse * flight.From + 2f * inverse * eased * flight.Control + eased * eased * flight.To;
                flight.Element.style.display = DisplayStyle.Flex;
                flight.Element.style.left = point.x;
                flight.Element.style.top = point.y;
            }

            if (hasLanded)
            {
                RefreshResources();
            }

            if (now >= _flightsDoneAt && _flights.Count == 0)
            {
                Action done = _onFlightsDone;
                _flightsDoneAt = -1f;
                _onFlightsDone = null;
                _landed.Clear();
                done?.Invoke();
            }
        }

        private void ClearFlights()
        {
            foreach (Flight flight in _flights)
            {
                flight.Element.RemoveFromHierarchy();
            }

            _flights.Clear();
            _landed.Clear();
            _flightsDoneAt = -1f;
        }

        private void UpdateFloaters()
        {
            if (_floaters.Count == 0)
            {
                return;
            }

            Camera worldCamera = _game.WorldCamera;
            float now = Time.unscaledTime;

            for (int index = _floaters.Count - 1; index >= 0; index -= 1)
            {
                Floater floater = _floaters[index];
                float t = (now - floater.StartAt) / FloaterSeconds;
                if (t >= 1f || worldCamera.WorldToViewportPoint(floater.World).z <= 0f)
                {
                    floater.Element.RemoveFromHierarchy();
                    _floaters.RemoveAt(index);

                    continue;
                }

                Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, floater.World, worldCamera);
                floater.Element.style.display = DisplayStyle.Flex;
                floater.Element.style.left = position.x;
                floater.Element.style.top = position.y - 46f * t;
                floater.Element.style.opacity = 1f - t * t;
            }
        }

        /// <summary>
        /// The slot's modal. The reels spin and stop one after another; the result
        /// and the way on show only once the last reel has stopped.
        /// </summary>
        private void AddSpin(VisualElement modal, SlotSpin spin)
        {
            // The modal is rebuilt on every refresh: the spin keeps its own clock.
            if (spin != _spinShown)
            {
                _spinShown = spin;
                _spinStart = Time.unscaledTime;
            }

            Text("Слот токсичности · " + ToxicSlot.GetZone(spin.Level).Label, "panel-subtitle", modal);

            VisualElement reels = El("reels", modal);
            List<VisualElement> frames = new List<VisualElement>();
            List<VisualElement> icons = new List<VisualElement>();
            foreach (SlotSymbol symbol in spin.Reels)
            {
                VisualElement reel = El("reel", reels);
                frames.Add(reel);
                icons.Add(Icon(SymbolIcon(symbol), "reel-icon", reel));
            }

            VisualElement result = El("slot-result", modal);
            bool isGood = spin.Outcome == SlotOutcome.Luck || spin.Outcome == SlotOutcome.Fortune;
            Text(ToxicSlot.OutcomeLabels[spin.Outcome] + ": " + spin.Event.Title, isGood ? "modal-title good" : "modal-title bad", result);
            Text(spin.Event.Text, "panel-text", result);

            foreach (SlotEffectLine line in spin.Lines)
            {
                VisualElement row = El("row", result);
                Icon(line.Icon, "face-icon", row);
                Text(line.Text, line.IsGood ? "face-amount good" : "face-amount bad", row);
            }

            Button next = Btn("Дальше", "end-button", _game.DismissSpin, modal);
            SlotSymbol[] symbols = (SlotSymbol[])Enum.GetValues(typeof(SlotSymbol));
            float lastStop = ReelStopSeconds[ReelStopSeconds.Length - 1];

            void Show()
            {
                float age = Time.unscaledTime - _spinStart;
                for (int index = 0; index < icons.Count; index += 1)
                {
                    bool hasStopped = age >= ReelStopSeconds[Mathf.Min(index, ReelStopSeconds.Length - 1)];
                    // A spinning reel runs through the symbols; each reel starts at its own.
                    SlotSymbol shown = hasStopped ? spin.Reels[index] : symbols[(Mathf.FloorToInt(age / ReelFrameSeconds) + index * 2) % symbols.Length];
                    SetIcon(icons[index], SymbolIcon(shown));
                    frames[index].EnableInClassList("bad", hasStopped && ToxicSlot.IsBad(shown));
                    frames[index].EnableInClassList("spinning", !hasStopped);
                }

                bool isRevealed = age >= lastStop + SlotRevealPause;
                result.style.visibility = isRevealed ? Visibility.Visible : Visibility.Hidden;
                next.SetEnabled(isRevealed);
            }

            Show();
            modal.schedule.Execute(Show).Every(30).Until(() => Time.unscaledTime - _spinStart > lastStop + SlotRevealPause + 0.2f);
        }

        private void SetIcon(VisualElement icon, string iconName)
        {
            if (!_icons.TryGetValue(iconName, out Texture2D texture))
            {
                texture = Resources.Load<Texture2D>("Icons/" + iconName);
                _icons[iconName] = texture;
            }

            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }
        }

        /// <summary>The layer of everything that flies: above the panels, under the modals.</summary>
        private void EnsureEffectsLayer()
        {
            if (_effects != null && _effects.parent == _root)
            {
                return;
            }

            _effects = new VisualElement();
            _effects.AddToClassList("plates");
            _effects.pickingMode = PickingMode.Ignore;
            _root.Insert(_root.IndexOf(_overlay), _effects);
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float shifted = Mathf.Clamp01(t) - 1f;

            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }
    }
}
