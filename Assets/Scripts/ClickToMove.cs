using UnityEngine;

[RequireComponent(typeof(PlayerAgent))]
public class ClickToMove : MonoBehaviour
{
    public Camera cam;
    public LayerMask groundMask = ~0; // optional: limit raycast

    [Header("Drag Interactions")]
    [Tooltip("Physics layers considered when searching for draggable occupants.")]
    public LayerMask draggableMask = ~0;
    [Tooltip("Maximum distance used when raycasting to find draggable occupants.")]
    public float dragRayDistance = 100f;

    PlayerAgent _agent;
    Grid2D _grid;
    DragInteractable _activeDrag;
    int _activePointerId = kNoPointer;

    const int kMousePointer = -1;
    const int kNoPointer = int.MinValue;

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

        // Do not accept input during AI turn or after game over.
        if (_agent.gameManager != null)
        {
            if (_agent.gameManager.IsGameOver || !_agent.gameManager.IsPlayerTurn) return;
        }

        if (_activeDrag != null)
        {
            HandleActiveDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!TryBeginDrag(Input.mousePosition, kMousePointer))
            {
                if (RaycastToGrid(Input.mousePosition, out Vector2Int cell))
                    _agent.TryMoveToCell(cell);
            }
            return;
        }

        // Touch support (single touch for movement/drag)
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began) continue;

            if (TryBeginDrag(touch.position, touch.fingerId))
                return;

            if (RaycastToGrid(touch.position, out Vector2Int cell))
                _agent.TryMoveToCell(cell);

            return; // handle only first began touch per frame
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

    bool RaycastToPlane(Vector2 screenPos, out Vector3 world)
    {
        world = default;
        Ray ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, new Vector3(0f, _grid.gridY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    bool TryBeginDrag(Vector2 screenPos, int pointerId)
    {
        if (_agent.IsMoving) return false;
        if (_agent.gameManager != null && !_agent.gameManager.IsPlayerTurn) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, dragRayDistance, draggableMask, QueryTriggerInteraction.Collide))
        {
            var drag = hit.collider.GetComponentInParent<DragInteractable>();
            if (drag != null && drag.CanBeginDrag())
            {
                Vector3 pointerWorld;
                if (!RaycastToPlane(screenPos, out pointerWorld))
                    pointerWorld = hit.point;

                if (!drag.BeginDrag(pointerWorld))
                    return false;

                _activeDrag = drag;
                _activePointerId = pointerId;
                return true;
            }
        }

        return false;
    }

    void HandleActiveDrag()
    {
        if (_activeDrag == null) return;

        if (_activePointerId == kMousePointer)
        {
            if (Input.GetMouseButton(0))
            {
                if (RaycastToPlane(Input.mousePosition, out var world))
                    _activeDrag.UpdateDrag(world);
            }

            if (Input.GetMouseButtonUp(0))
            {
                FinishDrag(Input.mousePosition);
            }
        }
        else
        {
            bool found = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId != _activePointerId) continue;
                found = true;

                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    if (RaycastToPlane(touch.position, out var world))
                        _activeDrag.UpdateDrag(world);
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    FinishDrag(touch.position, touch.phase == TouchPhase.Canceled);
                }
                break;
            }

            if (!found)
            {
                CancelActiveDrag();
            }
        }
    }

    void FinishDrag(Vector2 screenPos, bool canceled = false)
    {
        if (_activeDrag == null) return;

        bool moved = false;
        if (canceled)
        {
            _activeDrag.CancelDrag();
        }
        else
        {
            Vector3 world;
            if (!RaycastToPlane(screenPos, out world))
                world = _activeDrag.transform.position;
            moved = _activeDrag.EndDrag(world);
        }

        if (moved && _agent.gameManager != null)
        {
            _agent.gameManager.NotifyPlayerTurnEnded(_agent);
        }

        _activeDrag = null;
        _activePointerId = kNoPointer;
    }

    void CancelActiveDrag()
    {
        if (_activeDrag == null) return;
        _activeDrag.CancelDrag();
        _activeDrag = null;
        _activePointerId = kNoPointer;
    }
}