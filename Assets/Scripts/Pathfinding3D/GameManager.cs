using System.Collections.Generic;
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

    // ===== ENEMIES =====
    [Header("Enemies")]
    [Tooltip("If empty, enemies will be auto-discovered at Start().")]
    public List<Enemy> enemies = new List<Enemy>();

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (flag != null && grid.WorldToCell(flag.position, out var cell))
        {
            FlagCell = cell;
            flag.position = grid.CellToWorldCenter(cell);
        }

        if (enemies == null || enemies.Count == 0)
        {
            var found = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            enemies = new List<Enemy>(found);
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
    }

    // ===== ENEMIES: Apply at end-of-turn only =====
    public void ApplyEndOfTurnEnemyEffects(PlayerAgent agent)
    {
        if (IsGameOver || agent == null || enemies == null) return;
        int totalDamage = 0;

        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (!e.triggerOnTurnEndOnly) continue; // (future-proof if you add other timing modes)

            // Manhattan distance on grid
            int dx = Mathf.Abs(e.Cell.x - agent.CurrentCell.x);
            int dy = Mathf.Abs(e.Cell.y - agent.CurrentCell.y);
            if (dx + dy <= e.range)
                totalDamage += e.power;
        }

        if (totalDamage > 0)
        {
            agent.ApplyExternalEnergyLoss(totalDamage);
            Debug.Log($"Enemy hazards dealt {totalDamage} energy.");
            if (agent.currentEnergy <= 0)
                Lose();
        }
    }
}
