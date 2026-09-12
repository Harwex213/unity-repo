using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The camera that looks down on the island. The rig object is the point the
/// camera orbits, and the camera itself hangs behind and above it.
/// </summary>
/// <remarks>
/// Mouse wheel zooms, dragging with the right mouse button pans, dragging with
/// the middle mouse button turns the island, and so do the Q and E keys. The
/// pivot cannot leave a circle around the island, so the player never loses the
/// board.
/// </remarks>
public sealed class HexworldCameraRig : MonoBehaviour
{
    /// <summary>The camera that hangs on the rig.</summary>
    [SerializeField]
    private Transform _cameraTransform;

    /// <summary>How steeply the camera looks down, in degrees.</summary>
    [Header("Framing")]
    [SerializeField]
    [Range(20f, 89f)]
    private float _pitch = 55f;

    /// <summary>Which way the camera looks around the island, in degrees.</summary>
    [SerializeField]
    private float _yaw = 30f;

    /// <summary>How far the camera sits from the pivot.</summary>
    [SerializeField]
    private float _distance = 16f;

    /// <summary>The closest the camera may come.</summary>
    [SerializeField]
    private float _minDistance = 8f;

    /// <summary>The furthest the camera may go.</summary>
    [SerializeField]
    private float _maxDistance = 28f;

    /// <summary>How far the pivot may wander from the island centre.</summary>
    [SerializeField]
    private float _panRadius = 7f;

    /// <summary>How much one wheel notch changes the distance.</summary>
    [Header("Controls")]
    [SerializeField]
    private float _zoomStep = 1.6f;

    /// <summary>How far one screen pixel of drag moves the pivot at unit distance.</summary>
    [SerializeField]
    private float _panSensitivity = 0.0022f;

    /// <summary>How many degrees one screen pixel of drag turns the rig.</summary>
    [SerializeField]
    private float _dragRotateSensitivity = 0.25f;

    /// <summary>How many degrees per second the Q and E keys turn the rig.</summary>
    [SerializeField]
    private float _keyRotateSpeed = 80f;

    /// <summary>How fast the rig catches up with the values the player asked for.</summary>
    [SerializeField]
    private float _smoothing = 12f;

    /// <summary>True to read the mouse and the keyboard.</summary>
    [SerializeField]
    private bool _controlsEnabled = true;

    /// <summary>The centre of the island the pivot stays close to.</summary>
    private Vector3 _focusCenter;

    /// <summary>The distance the player asked for.</summary>
    private float _targetDistance;

    /// <summary>The angle the player asked for.</summary>
    private float _targetYaw;

    /// <summary>The pivot position the player asked for.</summary>
    private Vector3 _targetPivot;

    /// <summary>Where the mouse was during the previous frame.</summary>
    private Vector2 _lastPointer;

    /// <summary>True once a drag has a previous mouse position to compare with.</summary>
    private bool _dragging;

    /// <summary>The camera that hangs on the rig, or null.</summary>
    public Camera Camera
    {
        get { return _cameraTransform == null ? null : _cameraTransform.GetComponent<Camera>(); }
    }

    /// <summary>True to read the mouse and the keyboard. UI can switch it off.</summary>
    public bool ControlsEnabled
    {
        get { return _controlsEnabled; }
        set { _controlsEnabled = value; }
    }

    /// <summary>Which way the camera looks around the island, in degrees.</summary>
    public float Yaw
    {
        get { return _yaw; }
    }

    /// <summary>How far the camera sits from the pivot.</summary>
    public float Distance
    {
        get { return _distance; }
    }

