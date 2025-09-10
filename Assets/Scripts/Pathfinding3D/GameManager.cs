using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;
    public Transform flag; // place a simple object at the desired cell

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;
    public UnityEvent<int, int> onEnergyChanged;

    public bool IsGameOver { get; private set; }
    public Vector2Int FlagCell { get; private set; }

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (flag != null && grid.WorldToCell(flag.position, out var cell))
        {
            FlagCell = cell;
            // snap to center
            flag.position = grid.CellToWorldCenter(cell);
        }
    }

    public void Win()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        onWin?.Invoke();
        Debug.Log("🏁 You win!");
    }

    public void Lose()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        onLose?.Invoke();
        Debug.Log("💀 Game Over (energy depleted)");
    }

    public void OnEnergyChanged(int current, int max)
    {
        onEnergyChanged?.Invoke(current, max);
        // hook UI here
    }

    // (Optional) Reset / reload can be added here
}