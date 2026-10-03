using UnityEngine;

/// <summary>
/// Keeps an object above or below a target on the ground plane.
/// The object copies the X and Z of the target plus an offset. The height stays as it is.
/// </summary>
/// <remarks>
/// The mist layers use this component. Their particles live in world space, so
/// the clouds stay behind while the emitter follows the island.
/// </remarks>
public sealed class FollowTargetXZ : MonoBehaviour
{
    /// <summary>The object to follow.</summary>
    [SerializeField]
    private Transform _target;

    /// <summary>The offset from the target on the ground plane.</summary>
    [SerializeField]
    private Vector2 _offset;

    /// <summary>The object to follow.</summary>
    public Transform Target
    {
        get => _target;
        set => _target = value;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 p = _target.position;
        transform.position = new Vector3(p.x + _offset.x, transform.position.y, p.z + _offset.y);
    }
}
