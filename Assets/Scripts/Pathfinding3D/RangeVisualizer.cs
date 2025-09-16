using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime range visualizer for board elements (hazards, buffs, etc.).
/// - Single master toggle (enabledRuntime).
/// - Two modes: Outline OR CellRects (exactly one is drawn).
/// - Outline uses a diamond (Manhattan) silhouette sized to enclose all covered cells.
/// - CellRects draws a thin rectangle per covered cell.
/// - Auto-reads Enemy.range if present; otherwise uses manualRange.
/// - Follows moving objects (e.g., GenericAIMover) by refreshing each LateUpdate.
/// </summary>
[DisallowMultipleComponent]
public class RangeVisualizer : MonoBehaviour
{
    public enum Mode { Outline, CellRects }

    [Header("Basics")]
    public Grid2D grid;                            // auto-found if null
    public bool enabledRuntime = true;
    public Mode mode = Mode.Outline;

    [Header("Range Source")]
    [Tooltip("If true and an Enemy is present, read range from Enemy.range; otherwise use manualRange.")]
    public bool autoFromEnemy = true;
    [Min(0)] public int manualRange = 0;

    [Header("Rendering")]
    [Tooltip("Material used by both outline and cell-rect LineRenderers.")]
    public Material lineMaterial;
    [Min(0.001f)] public float lineWidth = 0.04f;
    public float yOffset = 0.015f;

    [Header("CellRects Mode")]
    [Min(0.001f)] public float rectBorderWidth = 0.03f;
    [Range(0f, 0.45f)] public float rectInset = 0.06f;  // shrink rectangles inside the cell
    [Min(1)] public int maxCellRects = 256;

    [Header("Refresh")]
    [Tooltip("If true, recompute every LateUpdate; if false, call ForceRefresh() manually.")]
    public bool refreshEveryFrame = true;

