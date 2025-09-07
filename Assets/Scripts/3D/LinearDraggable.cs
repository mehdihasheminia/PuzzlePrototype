using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Drag a 3D object linearly along a single axis with optional distance limits.
/// Requires a Collider on the object (for OnMouse* to work).
/// </summary>
[RequireComponent(typeof(Collider))]
public class LinearDraggable : MonoBehaviour
{
    [Header("Axis")]
    [Tooltip("Axis to slide along, in the object's local space.")]
    public Vector3 localAxis = Vector3.up;

    [Header("Limits (relative to origin)")]
    [Tooltip("Enable to clamp motion relative to the 'origin' captured on Awake (or via ResetOrigin()).")]
    public bool useDistanceLimits = false;
    [Tooltip("Min distance (in world units) from origin along the axis.")]
    public float minDistance = -1f;
    [Tooltip("Max distance (in world units) from origin along the axis.")]
    public float maxDistance =  1f;

    [Header("Feel")]
    [Tooltip("Multiply the computed offset for extra/less sensitivity (usually 1).")]
    public float sensitivity = 1f;

    [Serializable]
    public struct TranslationLink
    {
        public LinearDraggable m_Draggable;
        [Tooltip("Incoming delta is multiplied by this factor.")]
        public float m_Multiplier;
    }

    [Header("Link")]
    [Tooltip("Objects that influence this one (one-way).")]
    public List<TranslationLink> m_Influencers;

    /// <summary>
    /// Fired when this object changes by a signed delta (in world units).
    /// Subscribers can translate themselves by delta * multiplier.
    /// </summary>
    public event Action<LinearDraggable, float> TranslationUpdated;

    // --- Runtime state ---
    Camera _cam;
    Vector3 _axisWorld;
    Plane _dragPlane;

    // Persistent origin + absolute offset (prevents first-tick snap)
    Vector3 _originPos;                 // "rest/original" position (captured on Awake or via ResetOrigin)
    float   _absOffsetFromOrigin;       // signed distance from origin along axis (world units)

    // Drag-session state
    Vector3 _sessionOriginPos;          // equals _originPos at mouse down
    float   _sessionStartOffset;        // absolute offset at mouse down
    float   _lastAppliedOffset;         // last absolute offset applied
    float   _tStart;                    // scalar along axis at mouse down
    bool    _dragging;

    // Linking
    Dictionary<LinearDraggable, float> _linkMap;
    bool _suppressEvent;                // simple loop guard

    // -------------------- Unity --------------------

    void Awake()
    {
        if (_cam == null) _cam = Camera.main;
        if (localAxis.sqrMagnitude < 1e-12f) localAxis = Vector3.up;

        // Capture a stable origin once. You can call ResetOrigin() at runtime to rebake.
        _originPos = transform.position;
        _absOffsetFromOrigin = 0f;

        _axisWorld = transform.TransformDirection(localAxis).normalized;

        _linkMap = new Dictionary<LinearDraggable, float>();
        if (m_Influencers != null)
            _linkMap = m_Influencers.ToDictionary(x => x.m_Draggable, y => y.m_Multiplier);

        foreach (var kvp in _linkMap)
            if (kvp.Key) kvp.Key.TranslationUpdated += OnLinkTranslationUpdated;
    }

    void OnDestroy()
    {
        if (_linkMap != null)
            foreach (var kvp in _linkMap)
                if (kvp.Key) kvp.Key.TranslationUpdated -= OnLinkTranslationUpdated;
    }

    void OnValidate()
    {
        // Keep axis reasonable in editor
        if (localAxis.sqrMagnitude < 1e-8f) localAxis = Vector3.up;
    }

    // -------------------- Input --------------------

    void OnMouseDown()
    {
        if (_cam == null) return;

        // Refresh axis and create a stable plane that contains the motion axis
        _axisWorld   = transform.TransformDirection(localAxis).normalized;
        _dragPlane   = MakeAxisDragPlane(_axisWorld, transform.position, _cam.transform.forward);

        if (!RaycastToPlane(Input.mousePosition, out var hit)) return;

        // Session baselines reference the persistent origin
        _sessionOriginPos   = _originPos;
        _sessionStartOffset = _absOffsetFromOrigin;
        _lastAppliedOffset  = _sessionStartOffset;
        _tStart             = ScalarOnAxis(hit, _sessionOriginPos, _axisWorld);
        _dragging           = true;
    }

