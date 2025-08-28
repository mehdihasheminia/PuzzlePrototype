using UnityEngine;

/// <summary>
/// Drag anywhere on the tube to twist it around a single axis,
/// with optional angle limits. Requires a Collider on the object.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TwistDraggable : MonoBehaviour
{
    [Header("Axis")]
    [Tooltip("Axis to rotate around, in the tube's local space.")]
    public Vector3 localAxis = Vector3.up;

    [Header("Limits")]
    public bool useAngleLimits = false;
    [Tooltip("Degrees relative to the rotation at drag start.")]
    public float minAngle = -90f;
    public float maxAngle = 90f;

    [Header("Feel")]
    [Tooltip("Multiply the computed angle for extra/less sensitivity (usually 1).")]
    public float sensitivity = 1f;

    Camera _cam;
    Quaternion _startRotation;
    Vector3 _axisWorld;
    Plane _dragPlane;
    Vector3 _startDirOnPlane; // direction from pivot to initial hit projected onto plane
    bool _dragging;

    void Awake()
    {
        _cam = Camera.main;
    }

    void OnMouseDown()
    {
        if (_cam == null) return;

        // World axis and drag plane through the tube's pivot, normal = axis
        _axisWorld = transform.TransformDirection(localAxis).normalized;
        _dragPlane = new Plane(_axisWorld, transform.position);

        if (!RaycastToPlane(Input.mousePosition, out var hitPt)) return;

        // Direction on the plane from pivot to mouse hit
        _startDirOnPlane = Vector3.ProjectOnPlane(hitPt - transform.position, _axisWorld).normalized;
        if (_startDirOnPlane.sqrMagnitude < 1e-6f) return;

        _startRotation = transform.rotation;
        _dragging = true;
    }

    void OnMouseDrag()
    {
        if (!_dragging || _cam == null) return;
        if (!RaycastToPlane(Input.mousePosition, out var hitPt)) return;

        // Current direction on the same plane
        var curDir = Vector3.ProjectOnPlane(hitPt - transform.position, _axisWorld).normalized;
        if (curDir.sqrMagnitude < 1e-6f) return;

        // Signed angle from start to current around the axis
        float angle = Vector3.SignedAngle(_startDirOnPlane, curDir, _axisWorld) * sensitivity;

        if (useAngleLimits)
            angle = Mathf.Clamp(angle, minAngle, maxAngle);

        // Apply relative to the rotation we had when the drag began
        transform.rotation = Quaternion.AngleAxis(angle, _axisWorld) * _startRotation;
    }

    void OnMouseUp()
    {
        _dragging = false;
    }

    bool RaycastToPlane(Vector3 screenPos, out Vector3 worldHit)
    {
        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (_dragPlane.Raycast(ray, out float dist))
        {
            worldHit = ray.GetPoint(dist);
            return true;
        }
        worldHit = default;
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw axis gizmo
        var axisWorld = Application.isPlaying ? _axisWorld : transform.TransformDirection(localAxis).normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position - axisWorld * 0.5f, transform.position + axisWorld * 0.5f);
        Gizmos.DrawSphere(transform.position + axisWorld * 0.5f, 0.01f);
        Gizmos.DrawSphere(transform.position - axisWorld * 0.5f, 0.01f);
    }
#endif
}
