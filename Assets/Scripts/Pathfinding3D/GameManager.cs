using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;
    public Transform flag;

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;
    public UnityEvent<int, int> onEnergyChanged;

    public bool IsGameOver { get; private set; }
    public Vector2Int FlagCell { get; private set; }

    // ===== ENEMIES =====
    [Header("Enemies")]
    public List<Enemy> enemies = new List<Enemy>();

    // ===== BUFFS =====
    [Header("Buffs")]
    public List<Buff> buffs = new List<Buff>();   // NEW

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (flag != null && grid.WorldToCell(flag.position, out var cell))
        {
            FlagCell = cell;
            flag.position = grid.CellToWorldCenter(cell);
        }

        if (enemies == null || enemies.Count == 0)
            enemies = new List<Enemy>(FindObjectsByType<Enemy>(FindObjectsSortMode.None));

        if (buffs == null || buffs.Count == 0)     // NEW
            buffs = new List<Buff>(FindObjectsByType<Buff>(FindObjectsSortMode.None));
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

    // ===== Turn-end effects aggregation =====
    public void ApplyEndOfTurnEffects(PlayerAgent agent)
    {
        if (IsGameOver || agent == null) return;

        // 1) Hazards first (design choice). If you want buffs to potentially save from lethal damage,
        //    swap the order or add a flag to control precedence.
        int totalDamage = 0;
        foreach (var e in enemies)
        {
            if (e == null || !e.triggerOnTurnEndOnly) continue;
            int dx = Mathf.Abs(e.Cell.x - agent.CurrentCell.x);
            int dy = Mathf.Abs(e.Cell.y - agent.CurrentCell.y);
            if (dx + dy <= e.range) totalDamage += e.power;
        }
        if (totalDamage > 0)
        {
            agent.ApplyExternalEnergyLoss(totalDamage);
            Debug.Log($"Hazards dealt {totalDamage} energy.");
            if (agent.currentEnergy <= 0) { Lose(); return; }
        }

        // 2) Buffs (range 0; consumable)
        int gained = 0;
        foreach (var b in buffs)
        {
            if (b == null || !b.triggerOnTurnEndOnly) continue;
            gained += b.ConsumeIfApplicable(agent.CurrentCell);
        }
        if (gained > 0)
        {
            agent.ApplyExternalEnergyGain(gained, capToMax:true);
            Debug.Log($"+{gained} energy from buffs.");
        }
    }
}