    /// <summary>
    /// Points the rig at an island of a given size and picks a distance that
    /// fits the whole board on screen.
    /// </summary>
    /// <param name="center">World centre of the island.</param>
    /// <param name="worldRadius">How far the island reaches from its centre.</param>
    public void Frame(Vector3 center, float worldRadius)
    {
        _focusCenter = center;
        _targetPivot = center;
        _targetYaw = _yaw;

        _panRadius = Mathf.Max(2f, worldRadius * 1.1f);
        _minDistance = Mathf.Max(3f, worldRadius * 1.1f);
        _maxDistance = Mathf.Max(_minDistance + 4f, worldRadius * 4f);
        _targetDistance = Mathf.Clamp(worldRadius * 2.3f, _minDistance, _maxDistance);

        ApplyImmediate();
    }

    /// <summary>
    /// Snaps the rig and the camera to the values the player asked for, without
    /// any smoothing. The editor scripts that render the scene use it.
    /// </summary>
    public void ApplyImmediate()
    {
        if (_targetDistance <= 0f)
        {
            _targetDistance = _distance;
        }

        _distance = _targetDistance;
        _yaw = _targetYaw;
        transform.position = _targetPivot;
        ApplyCamera();
    }

    /// <summary>
    /// Sets up the starting values from what the scene already holds.
    /// </summary>
    private void Awake()
    {
        _focusCenter = transform.position;
        _targetPivot = transform.position;
        _targetDistance = _distance;
        _targetYaw = _yaw;
        ApplyCamera();
    }

    /// <summary>
    /// Reads the mouse and the keyboard once per frame.
    /// </summary>
    private void Update()
    {
        if (_controlsEnabled)
        {
            ReadZoom();
            ReadRotate();
            ReadPan();
        }

        float t = Mathf.Clamp01(Time.deltaTime * _smoothing);
        _distance = Mathf.Lerp(_distance, _targetDistance, t);
        _yaw = Mathf.LerpAngle(_yaw, _targetYaw, t);
        transform.position = Vector3.Lerp(transform.position, _targetPivot, t);
        ApplyCamera();
    }

    /// <summary>
    /// Turns the mouse wheel into a change of distance.
    /// </summary>
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

        float notches = Mathf.Clamp(scroll / 120f, -3f, 3f);
        if (Mathf.Abs(notches) < 0.01f)
        {
            notches = Mathf.Sign(scroll);
        }

        _targetDistance = Mathf.Clamp(_targetDistance - (notches * _zoomStep), _minDistance, _maxDistance);
    }

    /// <summary>
    /// Turns the Q and E keys into a rotation around the island.
    /// </summary>
    private void ReadRotate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float direction = 0f;
        if (keyboard.qKey.isPressed)
        {
            direction -= 1f;
        }

        if (keyboard.eKey.isPressed)
        {
            direction += 1f;
        }

        if (Mathf.Abs(direction) > 0.01f)
        {
            _targetYaw += direction * _keyRotateSpeed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Turns a mouse drag into panning or rotating, depending on the button.
    /// </summary>
    private void ReadPan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        bool pan = mouse.rightButton.isPressed;
        bool rotate = mouse.middleButton.isPressed;

        if (!pan && !rotate)
        {
            _dragging = false;
            return;
        }

        Vector2 pointer = mouse.position.ReadValue();
        if (!_dragging)
        {
            _dragging = true;
            _lastPointer = pointer;
            return;
        }

        Vector2 delta = pointer - _lastPointer;
        _lastPointer = pointer;

        if (rotate)
        {
            _targetYaw += delta.x * _dragRotateSensitivity;
            return;
        }

        Quaternion flat = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 move = flat * new Vector3(-delta.x, 0f, -delta.y);
        _targetPivot += move * (_panSensitivity * _distance);

        Vector3 offset = _targetPivot - _focusCenter;
        offset.y = 0f;
        if (offset.magnitude > _panRadius)
        {
            offset = offset.normalized * _panRadius;
        }

        _targetPivot = _focusCenter + offset;
    }

    /// <summary>
    /// Places the camera behind and above the pivot and points it at the pivot.
    /// </summary>
    private void ApplyCamera()
    {
        if (_cameraTransform == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _cameraTransform.SetPositionAndRotation(
            transform.position - (rotation * Vector3.forward * _distance),
            rotation);
    }
}
