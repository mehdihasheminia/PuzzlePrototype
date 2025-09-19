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

        // makeUnwalkable
        for (int i = 0; i < pattern.makeUnwalkable.Count; i++)
        {
            var c = pattern.makeUnwalkable[i];
            if (grid.board.InBounds(c.x, c.y))
                grid.board.SetWalkable(c.x, c.y, false);
        }
        // makeWalkable
        for (int i = 0; i < pattern.makeWalkable.Count; i++)
        {
            var c = pattern.makeWalkable[i];
            if (grid.board.InBounds(c.x, c.y))
                grid.board.SetWalkable(c.x, c.y, true);
        }
        // Grid2D reads BoardAsset via IsWalkable(), so changes take effect immediately. :contentReference[oaicite:7]{index=7}
    }

    void Revert()
    {
        if (grid == null || grid.board == null || pattern == null) return;

        // Revert by restoring base meaning: un-do our explicit sets
        // For simplicity, we invert the effect:
        for (int i = 0; i < pattern.makeUnwalkable.Count; i++)
        {
            var c = pattern.makeUnwalkable[i];
            if (grid.board.InBounds(c.x, c.y))
                grid.board.SetWalkable(c.x, c.y, true);
        }
        for (int i = 0; i < pattern.makeWalkable.Count; i++)
        {
            var c = pattern.makeWalkable[i];
            if (grid.board.InBounds(c.x, c.y))
                grid.board.SetWalkable(c.x, c.y, false);
        }
    }
}
