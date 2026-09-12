using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The island on screen. It spawns one <see cref="HexTileView"/> per board
/// coordinate and keeps every tile in step with the game by listening to the
/// board events.
/// </summary>
/// <remarks>
/// Attach the view to a game before the game is started, so the opening board
/// is drawn and no later event is missed.
/// </remarks>
public sealed class HexBoardView : MonoBehaviour
{
    /// <summary>The parent of every spawned tile.</summary>
    [SerializeField]
    private Transform _tileRoot;

    /// <summary>The prefabs and materials the tiles use.</summary>
    [SerializeField]
    private HexworldViewLibrary _library = new HexworldViewLibrary();

    /// <summary>Tile views by coordinate.</summary>
    private readonly Dictionary<HexCoord, HexTileView> _views = new Dictionary<HexCoord, HexTileView>();

    /// <summary>The game the view follows, or null.</summary>
    private HexworldGame _game;

    /// <summary>The prefabs and materials the tiles use.</summary>
    public HexworldViewLibrary Library
    {
        get { return _library; }
    }

    /// <summary>The game the view follows, or null.</summary>
    public HexworldGame Game
    {
        get { return _game; }
    }

    /// <summary>How many tile views the board holds.</summary>
    public int TileViewCount
    {
        get { return _views.Count; }
    }

    /// <summary>Every tile view, in no particular order.</summary>
    public IEnumerable<HexTileView> TileViews
    {
        get { return _views.Values; }
    }

    /// <summary>The world position the camera should look at.</summary>
    public Vector3 BoardCenter
    {
        get { return _tileRoot == null ? transform.position : _tileRoot.position; }
    }

    /// <summary>How far the island reaches from its centre, in world units.</summary>
    public float BoardWorldRadius
    {
        get
        {
            int radius = _game == null ? 3 : _game.Board.Radius;
            return (radius * HexLayout.HexRadius * 1.7320508f) + HexLayout.HexRadius;
        }
    }

    /// <summary>
    /// Follows a game: draws its board and subscribes to the board events. A
    /// previously followed game is dropped first.
    /// </summary>
    /// <param name="game">The game to follow.</param>
    public void Attach(HexworldGame game)
    {
        Detach();

        _game = game;
        if (_game == null)
        {
            return;
        }

        Subscribe();
        Rebuild();
    }

    /// <summary>
    /// Stops following the current game and clears the board.
    /// </summary>
    public void Detach()
    {
        if (_game != null)
        {
            Unsubscribe();
            _game = null;
        }

        Clear();
    }

