using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Grid2D : MonoBehaviour
{
    [Header("Board Data")]
    public BoardAsset board;

    [Header("World Placement")]
    public Vector3 origin = Vector3.zero;  // bottom-left corner of (0,0)
    public float gridY = 0f;               // plane Y
    [Min(0.1f)] public float cellSize = 1f;

    public int Width  => board != null ? Mathf.Max(1, board.width)  : 1;
    public int Height => board != null ? Mathf.Max(1, board.height) : 1;

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

    public bool IsWalkable(Vector2Int c) =>
        board != null && InBounds(c) && board.GetWalkable(c.x, c.y);

    public Vector3 CellToWorldCenter(Vector2Int c)
    {
        var basePos = origin + new Vector3(c.x * cellSize, 0f, c.y * cellSize);
        return new Vector3(basePos.x + cellSize * 0.5f, gridY, basePos.z + cellSize * 0.5f);
    }

    public bool WorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
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
        int w = Width, h = Height;
        if (w <= 0 || h <= 0 || cellSize <= 0f) return;

        // Outline
        Gizmos.color = Color.white;
        var total = new Vector3(w * cellSize, 0, h * cellSize);
        var bl = new Vector3(origin.x, gridY, origin.z);
        Gizmos.DrawWireCube(bl + total * 0.5f, total);

        // Cells with walkability
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            bool walk = board == null ? true : board.GetWalkable(x, y);
            Gizmos.color = walk ? new Color(0f, 1f, 0f, 0.18f) : new Color(1f, 0f, 0f, 0.35f);
            var center = CellToWorldCenter(new Vector2Int(x, y));
            Gizmos.DrawCube(center, new Vector3(cellSize * 0.98f, 0.01f, cellSize * 0.98f));
        }

        // Grid lines
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
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
