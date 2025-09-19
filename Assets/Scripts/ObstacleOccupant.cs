using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ObstacleOccupant : MonoBehaviour, IBoardAffectsWalkability
{
    [Header("Placement")]
    public Grid2D grid;
    public Vector2Int Cell { get; private set; }

    [Header("Footprint (Relative Offsets in cells, defined at 0° yaw)")]
    [Tooltip("Walkability grid describing which cells become blocked relative to this object.\n" +
             "Cells marked Blocked in the asset will be blocked. Unspecified/Walkable cells are ignored.")]
    public CellStatusGridAsset footprintAsset;

    [Tooltip("Footprint grid cell treated as this object's cell (local origin).")]
    public Vector2Int footprintAnchor = Vector2Int.zero;

    [SerializeField, HideInInspector, FormerlySerializedAs("relativeFootprint")]
    List<Vector2Int> legacyRelativeFootprint = new() { Vector2Int.zero };

    [Header("Rotation Handling")]
    [Tooltip("If true, uses the object's Y euler rotation, snapped to 0/90/180/270, to rotate the footprint on the grid.")]
    public bool quantizeRotationToRightAngles = true;

    [Tooltip("If enabled, a negative localScale.x flips the footprint across X, and negative localScale.z flips across Y (grid-forward).")]
    public bool respectNegativeScaleAsMirroring = false;

    // Cache to avoid unnecessary overlay recomputes
    Vector2Int _lastCell = new(int.MinValue, int.MinValue);
    int _lastRotSteps = int.MinValue;
    bool _registered;

    void OnEnable()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        SyncCellFromWorld();

        // Register once so the grid tracks our dynamic footprint
        if (Application.isPlaying)
        {
            if (!_registered && grid != null)
            {
                grid.RegisterDynamicBlocker(this);   // will pull GetBlockedCells()
                _registered = true;
            }
        }

        // Keep grid overlays up to date in the editor too
        NotifyIfChanged(force: true);
    }

    void OnDisable()
    {
        if (grid != null && _registered)
        {
            grid.UnregisterDynamicBlocker(this);
            _registered = false;
        }
    }

    void LateUpdate()
    {
        // Play mode: react to move/rotate changes
        if (!Application.isPlaying) return;

        bool changed = false;

        // Track cell change
        if (grid != null && grid.WorldToCell(transform.position, out var c) && c != Cell)
        {
            Cell = c;
            changed = true;
        }

        // Track rotation change
        int rot = QuantizedRotSteps();
        if (rot != _lastRotSteps) { _lastRotSteps = rot; changed = true; }

        // Optional mirroring via scale
        // (We notify grid on any transform change, since mirroring can change)
        if (transform.hasChanged && respectNegativeScaleAsMirroring) changed = true;

        if (changed && grid != null)
            grid.NotifyDynamicOccupantMoved(this);

        transform.hasChanged = false; // clear change flag
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Editor: keep Cell & overlays in sync when moving/rotating in scene view
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        SyncCellFromWorld();
        NotifyIfChanged(force: true);
    }

    void OnDrawGizmosSelected()
    {
        if (grid == null) return;

        var center = grid.CellToWorldCenter(Cell) + Vector3.up * 0.01f;

        // Draw base cell
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawCube(center, new Vector3(grid.cellSize * 0.95f, 0.01f, grid.cellSize * 0.95f));

        // Draw rotated footprint cells
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.50f);
        foreach (var c in GetBlockedCells())
        {
            var p = grid.CellToWorldCenter(c) + Vector3.up * 0.012f;
            Gizmos.DrawCube(p, new Vector3(grid.cellSize * 0.9f, 0.01f, grid.cellSize * 0.9f));
        }

        if (footprintAsset != null)
        {
            footprintAsset.EnsureSize();
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.18f);
            int steps = QuantizedRotSteps();
            foreach (var (cell, status) in footprintAsset.EnumerateCells())
            {
                if (status == CellStatusGridAsset.CellStatus.Blocked)
                    continue; // already drawn above

                var rotated = RotateRightAngles(cell - footprintAnchor, steps);
                var world = grid.CellToWorldCenter(Cell + rotated) + Vector3.up * 0.006f;
                Gizmos.DrawWireCube(world, new Vector3(grid.cellSize * 0.88f, 0.01f, grid.cellSize * 0.88f));
            }
        }
    }
