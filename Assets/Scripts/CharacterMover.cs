using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterMover : MonoBehaviour
{
    [Header("References")]
    public GridBoard grid;

    [Header("Movement")]
    public float moveSpeed = 6f; // units per second

    public float arriveThreshold = 0.02f;
    public bool snapZToZero = true;

    private readonly List<Vector2Int> _pathCells = new List<Vector2Int>();
    private int _pathIndex = -1; // -1 means idle
    private Vector3 _currentTarget;

    public KeyCode interactMouse = KeyCode.Mouse1; // Right-click
    private ICellInteractable _pendingInteract;
    private Vector2Int _pendingInteractCell;

    void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();

        // snap to nearest valid cell at start
        var startCell = grid.WorldToCell(transform.position);
        if (!grid.IsWalkable(startCell.x, startCell.y))
        {
            // find nearest walkable around you (optional; here we just clamp to the cell)
            // In simple case, just place on center anyway:
        }

        var startPos = grid.CellToWorldCenter(startCell.x, startCell.y, snapZToZero ? 0f : transform.position.z);
        transform.position = startPos;
        StopMoving();
    }

    void Update()
    {
        HandleClick();
        HandleInteractClick();
        FollowPath();
    }

    void HandleClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main;
        if (cam == null) return;

        var sp = Input.mousePosition;
        sp.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        var world = cam.ScreenToWorldPoint(sp);

        // target cell must be walkable
        var targetCell = grid.WorldToCell(world);
        if (!grid.IsWalkable(targetCell.x, targetCell.y))
        {
            // No move if clicked on a blocked cell
            // (You could search nearest walkable to click here if desired.)
            return;
        }

        var startCell = grid.WorldToCell(transform.position);

        // If already there, do nothing
        if (startCell == targetCell)
        {
            StopMoving();
            return;
        }

        // Find path
        if (GridPathfinder.FindPath(grid, startCell, targetCell, _pathCells))
        {
            // Convert first step to world target
            _pathIndex = 0;
            _currentTarget = CellCenterToWorld(_pathCells[_pathIndex]);
        }
        else
        {
            // No route — character stays behind the barrier
            StopMoving();
        }
    }

    void HandleInteractClick()
    {
        if (!Input.GetKeyDown(interactMouse)) return;

        var cam = Camera.main;
        if (cam == null) return;

        var sp = Input.mousePosition;
        sp.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        var world = cam.ScreenToWorldPoint(sp);

        var clickedCell = grid.WorldToCell(world);

        // Is there an interactable on that cell?
        if (!grid.TryGetInteractable(clickedCell, out var interactable))
            return;

        var myCell = grid.WorldToCell(transform.position);

        // If already allowed to activate (adjacent or same per rule), do it now
        if (interactable.CanActivate(myCell))
        {
            interactable.Activate();
            return;
        }

        // Otherwise, path to a reachable adjacent walkable cell next to the switch
        var candidateAdj = grid.WalkableNeighbors(clickedCell).ToList();
        if (candidateAdj.Count == 0) return;

        // Choose the closest (by A* path length); first one that yields a path wins
        foreach (var adj in candidateAdj.OrderBy(c => Mathf.Abs(c.x - myCell.x) + Mathf.Abs(c.y - myCell.y)))
        {
            if (GridPathfinder.FindPath(grid, myCell, adj, _pathCells))
            {
                _pathIndex = 0;
                _currentTarget = CellCenterToWorld(_pathCells[_pathIndex]);
                _pendingInteract = interactable; // remember to activate on arrival
                _pendingInteractCell = adj;
                return;
            }
        }

        // No reachable adjacent => give up
    }

    void FollowPath()
    {
        if (_pathIndex < 0 || _pathIndex >= _pathCells.Count) return;

        transform.position = Vector3.MoveTowards(transform.position, _currentTarget, moveSpeed * Time.deltaTime);

        if ((transform.position - _currentTarget).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            _pathIndex++;
            if (_pathIndex >= _pathCells.Count)
            {
                // Arrived at destination
                var myCell = grid.WorldToCell(transform.position);

                // If we were pathing to interact, and it's now valid, activate
                if (_pendingInteract != null && _pendingInteract.CanActivate(myCell))
                {
                    _pendingInteract.Activate();
                }

                _pendingInteract = null;
                _pathCells.Clear();
                _pathIndex = -1;
            }
            else
            {
                _currentTarget = CellCenterToWorld(_pathCells[_pathIndex]);
            }
        }
    }

    void StopMoving()
    {
        _pathIndex = -1;
        _pathCells.Clear();
    }

    Vector3 CellCenterToWorld(Vector2Int cell)
    {
        return grid.CellToWorldCenter(cell.x, cell.y, snapZToZero ? 0f : transform.position.z);
    }

    void OnDrawGizmosSelected()
    {
        // Visualize current path
        if (_pathCells.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _pathCells.Count; i++)
            {
                var p = CellCenterToWorld(_pathCells[i]);
                Gizmos.DrawWireSphere(p, Mathf.Max(0.05f, grid != null ? grid.cellSize * 0.2f : 0.1f));
                if (i > 0)
                {
                    var prev = CellCenterToWorld(_pathCells[i - 1]);
                    Gizmos.DrawLine(prev, p);
                }
            }
        }
    }
}
