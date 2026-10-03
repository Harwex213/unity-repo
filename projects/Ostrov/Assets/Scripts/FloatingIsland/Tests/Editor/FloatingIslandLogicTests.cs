using NUnit.Framework;
using UnityEngine;

namespace Ostrov.FloatingIsland.Tests
{
    /// <summary>
    /// Steps the island and camera scripts by hand and checks the movement rules:
    /// the zoom and the pan stay inside their limits, the camera follows the
    /// drifting island without lag that grows, and the keys move the island
    /// relative to the camera.
    /// </summary>
    public sealed class FloatingIslandLogicTests
    {
        private const float Dt = 1f / 60f;

        private GameObject _island;
        private GameObject _cameraGo;
        private IslandMover _mover;
        private IsoCameraController _camera;

        [SetUp]
        public void SetUp()
        {
            _island = new GameObject("Island");
            _mover = _island.AddComponent<IslandMover>();

            _cameraGo = new GameObject("Camera");
            _cameraGo.AddComponent<Camera>().fieldOfView = 30f;
            _camera = _cameraGo.AddComponent<IsoCameraController>();
            _camera.Target = _island.transform;
            _camera.Configure(4f, 105f, 40f, 190f, 55f);
            _camera.SnapToTarget();
            _mover.ViewTransform = _cameraGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_island);
            Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void Zoom_StaysInsideLimits()
        {
            for (int i = 0; i < 100; i++)
            {
                _camera.Zoom(3f);
            }

            Assert.AreEqual(_camera.MinDistance, _camera.TargetDistance, 1e-3f);
            for (int i = 0; i < 600; i++)
            {
                _camera.Tick(Dt);
            }

            Assert.AreEqual(_camera.MinDistance, _camera.Distance, 0.05f);

            for (int i = 0; i < 100; i++)
            {
                _camera.Zoom(-3f);
            }

            Assert.AreEqual(_camera.MaxDistance, _camera.TargetDistance, 1e-3f);
        }

        [Test]
        public void Pan_CannotLeaveLimit()
        {
            Vector2[] drags = { new Vector2(500f, 0f), new Vector2(0f, -700f), new Vector2(-400f, 900f), new Vector2(3000f, 3000f) };
            foreach (Vector2 drag in drags)
            {
                for (int i = 0; i < 200; i++)
                {
                    _camera.Pan(drag, 900f);
                    _camera.Tick(Dt);
                    Assert.LessOrEqual(_camera.PanOffset.magnitude, _camera.PanLimit + 1e-3f);
                    Assert.AreEqual(0f, _camera.PanOffset.y, 1e-5f);
                }
            }

            // After the camera settles, the focus is no farther from the island than the limit.
            for (int i = 0; i < 600; i++)
            {
                _camera.Tick(Dt);
            }

            Vector3 flat = _camera.Focus - _island.transform.position;
            flat.y = 0f;
            Assert.LessOrEqual(flat.magnitude, _camera.PanLimit + 0.01f);

            _camera.ResetPan();
            Assert.AreEqual(Vector3.zero, _camera.PanOffset);
        }

        [Test]
        public void Pan_DragRightMovesFocusLeftOnScreen()
        {
            _camera.Pan(new Vector2(100f, 0f), 900f);
            Vector3 screenRight = _cameraGo.transform.right;
            Assert.Less(Vector3.Dot(_camera.PanOffset, screenRight), 0f);
        }

