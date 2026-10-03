using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the floating island. The island always drifts slowly in one direction.
/// WASD and the arrow keys add a second movement on top of the drift.
/// </summary>
/// <remarks>
/// The keys move the island relative to the camera. W pushes the island away
/// from the camera along the ground plane, D pushes it to the right of the
/// screen. The movement only changes X and Z, never the height.
/// </remarks>
public sealed class IslandMover : MonoBehaviour
{
    /// <summary>The direction of the constant drift. Only X and Z matter.</summary>
    [Header("Drift")]
    [SerializeField]
    private Vector3 _driftDirection = Vector3.right;

    /// <summary>The speed of the constant drift, in metres per second.</summary>
    [SerializeField]
    [Min(0f)]
    private float _driftSpeed = 0.8f;

    /// <summary>The camera whose view direction the keys follow.</summary>
    [Header("Player control")]
    [SerializeField]
    private Transform _viewTransform;

    /// <summary>The top speed that the keys give, in metres per second.</summary>
    [SerializeField]
    [Min(0f)]
    private float _moveSpeed = 6f;

    /// <summary>How fast the key movement speeds up and slows down, in metres per second squared.</summary>
    [SerializeField]
    [Min(0.01f)]
    private float _acceleration = 9f;

    /// <summary>The current velocity from the keys, without the drift.</summary>
    private Vector3 _controlVelocity;

    /// <summary>The full current velocity: the drift plus the key movement.</summary>
    public Vector3 Velocity => DriftVelocity + _controlVelocity;

    /// <summary>The velocity of the constant drift.</summary>
    public Vector3 DriftVelocity
    {
        get
        {
            Vector3 flat = new Vector3(_driftDirection.x, 0f, _driftDirection.z);
            return flat.sqrMagnitude > 0f ? flat.normalized * _driftSpeed : Vector3.zero;
        }
    }

    /// <summary>The camera whose view direction the keys follow.</summary>
    public Transform ViewTransform
    {
        get => _viewTransform;
        set => _viewTransform = value;
    }

    private void Update()
    {
        Step(Time.deltaTime, ReadInput());
    }

    /// <summary>
    /// Moves the island by one time step. <see cref="Update"/> calls this
    /// every frame with the keyboard input. Tests call it directly.
    /// </summary>
    /// <param name="dt">The time step, in seconds.</param>
    /// <param name="input">
    /// The key input: X is right of the screen, Y is away from the camera.
    /// A length above one is cut to one.
    /// </param>
    public void Step(float dt, Vector2 input)
    {
        Vector3 wanted = ToWorldDirection(Vector2.ClampMagnitude(input, 1f)) * _moveSpeed;
        _controlVelocity = Vector3.MoveTowards(_controlVelocity, wanted, _acceleration * dt);
        transform.position += Velocity * dt;
    }

    /// <summary>Reads WASD and the arrow keys. The length of the result is at most one.</summary>
    private static Vector2 ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            input.x -= 1f;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>
    /// Turns the key input into a direction on the ground plane, relative to the view.
    /// </summary>
    private Vector3 ToWorldDirection(Vector2 input)
    {
        if (input == Vector2.zero)
        {
            return Vector3.zero;
        }

        Transform view = _viewTransform != null ? _viewTransform : (Camera.main != null ? Camera.main.transform : null);
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (view != null)
        {
            // The camera looks down, so its up vector also points away from
            // the camera on the ground. It helps when the camera looks straight down.
            forward = Flatten(view.forward + view.up, Vector3.forward);
            right = Flatten(view.right, Vector3.right);
        }

        return (forward * input.y) + (right * input.x);
    }

    /// <summary>Projects a vector onto the ground plane and normalises it.</summary>
    private static Vector3 Flatten(Vector3 vector, Vector3 fallback)
    {
        vector.y = 0f;
        return vector.sqrMagnitude > 0.0001f ? vector.normalized : fallback;
    }
}
