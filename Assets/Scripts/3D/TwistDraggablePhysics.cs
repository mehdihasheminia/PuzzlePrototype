using UnityEngine;

/// <summary>
/// Applies torque around a single allowed axis based on mouse drag.
/// Rigidbody should have position frozen and only one rotation axis free.
/// </summary>
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class TwistDraggablePhysics : MonoBehaviour
{
    public Vector3 localAxis = Vector3.up;
    public float torquePerPixel = 0.05f; // tweak feel
    public float maxAngularSpeed = 360f; // deg/sec cap

    Camera _cam;
    Rigidbody _rb;
    Vector3 _axisWorld;
    Vector3 _prevMouse;
    bool _dragging;

    void Awake()
    {
        _cam = Camera.main;
        _rb = GetComponent<Rigidbody>();
        _rb.maxAngularVelocity = Mathf.Max(_rb.maxAngularVelocity, maxAngularSpeed * Mathf.Deg2Rad);
    }

    void OnMouseDown()
    {
        _axisWorld = transform.TransformDirection(localAxis).normalized;
        _prevMouse = Input.mousePosition;
        _dragging = true;
    }

    void OnMouseDrag()
    {
        if (!_dragging) return;

        Vector3 cur = Input.mousePosition;
        Vector3 delta = cur - _prevMouse;
        _prevMouse = cur;

        // Map screen-space horizontal motion into torque around the tube axis.
        // You can also use vertical or project along screen tangent to make it camera-angle independent.
        float signed = delta.x; // simple & effective; swap for .y if you prefer
        Vector3 torque = _axisWorld * (signed * torquePerPixel);

        _rb.AddTorque(torque, ForceMode.Acceleration);
    }

    void OnMouseUp()
    {
        _dragging = false;
    }
}