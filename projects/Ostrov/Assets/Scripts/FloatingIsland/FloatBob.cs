using UnityEngine;

/// <summary>
/// Makes an object bob up and down and sway a little, like a rock that floats in the air.
/// </summary>
/// <remarks>
/// The motion is relative to the local pose that the object has when it starts.
/// The component only changes the local position and the local rotation, so a
/// parent can still move the object.
/// </remarks>
public sealed class FloatBob : MonoBehaviour
{
    /// <summary>How far the object moves up and down, in metres.</summary>
    [SerializeField]
    [Min(0f)]
    private float _amplitude = 0.25f;

    /// <summary>The time of one full bob, in seconds.</summary>
    [SerializeField]
    [Min(0.1f)]
    private float _period = 7f;

    /// <summary>The largest tilt of the sway, in degrees.</summary>
    [SerializeField]
    [Min(0f)]
    private float _swayAngle = 0.6f;

    /// <summary>A time offset, so that several objects do not move together.</summary>
    [SerializeField]
    private float _phase;

    /// <summary>The local position at start.</summary>
    private Vector3 _basePosition;

    /// <summary>The local rotation at start.</summary>
    private Quaternion _baseRotation;

    /// <summary>A time offset, so that several objects do not move together.</summary>
    public float Phase
    {
        get => _phase;
        set => _phase = value;
    }

    /// <summary>Sets the size and speed of the motion.</summary>
    public void Configure(float amplitude, float period, float swayAngle, float phase)
    {
        _amplitude = amplitude;
        _period = Mathf.Max(0.1f, period);
        _swayAngle = swayAngle;
        _phase = phase;
    }

    private void Awake()
    {
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
    }

    private void Update()
    {
        float t = (Time.time + _phase) * Mathf.PI * 2f / _period;
        transform.localPosition = _basePosition + (Vector3.up * (Mathf.Sin(t) * _amplitude));
        float swayX = Mathf.Sin(t * 0.73f + 1.3f) * _swayAngle;
        float swayZ = Mathf.Cos(t * 0.51f) * _swayAngle;
        transform.localRotation = _baseRotation * Quaternion.Euler(swayX, 0f, swayZ);
    }
}
