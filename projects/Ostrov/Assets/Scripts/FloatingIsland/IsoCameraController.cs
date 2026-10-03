using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A near-isometric camera that follows the floating island.
/// </summary>
/// <remarks>
/// The camera looks at a focus point from a pitch and a yaw. The focus point
/// is the island position plus a pan offset. The mouse wheel changes the
/// distance between the camera and the focus point. Dragging with the left or
/// the right mouse button changes the pan offset. Dragging with the middle
/// mouse button (the wheel press) orbits the camera around the focus point:
/// a horizontal drag changes the yaw, a vertical drag changes the pitch. The
/// pitch stays inside a near-isometric range. The pan offset stays inside a
/// circle around the island, so the player cannot lose the island. Space or F
/// resets the pan, the yaw and the pitch.
/// </remarks>
public sealed class IsoCameraController : MonoBehaviour
{
    /// <summary>The island that the camera follows.</summary>
    [SerializeField]
    private Transform _target;

    /// <summary>The height above the island origin that the camera looks at.</summary>
    [SerializeField]
    private float _focusHeight = 1.5f;

    /// <summary>The start pitch: how steeply the camera looks down, in degrees. Reset returns to it.</summary>
    [Header("Framing")]
    [SerializeField]
    [Range(10f, 89f)]
    private float _pitch = 42f;

    /// <summary>The start yaw: the direction the camera looks around the island, in degrees. Reset returns to it.</summary>
    [SerializeField]
    private float _yaw = 45f;

    /// <summary>The smallest pitch that the orbit allows, in degrees.</summary>
    [Header("Orbit")]
    [SerializeField]
    [Range(10f, 89f)]
    private float _minPitch = 25f;

    /// <summary>The largest pitch that the orbit allows, in degrees.</summary>
    [SerializeField]
    [Range(10f, 89f)]
    private float _maxPitch = 65f;

    /// <summary>The orbit angle that one pixel of a middle-button drag gives, in degrees.</summary>
    [SerializeField]
    [Min(0f)]
    private float _orbitSensitivity = 0.25f;

    /// <summary>How fast the yaw and the pitch reach the drag values. Higher is faster.</summary>
    [SerializeField]
    [Min(0.1f)]
    private float _orbitSharpness = 14f;

    /// <summary>The start distance between the camera and the focus point.</summary>
    [Header("Zoom")]
    [SerializeField]
    private float _distance = 46f;

    /// <summary>The smallest distance the wheel allows.</summary>
    [SerializeField]
    private float _minDistance = 18f;

    /// <summary>The largest distance the wheel allows.</summary>
    [SerializeField]
    private float _maxDistance = 85f;

    /// <summary>The part of the current distance that one wheel notch changes.</summary>
    [SerializeField]
    [Range(0.01f, 0.5f)]
    private float _zoomStep = 0.12f;

    /// <summary>How fast the distance reaches the wheel value. Higher is faster.</summary>
    [SerializeField]
    [Min(0.1f)]
    private float _zoomSharpness = 10f;

    /// <summary>The largest distance between the focus point and the island, in metres.</summary>
    [Header("Pan")]
    [SerializeField]
    [Min(0f)]
    private float _panLimit = 22f;

    /// <summary>
    /// A multiplier for the drag speed. At 1 the ground under the cursor
    /// moves with the cursor.
    /// </summary>
    [SerializeField]
    [Min(0f)]
    private float _panSensitivity = 1f;

    /// <summary>How fast the focus point follows the island and the pan. Lower is faster.</summary>
    [Header("Follow")]
    [SerializeField]
    [Min(0f)]
    private float _followSmoothTime = 0.2f;

    /// <summary>The camera component. The controller reads its field of view.</summary>
    private Camera _camera;

    /// <summary>The distance that the wheel asks for.</summary>
    private float _targetDistance;

    /// <summary>The current pan offset from the island on the ground plane.</summary>
    private Vector3 _panOffset;

