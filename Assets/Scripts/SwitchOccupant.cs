using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SwitchOccupant : MonoBehaviour, IBoardCellOccupant
{
    [Header("Placement")]
    public Grid2D grid;
    public Vector2Int Cell { get; private set; }

    [Header("Behavior")]
    [Tooltip("Initial ON state when the scene starts.")]
    public bool startsOn = false;

    [Tooltip("If true, the switch toggles when the player ENDS a turn on this cell.")]
    public bool triggerOnPlayerTurnEnd = true;

    [Header("Events")]
    public UnityEvent onSwitchOn;
    public UnityEvent onSwitchOff;
    public UnityEvent<bool> onSwitchToggled; // passes new state

    bool _isOn;

    void OnEnable()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        SyncCellFromWorld();

        // Apply initial state without firing both events
        _isOn = startsOn;
        FireEvents(initialize: true);
    }

    // ===== IBoardCellOccupant =====
    public Grid2D GetGrid() => grid != null ? grid : FindFirstObjectByType<Grid2D>();

    public void SyncCellFromWorld()
    {
        var g = GetGrid();
        if (g != null && g.WorldToCell(transform.position, out var c)) Cell = c;
    }

    // Called by GameManager after the player finishes moving
    public void TryTriggerForPlayer(Vector2Int playerCell)
    {
        if (triggerOnPlayerTurnEnd && playerCell == Cell)
            Toggle();
    }

    public void Toggle()
    {
        _isOn = !_isOn;
        FireEvents();
    }

    public void SwitchOn()
    {
        if (_isOn) return;
        _isOn = true;
        FireEvents();
    }

    public void SwitchOff()
    {
        if (!_isOn) return;
        _isOn = false;
        FireEvents();
    }

    void FireEvents(bool initialize = false)
    {
        onSwitchToggled?.Invoke(_isOn);
        if (_isOn) { onSwitchOn?.Invoke();  if (!initialize) { /* optional: SFX */ } }
        else       { onSwitchOff?.Invoke(); if (!initialize) { /* optional: SFX */ } }
    }
}