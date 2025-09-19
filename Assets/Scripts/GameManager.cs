using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public enum TurnPhase { Player, AI }

    [Header("Refs")]
    public Grid2D grid;
    public Transform flag;

    [Header("Events")]
    public UnityEvent onWin;
    public UnityEvent onLose;
    public UnityEvent<int, int> onHealthChanged; // current, max

    public bool IsGameOver { get; private set; }
    public Vector2Int FlagCell { get; private set; }
    public TurnPhase CurrentTurn { get; private set; } = TurnPhase.Player;
    public bool IsPlayerTurn => CurrentTurn == TurnPhase.Player;

    [Header("AI Movers")]
    public List<GenericAIMover> movers = new List<GenericAIMover>();

    [Header("Enemies")]
    public List<Enemy> enemies = new List<Enemy>();

    [Header("Buffs")]
    public List<Buff> buffs = new List<Buff>();

    [Header("Switches")]
    public List<SwitchOccupant> switches = new List<SwitchOccupant>();

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (flag != null && grid.WorldToCell(flag.position, out var cell))
        {
            FlagCell = cell;
            flag.position = grid.CellToWorldCenter(cell);
        }

        if (movers == null || movers.Count == 0)
            movers = new List<GenericAIMover>(FindObjectsByType<GenericAIMover>(FindObjectsSortMode.None));

        if (enemies == null || enemies.Count == 0)
            enemies = new List<Enemy>(FindObjectsByType<Enemy>(FindObjectsSortMode.None));

        if (buffs == null || buffs.Count == 0)
            buffs = new List<Buff>(FindObjectsByType<Buff>(FindObjectsSortMode.None));

        if (switches == null || switches.Count == 0)
            switches = new List<SwitchOccupant>(FindObjectsByType<SwitchOccupant>(FindObjectsSortMode.None));

        CurrentTurn = TurnPhase.Player;
        IsGameOver = false;
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
        Debug.Log("💀 Game Over (health depleted)");
    }

    public void OnHealthChanged(int current, int max)
    {
        onHealthChanged?.Invoke(current, max);
    }

    /// <summary>Called by PlayerAgent when its movement for the turn is fully finished.</summary>
    public void NotifyPlayerTurnEnded(PlayerAgent agent)
    {
        if (IsGameOver) return;
        if (CurrentTurn != TurnPhase.Player) return;
        StartCoroutine(ResolveAITurn(agent));
    }

    IEnumerator ResolveAITurn(PlayerAgent agent)
    {
        CurrentTurn = TurnPhase.AI;
        yield return null;

        if (IsGameOver || agent == null) yield break;

        // (NEW) Trigger switches based on the player's final cell
        for (int i = 0; i < switches.Count; i++)
        {
            var sw = switches[i];
            if (sw != null) sw.TryTriggerForPlayer(agent.CurrentCell);
        }

        // 1) MOVE AI actors first (positions updated before hazards/buffs)
        for (int i = 0; i < movers.Count; i++)
        {
            var mv = movers[i];
            if (mv == null) continue;
            yield return StartCoroutine(mv.RunAIMove());
            if (IsGameOver) yield break;
        }

        // 2) HAZARDS
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
            agent.ApplyDamage(totalDamage);
            if (agent.currentHealth <= 0) { Lose(); yield break; }
        }

        // 3) BUFFS
        int healed = 0;
        foreach (var b in buffs)
        {
            if (b == null || !b.triggerOnTurnEndOnly) continue;
            healed += b.ConsumeIfApplicable(agent.CurrentCell);
        }
        if (healed > 0) agent.ApplyHeal(healed, capToMax: true);

        // 4) Win check
        if (!IsGameOver && FlagCell == agent.CurrentCell)
        {
            Win();
            yield break;
        }

        if (!IsGameOver) CurrentTurn = TurnPhase.Player;
    }
}