    /// <summary>The current smoothed focus point.</summary>
    private Vector3 _focus;

    /// <summary>The velocity that SmoothDamp keeps for the focus point.</summary>
    private Vector3 _focusVelocity;

    /// <summary>The pointer position in the previous frame of a drag.</summary>
    private Vector2 _lastPointer;

    /// <summary>True while a pan drag (left or right button) is going on.</summary>
    private bool _dragging;

    /// <summary>The pointer position in the previous frame of an orbit drag.</summary>
    private Vector2 _lastOrbitPointer;

    /// <summary>True while an orbit drag (middle button) is going on.</summary>
    private bool _orbiting;

    /// <summary>True after the current and target angles take the start values.</summary>
    private bool _orbitReady;

    /// <summary>The current smoothed pitch, in degrees.</summary>
    private float _currentPitch;

    /// <summary>The current smoothed yaw, in degrees, in the range [0, 360).</summary>
    private float _currentYaw;

    /// <summary>The pitch that the drag asks for, in degrees.</summary>
    private float _targetPitch;

    /// <summary>The yaw that the drag asks for, in degrees, in the range [0, 360).</summary>
    private float _targetYaw;

    /// <summary>The island that the camera follows.</summary>
    public Transform Target
    {
        get => _target;
        set => _target = value;
    }

    /// <summary>The current pan offset from the island on the ground plane.</summary>
    public Vector3 PanOffset => _panOffset;

