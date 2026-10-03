using UnityEngine;

/// <summary>
/// Keeps the background objects around a moving target.
/// </summary>
/// <remarks>
/// The children of this object stay still in the world, so the player sees the
/// island move past them. A child that falls too far behind the target on X or
/// Z jumps to the opposite side of the box. The jump happens far away in the
/// fog, so the player does not notice it. The box is a square around the target
/// with a half size of <see cref="_halfExtent"/>.
/// </remarks>
public sealed class BackdropWrapper : MonoBehaviour
{
    /// <summary>The object that the background surrounds.</summary>
    [SerializeField]
    private Transform _target;

    /// <summary>The half size of the square box around the target, in metres.</summary>
    [SerializeField]
    [Min(1f)]
    private float _halfExtent = 260f;

    /// <summary>The object that the background surrounds.</summary>
    public Transform Target
    {
        get => _target;
        set => _target = value;
    }

    /// <summary>The half size of the square box around the target, in metres.</summary>
    public float HalfExtent
    {
        get => _halfExtent;
        set => _halfExtent = value;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 center = _target.position;
        float size = _halfExtent * 2f;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Vector3 p = child.position;
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            bool moved = false;

            if (dx > _halfExtent)
            {
                p.x -= size;
                moved = true;
            }
            else if (dx < -_halfExtent)
            {
                p.x += size;
                moved = true;
            }

            if (dz > _halfExtent)
            {
                p.z -= size;
                moved = true;
            }
            else if (dz < -_halfExtent)
            {
                p.z += size;
                moved = true;
            }

            if (moved)
            {
                child.position = p;
            }
        }
    }
}
