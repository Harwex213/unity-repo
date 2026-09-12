using System;
using UnityEngine;

/// <summary>
/// The prefabs and materials the board view spawns. One instance lives on
/// <see cref="HexBoardView"/> and every tile view reads it, so the scene holds
/// the asset references in a single place.
/// </summary>
[Serializable]
public sealed class HexworldViewLibrary
{
    /// <summary>Prefab used for grass tiles.</summary>
    [Header("Tiles")]
    [SerializeField]
    private GameObject _grassTile;

    /// <summary>Prefab used for forest tiles.</summary>
    [SerializeField]
    private GameObject _forestTile;

    /// <summary>Prefab used for stone tiles.</summary>
    [SerializeField]
    private GameObject _stoneTile;

    /// <summary>Prefab used for rubble tiles.</summary>
    [SerializeField]
    private GameObject _rubbleTile;

    /// <summary>Prefab of the Castle.</summary>
    [Header("Buildings")]
    [SerializeField]
    private GameObject _castle;

    /// <summary>Prefab of the Cottage.</summary>
    [SerializeField]
    private GameObject _cottage;

    /// <summary>Prefab of the Farm.</summary>
    [SerializeField]
    private GameObject _farm;

    /// <summary>Prefab of the Quarry.</summary>
    [SerializeField]
    private GameObject _quarry;

    /// <summary>Prefab of the Church.</summary>
    [SerializeField]
    private GameObject _church;

    /// <summary>Prefab of the Barracks.</summary>
    [SerializeField]
    private GameObject _barracks;

    /// <summary>Prefab of the Monument.</summary>
    [SerializeField]
    private GameObject _monument;

    /// <summary>Rim colour of the tiles of the human player.</summary>
    [Header("Materials")]
    [SerializeField]
    private Material _ownerPlayerMaterial;

    /// <summary>Rim colour of the tiles of the AI.</summary>
    [SerializeField]
    private Material _ownerAiMaterial;

    /// <summary>Colour of the hover, selection and target rim.</summary>
    [SerializeField]
    private Material _highlightMaterial;

    /// <summary>Colour of the hover, selection and target rim.</summary>
    public Material HighlightMaterial
    {
        get { return _highlightMaterial; }
    }

    /// <summary>
    /// Returns the prefab that shows a terrain kind.
    /// </summary>
    /// <param name="terrain">The terrain to show.</param>
    /// <returns>The tile prefab, or null when it was not assigned.</returns>
    public GameObject GetTilePrefab(HexTerrainType terrain)
    {
        switch (terrain)
        {
            case HexTerrainType.Forest:
                return _forestTile;
            case HexTerrainType.Stone:
                return _stoneTile;
            case HexTerrainType.Rubble:
                return _rubbleTile;
            default:
                return _grassTile;
        }
    }

    /// <summary>
    /// Returns the prefab of a building kind.
    /// </summary>
    /// <param name="type">The building to show.</param>
    /// <returns>The building prefab, or null for <see cref="HexBuildingType.None"/>.</returns>
    public GameObject GetBuildingPrefab(HexBuildingType type)
    {
        switch (type)
        {
            case HexBuildingType.Castle:
                return _castle;
            case HexBuildingType.Cottage:
                return _cottage;
            case HexBuildingType.Farm:
                return _farm;
            case HexBuildingType.Quarry:
                return _quarry;
            case HexBuildingType.Church:
                return _church;
            case HexBuildingType.Barracks:
                return _barracks;
            case HexBuildingType.Monument:
                return _monument;
            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the rim material of an owner.
    /// </summary>
    /// <param name="owner">Player index, or <see cref="HexworldConfig.NeutralOwner"/>.</param>
    /// <returns>The material, or null when nobody owns the tile.</returns>
    public Material GetOwnerMaterial(int owner)
    {
        if (owner == 0)
        {
            return _ownerPlayerMaterial;
        }

        if (owner == 1)
        {
            return _ownerAiMaterial;
        }

        return null;
    }
}
