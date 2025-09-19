using UnityEngine;

/// <summary>
/// Adds drag-based interaction to a board occupant. Supports translation along a single axis or rotation
/// around a single axis with snapping behaviour. Designed to be controlled by <see cref="ClickToMove"/>.
/// </summary>
[DisallowMultipleComponent]
public class DragInteractable : MonoBehaviour
{
    public enum DragMode
    {
        Translation,
        Rotation
    }

    [Header("General")]
    public DragMode mode = DragMode.Translation;
    public Grid2D grid;

    [Header("Translation")]
    [Tooltip("Axis in grid-space (X/Z plane) the object is allowed to move along.")]
    public Vector2Int translationAxis = Vector2Int.right;
    [Tooltip("Minimum number of cell steps from the start position.")]
    public int minTranslationSteps = -1;
    [Tooltip("Maximum number of cell steps from the start position.")]
    public int maxTranslationSteps = 1;

    [Header("Rotation")]
    [Tooltip("Axis around which the object rotates while dragging.")]
    public Vector3 rotationAxis = Vector3.up;
    [Tooltip("Angle in degrees per snap step.")]
    public float rotationSnapAngle = 90f;
    [Tooltip("Minimum number of snap steps relative to the start rotation.")]
    public int minRotationSteps = -1;
    [Tooltip("Maximum number of snap steps relative to the start rotation.")]
    public int maxRotationSteps = 1;

    [Header("Board Update")]
    [Tooltip("If true, SyncCellFromWorld() is invoked on any IBoardCellOccupant found on this object after drag completes.")]
    public bool resyncOccupantAfterDrag = true;

    IBoardCellOccupant _occupant;
    IBoardAffectsWalkability _blocker;

    bool _dragging;
    Vector3 _startWorld;
    Quaternion _startRotation;
    Vector3 _pivotWorld;
    Vector3 _rotationAxisWorld;
    Vector3 _translationAxisWorld;
    int _currentStep;
    float _currentTranslationOffset;
    float _currentRotationAngle;

    void Awake()
    {
        _occupant = GetComponent<IBoardCellOccupant>();
        _blocker = GetComponent<IBoardAffectsWalkability>();
        if (grid == null && _occupant != null)
            grid = _occupant.GetGrid();
    }

    /// <summary>Returns false when the drag should be rejected (invalid setup or currently dragging).</summary>
    public bool CanBeginDrag()
    {
        if (_dragging) return false;
        var g = ResolveGrid();
        if (g == null) return false;

        if (mode == DragMode.Translation)
        {
            if (translationAxis == Vector2Int.zero) return false;
            if (minTranslationSteps > maxTranslationSteps) return false;
            if (minTranslationSteps == 0 && maxTranslationSteps == 0) return false;
        }
        else if (mode == DragMode.Rotation)
        {
            if (rotationSnapAngle <= 0.0001f) return false;
            if (rotationAxis.sqrMagnitude < 0.0001f) return false;
            if (minRotationSteps > maxRotationSteps) return false;
        }

        return true;
    }

    /// <summary>Initialises drag at the provided pointer position projected on the grid plane.</summary>
    public bool BeginDrag(Vector3 pointerWorldPosition)
    {
        if (!CanBeginDrag()) return false;

        _dragging = true;
        _startWorld = transform.position;
        _startRotation = transform.rotation;
        _currentStep = 0;
        _currentTranslationOffset = 0f;
        _currentRotationAngle = 0f;

        var g = ResolveGrid();
        if (mode == DragMode.Translation)
        {
            _translationAxisWorld = new Vector3(translationAxis.x, 0f, translationAxis.y);
            if (_translationAxisWorld.sqrMagnitude < 0.0001f)
                _translationAxisWorld = Vector3.right;
            _translationAxisWorld.y = 0f;
            _translationAxisWorld = _translationAxisWorld.normalized;
        }
        else
        {
            _rotationAxisWorld = rotationAxis.sqrMagnitude < 0.0001f ? Vector3.up : rotationAxis.normalized;
            _pivotWorld = transform.position;
            _rotationAxisWorld.Normalize();
        }

        UpdateDrag(pointerWorldPosition);
        return true;
    }

