using UnityEngine;

/// <summary>
/// Small helpers the view scripts share.
/// </summary>
public static class HexworldViewUtil
{
    /// <summary>
    /// Destroys an object both in play mode and in the editor. The editor
    /// scripts that build and check the scene run outside play mode, where
    /// <see cref="Object.Destroy"/> does nothing.
    /// </summary>
    /// <param name="target">The object to destroy. Null is ignored.</param>
    public static void Destroy(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