    LineRenderer _outline;
    readonly List<LineRenderer> _rectPool = new List<LineRenderer>();
    Enemy _enemy; // optional
    Vector2Int _lastCell;
    int _lastRange = -1;
    Mode _lastMode;
    float _lastCellSize = -1f;
    bool _dirty = true;

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        _enemy = GetComponent<Enemy>(); // optional source of range
    }

    void Start()
    {
        EnsureOutline();
        SetOutlineActive(false);
        SetRectPoolSize(0);
        ForceRefresh();
    }

    void LateUpdate()
    {
        if (!enabledRuntime) { HideAll(); return; }
        if (!refreshEveryFrame && !_dirty) return;
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        _dirty = false;
        if (grid == null) { HideAll(); return; }

        // Derive current cell and range
        if (!grid.WorldToCell(transform.position, out var cell))
        {
            HideAll();
            return;
        }

        int range = manualRange;
        if (autoFromEnemy && _enemy != null)
            range = Mathf.Max(0, _enemy.range);

        bool layoutChanged =
            (cell != _lastCell) ||
            (range != _lastRange) ||
            (_lastMode != mode) ||
            (grid.cellSize != _lastCellSize);

        _lastCell = cell;
        _lastRange = range;
        _lastMode = mode;
        _lastCellSize = grid.cellSize;

        if (!layoutChanged)
        {
            ApplyVisibility();
            return;
        }

        // Rebuild current mode
        if (mode == Mode.Outline)
            BuildOutline(cell, range);
        else
            BuildCellRects(cell, range);

        ApplyVisibility();
    }

    // ======================= Outline (Manhattan silhouette) =======================
    void EnsureOutline()
    {
        if (_outline == null)
        {
            var go = new GameObject("OutlineLR");
            go.transform.SetParent(transform, worldPositionStays: true);
            _outline = go.AddComponent<LineRenderer>();
            _outline.useWorldSpace = true;
            _outline.textureMode = LineTextureMode.Stretch;
            _outline.numCornerVertices = 2;
            _outline.numCapVertices = 2;
            _outline.shadowBias = 0f;
        }
        _outline.widthMultiplier = lineWidth;
        if (lineMaterial != null) _outline.material = lineMaterial;
    }

    void BuildOutline(Vector2Int centerCell, int range)
    {
        EnsureOutline();

        // For range == 0, draw an axis-aligned rectangle tightly around the single cell.
        if (range <= 0)
        {
            Vector3 c = grid.CellToWorldCenter(centerCell);
            float half = Mathf.Max(0.02f, grid.cellSize * 0.5f - rectInset);
            Vector3 dx = Vector3.right * half;
            Vector3 dz = Vector3.forward * half;
            Vector3 y  = Vector3.up * yOffset;

            Vector3[] rect =
            {
                c - dx - dz + y,
                c + dx - dz + y,
                c + dx + dz + y,
                c - dx + dz + y,
                c - dx - dz + y // close
            };
            _outline.positionCount = rect.Length;
            _outline.SetPositions(rect);
            _outline.widthMultiplier = lineWidth;
            return;
        }

        // For range >= 1, draw the diamond that encloses ALL cells with Manhattan distance <= range.
        // Silhouette radius is (range + 0.5) * cellSize in world units along axes from the center.
        Vector3 basePos = grid.CellToWorldCenter(centerCell) + Vector3.up * yOffset;
        float r = (range + 0.5f) * grid.cellSize;

        Vector3 top    = basePos + Vector3.forward * r;
        Vector3 right  = basePos + Vector3.right   * r;
        Vector3 bottom = basePos + Vector3.back    * r;
        Vector3 left   = basePos + Vector3.left    * r;

        // Connect in order and close loop
        Vector3[] ring = { top, right, bottom, left, top };
        _outline.positionCount = ring.Length;
        _outline.SetPositions(ring);
        _outline.widthMultiplier = lineWidth;
    }

    // ======================= CellRects (each covered cell) =======================
    void BuildCellRects(Vector2Int centerCell, int range)
    {
        var cells = CollectCellsInManhattan(centerCell, range, maxCellRects);
        SetRectPoolSize(cells.Count);

        float half = Mathf.Max(0.02f, grid.cellSize * 0.5f - rectInset);
        Vector3 dx = Vector3.right * half;
        Vector3 dz = Vector3.forward * half;
        Vector3 y  = Vector3.up * yOffset;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 c = grid.CellToWorldCenter(cells[i]);
            var lr = _rectPool[i];
            lr.widthMultiplier = rectBorderWidth;

            Vector3[] rect =
            {
                c - dx - dz + y,
                c + dx - dz + y,
                c + dx + dz + y,
                c - dx + dz + y,
                c - dx - dz + y // close
            };
            lr.positionCount = rect.Length;
            lr.SetPositions(rect);
        }
    }

    List<Vector2Int> CollectCellsInManhattan(Vector2Int center, int range, int cap)
    {
        var list = new List<Vector2Int>(Mathf.Min(cap, (range * 2 + 1) * (range * 2 + 1)));
        if (!grid.InBounds(center)) return list;

        for (int r = 0; r <= range; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int dy = r - Mathf.Abs(dx);

                // upper row
                var c1 = new Vector2Int(center.x + dx, center.y + dy);
                if (grid.InBounds(c1)) list.Add(c1);
                if (list.Count >= cap) return list;

                if (dy != 0)
                {
                    // lower row
                    var c2 = new Vector2Int(center.x + dx, center.y - dy);
                    if (grid.InBounds(c2)) list.Add(c2);
                    if (list.Count >= cap) return list;
                }
            }
        }
        return list;
    }

    // ======================= Render helpers =======================
    void ApplyVisibility()
    {
        bool showOutline = enabledRuntime && mode == Mode.Outline;
        bool showRects   = enabledRuntime && mode == Mode.CellRects;

        SetOutlineActive(showOutline);
        SetRectPoolActive(showRects);
    }

    void SetOutlineActive(bool active)
    {
        EnsureOutline();
        _outline.enabled = active;
        if (!active) _outline.positionCount = 0;
    }

    void SetRectPoolSize(int count)
    {
        // Grow
        while (_rectPool.Count < count)
        {
            var lr = CreateRectLR();
            _rectPool.Add(lr);
        }
        // Shrink
        for (int i = _rectPool.Count - 1; i >= count; i--)
        {
            if (_rectPool[i] != null) Destroy(_rectPool[i]);
            _rectPool.RemoveAt(i);
        }
    }

    LineRenderer CreateRectLR()
    {
        var go = new GameObject("RangeRectLR");
        go.transform.SetParent(transform, worldPositionStays: true);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.shadowBias = 0f;
        lr.widthMultiplier = rectBorderWidth;
        lr.enabled = false;
        if (lineMaterial != null) lr.material = lineMaterial;
        return lr;
    }

    void SetRectPoolActive(bool active)
    {
        for (int i = 0; i < _rectPool.Count; i++)
        {
            var lr = _rectPool[i];
            if (lr == null) continue;
            lr.enabled = active;
            if (!active) lr.positionCount = 0;
        }
    }

    void HideAll()
    {
        SetOutlineActive(false);
        SetRectPoolActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        _dirty = true;
        if (_outline != null)
        {
            _outline.widthMultiplier = lineWidth;
            if (lineMaterial != null) _outline.material = lineMaterial;
        }
        // Propagate material to rect pool if edited in inspector
        for (int i = 0; i < _rectPool.Count; i++)
        {
            if (_rectPool[i] != null && lineMaterial != null)
                _rectPool[i].material = lineMaterial;
        }
    }
#endif
}
