using UnityEngine;
using UnityEngine.Tilemaps;
using System;

[RequireComponent(typeof(Tilemap))]
public class GridPainterTilemap : MonoBehaviour
{
    [Header("References")]
    public GridBoard gridBoard;
    public TileBase walkableTile;
    public TileBase blockedTile;

    private Tilemap _tilemap;

    void Awake()
    {
        _tilemap = GetComponent<Tilemap>();
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
    }

    void OnEnable()
    {
        DrawAll();
        // Optional: subscribe to changes if you raise an event in GridBoard
        GridBoardEvents.SubscribeToWalkableChanged(gridBoard, OnWalkableChanged);
    }

    void OnDisable()
    {
        GridBoardEvents.UnsubscribeFromWalkableChanged(gridBoard, OnWalkableChanged);
    }

    void OnValidate()
    {
        if (_tilemap == null) _tilemap = GetComponent<Tilemap>();
        if (gridBoard != null && _tilemap != null && walkableTile != null && blockedTile != null)
        {
            DrawAll();
        }
    }

    public void DrawAll()
    {
        if (gridBoard == null || _tilemap == null || gridBoard.Walkable == null) return;

        _tilemap.ClearAllTiles();
        int rows = gridBoard.rows;
        int cols = gridBoard.cols;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var cell = new Vector3Int(c, r, 0); // x=>col, y=>row
            _tilemap.SetTile(cell, gridBoard.Walkable[r, c] ? walkableTile : blockedTile);
        }

        // Make tile physical size match GridBoard.cellSize (if needed)
        Vector3 scale = Vector3.one * gridBoard.cellSize;
        _tilemap.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        // Align position to origin
        _tilemap.transform.position = new Vector3(gridBoard.origin.x, gridBoard.origin.y, _tilemap.transform.position.z);
    }

    private void OnWalkableChanged(int r, int c, bool isWalkable)
    {
        var cell = new Vector3Int(c, r, 0);
        _tilemap.SetTile(cell, isWalkable ? walkableTile : blockedTile);
    }
}
