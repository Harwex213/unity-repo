using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Switches the camera controls off while the cursor rests on a panel. Without
/// it a drag that starts on a panel would also spin the island behind it.
/// </summary>
public sealed class HexworldCameraGuard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>The rig whose controls are switched off.</summary>
    [SerializeField]
    private HexworldCameraRig _cameraRig;

    /// <summary>
    /// Switches the camera controls off.
    /// </summary>
    /// <param name="eventData">The pointer that entered the panel.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetControls(false);
    }

    /// <summary>
    /// Switches the camera controls back on.
    /// </summary>
    /// <param name="eventData">The pointer that left the panel.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetControls(true);
    }

    /// <summary>
    /// Gives the controls back when the panel is hidden under the cursor.
    /// </summary>
    private void OnDisable()
    {
        SetControls(true);
    }

    /// <summary>
    /// Writes the flag on the rig.
    /// </summary>
    /// <param name="value">True to let the mouse move the camera.</param>
    private void SetControls(bool value)
    {
        if (_cameraRig != null)
        {
            _cameraRig.ControlsEnabled = value;
        }
    }
}
