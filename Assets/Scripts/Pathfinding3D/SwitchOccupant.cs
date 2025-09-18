using UnityEngine;

[DisallowMultipleComponent]
public class SwitchOccupant : MonoBehaviour, IBoardCellOccupant
{
    [Header("Refs")]
    public Grid2D grid;
    public BoardPattern pattern;

    [Header("Behavior")]
    [Tooltip("If true, switch toggles on every time the player ENDS a turn on this cell.")]
    public bool toggleOnPlayerTurnEnd = true;
    public bool startsOn = false;

    public Vector2Int Cell { get; private set; }
    bool _isOn;

    void OnEnable()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        SyncCellFromWorld();

        // Apply initial state
        if (startsOn && !_isOn) { ApplyOn(); }
    }

    void OnDisable()
    {
        // Leave pattern as-is; if you want auto-cleanup when disabled, uncomment:
        // if (_isOn) RevertOff();
    }

    // === IBoardCellOccupant ===
    public Grid2D GetGrid() => grid != null ? grid : FindFirstObjectByType<Grid2D>();

    public void SyncCellFromWorld()
    {
        var g = GetGrid();
        if (g != null && g.WorldToCell(transform.position, out var c))
            Cell = c;
    }

    // === Toggling API ===
    public void Trigger()
    {
        if (pattern == null || grid == null) return;
        if (_isOn) RevertOff();
        else       ApplyOn();
    }

    public void TryTriggerForPlayer(Vector2Int playerCell)
    {
        if (!toggleOnPlayerTurnEnd) return;
        if (playerCell == Cell) Trigger();
    }

    void ApplyOn()
    {
        grid.ApplyPattern(pattern);
        _isOn = true;
    }

    void RevertOff()
    {
        grid.RevertPattern(pattern);
        _isOn = false;
    }
}