    /// <summary>The largest pan offset, in metres.</summary>
    public float PanLimit => _panLimit;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _targetDistance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        _distance = _targetDistance;
        EnsureOrbit();
    }

    private void OnEnable()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        ReadZoom();
        ReadPan();
        ReadOrbit();
        ReadReset();
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Moves the distance and the focus point one step towards their targets
    /// and places the camera. <see cref="LateUpdate"/> calls this every frame
    /// after it reads the input. Tests call it directly.
    /// </summary>
    /// <param name="dt">The time step, in seconds.</param>
    public void Tick(float dt)
    {
        EnsureOrbit();
        float orbitBlend = 1f - Mathf.Exp(-_orbitSharpness * dt);
        _currentYaw = Mathf.Repeat(_currentYaw + (Mathf.DeltaAngle(_currentYaw, _targetYaw) * orbitBlend), 360f);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, orbitBlend);
        _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_zoomSharpness * dt));
        _focus = Vector3.SmoothDamp(_focus, DesiredFocus(), ref _focusVelocity, _followSmoothTime, Mathf.Infinity, dt);
        ApplyPose();
    }

    /// <summary>
    /// Changes the target distance by a number of wheel notches.
    /// A positive value zooms in. The result stays between the zoom limits.
    /// </summary>
    /// <param name="notches">The number of wheel notches.</param>
    public void Zoom(float notches)
    {
        float factor = Mathf.Pow(1f - _zoomStep, notches);
        _targetDistance = Mathf.Clamp(_targetDistance * factor, _minDistance, _maxDistance);
    }

    /// <summary>
    /// Moves the pan offset by a pointer drag. The result stays inside the pan limit.
    /// </summary>
    /// <param name="pixelDelta">The pointer movement, in screen pixels.</param>
    /// <param name="screenHeight">The screen height, in pixels.</param>
    public void Pan(Vector2 pixelDelta, float screenHeight)
    {
        // Metres on the focus plane that one screen pixel covers.
        float fov = _camera != null ? _camera.fieldOfView : 30f;
        float worldPerPixel = 2f * _distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, screenHeight);

        EnsureOrbit();
        Quaternion yawOnly = Quaternion.Euler(0f, _currentYaw, 0f);
        Vector3 right = yawOnly * Vector3.right;
        Vector3 forward = yawOnly * Vector3.forward;

        // Screen Y maps onto the ground stretched by the viewing angle.
        float stretch = 1f / Mathf.Max(0.2f, Mathf.Sin(_currentPitch * Mathf.Deg2Rad));
        Vector3 move = (right * pixelDelta.x) + (forward * (pixelDelta.y * stretch));
        _panOffset -= move * (worldPerPixel * _panSensitivity);
        _panOffset.y = 0f;
        _panOffset = Vector3.ClampMagnitude(_panOffset, _panLimit);
    }

    /// <summary>Puts the focus back on the island.</summary>
    public void ResetPan()
    {
        _panOffset = Vector3.zero;
    }

    /// <summary>
    /// Changes the target yaw and the target pitch. The yaw wraps around 360 degrees.
    /// The pitch stays between the pitch limits. <see cref="Tick"/> moves the
    /// camera towards the new angles.
    /// </summary>
    /// <param name="deltaYaw">The yaw change, in degrees. A positive value turns the camera clockwise seen from above.</param>
    /// <param name="deltaPitch">The pitch change, in degrees. A positive value makes the camera look down more steeply.</param>
    public void Orbit(float deltaYaw, float deltaPitch)
    {
        EnsureOrbit();
        _targetYaw = Mathf.Repeat(_targetYaw + deltaYaw, 360f);
        _targetPitch = ClampPitch(_targetPitch + deltaPitch);
    }

    /// <summary>Puts the yaw and the pitch back to their start values. The camera turns back smoothly.</summary>
    public void ResetOrbit()
    {
        EnsureOrbit();
        _targetYaw = Mathf.Repeat(_yaw, 360f);
        _targetPitch = ClampPitch(_pitch);
    }

    /// <summary>The current smoothed yaw, in degrees, in the range [0, 360).</summary>
    public float Yaw
    {
        get
        {
            EnsureOrbit();
            return _currentYaw;
        }
    }

    /// <summary>The current smoothed pitch, in degrees.</summary>
    public float Pitch
    {
        get
        {
            EnsureOrbit();
            return _currentPitch;
        }
    }

    /// <summary>The yaw that the drag asks for, in degrees, in the range [0, 360).</summary>
    public float TargetYaw
    {
        get
        {
            EnsureOrbit();
            return _targetYaw;
        }
    }

    /// <summary>The pitch that the drag asks for, in degrees.</summary>
    public float TargetPitch
    {
        get
        {
            EnsureOrbit();
            return _targetPitch;
        }
    }

    /// <summary>The smallest pitch that the orbit allows, in degrees.</summary>
    public float MinPitch => _minPitch;

    /// <summary>The largest pitch that the orbit allows, in degrees.</summary>
    public float MaxPitch => _maxPitch;

    /// <summary>The distance that the wheel asks for.</summary>
    public float TargetDistance => _targetDistance;

    /// <summary>The current distance between the camera and the focus point.</summary>
    public float Distance => _distance;

    /// <summary>The smallest distance the wheel allows.</summary>
    public float MinDistance => _minDistance;

    /// <summary>The largest distance the wheel allows.</summary>
    public float MaxDistance => _maxDistance;

    /// <summary>The current smoothed focus point.</summary>
    public Vector3 Focus => _focus;

    /// <summary>The height above the island origin that the camera looks at.</summary>
    public float FocusHeight => _focusHeight;

    /// <summary>
    /// Sets the framing and zoom tunables. The scene builder calls this.
    /// </summary>
    public void Configure(float focusHeight, float distance, float minDistance, float maxDistance, float panLimit)
    {
        _focusHeight = focusHeight;
        _minDistance = Mathf.Max(1f, minDistance);
        _maxDistance = Mathf.Max(_minDistance, maxDistance);
        _distance = Mathf.Clamp(distance, _minDistance, _maxDistance);
        _targetDistance = _distance;
        _panLimit = Mathf.Max(0f, panLimit);
    }

    /// <summary>
    /// Puts the camera at its final pose at once, with no smoothing.
    /// The scene builder calls this so the saved scene already shows the island.
    /// </summary>
    public void SnapToTarget()
    {
        _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        _targetDistance = _distance;
        EnsureOrbit();
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
        _focus = DesiredFocus();
        _focusVelocity = Vector3.zero;
        ApplyPose();
    }

    /// <summary>The rotation from the pitch and the yaw.</summary>
    private Quaternion ViewRotation => Quaternion.Euler(_currentPitch, _currentYaw, 0f);

    /// <summary>
    /// Gives the current and target angles their start values once. Edit-mode
    /// tests do not run <see cref="Awake"/>, so every public entry point calls this.
    /// </summary>
    private void EnsureOrbit()
    {
        if (_orbitReady)
        {
            return;
        }

        _orbitReady = true;
        _targetYaw = Mathf.Repeat(_yaw, 360f);
        _targetPitch = ClampPitch(_pitch);
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
    }

    /// <summary>Keeps a pitch between the pitch limits.</summary>
    private float ClampPitch(float pitch)
    {
        return Mathf.Clamp(pitch, _minPitch, Mathf.Max(_minPitch, _maxPitch));
    }

    /// <summary>The focus point that the camera moves towards.</summary>
    private Vector3 DesiredFocus()
    {
        Vector3 origin = _target != null ? _target.position : Vector3.zero;
        return origin + (Vector3.up * _focusHeight) + _panOffset;
    }

    /// <summary>Places the camera behind the focus point.</summary>
    private void ApplyPose()
    {
        Quaternion rotation = ViewRotation;
        transform.SetPositionAndRotation(_focus - (rotation * Vector3.forward * _distance), rotation);
    }

    /// <summary>Turns the mouse wheel into a new target distance.</summary>
    private void ReadZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        // Some platforms report 120 per notch, others report 1 per notch
        // (and trackpads report fractions of 1). A large value is a 120-based one.
        float notches = Mathf.Abs(scroll) >= 30f ? scroll / 120f : scroll;
        Zoom(Mathf.Clamp(notches, -3f, 3f));
    }

    /// <summary>Turns a mouse drag into a change of the pan offset.</summary>
    private void ReadPan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            _dragging = false;
            return;
        }

        bool held = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
        Vector2 pointer = mouse.position.ReadValue();
        if (!held)
        {
            _dragging = false;
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            _lastPointer = pointer;
            return;
        }

        Vector2 delta = pointer - _lastPointer;
        _lastPointer = pointer;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Pan(delta, Screen.height);
    }

    /// <summary>Turns a middle-button drag into a change of the yaw and the pitch.</summary>
    private void ReadOrbit()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.middleButton.isPressed)
        {
            _orbiting = false;
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (!_orbiting)
        {
            _orbiting = true;
            _lastOrbitPointer = pointer;
            return;
        }

        Vector2 delta = pointer - _lastOrbitPointer;
        _lastOrbitPointer = pointer;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // A drag to the right turns the view to the right. A drag up flattens the view.
        Orbit(delta.x * _orbitSensitivity, -delta.y * _orbitSensitivity);
    }

    /// <summary>Space or F puts the focus back on the island and the camera back to its start angles.</summary>
    private void ReadReset()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
        {
            ResetPan();
            ResetOrbit();
        }
    }

    private void OnValidate()
    {
        _minDistance = Mathf.Max(1f, _minDistance);
        _maxDistance = Mathf.Max(_minDistance, _maxDistance);
        _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        _panLimit = Mathf.Max(0f, _panLimit);
        _maxPitch = Mathf.Max(_minPitch, _maxPitch);
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
    }

    private void OnDrawGizmosSelected()
    {
        if (_target == null)
        {
            return;
        }

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Vector3 center = _target.position + (Vector3.up * _focusHeight);
        const int Segments = 48;
        for (int i = 0; i < Segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / Segments;
            float a1 = (i + 1) * Mathf.PI * 2f / Segments;
            Vector3 p0 = center + (new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * _panLimit);
            Vector3 p1 = center + (new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * _panLimit);
            Gizmos.DrawLine(p0, p1);
        }
    }
}
