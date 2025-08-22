using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterMover : MonoBehaviour
{
    [Header("References")]
    public GridBoard grid;

    [Header("Movement")]
    public float moveSpeed = 6f;         // units per second
    public float arriveThreshold = 0.02f;
    public bool snapZToZero = true;

    private readonly List<Vector2Int> _pathCells = new List<Vector2Int>();
    private int _pathIndex = -1;         // -1 means idle
    private Vector3 _currentTarget;

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

    void FollowPath()
    {
        if (_pathIndex < 0 || _pathIndex >= _pathCells.Count) return;

        transform.position = Vector3.MoveTowards(transform.position, _currentTarget, moveSpeed * Time.deltaTime);

        if ((transform.position - _currentTarget).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            // advance to next waypoint
            _pathIndex++;
            if (_pathIndex >= _pathCells.Count)
            {
                StopMoving();
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