    /// <summary>
    /// Throws every tile away and draws the board of the followed game again.
    /// </summary>
    public void Rebuild()
    {
        Clear();

        if (_game == null)
        {
            return;
        }

        Transform parent = EnsureTileRoot();

        IReadOnlyList<HexTile> tiles = _game.Board.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            HexTile tile = tiles[i];
            var go = new GameObject(string.Format("Tile_{0}_{1}", tile.Coord.Q, tile.Coord.R));
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<HexTileView>();
            view.Initialize(tile.Coord, _library);
            view.SetTerrain(tile.Terrain, true);
            view.SetOwner(tile.Owner);
            view.SetBuilding(tile.HasBuilding ? tile.Building.Type : HexBuildingType.None, false);
            if (tile.IsVoided)
            {
                view.PlayVoided();
            }

            _views[tile.Coord] = view;
        }
    }

    /// <summary>
    /// Returns the view of one tile.
    /// </summary>
    /// <param name="coord">The coordinate to look up.</param>
    /// <param name="view">Receives the view, or null.</param>
    /// <returns>True when a view exists for that coordinate.</returns>
    public bool TryGetTileView(HexCoord coord, out HexTileView view)
    {
        return _views.TryGetValue(coord, out view);
    }

    /// <summary>
    /// Returns the view of one tile.
    /// </summary>
    /// <param name="coord">The coordinate to look up.</param>
    /// <returns>The view, or null when the coordinate is off the island.</returns>
    public HexTileView GetTileView(HexCoord coord)
    {
        HexTileView view;
        return _views.TryGetValue(coord, out view) ? view : null;
    }

    /// <summary>
    /// Clears the "legal target" highlight from every tile.
    /// </summary>
    public void ClearValidTargets()
    {
        foreach (HexTileView view in _views.Values)
        {
            view.SetValidTarget(false);
        }
    }

    /// <summary>
    /// Highlights exactly the listed tiles as legal targets.
    /// </summary>
    /// <param name="coords">The tiles the player may act on, or null for none.</param>
    public void SetValidTargets(IEnumerable<HexCoord> coords)
    {
        ClearValidTargets();

        if (coords == null)
        {
            return;
        }

        foreach (HexCoord coord in coords)
        {
            HexTileView view;
            if (_views.TryGetValue(coord, out view))
            {
                view.SetValidTarget(true);
            }
        }
    }

    /// <summary>
    /// Drops the followed game when the object goes away.
    /// </summary>
    private void OnDestroy()
    {
        if (_game != null)
        {
            Unsubscribe();
            _game = null;
        }
    }

    /// <summary>
    /// Removes every spawned tile.
    /// </summary>
    private void Clear()
    {
        _views.Clear();

        Transform parent = _tileRoot;
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;

            // Destroy is deferred to the end of the frame in play mode, so the
            // old tiles are switched off at once to keep them out of raycasts.
            child.SetActive(false);
            HexworldViewUtil.Destroy(child);
        }
    }

    /// <summary>
    /// Returns the parent of the tiles, creating it when the scene has none.
    /// </summary>
    /// <returns>The tile parent transform.</returns>
    private Transform EnsureTileRoot()
    {
        if (_tileRoot != null)
        {
            return _tileRoot;
        }

        var go = new GameObject("Tiles");
        go.transform.SetParent(transform, false);
        _tileRoot = go.transform;
        return _tileRoot;
    }

    /// <summary>
    /// Listens to every game event that changes the board.
    /// </summary>
    private void Subscribe()
    {
        _game.BuildingBuilt += OnBuildingBuilt;
        _game.BuildingDestroyed += OnBuildingDestroyed;
        _game.TileOwnerChanged += OnTileOwnerChanged;
        _game.TileVoided += OnTileVoided;
        _game.TerrainChanged += HandleTerrainChanged;
    }

    /// <summary>
    /// Stops listening to the game events.
    /// </summary>
    private void Unsubscribe()
    {
        _game.BuildingBuilt -= OnBuildingBuilt;
        _game.BuildingDestroyed -= OnBuildingDestroyed;
        _game.TileOwnerChanged -= OnTileOwnerChanged;
        _game.TileVoided -= OnTileVoided;
        _game.TerrainChanged -= HandleTerrainChanged;
    }

    /// <summary>
    /// Shows a freshly built building.
    /// </summary>
    /// <param name="data">What was built and where.</param>
    private void OnBuildingBuilt(HexworldBuildingBuiltEvent data)
    {
        HexTileView view;
        if (_views.TryGetValue(data.Coord, out view))
        {
            view.SetBuilding(data.BuildingType, true);
        }
    }

    /// <summary>
    /// Removes a destroyed building.
    /// </summary>
    /// <param name="data">What was destroyed and where.</param>
    private void OnBuildingDestroyed(HexworldBuildingDestroyedEvent data)
    {
        HexTileView view;
        if (_views.TryGetValue(data.Coord, out view))
        {
            view.RemoveBuilding(true);
        }
    }

    /// <summary>
    /// Repaints the ownership rim of a tile that changed hands.
    /// </summary>
    /// <param name="data">Which tile changed and to whom.</param>
    private void OnTileOwnerChanged(HexworldTileOwnerChangedEvent data)
    {
        HexTileView view;
        if (_views.TryGetValue(data.Coord, out view))
        {
            view.SetOwner(data.NewOwner);
        }
    }

    /// <summary>
    /// Drops a tile that fell into the Ether.
    /// </summary>
    /// <param name="data">Which tile was dropped.</param>
    private void OnTileVoided(HexworldTileVoidedEvent data)
    {
        HexTileView view;
        if (_views.TryGetValue(data.Coord, out view))
        {
            view.PlayVoided();
        }
    }

    /// <summary>
    /// Swaps the terrain art of a tile, which happens when rubble is cleared.
    /// </summary>
    /// <param name="data">Which tile changed and to what.</param>
    private void HandleTerrainChanged(HexworldTerrainChangedEvent data)
    {
        HexTileView view;
        if (_views.TryGetValue(data.Coord, out view))
        {
            view.SetTerrain(data.NewTerrain, false);
        }
    }
}