        [Test]
        public void Camera_FollowsDriftingIsland_WithBoundedSmoothLag()
        {
            float previousLag = float.MaxValue;
            Vector3 previousCamera = _cameraGo.transform.position;
            float previousStep = -1f;
            for (int i = 0; i < 60 * 30; i++)
            {
                _mover.Step(Dt, Vector2.zero);
                _camera.Tick(Dt);

                Vector3 cameraStep = _cameraGo.transform.position - previousCamera;
                previousCamera = _cameraGo.transform.position;

                // After the start-up second, the camera moves the same amount every frame: no jitter.
                if (i > 120)
                {
                    Assert.AreEqual(previousStep, cameraStep.magnitude, 1e-4f, $"frame {i}");
                }

                previousStep = cameraStep.magnitude;
                Vector3 wanted = _island.transform.position + (Vector3.up * _camera.FocusHeight);
                previousLag = (_camera.Focus - wanted).magnitude;
            }

            Assert.Greater(_island.transform.position.magnitude, 10f, "the island drifts");
            Assert.Less(previousLag, 0.5f, "the camera keeps up with the drift");
            Vector3 toCamera = _cameraGo.transform.position - _camera.Focus;
            Assert.AreEqual(_camera.Distance, toCamera.magnitude, 1e-3f);
        }

        [Test]
        public void Orbit_PitchStaysInsideLimits()
        {
            _camera.Orbit(0f, 500f);
            Assert.AreEqual(_camera.MaxPitch, _camera.TargetPitch, 1e-4f);
            Settle();
            Assert.AreEqual(_camera.MaxPitch, _camera.Pitch, 0.01f);
            Assert.AreEqual(_camera.MaxPitch, _cameraGo.transform.eulerAngles.x, 0.01f);

            _camera.Orbit(0f, -500f);
            Assert.AreEqual(_camera.MinPitch, _camera.TargetPitch, 1e-4f);
            Settle();
            Assert.AreEqual(_camera.MinPitch, _camera.Pitch, 0.01f);
            Assert.AreEqual(_camera.MinPitch, _cameraGo.transform.eulerAngles.x, 0.01f);
        }

        [Test]
        public void Orbit_YawRotatesAndWraps()
        {
            float startYaw = _camera.Yaw;
            _camera.Orbit(90f, 0f);
            _camera.Tick(Dt);
            float firstStep = Mathf.DeltaAngle(startYaw, _camera.Yaw);
            Assert.Greater(firstStep, 0f, "the yaw starts to turn");
            Assert.Less(firstStep, 90f, "the yaw turns smoothly, not at once");

            Settle();
            Assert.AreEqual(0f, Mathf.DeltaAngle(startYaw + 90f, _camera.Yaw), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(startYaw + 90f, _cameraGo.transform.eulerAngles.y), 0.01f);

            // The camera still looks at the focus from the same distance.
            Vector3 toFocus = _camera.Focus - _cameraGo.transform.position;
            Assert.AreEqual(_camera.Distance, toFocus.magnitude, 1e-3f);
            Assert.Greater(Vector3.Dot(toFocus.normalized, _cameraGo.transform.forward), 0.9999f);

            // Many turns wrap around 360 degrees and take the short way.
            for (int i = 0; i < 10; i++)
            {
                _camera.Orbit(100f, 0f);
            }

            Assert.GreaterOrEqual(_camera.TargetYaw, 0f);
            Assert.Less(_camera.TargetYaw, 360f);
            Settle();
            Assert.AreEqual(0f, Mathf.DeltaAngle(startYaw + 90f + 1000f, _camera.Yaw), 0.01f);

            _camera.ResetOrbit();
            Settle();
            Assert.AreEqual(0f, Mathf.DeltaAngle(startYaw, _camera.Yaw), 0.01f);
            Assert.AreEqual(42f, _camera.Pitch, 0.01f);
        }

