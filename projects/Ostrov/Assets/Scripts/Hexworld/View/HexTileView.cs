using UnityEngine;

/// <summary>
/// One island tile on screen. It owns the terrain art, the coloured ownership
/// rim, the highlight rim and the building standing on it.
/// </summary>
/// <remarks>
/// The view never reads the game by itself. <see cref="HexBoardView"/> pushes
/// every change into it, either while building the board or from a game event.
/// </remarks>
public sealed class HexTileView : MonoBehaviour
{
    /// <summary>How far above the tile top the ownership band lies.</summary>
    private const float OwnerRimHeight = 0.015f;

    /// <summary>How far above the tile top the highlight band lies.</summary>
    private const float HighlightRimHeight = 0.025f;

    /// <summary>How high a hovered tile rises.</summary>
    private const float HoverLift = 0.08f;

    /// <summary>How high a selected tile rises.</summary>
    private const float SelectionLift = 0.16f;

    /// <summary>How fast the tile reaches its target height.</summary>
    private const float LiftSpeed = 12f;

    /// <summary>How fast a voided tile falls into the Ether.</summary>
    private const float VoidFallSpeed = 4f;

    /// <summary>How deep a voided tile falls before it is hidden.</summary>
    private const float VoidFallDepth = 6f;

    /// <summary>The transform that carries the art and moves on hover.</summary>
    private Transform _visualRoot;

    /// <summary>The spawned terrain art.</summary>
    private GameObject _terrainInstance;

    /// <summary>Renderer of the coloured ownership rim.</summary>
    private MeshRenderer _ownerRim;

    /// <summary>Renderer of the highlight rim.</summary>
    private MeshRenderer _highlightRim;

    /// <summary>The building standing here, or null.</summary>
    private HexBuildingView _building;

    /// <summary>The shared prefabs and materials.</summary>
    private HexworldViewLibrary _library;

    /// <summary>How high the tile currently stands.</summary>
    private float _currentLift;

    /// <summary>True once the tile dropped into the Ether.</summary>
    private bool _voided;

    /// <summary>How far the voided tile has already fallen.</summary>
    private float _voidFall;

    /// <summary>Where the tile sits on the island.</summary>
    public HexCoord Coord { get; private set; }

    /// <summary>What the tile currently shows.</summary>
    public HexTerrainType Terrain { get; private set; }

    /// <summary>Who owns the tile, or <see cref="HexworldConfig.NeutralOwner"/>.</summary>
    public int Owner { get; private set; }

    /// <summary>True while the cursor rests on this tile.</summary>
    public bool IsHovered { get; private set; }

    /// <summary>True while this tile is the selected one.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>True while this tile is offered as a legal target.</summary>
    public bool IsValidTarget { get; private set; }

    /// <summary>True once the tile dropped into the Ether.</summary>
    public bool IsVoided
    {
        get { return _voided; }
    }

    /// <summary>Which building stands here, or <see cref="HexBuildingType.None"/>.</summary>
    public HexBuildingType BuildingType
    {
        get { return _building == null ? HexBuildingType.None : _building.BuildingType; }
    }

    /// <summary>
    /// Builds the tile: places it in the world, spawns the terrain art and the
    /// two rims. Call it once, right after the component is added.
    /// </summary>
    /// <param name="coord">Where the tile sits.</param>
    /// <param name="library">The shared prefabs and materials.</param>
    public void Initialize(HexCoord coord, HexworldViewLibrary library)
    {
        Coord = coord;
        _library = library;
        Owner = HexworldConfig.NeutralOwner;

        transform.localPosition = HexLayout.ToWorld(coord);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        var visual = new GameObject("Visual");
        _visualRoot = visual.transform;
        _visualRoot.SetParent(transform, false);

        _ownerRim = CreateRim("OwnerRim", HexworldViewMeshes.OwnerRing, OwnerRimHeight);
        _highlightRim = CreateRim("HighlightRim", HexworldViewMeshes.HighlightRing, HighlightRimHeight);
        if (_highlightRim != null && library != null)
        {
            _highlightRim.sharedMaterial = library.HighlightMaterial;
        }

        _ownerRim.enabled = false;
        _highlightRim.enabled = false;

        Terrain = HexTerrainType.Grass;
        SpawnTerrain(Terrain);
    }

    /// <summary>
    /// Shows a terrain kind. The art is respawned only when the terrain really
    /// changed, which is what clearing rubble does.
    /// </summary>
    /// <param name="terrain">The terrain to show.</param>
    /// <param name="force">True to respawn the art even when nothing changed.</param>
    public void SetTerrain(HexTerrainType terrain, bool force)
    {
        if (!force && _terrainInstance != null && Terrain == terrain)
        {
            return;
        }

        Terrain = terrain;
        SpawnTerrain(terrain);
    }

    /// <summary>
    /// Paints the ownership rim. A neutral tile hides it.
    /// </summary>
    /// <param name="owner">Player index, or <see cref="HexworldConfig.NeutralOwner"/>.</param>
    public void SetOwner(int owner)
    {
        Owner = owner;

        if (_ownerRim == null)
        {
            return;
        }

        Material material = _library == null ? null : _library.GetOwnerMaterial(owner);
        if (material == null)
        {
            _ownerRim.enabled = false;
            return;
        }

        _ownerRim.sharedMaterial = material;
        _ownerRim.enabled = !_voided;
    }

