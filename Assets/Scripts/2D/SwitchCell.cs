using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SnapToGrid))]
public class SwitchCell : MonoBehaviour, ICellInteractable
{
    public GridBoard grid;
    [Tooltip("Where the switch lives (row, col).")]
    public Vector2Int cell;

    [Tooltip("Cells to toggle when the switch is activated (row, col).")]
    public List<Vector2Int> targetCells = new List<Vector2Int>();

    [Tooltip("Require the actor to stand on an adjacent (4-dir) cell?")]
    public bool requireAdjacency = true;

    [Header("Optional: Visuals")]
    public SpriteRenderer indicator; // e.g., change color on press
    public Color idleColor = Color.white;
    public Color pressedColor = new Color(1f, 0.9f, 0.3f);

    SnapToGrid _snap;

    public Vector2Int Cell => cell;

    void Awake()
    {
        _snap = GetComponent<SnapToGrid>();
        if (grid == null) grid = _snap?.grid ?? FindObjectOfType<GridBoard>();
        if (_snap != null) { _snap.grid = grid; _snap.cell = cell; _snap.Snap(); }
    }

    void OnEnable()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        grid?.RegisterInteractable(this);
        SetIndicator(false);
    }

    void OnDisable()
    {
        grid?.UnregisterInteractable(this);
    }

    public bool CanActivate(Vector2Int actorCell)
    {
        if (!requireAdjacency) return true;
        int dr = Mathf.Abs(actorCell.x - cell.x);
        int dc = Mathf.Abs(actorCell.y - cell.y);
        return (dr + dc) == 1; // Manhattan distance == 1 (adjacent)
    }

    public void Activate()
    {
        // Toggle each target's walkability
        foreach (var t in targetCells)
        {
            if (!grid.InBounds(t.x, t.y)) continue;
            bool next = !grid.IsWalkable(t.x, t.y);
            grid.SetWalkable(t.x, t.y, next);
        }

        // Optional visual ping
        StartCoroutine(Flash());
    }

    System.Collections.IEnumerator Flash()
    {
        SetIndicator(true);
        yield return new WaitForSeconds(0.08f);
        SetIndicator(false);
    }

    void SetIndicator(bool pressed)
    {
        if (indicator != null)
            indicator.color = pressed ? pressedColor : idleColor;
    }
}
