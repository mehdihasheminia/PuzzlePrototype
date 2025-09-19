using UnityEngine;

/// <summary>
/// Listener for SwitchOccupant events: applies/reverts a BoardPattern
/// by modifying the underlying BoardAsset walkability in-place.
/// </summary>
[DisallowMultipleComponent]
public class SwitchApplyPattern : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;
    public BoardPattern pattern;

    [Header("Options")]
    [Tooltip("If true, re-apply the current state on Enable (useful when toggling GameObjects).")]
    public bool applyOnEnable = true;

    bool _applied;

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
    }

    void OnEnable()
    {
        if (applyOnEnable && _applied) Apply();
        else if (applyOnEnable && !_applied) Revert();
    }

    // Hook these from SwitchOccupant events in the Inspector
    public void SwitchOn()  { _applied = true;  Apply();  }
    public void SwitchOff() { _applied = false; Revert(); }

    void Apply()
    {
        if (grid == null || grid.board == null || pattern == null) return;

        grid.board.EnsureSize();

        foreach (var (cell, status) in pattern.EnumerateBoardCells())
        {
            if (!grid.board.InBounds(cell.x, cell.y))
                continue;

            switch (status)
            {
                case CellStatusGridAsset.CellStatus.Blocked:
                    grid.board.SetStatus(cell.x, cell.y, CellStatusGridAsset.CellStatus.Blocked);
                    break;
                case CellStatusGridAsset.CellStatus.Walkable:
                    grid.board.SetStatus(cell.x, cell.y, CellStatusGridAsset.CellStatus.Walkable);
                    break;
            }
        }
        // Grid2D reads BoardAsset via IsWalkable(), so changes take effect immediately.
    }

    void Revert()
    {
        if (grid == null || grid.board == null || pattern == null) return;

        grid.board.EnsureSize();

        foreach (var (cell, status) in pattern.EnumerateBoardCells())
        {
            if (!grid.board.InBounds(cell.x, cell.y))
                continue;

            switch (status)
            {
                case CellStatusGridAsset.CellStatus.Blocked:
                    grid.board.SetStatus(cell.x, cell.y, CellStatusGridAsset.CellStatus.Walkable);
                    break;
                case CellStatusGridAsset.CellStatus.Walkable:
                    grid.board.SetStatus(cell.x, cell.y, CellStatusGridAsset.CellStatus.Blocked);
                    break;
            }
        }
    }
}
