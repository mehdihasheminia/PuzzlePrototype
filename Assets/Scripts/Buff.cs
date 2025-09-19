using UnityEngine;

[DisallowMultipleComponent]
public class Buff : MonoBehaviour, IBoardCellOccupant
{
    [Header("Buff")]
    [Min(1)] public int extraTurns = 1;        // additional turns granted on consume
    [Tooltip("If true, the buff only triggers when the player finishes their move on this cell.")]
    public bool triggerOnTurnEndOnly = true;   // remains for consistency
    public bool consumed { get; private set; } // persisted only while scene runs

    [Header("Placement")]
    public Grid2D grid;                        // optional; auto-found
    public Vector2Int Cell { get; private set; }

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        SyncCellFromWorld();
        if (grid != null) transform.position = grid.CellToWorldCenter(Cell);
    }

    // === IBoardCellOccupant ===
    public Grid2D GetGrid() => grid != null ? grid : FindFirstObjectByType<Grid2D>();

    public void SyncCellFromWorld()
    {
        var g = GetGrid();
        if (g != null && g.WorldToCell(transform.position, out var c))
            Cell = c;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (grid == null) grid = FindFirstObjectByType<Grid2D>();
            var g = GetGrid();
            if (g != null && g.WorldToCell(transform.position, out var c))
                Cell = c;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (grid == null) return;
        var center = grid.CellToWorldCenter(Cell);
        // Cell highlight (blue-green)
        Gizmos.color = consumed ? new Color(0.2f, 0.5f, 0.5f, 0.20f) : new Color(0.2f, 1f, 1f, 0.35f);
        Gizmos.DrawCube(center + Vector3.up * 0.005f, new Vector3(grid.cellSize * 0.95f, 0.01f, grid.cellSize * 0.95f));

        #if UNITY_EDITOR
        UnityEditor.Handles.color = consumed ? new Color(0.6f, 0.9f, 0.9f, 0.6f) : new Color(0f, 1f, 1f, 1f);
        UnityEditor.Handles.Label(center + new Vector3(0, 0.03f, 0), consumed ? $"BUFF (used)" : $"BUFF +{extraTurns} Turn{(extraTurns > 1 ? "s" : string.Empty)}");
        #endif
    }
#endif

    /// <summary>Consumes the buff if the player occupies this cell.</summary>
    /// <param name="playerCell">Player's current cell.</param>
    /// <param name="triggeredDuringMovement">True if the player is still moving towards another cell.</param>
    /// <returns>The number of extra turns granted.</returns>
    public int Consume(Vector2Int playerCell, bool triggeredDuringMovement)
    {
        if (consumed) return 0;
        if (playerCell != Cell) return 0; // range is exactly 0
        if (triggerOnTurnEndOnly && triggeredDuringMovement) return 0;

        consumed = true;
        gameObject.SetActive(false);
        return Mathf.Max(0, extraTurns);
    }
}
