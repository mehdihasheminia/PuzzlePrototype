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

    // ===== Runtime overlays =====
    // Cells made un/walkable by switches/patterns
    readonly HashSet<Vector2Int> _forcedUnwalkable = new();
    readonly HashSet<Vector2Int> _forcedWalkable   = new();

    // Cells blocked by dynamic occupants (e.g., obstacles)
    readonly HashSet<Vector2Int> _blockedByOccupants = new();
    readonly Dictionary<IBoardAffectsWalkability, List<Vector2Int>> _blockers = new();

    // For optional undo of applied patterns
    readonly HashSet<BoardPattern> _activePatterns = new();

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.x < Width && c.y >= 0 && c.y < Height;

    public bool IsWalkable(Vector2Int c)
    {
        if (!InBounds(c)) return false;

        // Base board
        bool walk = board == null || board.GetWalkable(c.x, c.y);
        // Pattern overlays
        if (_forcedUnwalkable.Contains(c)) walk = false;
        if (_forcedWalkable.Contains(c))   walk = true;
        // Dynamic blockers (obstacles etc.)
        if (_blockedByOccupants.Contains(c)) walk = false;

        return walk;
    }

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
        for (int i = 0; i < _dirs4.Length; i++)
        {
            var n = c + _dirs4[i];
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

    // ===== Pattern API (used by SwitchOccupant) =====
    public void ApplyPattern(BoardPattern pattern)
    {
        if (pattern == null) return;
        foreach (var c in pattern.makeUnwalkable)
            if (InBounds(c)) { _forcedWalkable.Remove(c); _forcedUnwalkable.Add(c); }
        foreach (var c in pattern.makeWalkable)
            if (InBounds(c)) { _forcedUnwalkable.Remove(c); _forcedWalkable.Add(c); }
        _activePatterns.Add(pattern);
    }

    public void RevertPattern(BoardPattern pattern)
    {
        if (pattern == null || !_activePatterns.Contains(pattern)) return;
        foreach (var c in pattern.makeUnwalkable)
            if (InBounds(c)) _forcedUnwalkable.Remove(c);
        foreach (var c in pattern.makeWalkable)
            if (InBounds(c)) _forcedWalkable.Remove(c);
        _activePatterns.Remove(pattern);
    }

    // ===== Dynamic blockers API (used by ObstacleOccupant and movers) =====
    public void RegisterDynamicBlocker(IBoardAffectsWalkability blocker)
    {
        if (blocker == null) return;
        if (_blockers.ContainsKey(blocker)) { NotifyDynamicOccupantMoved(blocker); return; }
        var cells = CollectValid(blocker.GetBlockedCells());
        _blockers[blocker] = cells;
        for (int i = 0; i < cells.Count; i++) _blockedByOccupants.Add(cells[i]);
    }

    public void UnregisterDynamicBlocker(IBoardAffectsWalkability blocker)
    {
        if (blocker == null) return;
        if (_blockers.TryGetValue(blocker, out var prev))
        {
            for (int i = 0; i < prev.Count; i++) _blockedByOccupants.Remove(prev[i]);
            _blockers.Remove(blocker);
        }
    }

    public void NotifyDynamicOccupantMoved(IBoardAffectsWalkability blocker)
    {
        if (blocker == null) return;

        // Remove previous footprint
        if (_blockers.TryGetValue(blocker, out var prev))
        {
            for (int i = 0; i < prev.Count; i++) _blockedByOccupants.Remove(prev[i]);
        }

        // Add new footprint
        var cur = CollectValid(blocker.GetBlockedCells());
        _blockers[blocker] = cur;
        for (int i = 0; i < cur.Count; i++) _blockedByOccupants.Add(cur[i]);
    }

    List<Vector2Int> CollectValid(IEnumerable<Vector2Int> cells)
    {
        var list = new List<Vector2Int>();
        if (cells == null) return list;
        foreach (var c in cells) if (InBounds(c)) list.Add(c);
        return list;
    }

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

        // Cells w/ final walkability
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            var c = new Vector2Int(x, y);
            bool walk = IsWalkable(c);
            Gizmos.color = walk ? new Color(0f, 1f, 0f, 0.18f) : new Color(1f, 0f, 0f, 0.35f);
            var center = CellToWorldCenter(c);
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
