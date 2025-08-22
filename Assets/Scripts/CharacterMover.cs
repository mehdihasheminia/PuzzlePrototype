using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterMover : MonoBehaviour
{
    [Header("References")]
    public GridBoard grid;

    [Header("Movement")]
    public float moveSpeed = 6f;     // units per second
    public float arriveThreshold = 0.01f;
    public bool snapZToZero = true;

    private Vector3 _targetPos;
    private bool _hasTarget;

    void Start()
    {
        if (grid == null)
        {
            grid = FindObjectOfType<GridBoard>();
        }

        // Start on nearest cell (optional)
        var startCell = grid.WorldToCell(transform.position);
        _targetPos = grid.CellToWorldCenter(startCell.x, startCell.y, snapZToZero ? 0f : transform.position.z);
        transform.position = _targetPos;
        _hasTarget = false;
    }

    void Update()
    {
        HandleClick();
        MoveTowardsTarget();
    }

    void HandleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 world = ScreenToWorld2D(Input.mousePosition);
            Vector2Int cell;
            Vector3 center = grid.NearestCellCenter(world, out cell, snapZToZero ? 0f : transform.position.z);

            if (grid.IsWalkable(cell.x, cell.y))
            {
                _targetPos = center;
                _hasTarget = true;
            }
            else
            {
                // Optional: feedback when clicking an unwalkable cell
                // Debug.Log("Clicked an unwalkable cell at " + cell);
            }
        }
    }

    void MoveTowardsTarget()
    {
        if (!_hasTarget) return;

        transform.position = Vector3.MoveTowards(transform.position, _targetPos, moveSpeed * Time.deltaTime);
        if ((transform.position - _targetPos).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            transform.position = _targetPos;
            _hasTarget = false;
        }
    }

    Vector3 ScreenToWorld2D(Vector3 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return screenPos;

        // For 2D, set Z so that ScreenToWorldPoint projects onto character's plane.
        screenPos.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(screenPos);
    }

    // Optional visualization of current target in-editor
    void OnDrawGizmosSelected()
    {
        if (_hasTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_targetPos, Mathf.Max(0.05f, grid != null ? grid.cellSize * 0.2f : 0.1f));
        }
    }
}