    /// <summary>
    /// Puts a building on the tile. An existing one is removed first.
    /// </summary>
    /// <param name="type">Which building to show, or <see cref="HexBuildingType.None"/> for none.</param>
    /// <param name="animate">True to grow the new building in.</param>
    public void SetBuilding(HexBuildingType type, bool animate)
    {
        if (_building != null && _building.BuildingType == type && !_building.IsDying)
        {
            return;
        }

        RemoveBuilding(false);

        if (type == HexBuildingType.None || _library == null)
        {
            return;
        }

        var holder = new GameObject("Building");
        holder.transform.SetParent(_visualRoot, false);
        var view = holder.AddComponent<HexBuildingView>();
        view.Initialize(type, _library.GetBuildingPrefab(type), animate);
        _building = view;
    }

    /// <summary>
    /// Takes the building off the tile.
    /// </summary>
    /// <param name="animate">True to shrink it away instead of removing it at once.</param>
    public void RemoveBuilding(bool animate)
    {
        if (_building == null)
        {
            return;
        }

        if (animate)
        {
            _building.PlayDestroy();
        }
        else
        {
            HexworldViewUtil.Destroy(_building.gameObject);
        }

        _building = null;
    }

    /// <summary>
    /// Marks the tile as the one under the cursor.
    /// </summary>
    /// <param name="hovered">True when the cursor rests on it.</param>
    public void SetHovered(bool hovered)
    {
        IsHovered = hovered;
        RefreshHighlight();
    }

    /// <summary>
    /// Marks the tile as the selected one.
    /// </summary>
    /// <param name="selected">True when it is selected.</param>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        RefreshHighlight();
    }

    /// <summary>
    /// Marks the tile as a legal target of the running action.
    /// </summary>
    /// <param name="validTarget">True when the player may act on it.</param>
    public void SetValidTarget(bool validTarget)
    {
        IsValidTarget = validTarget;
        RefreshHighlight();
    }

    /// <summary>
    /// Drops the tile into the Ether. It falls away and stops taking clicks.
    /// </summary>
    public void PlayVoided()
    {
        if (_voided)
        {
            return;
        }

        _voided = true;
        RemoveBuilding(true);
        SetHovered(false);
        SetSelected(false);
        SetValidTarget(false);

        if (_ownerRim != null)
        {
            _ownerRim.enabled = false;
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        if (!Application.isPlaying)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Moves the tile toward its target height and drops voided tiles away.
    /// </summary>
    private void Update()
    {
        if (_visualRoot == null)
        {
            return;
        }

        if (_voided)
        {
            _voidFall += Time.deltaTime * VoidFallSpeed;
            _visualRoot.localPosition = new Vector3(0f, -_voidFall, 0f);
            if (_voidFall >= VoidFallDepth)
            {
                gameObject.SetActive(false);
            }

            return;
        }

        float target = TargetLift();
        if (Mathf.Abs(target - _currentLift) < 0.0005f)
        {
            return;
        }

        _currentLift = Mathf.Lerp(_currentLift, target, Mathf.Clamp01(Time.deltaTime * LiftSpeed));
        _visualRoot.localPosition = new Vector3(0f, _currentLift, 0f);
    }

    /// <summary>
    /// Returns how high the tile should stand right now.
    /// </summary>
    /// <returns>The target height above the board plane.</returns>
    private float TargetLift()
    {
        if (IsSelected)
        {
            return SelectionLift;
        }

        if (IsHovered)
        {
            return HoverLift;
        }

        return 0f;
    }

    /// <summary>
    /// Shows the highlight rim when the tile is hovered, selected or offered.
    /// </summary>
    private void RefreshHighlight()
    {
        if (_highlightRim == null)
        {
            return;
        }

        _highlightRim.enabled = !_voided && (IsHovered || IsSelected || IsValidTarget);

        if (!Application.isPlaying && _visualRoot != null)
        {
            _currentLift = TargetLift();
            _visualRoot.localPosition = new Vector3(0f, _currentLift, 0f);
        }
    }

    /// <summary>
    /// Replaces the terrain art with the prefab of a terrain kind.
    /// </summary>
    /// <param name="terrain">The terrain to show.</param>
    private void SpawnTerrain(HexTerrainType terrain)
    {
        if (_terrainInstance != null)
        {
            HexworldViewUtil.Destroy(_terrainInstance);
            _terrainInstance = null;
        }

        GameObject prefab = _library == null ? null : _library.GetTilePrefab(terrain);
        if (prefab == null)
        {
            return;
        }

        _terrainInstance = Instantiate(prefab, _visualRoot);
        _terrainInstance.name = prefab.name;
        _terrainInstance.transform.localPosition = Vector3.zero;
        _terrainInstance.transform.localRotation = Quaternion.identity;
        _terrainInstance.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Creates one flat band lying on the top face of the tile.
    /// </summary>
    /// <param name="name">Name of the new object.</param>
    /// <param name="mesh">The ring mesh to show.</param>
    /// <param name="height">How far above the tile top it lies.</param>
    /// <returns>The renderer of the new band.</returns>
    private MeshRenderer CreateRim(string name, Mesh mesh, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_visualRoot, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }
}
