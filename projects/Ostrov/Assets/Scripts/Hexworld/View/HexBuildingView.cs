using UnityEngine;

/// <summary>
/// One building standing on a tile. It spawns the art prefab, grows it in when
/// it is built and shrinks it away when it is destroyed.
/// </summary>
/// <remarks>
/// A building that is going away detaches itself from the tile view first, so
/// the tile can accept a new building while the old one is still animating.
/// </remarks>
public sealed class HexBuildingView : MonoBehaviour
{
    /// <summary>How long the grow-in animation takes, in seconds.</summary>
    private const float SpawnDuration = 0.25f;

    /// <summary>How long the shrink-away animation takes, in seconds.</summary>
    private const float DespawnDuration = 0.3f;

    /// <summary>How deep a destroyed building sinks before it disappears.</summary>
    private const float DespawnSink = 0.35f;

    /// <summary>The spawned art prefab.</summary>
    private Transform _model;

    /// <summary>How long the running animation has been playing, in seconds.</summary>
    private float _elapsed;

    /// <summary>True while the shrink-away animation runs.</summary>
    private bool _dying;

    /// <summary>Which building this view shows.</summary>
    public HexBuildingType BuildingType { get; private set; }

    /// <summary>True once the building started to disappear.</summary>
    public bool IsDying
    {
        get { return _dying; }
    }

    /// <summary>
    /// Spawns the art of a building kind.
    /// </summary>
    /// <param name="type">Which building to show.</param>
    /// <param name="prefab">The art prefab, or null to show nothing.</param>
    /// <param name="animate">True to grow the building in.</param>
    public void Initialize(HexBuildingType type, GameObject prefab, bool animate)
    {
        BuildingType = type;

        // Outside play mode no update loop runs, so an animated building would
        // stay at its starting scale forever.
        _elapsed = animate && Application.isPlaying ? 0f : SpawnDuration;

        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.name = prefab.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        _model = instance.transform;

        ApplySpawnScale();
    }

    /// <summary>
    /// Starts the shrink-away animation. Outside play mode the object is
    /// removed at once because no update loop runs there.
    /// </summary>
    public void PlayDestroy()
    {
        if (_dying)
        {
            return;
        }

        _dying = true;
        _elapsed = 0f;

        if (!Application.isPlaying)
        {
            HexworldViewUtil.Destroy(gameObject);
        }
    }

    /// <summary>
    /// Drives the grow-in and shrink-away animations.
    /// </summary>
    private void Update()
    {
        if (_dying)
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / DespawnDuration);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, 1f - t);
            Vector3 position = transform.localPosition;
            position.y = -DespawnSink * t;
            transform.localPosition = position;

            if (t >= 1f)
            {
                HexworldViewUtil.Destroy(gameObject);
            }

            return;
        }

        if (_elapsed < SpawnDuration)
        {
            _elapsed += Time.deltaTime;
            ApplySpawnScale();
        }
    }

    /// <summary>
    /// Applies the scale of the grow-in animation for the current time.
    /// </summary>
    private void ApplySpawnScale()
    {
        if (_model == null)
        {
            return;
        }

        float t = Mathf.Clamp01(_elapsed / SpawnDuration);
        float eased = 1f - ((1f - t) * (1f - t));
        float scale = Mathf.Lerp(0.05f, 1f, eased);
        _model.localScale = new Vector3(scale, scale, scale);
    }
}