#endif

    // === IBoardCellOccupant ===
    public Grid2D GetGrid() => grid != null ? grid : FindFirstObjectByType<Grid2D>();

    public void SyncCellFromWorld()
    {
        var g = GetGrid();
        if (g != null && g.WorldToCell(transform.position, out var c))
            Cell = c;
    }

    // === IBoardAffectsWalkability ===
    public IEnumerable<Vector2Int> GetBlockedCells()
    {
        // Rotate/mirror footprint on demand from current transform
        int steps = QuantizedRotSteps();

        bool mirrorX = respectNegativeScaleAsMirroring && transform.localScale.x < 0f;
        bool mirrorY = respectNegativeScaleAsMirroring && transform.localScale.z < 0f; // z == grid forward

        var seen = new HashSet<Vector2Int>();

        bool yieldedAny = false;

        foreach (var offset in EnumerateLocalBlockedOffsets())
        {
            var o = offset;

            // Optional mirror first (local)
            if (mirrorX) o.x = -o.x;
            if (mirrorY) o.y = -o.y;

            // Rotate around (0,0) by 0/90/180/270
            var r = RotateRightAngles(o, steps);

            var cell = Cell + r;
            if (seen.Add(cell))
            {
                yieldedAny = true;
                yield return cell;
            }
        }

        if (!yieldedAny)
            yield return Cell;
    }

    // === Helpers ===
    IEnumerable<Vector2Int> EnumerateLocalBlockedOffsets()
    {
        if (footprintAsset != null)
        {
            footprintAsset.EnsureSize();
            foreach (var (cell, status) in footprintAsset.EnumerateCells())
            {
                if (status != CellStatusGridAsset.CellStatus.Blocked)
                    continue;
                yield return cell - footprintAnchor;
            }

            yield break;
        }

        if (legacyRelativeFootprint == null || legacyRelativeFootprint.Count == 0)
        {
            yield return Vector2Int.zero;
            yield break;
        }

        foreach (var c in legacyRelativeFootprint)
            yield return c;
    }

    void NotifyIfChanged(bool force = false)
    {
        if (grid == null) return;

        bool changed = force;

        if (grid.WorldToCell(transform.position, out var c) && c != _lastCell)
        {
            _lastCell = c;
            changed = true;
        }

        int rot = QuantizedRotSteps();
        if (rot != _lastRotSteps)
        {
            _lastRotSteps = rot;
            changed = true;
        }

        if (changed)
        {
            if (Application.isPlaying)
            {
                // In play mode, keep registration + notify deltas
                if (!_registered) { grid.RegisterDynamicBlocker(this); _registered = true; }
                grid.NotifyDynamicOccupantMoved(this);
            }
            else
            {
                // In edit mode, re-register to refresh gizmos / overlay view if needed
                if (!_registered) { grid.RegisterDynamicBlocker(this); _registered = true; }
                grid.NotifyDynamicOccupantMoved(this);
            }
        }
    }

    int QuantizedRotSteps()
    {
        if (!quantizeRotationToRightAngles) return 0; // treat as 0°
        float yaw = transform.eulerAngles.y;
        int steps = Mathf.RoundToInt(yaw / 90f);
        steps %= 4;
        if (steps < 0) steps += 4;
        return steps;
    }

    static Vector2Int RotateRightAngles(Vector2Int v, int steps)
    {
        steps &= 3; // 0..3
        switch (steps)
        {
            case 0: return v;                         // ( x,  y)
            case 1: return new Vector2Int(-v.y, v.x); // (-y,  x)
            case 2: return new Vector2Int(-v.x, -v.y);// (-x, -y)
            default:return new Vector2Int( v.y, -v.x);// ( y, -x)
        }
    }
}
