using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider))]
public class PlayerAgent : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;
    public Pathfinder pathfinder;
    public GameManager gameManager;

    [Header("Movement")]
    [Min(0.1f)] public float moveSpeed = 4f;
    public AnimationCurve stepCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Turn Rules")]
    [Min(1)] public int cellsPerTurn = 5; // energy: how many cells we can traverse per turn

    public int CellsPerTurn => Mathf.Max(1, cellsPerTurn);

    public Vector2Int CurrentCell { get; private set; }

    bool _moving;
    public bool IsMoving => _moving; // <-- exposed for UI/skip gating

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (pathfinder == null) pathfinder = FindFirstObjectByType<Pathfinder>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        if (!grid.WorldToCell(transform.position, out var cell))
            cell = new Vector2Int(0, 0);

        if (!grid.IsWalkable(cell))
        {
            for (int r = 1; r < 8 && !grid.IsWalkable(cell); r++)
            {
                var candidates = new[]
                {
                    new Vector2Int(0,0),
                    new Vector2Int(r,0),
                    new Vector2Int(0,r),
                    new Vector2Int(r,r)
                };
                foreach (var c in candidates) if (grid.InBounds(c) && grid.IsWalkable(c)) { cell = c; break; }
            }
        }

        CurrentCell = cell;
        transform.position = grid.CellToWorldCenter(CurrentCell);

    }

    public void TryMoveToCell(Vector2Int targetCell)
    {
        if (_moving || gameManager?.IsGameOver == true) return;
        if (gameManager != null && !gameManager.IsPlayerTurn) return;

        if (!grid.InBounds(targetCell) || !grid.IsWalkable(targetCell)) return;
        if (!pathfinder.TryFindPath(CurrentCell, targetCell, out List<Vector2Int> path)) return;

        int steps = Mathf.Max(0, path.Count - 1);
        if (steps == 0) return;

        if (steps > CellsPerTurn)
        {
            Debug.Log($"Move rejected: requires {steps} steps, allowed per turn: {CellsPerTurn}.");
            return;
        }

        StartCoroutine(MoveAlong(path));
    }

    IEnumerator MoveAlong(List<Vector2Int> path)
    {
        _moving = true;

        int extraTurnsGranted = 0;

        for (int i = 1; i < path.Count; i++)
        {
            var from = grid.CellToWorldCenter(CurrentCell);
            var to = grid.CellToWorldCenter(path[i]);
            float dist = Vector3.Distance(from, to);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * (moveSpeed / Mathf.Max(0.0001f, dist));
                float k = stepCurve.Evaluate(Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }

            CurrentCell = path[i];

            if (gameManager != null)
            {
                extraTurnsGranted += gameManager.TryConsumeBuff(CurrentCell, triggeredDuringMovement: i < path.Count - 1);
            }
        }

        _moving = false;

        if (gameManager != null)
        {
            if (extraTurnsGranted > 0)
            {
                gameManager.GrantExtraTurns(extraTurnsGranted);
            }

            // Player turn ends after movement completes (unless extra turns were granted).
            gameManager.NotifyPlayerTurnEnded(this);
        }
    }

    // --- Skip Turn ---
    public void SkipTurn()
    {
        if (_moving || gameManager == null) return;
        if (gameManager.IsGameOver || !gameManager.IsPlayerTurn) return;

        // End the player's turn without moving.
        gameManager.NotifyPlayerTurnEnded(this);
    }

    // Optional helper for UI preview
    public bool CanReachInOneTurn(Vector2Int targetCell, out int steps)
    {
        steps = 0;
        if (!grid.InBounds(targetCell) || !grid.IsWalkable(targetCell)) return false;
        if (!pathfinder.TryFindPath(CurrentCell, targetCell, out var path)) return false;
        steps = Mathf.Max(0, path.Count - 1);
        return steps > 0 && steps <= CellsPerTurn;
    }
}