    public void UpdateDrag(Vector3 pointerWorldPosition)
    {
        if (!_dragging) return;

        var g = ResolveGrid();
        if (g == null) return;

        if (mode == DragMode.Translation)
        {
            Vector3 planePos = ProjectPointToPlane(pointerWorldPosition, g.gridY);
            Vector3 delta = planePos - _startWorld;
            float axisDistance = Vector3.Dot(delta, _translationAxisWorld);
            float cellSize = Mathf.Max(0.0001f, g.cellSize);
            float maxDistance = cellSize * Mathf.Max(0, maxTranslationSteps);
            float minDistance = cellSize * Mathf.Min(0, minTranslationSteps);
            axisDistance = Mathf.Clamp(axisDistance, minDistance, maxDistance);

            _currentTranslationOffset = axisDistance;

            Vector3 target = _startWorld + _translationAxisWorld * _currentTranslationOffset;
            target.y = _startWorld.y;
            transform.position = target;
        }
        else
        {
            Vector3 planePos = ProjectPointToPlane(pointerWorldPosition, g.gridY);
            Vector3 fromStart = ProjectVectorOnPlane(_startRotation * Vector3.forward, _rotationAxisWorld);
            if (fromStart.sqrMagnitude < 0.0001f)
            {
                fromStart = ProjectVectorOnPlane(transform.forward, _rotationAxisWorld);
            }
            Vector3 currentDir = ProjectVectorOnPlane(planePos - _pivotWorld, _rotationAxisWorld);
            if (currentDir.sqrMagnitude < 0.0001f)
            {
                currentDir = fromStart;
            }

            fromStart.Normalize();
            currentDir.Normalize();

            float signedAngle = Vector3.SignedAngle(fromStart, currentDir, _rotationAxisWorld);
            float snapAngle = Mathf.Max(0.0001f, rotationSnapAngle);
            float maxAngle = snapAngle * Mathf.Max(0, maxRotationSteps);
            float minAngle = snapAngle * Mathf.Min(0, minRotationSteps);
            signedAngle = Mathf.Clamp(signedAngle, minAngle, maxAngle);

            _currentRotationAngle = signedAngle;

            transform.rotation = _startRotation * Quaternion.AngleAxis(_currentRotationAngle, _rotationAxisWorld);
        }
    }

    /// <summary>
    /// Finalises the drag interaction. Returns true if the object ended up in a new snap step.
    /// </summary>
    public bool EndDrag(Vector3 pointerWorldPosition)
    {
        if (!_dragging) return false;

        UpdateDrag(pointerWorldPosition);

        bool moved;
        if (mode == DragMode.Translation)
        {
            var g = ResolveGrid();
            if (g == null)
            {
                CancelDrag();
                return false;
            }

            float cellSize = Mathf.Max(0.0001f, g.cellSize);
            int desiredStep = Mathf.RoundToInt(_currentTranslationOffset / cellSize);
            desiredStep = Mathf.Clamp(desiredStep, minTranslationSteps, maxTranslationSteps);

            moved = desiredStep != 0;
            if (moved)
            {
                _currentStep = desiredStep;
                Vector3 snapped = _startWorld + _translationAxisWorld * (cellSize * _currentStep);
                snapped.y = _startWorld.y;
                transform.position = snapped;
                _currentTranslationOffset = cellSize * _currentStep;
            }
            else
            {
                transform.position = _startWorld;
            }
        }
        else
        {
            float snapAngle = Mathf.Max(0.0001f, rotationSnapAngle);
            int desiredStep = Mathf.RoundToInt(_currentRotationAngle / snapAngle);
            desiredStep = Mathf.Clamp(desiredStep, minRotationSteps, maxRotationSteps);

            moved = desiredStep != 0;
            if (moved)
            {
                _currentStep = desiredStep;
                float snappedAngle = rotationSnapAngle * _currentStep;
                transform.rotation = _startRotation * Quaternion.AngleAxis(snappedAngle, _rotationAxisWorld);
                _currentRotationAngle = snappedAngle;
            }
            else
            {
                transform.rotation = _startRotation;
            }
        }

        if (!moved)
        {
            // revert fully to initial state if no movement happened
            transform.position = _startWorld;
            transform.rotation = _startRotation;
        }

        _dragging = false;

        if (moved)
        {
            PostDragSync();
        }

        return moved;
    }

    public void CancelDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        transform.position = _startWorld;
        transform.rotation = _startRotation;
        _currentTranslationOffset = 0f;
        _currentRotationAngle = 0f;
        _currentStep = 0;
    }

    void PostDragSync()
    {
        var g = ResolveGrid();
        if (g == null) return;

        if (resyncOccupantAfterDrag && _occupant != null)
        {
            _occupant.SyncCellFromWorld();
        }

        if (_blocker != null)
        {
            g.NotifyDynamicOccupantMoved(_blocker);
        }
    }

    Grid2D ResolveGrid()
    {
        if (grid != null) return grid;
        if (_occupant != null) grid = _occupant.GetGrid();
        return grid;
    }

    static Vector3 ProjectPointToPlane(Vector3 point, float planeY)
    {
        point.y = planeY;
        return point;
    }

    static Vector3 ProjectVectorOnPlane(Vector3 vector, Vector3 normal)
    {
        return Vector3.ProjectOnPlane(vector, normal);
    }
}
