using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Grid2D : MonoBehaviour
{
    [Header("Dimensions")]
    [Min(1)] public int width = 10;
    [Min(1)] public int height = 10;
    [Min(0.1f)] public float cellSize = 1f;

    [Header("World Placement")]
    public Vector3 origin = Vector3.zero;  // bottom-left corner of (0,0)
    public float gridY = 0f;               // plane Y for raycasts / conversions

    [Header("Cells")]
    [Tooltip("Cells toggled to unwalkable at Start. Values are (x,y).")]
    public List<Vector2Int> initialBlocked = new();

    bool[,] _walkable;

    public event Action<Vector2Int, bool> OnCellWalkableChanged;

    void Awake()
    {
        _walkable = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _walkable[x, y] = true;

        foreach (var p in initialBlocked)
            if (InBounds(p)) _walkable[p.x, p.y] = false;
    }

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    public bool IsWalkable(Vector2Int c) =>
        InBounds(c) && _walkable[c.x, c.y];

    public void SetWalkable(Vector2Int c, bool value)
    {
        if (!InBounds(c)) return;
        if (_walkable[c.x, c.y] == value) return;
        _walkable[c.x, c.y] = value;
        OnCellWalkableChanged?.Invoke(c, value);
    }

    public Vector3 CellToWorldCenter(Vector2Int c)
    {
        var basePos = origin + new Vector3(c.x * cellSize, 0f, c.y * cellSize);
        return new Vector3(basePos.x + cellSize * 0.5f, gridY, basePos.z + cellSize * 0.5f);
    }

    public bool WorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        // Project onto grid plane
        Vector3 local = worldPos - new Vector3(origin.x, gridY, origin.z);
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        cell = new Vector2Int(x, y);
        return InBounds(cell);
    }

    public IEnumerable<Vector2Int> GetNeighbors4(Vector2Int c)
    {
        var dirs = _dirs4;
        for (int i = 0; i < dirs.Length; i++)
        {
            var n = c + dirs[i];
            if (InBounds(n) && IsWalkable(n))
                yield return n;
        }
    }

    static readonly Vector2Int[] _dirs4 = new[]
    {
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int( 0, 1),
        new Vector2Int( 0,-1),
    };

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (width <= 0 || height <= 0 || cellSize <= 0f) return;

        // Draw cell rectangles and color by walkability (in play mode reflect runtime state)
        var w = Mathf.Max(1, width);
        var h = Mathf.Max(1, height);

        // Outline
        Gizmos.color = Color.white;
        var total = new Vector3(w * cellSize, 0, h * cellSize);
        var bl = new Vector3(origin.x, gridY, origin.z);
        Gizmos.DrawWireCube(bl + total * 0.5f, total + new Vector3(0, 0, 0));

        // Cells
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool walkable = true;
                if (Application.isPlaying && _walkable != null && x < _walkable.GetLength(0) && y < _walkable.GetLength(1))
                    walkable = _walkable[x, y];
                else if (!Application.isPlaying)
                    walkable = !initialBlocked.Contains(new Vector2Int(x, y));

                Gizmos.color = walkable ? new Color(0f, 1f, 0f, 0.18f) : new Color(1f, 0f, 0f, 0.35f);
                var center = CellToWorldCenter(new Vector2Int(x, y));
                Gizmos.DrawCube(center, new Vector3(cellSize * 0.98f, 0.01f, cellSize * 0.98f));
            }
        }

        // Grid lines (optional – comment if noisy)
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        for (int x = 0; x <= w; x++)
        {
            var a = bl + new Vector3(x * cellSize, 0, 0);
            var b = bl + new Vector3(x * cellSize, 0, h * cellSize);
            Gizmos.DrawLine(a, b);
        }
        for (int y = 0; y <= h; y++)
        {
            var a = bl + new Vector3(0, 0, y * cellSize);
            var b = bl + new Vector3(w * cellSize, 0, y * cellSize);
            Gizmos.DrawLine(a, b);
        }
    }
#endif
}
