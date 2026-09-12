using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Turns mouse positions into island coordinates. It casts a ray from the
/// camera against the tile colliders, tells the tile views what is hovered and
/// raises an event when a tile is clicked.
/// </summary>
/// <remarks>
/// A click that lands on a UI element is dropped, so the buttons of the panels
/// never reach the board behind them.
/// </remarks>
public sealed class HexworldInputController : MonoBehaviour
{
    /// <summary>The camera the ray starts from.</summary>
    [SerializeField]
    private Camera _camera;

    /// <summary>The board the ray is cast against.</summary>
    [SerializeField]
    private HexBoardView _boardView;

    /// <summary>How far the ray reaches.</summary>
    [SerializeField]
    private float _rayLength = 500f;

    /// <summary>True to remember the clicked tile as the selected one.</summary>
    [SerializeField]
    private bool _selectOnClick = true;

    /// <summary>The tile under the cursor, or null.</summary>
    private HexTileView _hovered;

    /// <summary>The selected tile, or null.</summary>
    private HexTileView _selected;

    /// <summary>The tiles currently offered as legal targets.</summary>
    private readonly List<HexCoord> _validTargets = new List<HexCoord>();

    /// <summary>Raised when the player clicks a tile that is not covered by UI.</summary>
    public event Action<HexCoord> TileClicked;

    /// <summary>Raised when the tile under the cursor changes. Carries null when the cursor left the board.</summary>
    public event Action<HexCoord?> HoverChanged;

    /// <summary>Raised when the selected tile changes. Carries null when the selection was cleared.</summary>
    public event Action<HexCoord?> SelectionChanged;

    /// <summary>False to ignore the mouse, for example while the AI plays.</summary>
    public bool InteractionEnabled { get; set; }

    /// <summary>The camera the ray starts from.</summary>
    public Camera RaycastCamera
    {
        get { return _camera; }
        set { _camera = value; }
    }

    /// <summary>The tile under the cursor, or null.</summary>
    public HexTileView HoveredTile
    {
        get { return _hovered; }
    }

    /// <summary>The coordinate under the cursor, or null when the cursor is off the board.</summary>
    public HexCoord? HoveredCoord
    {
        get { return _hovered == null ? (HexCoord?)null : _hovered.Coord; }
    }

    /// <summary>The selected tile, or null.</summary>
    public HexTileView SelectedTile
    {
        get { return _selected; }
    }

    /// <summary>The selected coordinate, or null when nothing is selected.</summary>
    public HexCoord? SelectedCoord
    {
        get { return _selected == null ? (HexCoord?)null : _selected.Coord; }
    }

    /// <summary>The tiles currently offered as legal targets.</summary>
    public IReadOnlyList<HexCoord> ValidTargets
    {
        get { return _validTargets; }
    }

    /// <summary>
    /// Selects a tile from code, for example after a UI panel picked one.
    /// </summary>
    /// <param name="coord">The tile to select.</param>
    public void Select(HexCoord coord)
    {
        HexTileView view = _boardView == null ? null : _boardView.GetTileView(coord);
        ApplySelection(view);
    }

    /// <summary>
    /// Drops the selection.
    /// </summary>
    public void ClearSelection()
    {
        ApplySelection(null);
    }

    /// <summary>
    /// Highlights the tiles the player may act on right now.
    /// </summary>
    /// <param name="coords">The legal targets, or null to clear them.</param>
    public void SetValidTargets(IEnumerable<HexCoord> coords)
    {
        _validTargets.Clear();
        if (coords != null)
        {
            _validTargets.AddRange(coords);
        }

        if (_boardView != null)
        {
            _boardView.SetValidTargets(_validTargets);
        }
    }

    /// <summary>
    /// Clears the legal target highlight.
    /// </summary>
    public void ClearValidTargets()
    {
        SetValidTargets(null);
    }

    /// <summary>
    /// Forgets the hovered and the selected tile. Call it when the board is
    /// rebuilt, because the old tile views are gone by then.
    /// </summary>
    public void ResetState()
    {
        _hovered = null;
        _selected = null;
        _validTargets.Clear();
    }

    /// <summary>
    /// Switches the mouse on by default.
    /// </summary>
    private void Awake()
    {
        InteractionEnabled = true;
        if (_camera == null)
        {
            _camera = UnityEngine.Camera.main;
        }
    }

    /// <summary>
    /// Reads the mouse once per frame.
    /// </summary>
    private void Update()
    {
        if (!InteractionEnabled || _camera == null || _boardView == null)
        {
            ApplyHover(null);
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ApplyHover(null);
            return;
        }

        if (IsPointerOverUi())
        {
            ApplyHover(null);
            return;
        }

        HexTileView view = Pick(mouse.position.ReadValue());
        ApplyHover(view);

        if (view != null && mouse.leftButton.wasPressedThisFrame)
        {
            if (_selectOnClick)
            {
                ApplySelection(view);
            }

            Action<HexCoord> handler = TileClicked;
            if (handler != null)
            {
                handler(view.Coord);
            }
        }
    }

    /// <summary>
    /// Tells whether the cursor rests on a UI element.
    /// </summary>
    /// <returns>True when a click would belong to the UI.</returns>
    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Casts a ray at a screen position and returns the tile it hit.
    /// </summary>
    /// <param name="screenPosition">Where the cursor is.</param>
    /// <returns>The tile under the cursor, or null.</returns>
    private HexTileView Pick(Vector2 screenPosition)
    {
        Ray ray = _camera.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, _rayLength))
        {
            return null;
        }

        HexTileView view = hit.collider.GetComponentInParent<HexTileView>();
        return view != null && !view.IsVoided ? view : null;
    }

    /// <summary>
    /// Moves the hover highlight to another tile and reports the change.
    /// </summary>
    /// <param name="view">The new hovered tile, or null.</param>
    private void ApplyHover(HexTileView view)
    {
        if (_hovered == view)
        {
            return;
        }

        if (_hovered != null)
        {
            _hovered.SetHovered(false);
        }

        _hovered = view;

        if (_hovered != null)
        {
            _hovered.SetHovered(true);
        }

        Action<HexCoord?> handler = HoverChanged;
        if (handler != null)
        {
            handler(HoveredCoord);
        }
    }

    /// <summary>
    /// Moves the selection to another tile and reports the change.
    /// </summary>
    /// <param name="view">The new selected tile, or null.</param>
    private void ApplySelection(HexTileView view)
    {
        if (_selected == view)
        {
            return;
        }

        if (_selected != null)
        {
            _selected.SetSelected(false);
        }

        _selected = view;

        if (_selected != null)
        {
            _selected.SetSelected(true);
        }

        Action<HexCoord?> handler = SelectionChanged;
        if (handler != null)
        {
            handler(SelectedCoord);
        }
    }
}
