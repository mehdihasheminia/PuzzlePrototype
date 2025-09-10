using UnityEngine;

[RequireComponent(typeof(PlayerAgent))]
public class ClickToMove : MonoBehaviour
{
    public Camera cam;
    public LayerMask groundMask = ~0; // optional: limit raycast

    PlayerAgent _agent;
    Grid2D _grid;

    void Awake()
    {
        _agent = GetComponent<PlayerAgent>();
    }

    void Start()
    {
        _grid = _agent.grid != null ? _agent.grid : FindFirstObjectByType<Grid2D>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null || _grid == null || _agent == null) return;

        // Mouse click / tap
        if (Input.GetMouseButtonDown(0))
        {
            if (RaycastToGrid(Input.mousePosition, out Vector2Int cell))
                _agent.TryMoveToCell(cell);
        }

        // (Optional) Touch support simple tap
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (RaycastToGrid(Input.GetTouch(0).position, out Vector2Int cell))
                _agent.TryMoveToCell(cell);
        }
    }

    bool RaycastToGrid(Vector2 screenPos, out Vector2Int cell)
    {
        cell = default;

        Ray ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, new Vector3(0f, _grid.gridY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            var hit = ray.GetPoint(enter);
            return _grid.WorldToCell(hit, out cell);
        }
        return false;
    }
}