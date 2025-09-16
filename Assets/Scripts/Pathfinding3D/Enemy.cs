using UnityEngine;

[DisallowMultipleComponent]
public class Enemy : MonoBehaviour, IBoardCellOccupant
{
    [Header("Enemy Stats")]
    [Min(0)] public int range = 0;  // Manhattan range
    [Min(0)] public int power = 1;  // Health lost if player ends turn within range
    [Tooltip("If true, only triggers when the player ENDS a turn; passing through mid-path has no effect.")]
    public bool triggerOnTurnEndOnly = true;

    [Header("Placement")]
    public Grid2D grid; // optional; auto-found if null
    public Vector2Int Cell { get; private set; }

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        // Snap once; mover (if present) will resync each time it lands.
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

        // Cell fill
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        var center = grid.CellToWorldCenter(Cell);
        Gizmos.DrawCube(center + Vector3.up * 0.005f, new Vector3(grid.cellSize * 0.95f, 0.01f, grid.cellSize * 0.95f));

        // Range outline
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
        float r = (range + 0.5f) * grid.cellSize; // envelope
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawWireDisc(center + Vector3.up * 0.01f, Vector3.up, r);
    }
#endif
}