        [Test]
        public void Pan_AfterOrbit_StaysCameraRelativeAndInsideLimit()
        {
            _camera.Orbit(137f, 15f);
            Settle();

            _camera.Pan(new Vector2(100f, 0f), 900f);
            Vector3 screenRight = _cameraGo.transform.right;
            Vector3 dir = _camera.PanOffset.normalized;
            Assert.Less(Vector3.Dot(dir, screenRight), -0.999f, "a drag to the right moves the focus to the screen left");

            _camera.ResetPan();
            _camera.Pan(new Vector2(0f, 100f), 900f);
            Vector3 groundForward = Vector3.ProjectOnPlane(_cameraGo.transform.forward, Vector3.up).normalized;
            Assert.Less(Vector3.Dot(_camera.PanOffset.normalized, groundForward), -0.999f, "a drag up moves the focus towards the camera");

            Vector2[] drags = { new Vector2(500f, 0f), new Vector2(0f, -700f), new Vector2(-400f, 900f), new Vector2(3000f, 3000f) };
            foreach (Vector2 drag in drags)
            {
                for (int i = 0; i < 200; i++)
                {
                    _camera.Pan(drag, 900f);
                    _camera.Orbit(3f, 0f);
                    _camera.Tick(Dt);
                    Assert.LessOrEqual(_camera.PanOffset.magnitude, _camera.PanLimit + 1e-3f);
                    Assert.AreEqual(0f, _camera.PanOffset.y, 1e-5f);
                }
            }

            Settle();
            Vector3 flat = _camera.Focus - _island.transform.position;
            flat.y = 0f;
            Assert.LessOrEqual(flat.magnitude, _camera.PanLimit + 0.01f);
        }

        [Test]
        public void Keys_FollowRotatedCamera()
        {
            _camera.Orbit(120f, -10f);
            Settle();

            Vector3 drift = _mover.DriftVelocity;
            Vector3 start = _island.transform.position;
            for (int i = 0; i < 300; i++)
            {
                _mover.Step(Dt, new Vector2(0f, 1f));
            }

            Vector3 moved = _island.transform.position - start - (drift * 300 * Dt);
            Vector3 viewForward = Vector3.ProjectOnPlane(_cameraGo.transform.forward, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(moved.normalized, viewForward), 0.999f, "W moves away from the rotated camera");
            Assert.AreEqual(0f, moved.y, 1e-4f);

            // Turn the camera again: the keys follow the new view at once.
            _camera.Orbit(-200f, 0f);
            Settle();
            for (int i = 0; i < 300; i++)
            {
                _mover.Step(Dt, new Vector2(1f, 0f));
            }

            Vector3 p0 = _island.transform.position;
            _mover.Step(Dt, new Vector2(1f, 0f));
            Vector3 lastStep = _island.transform.position - p0 - (drift * Dt);
            Assert.Greater(Vector3.Dot(lastStep.normalized, _cameraGo.transform.right), 0.999f, "D moves right on the rotated screen");
        }

        /// <summary>Ticks the camera long enough for every smoothing to finish.</summary>
        private void Settle()
        {
            for (int i = 0; i < 600; i++)
            {
                _camera.Tick(Dt);
            }
        }

        [Test]
        public void Keys_MoveIslandRelativeToCamera()
        {
            Vector3 drift = _mover.DriftVelocity;
            Vector3 start = _island.transform.position;
            for (int i = 0; i < 300; i++)
            {
                _mover.Step(Dt, new Vector2(0f, 1f));
            }

            Vector3 moved = _island.transform.position - start - (drift * 300 * Dt);
            Vector3 viewForward = Vector3.ProjectOnPlane(_cameraGo.transform.forward, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(moved.normalized, viewForward), 0.999f, "W moves away from the camera");
            Assert.AreEqual(0f, moved.y, 1e-4f);

            Vector3 before = _island.transform.position;
            for (int i = 0; i < 300; i++)
            {
                _mover.Step(Dt, new Vector2(1f, 0f));
            }

            // The velocity turns from forward to right, so look at the last frames only.
            Vector3 p0 = _island.transform.position;
            _mover.Step(Dt, new Vector2(1f, 0f));
            Vector3 lastStep = _island.transform.position - p0 - (drift * Dt);
            Assert.Greater(Vector3.Dot(lastStep.normalized, _cameraGo.transform.right), 0.999f, "D moves right on screen");
            Assert.AreNotEqual(before, _island.transform.position);
        }
    }
}
