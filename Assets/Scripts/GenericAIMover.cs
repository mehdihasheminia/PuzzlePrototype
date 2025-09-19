using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generic mover for board elements (enemies, buffs, props).
/// Runs ONLY during the AI turn when GameManager calls it.
/// Moves along a path of grid cells; optionally ping-pongs back when hitting an end.
/// Can draw a runtime LineRenderer to visualize the path.
/// </summary>
[DisallowMultipleComponent]
public class GenericAIMover : MonoBehaviour
{
    [Header("Grid")]
    public Grid2D grid; // auto-found if null

    [Header("Path (Grid Cells)")]
    [Tooltip("Ordered list of grid cells to follow. The object will move from index to index.")]
    public List<Vector2Int> pathCells = new List<Vector2Int>();

    [Header("Path (Optional Asset)")]
    [Tooltip("Optional status grid describing the path. Cells marked Walkable will be converted into an ordered path.")]
    public CellStatusGridAsset pathAsset;

    [Tooltip("Board cell that the asset's (0,0) maps to when generating the path.")]
    public Vector2Int pathOrigin;

    [Tooltip("If enabled, automatically rebuilds pathCells from the path asset when values change in the editor.")]
    public bool autoSyncPathFromAsset = true;

    [Header("Movement")]
    [Min(0.1f)] public float moveSpeed = 4f;         // units / sec between adjacent cell centers
    public AnimationCurve stepCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Min(1)] public int stepsPerAITurn = 1;          // how many cell steps per AI turn
    public bool pingPong = true;                     // return when reaching an end
    public bool snapToCellCenterOnStart = true;      // snap this transform to the start cell center at Play

    [Header("Visualization (Runtime)")]
    public bool visualizePath = true;
    [Min(0.001f)] public float lineWidth = 0.035f;
    public float lineYOffset = 0.0125f;

    LineRenderer lineRenderer;
    int _pathIndex = 0;      // current index in pathCells (we are AT this index)
    int _dir = +1;           // +1 forward, -1 backward

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
    }

    void Start()
    {
        if (pathAsset != null && autoSyncPathFromAsset)
            TrySyncPathFromAsset();

        if (grid != null && pathCells.Count == 0 && grid.WorldToCell(transform.position, out var c))
            pathCells.Add(c);

        if (snapToCellCenterOnStart && grid != null && pathCells.Count > 0)
        {
            transform.position = grid.CellToWorldCenter(pathCells[0]);
            _pathIndex = 0;
        }

        SetupLineRenderer();
        RefreshLineRenderer();
        TrySyncOccupantCell(); // initial sync at spawn
    }

    /// <summary>Regenerates pathCells from the assigned pathAsset.</summary>
    public bool TrySyncPathFromAsset()
    {
        if (pathAsset == null) return false;

        if (!TryBuildOrderedPathFromAsset(out var ordered))
            return false;

        pathCells = ordered;
        RefreshLineRenderer();
        return true;
    }

    void SetupLineRenderer()
    {
        if (!visualizePath) return;
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.shadowBias = 0f;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;
    }

    void RefreshLineRenderer()
    {
        if (!visualizePath || lineRenderer == null || grid == null || pathCells == null) return;
        if (pathCells.Count == 0) { lineRenderer.positionCount = 0; return; }
        lineRenderer.positionCount = pathCells.Count;
        for (int i = 0; i < pathCells.Count; i++)
            lineRenderer.SetPosition(i, grid.CellToWorldCenter(pathCells[i]) + Vector3.up * lineYOffset);
    }

    /// <summary>Called by GameManager during the AI phase.</summary>
    public IEnumerator RunAIMove()
    {
        if (grid == null || pathCells == null || pathCells.Count < 2 || stepsPerAITurn <= 0)
            yield break;

        for (int s = 0; s < stepsPerAITurn; s++)
        {
            int nextIndex = _pathIndex + _dir;

            if (nextIndex < 0 || nextIndex >= pathCells.Count)
            {
                if (!pingPong)
                {
                    _pathIndex = Mathf.Clamp(_pathIndex, 0, pathCells.Count - 1);
                    break;
                }
                _dir = -_dir;
                nextIndex = _pathIndex + _dir;
                if (nextIndex < 0 || nextIndex >= pathCells.Count) break;
            }

            Vector3 from = grid.CellToWorldCenter(pathCells[_pathIndex]);
            Vector3 to   = grid.CellToWorldCenter(pathCells[nextIndex]);

            float dist = Vector3.Distance(from, to);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (moveSpeed / Mathf.Max(0.0001f, dist));
                float k = stepCurve.Evaluate(Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }

            _pathIndex = nextIndex;
            TrySyncOccupantCell(); // <-- notify grid if this occupant blocks walkability
        }

        RefreshLineRenderer();
    }

    void TrySyncOccupantCell()
    {
        var occ = GetComponent<IBoardCellOccupant>();
        if (occ != null) occ.SyncCellFromWorld();

        // If this thing affects walkability (e.g., ObstacleOccupant), let the grid update overlays.
        if (grid != null)
        {
            var blocker = GetComponent<IBoardAffectsWalkability>();
            if (blocker != null) grid.NotifyDynamicOccupantMoved(blocker);
        }
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && autoSyncPathFromAsset)
            TrySyncPathFromAsset();
