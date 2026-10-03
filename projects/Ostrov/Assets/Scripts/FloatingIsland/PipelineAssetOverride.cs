using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Switches the project to a level-specific render pipeline asset while this component is active.
/// </summary>
/// <remarks>
/// The floating island level needs a longer shadow distance and stronger
/// ambient occlusion than the rest of the project. The level keeps those
/// settings in its own pipeline asset. This component puts that asset in place
/// on enable and puts the previous asset back on disable. The override only
/// works in play mode and in builds. The editor scene view keeps the project asset.
/// </remarks>
public sealed class PipelineAssetOverride : MonoBehaviour
{
    /// <summary>The pipeline asset for this level.</summary>
    [SerializeField]
    private RenderPipelineAsset _pipelineAsset;

    /// <summary>The override that was active before this component took over.</summary>
    private RenderPipelineAsset _previous;

    /// <summary>True while this component holds the override.</summary>
    private bool _applied;

    /// <summary>The pipeline asset for this level.</summary>
    public RenderPipelineAsset PipelineAsset
    {
        get => _pipelineAsset;
        set => _pipelineAsset = value;
    }

    private void OnEnable()
    {
        if (_pipelineAsset == null)
        {
            return;
        }

        _previous = QualitySettings.renderPipeline;
        QualitySettings.renderPipeline = _pipelineAsset;
        _applied = true;
    }

    private void OnDisable()
    {
        if (!_applied)
        {
            return;
        }

        QualitySettings.renderPipeline = _previous;
        _applied = false;
    }
}
