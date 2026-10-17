using UnityEngine;
using UnityEngine.InputSystem;

namespace Hexwex.View
{
    /// <summary>
    /// The camera over the island. The prototype pans and zooms a flat layer; in
    /// 3D the camera orbits a point on the ground. Right mouse button turns it,
    /// the wheel zooms, and WASD or the middle button moves the point. Over the
    /// global map the same rig orbits the planet's centre.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Vector3 target = new Vector3(0f, 0.3f, 0f);
        [SerializeField] private float distance = 19f;
        [SerializeField] private float yaw = 20f;
        [SerializeField] private float pitch = 48f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float panLimit = 8f;
        [SerializeField] private float minPitch = 15f;
        [SerializeField] private float maxPitch = 85f;

        /// <summary>The HUD sets this while the pointer is over a panel, so a scroll there does not zoom.</summary>
        public bool PointerOverUi { get; set; }

        /// <summary>
        /// While set, the rig looks at this point and the keys and the middle button
        /// no longer move it: in battle the camera follows the island, and WASD steers the island.
        /// </summary>
        public Vector3? Follow { get; set; }

        /// <summary>While set, the mouse and the keys do not move the camera: the main menu is open.</summary>
        public bool Locked { get; set; }

        /// <summary>Degrees per second the camera turns on its own, as behind the main menu.</summary>
        public float Spin { get; set; }

        /// <summary>The heading of the camera in degrees, for steering relative to the view.</summary>
        public float Yaw
        {
            get { return yaw; }
        }

        /// <summary>Everything the rig is aimed by, so a view can be left and come back to as it was.</summary>
        public struct Orbit
        {
            public Vector3 Target;
            public float Distance;
            public float Yaw;
            public float Pitch;
            public float MinDistance;
            public float MaxDistance;
            public float PanLimit;
            public float MinPitch;
            public float MaxPitch;
        }

        public Orbit Current
        {
            get
            {
                return new Orbit
                {
                    Target = target,
                    Distance = distance,
                    Yaw = yaw,
                    Pitch = pitch,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance,
                    PanLimit = panLimit,
                    MinPitch = minPitch,
                    MaxPitch = maxPitch,
                };
            }
            set
            {
                target = value.Target;
                distance = value.Distance;
                yaw = value.Yaw;
                pitch = value.Pitch;
                minDistance = value.MinDistance;
                maxDistance = value.MaxDistance;
                panLimit = value.PanLimit;
                minPitch = value.MinPitch;
                maxPitch = value.MaxPitch;
            }
        }

        /// <summary>
        /// An orbit around a sphere, seen from above the point of it that lies in
        /// <paramref name="direction"/>. The target does not move: the sphere turns under the camera.
        /// </summary>
        public static Orbit AroundSphere(Vector3 center, Vector3 direction, float radius)
        {
            Vector3 forward = -direction.normalized;

            return new Orbit
            {
                Target = center,
                Distance = radius * 3.1f,
                Yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                Pitch = Mathf.Clamp(Mathf.Asin(-forward.y) * Mathf.Rad2Deg, -80f, 80f),
                MinDistance = radius * 1.6f,
                MaxDistance = radius * 5f,
                PanLimit = 0f,
                MinPitch = -85f,
                MaxPitch = 85f,
            };
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse != null && !Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();

                if (mouse.rightButton.isPressed)
                {
                    yaw += delta.x * 0.25f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.2f, minPitch, maxPitch);
                }

                if (mouse.middleButton.isPressed && !Follow.HasValue)
                {
                    Pan(new Vector2(-delta.x, -delta.y) * distance * 0.0016f);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (!PointerOverUi && Mathf.Abs(scroll) > 0.01f)
                {
                    distance = Mathf.Clamp(distance * (scroll > 0f ? 0.9f : 1.1f), minDistance, maxDistance);
                }
            }

            if (keyboard != null && !Follow.HasValue && !Locked)
            {
                Vector2 move = Vector2.zero;
                move.x += keyboard.dKey.isPressed ? 1f : 0f;
                move.x -= keyboard.aKey.isPressed ? 1f : 0f;
                move.y += keyboard.wKey.isPressed ? 1f : 0f;
                move.y -= keyboard.sKey.isPressed ? 1f : 0f;
                Pan(move * distance * 0.6f * Time.unscaledDeltaTime);
            }

            if (Follow.HasValue)
            {
                target = Follow.Value;
            }

            yaw += Spin * Time.unscaledDeltaTime;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }

        private void Pan(Vector2 move)
        {
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            target += heading * new Vector3(move.x, 0f, move.y);
            target.x = Mathf.Clamp(target.x, -panLimit, panLimit);
            target.z = Mathf.Clamp(target.z, -panLimit, panLimit);
        }
    }
}