#endif
    }

    bool TryBuildOrderedPathFromAsset(out List<Vector2Int> ordered)
    {
        ordered = null;
        if (pathAsset == null) return false;

        pathAsset.EnsureSize();

        var candidates = new HashSet<Vector2Int>();
        foreach (var (cell, status) in pathAsset.EnumerateCells())
        {
            if (status != CellStatusGridAsset.CellStatus.Walkable)
                continue;
            candidates.Add(pathOrigin + cell);
        }

        if (candidates.Count == 0)
        {
            ordered = new List<Vector2Int>();
            return true;
        }

        static IEnumerable<Vector2Int> Directions()
        {
            yield return new Vector2Int(1, 0);
            yield return new Vector2Int(-1, 0);
            yield return new Vector2Int(0, 1);
            yield return new Vector2Int(0, -1);
        }

        var neighborMap = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var cell in candidates)
        {
            var neighbors = new List<Vector2Int>();
            foreach (var dir in Directions())
            {
                var n = cell + dir;
                if (candidates.Contains(n))
                    neighbors.Add(n);
            }
            neighborMap[cell] = neighbors;
        }

        Vector2Int start = default;
        bool startFound = false;
        foreach (var kvp in neighborMap)
        {
            if (kvp.Value.Count <= 1)
            {
                start = kvp.Key;
                startFound = true;
                break;
            }
        }

        if (!startFound)
            start = neighborMap.Keys.FirstOrDefault();

        var result = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        Vector2Int current = start;
        Vector2Int prev = new Vector2Int(int.MinValue, int.MinValue);

        while (true)
        {
            if (!visited.Add(current))
            {
                // Loop detected – unsupported.
                return false;
            }

            result.Add(current);

            var neighbors = neighborMap[current];
            if (neighbors.Count > 2)
            {
                // Branching path not supported by ordered path extraction.
                return false;
            }

            Vector2Int? next = null;
            for (int i = 0; i < neighbors.Count; i++)
            {
                var candidate = neighbors[i];
                if (candidate == prev) continue;
                next = candidate;
                break;
            }

            if (next == null)
                break;

            prev = current;
            current = next.Value;
        }

        ordered = result;
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (grid == null || pathCells == null || pathCells.Count == 0) return;

        Gizmos.color = new Color(1f, 1f, 0.2f, 0.65f);
        Vector3 prev = grid.CellToWorldCenter(pathCells[0]) + Vector3.up * lineYOffset;
        Gizmos.DrawSphere(prev, grid.cellSize * 0.08f);

        for (int i = 1; i < pathCells.Count; i++)
        {
            Vector3 next = grid.CellToWorldCenter(pathCells[i]) + Vector3.up * lineYOffset;
            Gizmos.DrawLine(prev, next);
            Gizmos.DrawSphere(next, grid.cellSize * 0.06f);
            prev = next;
        }
    }
#endif
}
