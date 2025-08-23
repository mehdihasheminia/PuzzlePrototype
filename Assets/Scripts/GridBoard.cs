using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridBoard : MonoBehaviour
{
    [Header("Grid")]
    public int rows = 10;
    public int cols = 15;
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero; //ToDo: Cell size and origin must match TileMap

    [Header("Walkability")]
    [Tooltip("Optional: initialize some cells as blocked in inspector (rows x cols).")]
    [SerializeField] private bool initializeAllWalkable = true;

    // walkable[row, col] => true if walkable
    [SerializeField, HideInInspector] private bool[] _walkableFlat;
    public bool[,] Walkable { get; private set; }

    void OnEnable()
    {
        EnsureGridData();
    }

    void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        cols = Mathf.Max(1, cols);
        cellSize = Mathf.Max(0.01f, cellSize);
        EnsureGridData();
    }

    void EnsureGridData()
    {
        if (_walkableFlat == null || _walkableFlat.Length != rows * cols)
        {
            _walkableFlat = new bool[rows * cols];
            for (int i = 0; i < _walkableFlat.Length; i++)
                _walkableFlat[i] = initializeAllWalkable;
        }

        if (Walkable == null || Walkable.GetLength(0) != rows || Walkable.GetLength(1) != cols)
        {
            Walkable = new bool[rows, cols];
        }

        // flatten -> 2D
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            Walkable[r, c] = _walkableFlat[r * cols + c];
        }
    }

    int FlatIndex(int r, int c) => r * cols + c;

    public void SetWalkable(int r, int c, bool isWalkable)
    {
        if (!InBounds(r, c)) return;
        Walkable[r, c] = isWalkable;
        _walkableFlat[FlatIndex(r, c)] = isWalkable;
        GridBoardEvents.RaiseWalkableChanged(r, c, isWalkable);
    }

    public bool InBounds(int r, int c)
    {
        return r >= 0 && r < rows && c >= 0 && c < cols;
    }

    public bool IsWalkable(int r, int c)
    {
        return InBounds(r, c) && Walkable[r, c];
    }

    /// <summary>
    /// Returns the cell indices for a world position (clamped to grid bounds).
    /// </summary>
    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        Vector2 local = worldPos - origin;
        int r = Mathf.FloorToInt(local.y / cellSize);
        int c = Mathf.FloorToInt(local.x / cellSize);
        r = Mathf.Clamp(r, 0, rows - 1);
        c = Mathf.Clamp(c, 0, cols - 1);
        return new Vector2Int(r, c);
    }

    /// <summary>
    /// Returns the world-space center of a cell.
    /// </summary>
    public Vector3 CellToWorldCenter(int r, int c, float z = 0f)
    {
        float x = origin.x + (c + 0.5f) * cellSize;
        float y = origin.y + (r + 0.5f) * cellSize;
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Returns the center of the nearest cell to a given world position (clamped to grid).
    /// Also outputs the chosen cell indices.
    /// </summary>
    public Vector3 NearestCellCenter(Vector2 worldPos, out Vector2Int cell, float z = 0f)
    {
        cell = WorldToCell(worldPos);
        return CellToWorldCenter(cell.x, cell.y, z);
    }

    /// <summary>
    /// Returns true if a world position maps to a walkable cell.
    /// </summary>
    public bool IsWalkableAtWorld(Vector2 worldPos)
    {
        var cell = WorldToCell(worldPos);
        return IsWalkable(cell.x, cell.y);
    }

    // Editor visualization
    void OnDrawGizmos()
    {
        EnsureGridData();
        // Draw grid lines
        Gizmos.color = Color.gray;
        for (int r = 0; r <= rows; r++)
        {
            Vector3 a = new Vector3(origin.x, origin.y + r * cellSize, 0f);
            Vector3 b = new Vector3(origin.x + cols * cellSize, origin.y + r * cellSize, 0f);
            Gizmos.DrawLine(a, b);
        }

        for (int c = 0; c <= cols; c++)
        {
            Vector3 a = new Vector3(origin.x + c * cellSize, origin.y, 0f);
            Vector3 b = new Vector3(origin.x + c * cellSize, origin.y + rows * cellSize, 0f);
            Gizmos.DrawLine(a, b);
        }

        // Mark unwalkables
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (!Walkable[r, c])
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                Gizmos.DrawCube(CellToWorldCenter(r, c), new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.01f));
            }
        }
    }
    
    //------------------------------------------------------------------------------------------------------------------
    private readonly Dictionary<Vector2Int, ICellInteractable> _interactables
        = new Dictionary<Vector2Int, ICellInteractable>();

    public void RegisterInteractable(ICellInteractable obj)
    {
        if (obj == null) return;
        _interactables[obj.Cell] = obj;
    }

    public void UnregisterInteractable(ICellInteractable obj)
    {
        if (obj == null) return;
        if (_interactables.TryGetValue(obj.Cell, out var curr) && curr == obj)
            _interactables.Remove(obj.Cell);
    }

    public bool TryGetInteractable(Vector2Int cell, out ICellInteractable interactable)
        => _interactables.TryGetValue(cell, out interactable);

    /// Convenience: walkable neighbors (4-dir)
    public IEnumerable<Vector2Int> WalkableNeighbors(Vector2Int cell)
    {
         Vector2Int[] DIRS4 = {
            new Vector2Int(-1,0), new Vector2Int(1,0),
            new Vector2Int(0,-1), new Vector2Int(0,1)
        };
        foreach (var d in DIRS4)
        {
            var n = new Vector2Int(cell.x + d.x, cell.y + d.y);
            if (IsWalkable(n.x, n.y)) yield return n;
        }
    }
}