    void OnMouseDrag()
    {
        if (!_dragging || _cam == null) return;
        if (!RaycastToPlane(Input.mousePosition, out var hit)) return;

        float tNow   = ScalarOnAxis(hit, _sessionOriginPos, _axisWorld);
        float delta  = (tNow - _tStart) * sensitivity;   // delta since mouse down (signed, world units)
        float target = _sessionStartOffset + delta;      // absolute offset from origin

        if (useDistanceLimits)
            target = Mathf.Clamp(target, minDistance, maxDistance);

        ApplyAbsoluteOffset(target, fromLink:false);
    }

    void OnMouseUp()
    {
        _dragging = false;
    }

    // -------------------- Linking --------------------

    void OnLinkTranslationUpdated(LinearDraggable link, float delta)
    {
        if (!link || link == this) return;

        // Always add delta to our absolute offset (no session baseline => no snap)
        float multiplier = _linkMap[link];
        float target     = _absOffsetFromOrigin + delta * multiplier;

        if (useDistanceLimits)
            target = Mathf.Clamp(target, minDistance, maxDistance);

        ApplyAbsoluteOffset(target, fromLink:true);
    }

    // -------------------- Movement Core --------------------

    void ApplyAbsoluteOffset(float absoluteOffset, bool fromLink)
    {
        float signedDelta = absoluteOffset - _absOffsetFromOrigin;

        _absOffsetFromOrigin = absoluteOffset;
        _lastAppliedOffset   = absoluteOffset;

        // Re-evaluate axis each move in case the object rotated since origin
        Vector3 axisNow = transform.TransformDirection(localAxis).normalized;
        transform.position = _originPos + axisNow * _absOffsetFromOrigin;

        // Relay only self-caused deltas to avoid loops
        if (!fromLink && Mathf.Abs(signedDelta) > Mathf.Epsilon && !_suppressEvent)
        {
            _suppressEvent = true;
            TranslationUpdated?.Invoke(this, signedDelta);
            _suppressEvent = false;
        }
    }

    // -------------------- Helpers --------------------

    // Returns scalar coordinate t for point 'p' along axis through 'origin'
    static float ScalarOnAxis(Vector3 p, Vector3 origin, Vector3 axisDirNormalized)
    {
        return Vector3.Dot(p - origin, axisDirNormalized);
    }

    // Build a plane that contains the axis and is view-stable:
    // Normal n = axis x (viewDir x axis)  => plane contains axis and faces camera sensibly.
    static Plane MakeAxisDragPlane(Vector3 axis, Vector3 throughPoint, Vector3 viewDir)
    {
        Vector3 v = Vector3.Cross(viewDir, axis);
        Vector3 n = Vector3.Cross(axis, v);
        if (n.sqrMagnitude < 1e-8f)
        {
            // Edge case: camera is parallel to axis; fall back to any valid orthogonal
            n = Vector3.Cross(axis, Vector3.up);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(axis, Vector3.right);
        }
        n.Normalize();
        return new Plane(n, throughPoint);
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

    /// <summary>
    /// Optional: rebake the current transform.position as the new origin at runtime.
    /// </summary>
    public void ResetOrigin()
    {
        _originPos = transform.position;
        _absOffsetFromOrigin = 0f;
        _lastAppliedOffset = 0f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var axisWorld = transform.TransformDirection(localAxis).normalized;

        // Preview from the baked origin if playing; otherwise use current position
        Vector3 origin = Application.isPlaying ? _originPos : transform.position;

        // Axis gizmo
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin - axisWorld * 0.5f, origin + axisWorld * 0.5f);
        Gizmos.DrawSphere(origin + axisWorld * 0.5f, 0.01f);
        Gizmos.DrawSphere(origin - axisWorld * 0.5f, 0.01f);

        // Limits preview (when enabled)
        if (useDistanceLimits)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + axisWorld * minDistance, origin + axisWorld * maxDistance);
            Gizmos.DrawSphere(origin + axisWorld * minDistance, 0.012f);
            Gizmos.DrawSphere(origin + axisWorld * maxDistance, 0.012f);
        }
    }
#endif
}
