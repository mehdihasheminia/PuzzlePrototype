using System.Collections;
using UnityEngine;

/// <summary>
/// Listener for SwitchOccupant events: moves a target occupant to specific cells
/// (e.g., slide an Obstacle onto/off a bridge). Works with any Transform.
/// If the target implements IBoardCellOccupant, its Cell is re-synced after moving.
/// </summary>
[DisallowMultipleComponent]
public class SwitchMoveOccupant : MonoBehaviour
{
    [Header("Refs")]
    public Grid2D grid;
    public Transform target;            // e.g., the Obstacle GameObject

    [Header("Cells")]
    public Vector2Int onCell;           // destination when switch turns ON
    public Vector2Int offCell;          // destination when switch turns OFF

    [Header("Motion")]
    public bool animate = true;
    [Min(0.01f)] public float moveSpeed = 4f;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

    Coroutine _moveCo;

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid2D>();
        if (target == null) target = transform;
    }

    public void SwitchOn()
    {
        MoveTo(onCell);
    }

    public void SwitchOff()
    {
        MoveTo(offCell);
    }

    void MoveTo(Vector2Int cell)
    {
        if (grid == null || !grid.InBounds(cell)) return;

        var to = grid.CellToWorldCenter(cell);
        if (!animate)
        {
            target.position = to;
            ResyncOccupant();
            return;
        }

        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(MoveRoutine(to));
    }

    IEnumerator MoveRoutine(Vector3 to)
    {
        Vector3 from = target.position;
        float dist = Vector3.Distance(from, to);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * (moveSpeed / Mathf.Max(0.0001f, dist));
            float k = curve.Evaluate(Mathf.Clamp01(t));
            target.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        ResyncOccupant();
    }

    void ResyncOccupant()
    {
        // If the moved object participates in cell logic, refresh its cached cell.
        var occ = target.GetComponent<IBoardCellOccupant>();
        if (occ != null) occ.SyncCellFromWorld();
        // If you use an obstacle script that affects walkability via offsets,
        // that script should react to transform changes or expose a refresh call.
    }
}
