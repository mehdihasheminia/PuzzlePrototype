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
    [Min(0.1f)] public float moveSpeed = 4f; // world units per second
    public AnimationCurve stepCurve = AnimationCurve.Linear(0, 0, 1, 1); // tweak for easing per segment

    [Header("Energy")]
    [Min(0)] public int maxEnergy = 20;
    public int currentEnergy;

    public Vector2Int CurrentCell { get; private set; }

    bool _moving;

    void Start()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (pathfinder == null) pathfinder = FindFirstObjectByType<Pathfinder>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        // Snap to nearest valid cell at start
        if (!grid.WorldToCell(transform.position, out var cell))
            cell = new Vector2Int(0, 0);

        if (!grid.IsWalkable(cell))
        {
            // Find any walkable nearby, fallback (0,0)
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
        currentEnergy = maxEnergy;
    }

    public void TryMoveToCell(Vector2Int targetCell)
    {
        if (_moving || gameManager.IsGameOver) return;
        if (!grid.InBounds(targetCell) || !grid.IsWalkable(targetCell)) return;

        if (!pathfinder.TryFindPath(CurrentCell, targetCell, out List<Vector2Int> path)) return;

        // Path includes start cell; steps = path.Count - 1
        int steps = Mathf.Max(0, path.Count - 1);
        if (steps == 0) return;

        if (steps > currentEnergy)
        {
            // Not enough energy → ignore (or you can move partial by trimming path)
            return;
        }

        StartCoroutine(MoveAlong(path));
    }

    IEnumerator MoveAlong(List<Vector2Int> path)
    {
        _moving = true;

        // Skip the first entry (current cell)
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
            currentEnergy = Mathf.Max(0, currentEnergy - 1);
            gameManager?.OnEnergyChanged(currentEnergy, maxEnergy);

            if (currentEnergy <= 0)
            {
                gameManager?.Lose();
                break;
            }

            // Check win (sharing cell with flag)
            if (gameManager != null && gameManager.FlagCell == CurrentCell)
            {
                gameManager.Win();
                break;
            }
        }

        _moving = false;
    }
}
