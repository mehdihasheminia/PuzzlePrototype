using System;
using UnityEngine;

public class ThickRaycaster : MonoBehaviour
{
    [Header("Ray Settings")]
    public Transform rayOrigin;     
    public float rayLength = 10f;
    public float rayThickness = 0.25f;
    public LayerMask m_LayerMask = ~0;

    [Header("Target Settings")]
    public GameObject targetObject;

    [Header("Debug Settings")]
    public Color hitColor = Color.green;
    public Color missColor = Color.red;

    Vector3 RayDirection => rayOrigin.forward;

    public bool DidHit => m_DidHit;

    bool m_DidHit;
    RaycastHit m_Hit;
    
    void Update()
    {
        if (rayOrigin == null || targetObject == null) 
            return;

        m_DidHit = Physics.SphereCast(
            rayOrigin.position,
            rayThickness,
            RayDirection,
            out m_Hit,
            rayLength,
            m_LayerMask
        );

        if (DidHit)
        {
            if (m_Hit.collider.gameObject == targetObject)
            {
                Debug.Log("Hit the target object!");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (rayOrigin == null) 
            return;

        // Default ray end position
        Vector3 end = rayOrigin.position + RayDirection * rayLength;

        // Cast check to visualize hit
        if (DidHit)
        {
            Gizmos.color = m_Hit.collider.gameObject == targetObject ? hitColor : missColor;
            end = m_Hit.point;
        }
        else
        {
            Gizmos.color = missColor;
        }

        // Draw main ray line
        Gizmos.DrawLine(rayOrigin.position, end);

        // Draw a sphere at the start and end for thickness visualization
        Gizmos.DrawWireSphere(rayOrigin.position, rayThickness);
        Gizmos.DrawWireSphere(end, rayThickness);
    }
}